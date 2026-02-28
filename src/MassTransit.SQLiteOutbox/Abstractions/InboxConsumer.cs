using MassTransit.SQLiteOutbox.Entities;
using MassTransit.SQLiteOutbox.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MassTransit.SQLiteOutbox.Abstractions;

/// <summary>
///    Base class for idempotent message consumption using the inbox pattern with SQLite.
///    Inherit from this class and implement <see cref="Consume(TMessage, IDbContextTransaction)" />
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
                                  .AnyAsync(x => x.MessageId == messageId && x.ConsumerId == _consumerId, ct)
                                  .ConfigureAwait(false);

      if (!exists)
      {
         dbContext.InboxMessages.Add(new InboxMessage
         {
            MessageId = messageId!.Value,
            CreatedAt = DateTime.UtcNow,
            State = MessageState.New,
            ConsumerId = _consumerId
         });

         await dbContext.SaveChangesAsync(ct)
                        .ConfigureAwait(false);
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
                                     ct)
                                  .ConfigureAwait(false);

      if (leased == 0)
      {
         return;
      }

      await using var transactionScope = await dbContext.Database
                                                        .BeginTransactionAsync(ct)
                                                        .ConfigureAwait(false);

      try
      {
         await Consume(context.Message, transactionScope)
            .ConfigureAwait(false);

         await dbContext.InboxMessages
                        .Where(x => x.MessageId == messageId && x.ConsumerId == _consumerId)
                        .ExecuteUpdateAsync(
                           x => x.SetProperty(m => m.State, MessageState.Done)
                                 .SetProperty(m => m.UpdatedAt, DateTime.UtcNow),
                           ct)
                        .ConfigureAwait(false);

         await transactionScope.CommitAsync(ct)
                               .ConfigureAwait(false);
      }
      catch (Exception ex)
      {
         InboxLog.ConsumeError(logger, messageId, _consumerId, ex);

         await transactionScope.RollbackAsync(CancellationToken.None)
                               .ConfigureAwait(false);

         // Release the lease so the message can be retried immediately
         await dbContext.InboxMessages
                        .Where(x => x.MessageId == messageId && x.ConsumerId == _consumerId)
                        .ExecuteUpdateAsync(
                           x => x.SetProperty(m => m.LeasedUntil, (DateTime?)null)
                                 .SetProperty(m => m.UpdatedAt, DateTime.UtcNow),
                           CancellationToken.None)
                        .ConfigureAwait(false);

         throw;
      }
   }

   /// <summary>
   ///    Implement this method to process the incoming message. The provided transaction
   ///    is managed by the base class — commit and rollback are handled automatically.
   /// </summary>
   /// <param name="message">The deserialized message payload.</param>
   /// <param name="transactionScope">
   ///    The active database transaction. Use it if you need to enlist additional operations
   ///    in the same atomic unit of work.
   /// </param>
   protected abstract Task Consume(TMessage message, IDbContextTransaction transactionScope);
}

internal static partial class InboxLog
{
   [LoggerMessage(Level = LogLevel.Error, Message = "Failed to consume inbox message {MessageId} by {ConsumerId}")]
   public static partial void ConsumeError(ILogger logger, Guid? messageId, string consumerId, Exception ex);
}