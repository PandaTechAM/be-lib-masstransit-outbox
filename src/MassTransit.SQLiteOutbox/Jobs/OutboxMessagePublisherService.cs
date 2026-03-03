using System.Text.Json;
using MassTransit.SQLiteOutbox.Abstractions;
using MassTransit.SQLiteOutbox.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MassTransit.SQLiteOutbox.Jobs;

internal class OutboxMessagePublisherService<TDbContext>(
   IServiceScopeFactory serviceScopeFactory,
   ILogger<OutboxMessagePublisherService<TDbContext>> logger,
   Settings settings)
   : BackgroundService
   where TDbContext : DbContext, IOutboxDbContext
{
   private readonly int _batchCount = settings.PublisherBatchCount;
   private readonly TimeSpan _leaseDuration = settings.LeaseDuration;
   private readonly PeriodicTimer _timer = new(settings.PublisherTimerPeriod);

   protected override async Task ExecuteAsync(CancellationToken stoppingToken)
   {
      while (await _timer.WaitForNextTickAsync(stoppingToken))
      {
         using var scope = serviceScopeFactory.CreateScope();
         await using var dbContext = scope.ServiceProvider.GetRequiredService<TDbContext>();
         var publishEndpoint = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();

         try
         {
            var utcNow = DateTime.UtcNow;
            var leaseExpiry = utcNow.Add(_leaseDuration);

            // Read candidate messages
            var messages = await dbContext.OutboxMessages
                                          .Where(x => x.State == MessageState.New)
                                          .Where(x => x.LeasedUntil == null || x.LeasedUntil < utcNow)
                                          .OrderBy(x => x.Id)
                                          .Take(_batchCount)
                                          .ToListAsync(stoppingToken);

            if (messages.Count == 0)
            {
               continue;
            }

            // Atomically lease the batch
            var messageIds = messages.Select(x => x.Id)
                                     .ToList();

            await dbContext.OutboxMessages
                           .Where(x => messageIds.Contains(x.Id))
                           .Where(x => x.LeasedUntil == null || x.LeasedUntil < utcNow)
                           .ExecuteUpdateAsync(
                              x => x.SetProperty(m => m.LeasedUntil, leaseExpiry),
                              stoppingToken);

            messages = await dbContext.OutboxMessages
                                      .Where(x => messageIds.Contains(x.Id))
                                      .Where(x => x.LeasedUntil == leaseExpiry)
                                      .OrderBy(x => x.CreatedAt)
                                      .ToListAsync(stoppingToken);

            if (messages.Count == 0)
            {
               continue;
            }

            // Publish each message
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
                     stoppingToken);

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

            var completedAt = DateTime.UtcNow;

            await dbContext.OutboxMessages
                           .Where(b => publishedMessageIds.Contains(b.Id))
                           .ExecuteUpdateAsync(
                              x => x.SetProperty(m => m.State, MessageState.Done)
                                    .SetProperty(m => m.UpdatedAt, completedAt),
                              stoppingToken);
         }
         catch (Exception ex)
         {
            OutboxPublisherLog.IterationFailed(logger, ex);
         }
      }
   }

   public override void Dispose()
   {
      _timer.Dispose();
      base.Dispose();
   }
}

internal static partial class OutboxPublisherLog
{
   [LoggerMessage(Level = LogLevel.Error, Message = "Outbox publisher iteration failed")]
   public static partial void IterationFailed(ILogger logger, Exception ex);

   [LoggerMessage(Level = LogLevel.Error, Message = "Failed to publish outbox message {MessageId}")]
   public static partial void PublishFailed(ILogger logger, Guid messageId, Exception ex);

   [LoggerMessage(Level = LogLevel.Error,
      Message = "Cannot resolve type '{TypeName}' for outbox message {MessageId}")]
   public static partial void TypeResolutionFailed(ILogger logger, Guid messageId, string typeName);
}