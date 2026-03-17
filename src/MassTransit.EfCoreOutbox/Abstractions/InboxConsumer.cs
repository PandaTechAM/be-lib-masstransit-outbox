using System.Text.Json;
using MassTransit.EfCoreOutbox.Entities;
using MassTransit.EfCoreOutbox.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MassTransit.EfCoreOutbox.Abstractions;

/// <summary>
///    Base class for idempotent message consumption using the inbox pattern with EF Core.
///    Inherit from this class and implement <see cref="ConsumeAsync(TMessage, IDbContextTransaction, CancellationToken)" />
///    to process messages exactly once. Concurrency is controlled via a lease-based strategy.
/// </summary>
/// <typeparam name="TMessage">The message type to consume.</typeparam>
/// <typeparam name="TDbContext">The <see cref="DbContext" /> type that implements <see cref="IInboxDbContext" />.</typeparam>
public abstract class InboxConsumer<TMessage, TDbContext> : IConsumer<TMessage>
   where TMessage : class
   where TDbContext : DbContext, IInboxDbContext
{
   private readonly string _consumerId;
   private readonly IServiceProvider _sp;

   protected InboxConsumer(IServiceProvider sp)
   {
      _consumerId = GetType()
         .ToString();
      _sp = sp;
   }

   public async Task Consume(ConsumeContext<TMessage> context)
   {
      var ct = context.CancellationToken;
      var messageId = context.Headers.Get<Guid>(Constants.OutboxMessageIdHeaderName) ?? context.MessageId;
      var dbContext = _sp.GetRequiredService<TDbContext>();
      var logger = _sp.GetRequiredService<ILogger<InboxConsumer<TMessage, TDbContext>>>();
      var settings = _sp.GetRequiredService<Settings>();

      var exists = await dbContext.InboxMessages
                                  .AnyAsync(x => x.MessageId == messageId && x.ConsumerId == _consumerId, ct);

      if (!exists)
      {
         try
         {
            dbContext.InboxMessages.Add(new InboxMessage
            {
               MessageId = messageId!.Value,
               CreatedAt = DateTime.UtcNow,
               State = MessageState.New,
               ConsumerId = _consumerId,
               Payload = settings.InboxRetryEnabled ? JsonSerializer.Serialize(context.Message) : null,
               Type = settings.InboxRetryEnabled ? GetVersionAgnosticTypeName() : null,
               DestinationAddress = settings.InboxRetryEnabled
                  ? context.ReceiveContext.InputAddress?.ToString()
                  : null
            });
            await dbContext.SaveChangesAsync(ct);
         }
         catch (DbUpdateException)
         {
            dbContext.ChangeTracker.Clear();
         }
      }

      // Atomically try to lease the message (replaces FOR UPDATE SKIP LOCKED)
      var utcNow = DateTime.UtcNow;
      var leaseExpiry = utcNow.Add(settings.LeaseDuration);

      var leased = await dbContext.InboxMessages
                                  .Where(x => x.MessageId == messageId)
                                  .Where(x => x.ConsumerId == _consumerId)
                                  .Where(x => x.State == MessageState.New)
                                  .Where(x => x.LeasedUntil == null || x.LeasedUntil < utcNow)
                                  .ExecuteUpdateAsync(
                                     x => x.SetProperty(m => m.LeasedUntil, leaseExpiry),
                                     ct);

      if (leased == 0)
      {
         return;
      }

      // Read retry count for potential retry handling (only when retry is enabled)
      int currentRetryCount = 0;
      if (settings.InboxRetryEnabled)
      {
         currentRetryCount = await dbContext.InboxMessages
                                            .Where(x => x.MessageId == messageId && x.ConsumerId == _consumerId)
                                            .Select(x => x.RetryCount)
                                            .FirstOrDefaultAsync(ct);
      }

      await using var transactionScope = await dbContext.Database
                                                        .BeginTransactionAsync(ct);

      try
      {
         await ConsumeAsync(context.Message, transactionScope, ct);

         // Persist any domain changes the consumer made via the DbContext
         await dbContext.SaveChangesAsync(ct);

         // Mark as done only if our lease is still valid — if another consumer stole the
         // lease (because processing exceeded LeaseDuration), this returns 0 and we roll
         // back to avoid overwriting the other consumer's result.
         var updated = await dbContext.InboxMessages
                                      .Where(x => x.MessageId == messageId && x.ConsumerId == _consumerId)
                                      .Where(x => x.LeasedUntil == leaseExpiry)
                                      .ExecuteUpdateAsync(
                                         x => x.SetProperty(m => m.State, MessageState.Done)
                                               .SetProperty(m => m.UpdatedAt, DateTime.UtcNow),
                                         ct);

         if (updated == 0)
         {
            await transactionScope.RollbackAsync(ct);
            return;
         }

         await transactionScope.CommitAsync(ct);
      }
      catch (Exception ex)
      {
         InboxLog.ConsumeError(logger, messageId, _consumerId, ex);

         await transactionScope.RollbackAsync(CancellationToken.None);

         if (!settings.InboxRetryEnabled)
         {
            // Release the lease so the message can be retried by MassTransit.
            // Lease check prevents overwriting state if another consumer took over.
            await dbContext.InboxMessages
                           .Where(x => x.MessageId == messageId && x.ConsumerId == _consumerId)
                           .Where(x => x.LeasedUntil == leaseExpiry)
                           .ExecuteUpdateAsync(
                              x => x.SetProperty(m => m.LeasedUntil, (DateTime?)null)
                                    .SetProperty(m => m.UpdatedAt, DateTime.UtcNow),
                              CancellationToken.None);
            throw;
         }

         var newRetryCount = currentRetryCount + 1;

         var isFailed = newRetryCount > settings.InboxRetryIntervals.Length;

         var nextRetryAt = isFailed
            ? (DateTime?)null
            : RetryDelayCalculator.ComputeNextRetryAt(currentRetryCount, settings);

         var newState = isFailed ? MessageState.Failed : MessageState.New;

         var errorMessage = ex.ToString();

         if (errorMessage.Length > 4000)
         {
            errorMessage = errorMessage[..4000];
         }

         // Update retry state and release lease — lease check prevents overwriting
         // state if another consumer took over after our lease expired.
         await dbContext.InboxMessages
                        .Where(x => x.MessageId == messageId && x.ConsumerId == _consumerId)
                        .Where(x => x.LeasedUntil == leaseExpiry)
                        .ExecuteUpdateAsync(x => x.SetProperty(p => p.UpdatedAt, DateTime.UtcNow)
                                                  .SetProperty(p => p.RetryCount, newRetryCount)
                                                  .SetProperty(p => p.State, newState)
                                                  .SetProperty(p => p.NextRetryAt, nextRetryAt)
                                                  .SetProperty(p => p.LastError, errorMessage)
                                                  .SetProperty(p => p.LeasedUntil, (DateTime?)null),
                           CancellationToken.None);

         if (isFailed)
         {
            InboxLog.RetryExhausted(logger, messageId, _consumerId, newRetryCount);
         }
         else
         {
            InboxLog.RetryScheduled(logger, messageId, _consumerId, newRetryCount, nextRetryAt!.Value);
         }
      }
   }

   /// <summary>
   ///    Processes the incoming message within a managed transaction. The base class
   ///    handles idempotency, locking, commit, and rollback — implementations should
   ///    only contain domain logic.
   /// </summary>
   /// <param name="message">The deserialized message payload.</param>
   /// <param name="transactionScope">
   ///    The active database transaction. Enlist additional operations in this
   ///    transaction if they must be atomic with the inbox state change.
   /// </param>
   /// <param name="ct">Cancellation token propagated from the MassTransit consume pipeline.</param>
   protected abstract Task ConsumeAsync(TMessage message, IDbContextTransaction transactionScope, CancellationToken ct);

   private static string GetVersionAgnosticTypeName()
   {
      var type = typeof(TMessage);
      return $"{type.FullName}, {type.Assembly.GetName().Name}";
   }
}

internal static partial class InboxLog
{
   [LoggerMessage(Level = LogLevel.Error, Message = "Failed to consume inbox message {MessageId} by {ConsumerId}")]
   public static partial void ConsumeError(ILogger logger, Guid? messageId, string consumerId, Exception ex);

   [LoggerMessage(Level = LogLevel.Warning,
      Message = "Inbox message {MessageId} for {ConsumerId} scheduled for retry {RetryCount} at {NextRetryAt}")]
   public static partial void RetryScheduled(ILogger logger, Guid? messageId, string consumerId, int retryCount,
      DateTime nextRetryAt);

   [LoggerMessage(Level = LogLevel.Error,
      Message = "Inbox message {MessageId} for {ConsumerId} exhausted all {RetryCount} retry attempts")]
   public static partial void RetryExhausted(ILogger logger, Guid? messageId, string consumerId, int retryCount);
}
