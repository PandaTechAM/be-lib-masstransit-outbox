using System.Data;
using System.Text.Json;
using EFCore.PostgresExtensions.Enums;
using EFCore.PostgresExtensions.Extensions;
using MassTransit.PostgresOutbox.Abstractions;
using MassTransit.PostgresOutbox.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MassTransit.PostgresOutbox.Jobs;

internal class OutboxMessagePublisherService<TDbContext>(
   IServiceScopeFactory serviceScopeFactory,
   ILogger<OutboxMessagePublisherService<TDbContext>> logger,
   Settings settings)
   : BackgroundService
   where TDbContext : DbContext, IOutboxDbContext
{
   private readonly int _batchCount = settings.PublisherBatchCount;
   private readonly PeriodicTimer _timer = new(settings.PublisherTimerPeriod);

   protected override async Task ExecuteAsync(CancellationToken stoppingToken)
   {
      while (await _timer.WaitForNextTickAsync(stoppingToken)
                         .ConfigureAwait(false))
      {
         OutboxPublisherLog.IterationStarted(logger);

         using var scope = serviceScopeFactory.CreateScope();
         await using var dbContext = scope.ServiceProvider.GetRequiredService<TDbContext>();
         var publishEndpoint = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();
         await using var transactionScope = await dbContext.Database
                                                           .BeginTransactionAsync(IsolationLevel.ReadCommitted,
                                                              stoppingToken)
                                                           .ConfigureAwait(false);

         try
         {
            var messages = await dbContext.OutboxMessages
                                          .Where(x => x.State == MessageState.New)
                                          .OrderBy(x => x.CreatedAt)
                                          .ForUpdate(LockBehavior.SkipLocked)
                                          .Take(_batchCount)
                                          .ToListAsync(stoppingToken)
                                          .ConfigureAwait(false);

            if (messages.Count == 0)
            {
               continue;
            }

            var publishedMessageIds = new List<Guid>(messages.Count);

            foreach (var message in messages)
            {
               try
               {
                  var type = Type.GetType(message.Type);

                  if (type is null)
                  {
                     OutboxPublisherLog.TypeResolutionFailed(logger, message.Id, message.Type);
                     continue;
                  }

                  var messageObject = JsonSerializer.Deserialize(message.Payload, type);

                  await publishEndpoint.Publish(messageObject!,
                                          type,
                                          x => x.Headers.Set(Constants.OutboxMessageIdHeaderName, message.Id),
                                          stoppingToken)
                                       .ConfigureAwait(false);

                  publishedMessageIds.Add(message.Id);
               }
               catch (Exception ex)
               {
                  OutboxPublisherLog.PublishFailed(logger, message.Id, ex);
               }
            }

            if (publishedMessageIds.Count == 0)
            {
               continue;
            }

            var utcNow = DateTime.UtcNow;

            await dbContext.OutboxMessages
                           .Where(b => publishedMessageIds.Contains(b.Id))
                           .ExecuteUpdateAsync(x => x.SetProperty(m => m.State, MessageState.Done)
                                                     .SetProperty(m => m.UpdatedAt, utcNow),
                              stoppingToken)
                           .ConfigureAwait(false);

            await transactionScope.CommitAsync(stoppingToken)
                                  .ConfigureAwait(false);

            OutboxPublisherLog.IterationCompleted(logger, publishedMessageIds.Count);
         }
         catch (Exception ex)
         {
            OutboxPublisherLog.IterationFailed(logger, ex);
            await transactionScope.RollbackAsync(CancellationToken.None)
                                  .ConfigureAwait(false);
         }
      }
   }
}

internal static partial class OutboxPublisherLog
{
   [LoggerMessage(Level = LogLevel.Debug, Message = "Outbox publisher started iteration")]
   public static partial void IterationStarted(ILogger logger);

   [LoggerMessage(Level = LogLevel.Debug, Message = "Outbox publisher completed: {PublishedCount} messages published")]
   public static partial void IterationCompleted(ILogger logger, int publishedCount);

   [LoggerMessage(Level = LogLevel.Error, Message = "Outbox publisher iteration failed")]
   public static partial void IterationFailed(ILogger logger, Exception ex);

   [LoggerMessage(Level = LogLevel.Error, Message = "Failed to publish outbox message {MessageId}")]
   public static partial void PublishFailed(ILogger logger, Guid messageId, Exception ex);

   [LoggerMessage(Level = LogLevel.Error,
      Message = "Cannot resolve type '{TypeName}' for outbox message {MessageId}")]
   public static partial void TypeResolutionFailed(ILogger logger, Guid messageId, string typeName);
}