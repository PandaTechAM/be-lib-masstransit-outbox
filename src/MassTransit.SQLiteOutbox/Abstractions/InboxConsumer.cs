using MassTransit.SQLiteOutbox.Entities;
using MassTransit.SQLiteOutbox.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MassTransit.SQLiteOutbox.Abstractions;

/// <summary>
///    Base class for idempotent message consumption using the inbox pattern with SQLite.
///    Inherit from this class and implement <see cref="ConsumeAsync(TMessage, IDbContextTransaction)" />
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

   /// <summary>
   ///    Initializes a new instance of the <see cref="InboxConsumer{TMessage, TDbContext}" /> class.
   /// </summary>
   /// <param name="sp">The service provider used to resolve scoped dependencies.</param>
   protected InboxConsumer(IServiceProvider sp)
   {
      _consumerId = GetType()
         .ToString();
      _sp = sp;
   }

   /// <inheritdoc />
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
               ConsumerId = _consumerId
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

      await using var transactionScope = await dbContext.Database
                                                        .BeginTransactionAsync(ct);

      try
      {
         await ConsumeAsync(context.Message, transactionScope, ct);

         await dbContext.InboxMessages
                        .Where(x => x.MessageId == messageId && x.ConsumerId == _consumerId)
                        .ExecuteUpdateAsync(
                           x => x.SetProperty(m => m.State, MessageState.Done)
                                 .SetProperty(m => m.UpdatedAt, DateTime.UtcNow),
                           ct);

         await transactionScope.CommitAsync(ct);
      }
      catch (Exception ex)
      {
         InboxLog.ConsumeError(logger, messageId, _consumerId, ex);

         await transactionScope.RollbackAsync(CancellationToken.None);

         // Release the lease so the message can be retried immediately
         await dbContext.InboxMessages
                        .Where(x => x.MessageId == messageId && x.ConsumerId == _consumerId)
                        .ExecuteUpdateAsync(
                           x => x.SetProperty(m => m.LeasedUntil, (DateTime?)null)
                                 .SetProperty(m => m.UpdatedAt, DateTime.UtcNow),
                           CancellationToken.None);

         throw;
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
}

internal static partial class InboxLog
{
   [LoggerMessage(Level = LogLevel.Error, Message = "Failed to consume inbox message {MessageId} by {ConsumerId}")]
   public static partial void ConsumeError(ILogger logger, Guid? messageId, string consumerId, Exception ex);
}