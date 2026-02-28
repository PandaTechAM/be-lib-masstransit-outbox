using System.Data;
using EFCore.PostgresExtensions.Enums;
using EFCore.PostgresExtensions.Extensions;
using MassTransit.PostgresOutbox.Entities;
using MassTransit.PostgresOutbox.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MassTransit.PostgresOutbox.Abstractions;

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

      await using var transactionScope = await dbContext.Database
                                                        .BeginTransactionAsync(IsolationLevel.ReadCommitted, ct)
                                                        .ConfigureAwait(false);

      var inboxMessage = await dbContext.InboxMessages
                                        .Where(x => x.MessageId == messageId)
                                        .Where(x => x.ConsumerId == _consumerId)
                                        .Where(x => x.State == MessageState.New)
                                        .ForUpdate(LockBehavior.SkipLocked)
                                        .FirstOrDefaultAsync(ct)
                                        .ConfigureAwait(false);
      if (inboxMessage is null)
      {
         return;
      }

      try
      {
         await Consume(context.Message, transactionScope)
            .ConfigureAwait(false);

         inboxMessage.State = MessageState.Done;
         inboxMessage.UpdatedAt = DateTime.UtcNow;

         await dbContext.SaveChangesAsync(ct)
                        .ConfigureAwait(false);
         await transactionScope.CommitAsync(ct)
                               .ConfigureAwait(false);
      }
      catch (Exception ex)
      {
         InboxLog.ConsumeError(logger, messageId, _consumerId, ex);

         await transactionScope.RollbackAsync(CancellationToken.None)
                               .ConfigureAwait(false);

         await dbContext.InboxMessages
                        .Where(x => x.MessageId == messageId && x.ConsumerId == _consumerId)
                        .ExecuteUpdateAsync(x => x.SetProperty(p => p.UpdatedAt, DateTime.UtcNow), ct)
                        .ConfigureAwait(false);

         throw;
      }
   }

   protected abstract Task Consume(TMessage message, IDbContextTransaction transactionScope);
}

internal static partial class InboxLog
{
   [LoggerMessage(Level = LogLevel.Error, Message = "Failed to consume inbox message {MessageId} by {ConsumerId}")]
   public static partial void ConsumeError(ILogger logger, Guid? messageId, string consumerId, Exception ex);
}