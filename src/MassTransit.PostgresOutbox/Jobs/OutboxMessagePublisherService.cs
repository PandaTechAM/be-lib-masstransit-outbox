using System.Data;
using System.Runtime.Serialization;
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
   IBus bus,
   ILogger<OutboxMessagePublisherService<TDbContext>> logger,
   Settings settings)
   : BackgroundService
   where TDbContext : DbContext, IOutboxDbContext
{
   private readonly int _batchCount = settings.PublisherBatchCount;
   private readonly PeriodicTimer _timer = new(settings.PublisherTimerPeriod);

   protected override async Task ExecuteAsync(CancellationToken stoppingToken)
   {
      while (await _timer.WaitForNextTickAsync(stoppingToken))
      {
         try
         {
            using var scope = serviceScopeFactory.CreateScope();
            await using var dbContext = scope.ServiceProvider.GetRequiredService<TDbContext>();
            await using var transactionScope = await dbContext.Database
                                                              .BeginTransactionAsync(IsolationLevel.ReadCommitted,
                                                                 stoppingToken);

            try
            {
               var messages = await dbContext.OutboxMessages
                                             .Where(x => x.State == MessageState.New)
                                             .OrderBy(x => x.Id)
                                             .ForUpdate(LockBehavior.SkipLocked)
                                             .Take(_batchCount)
                                             .ToListAsync(stoppingToken);

               if (messages.Count == 0)
               {
                  await transactionScope.RollbackAsync(stoppingToken);
                  continue;
               }

               var publishedMessageIds = new List<Guid>(messages.Count);
               var poisonedMessageIds = new List<Guid>();

               foreach (var message in messages)
               {
                  try
                  {
                     var type = Type.GetType(message.Type);

                     if (type is null)
                     {
                        poisonedMessageIds.Add(message.Id);
                        OutboxPublisherLog.TypeResolutionFailed(logger, message.Id, message.Type);
                        continue;
                     }

                     var messageObject = JsonSerializer.Deserialize(message.Payload, type);

                     if (message.DestinationAddress is not null)
                     {
                        var endpoint = await bus.GetSendEndpoint(new Uri(message.DestinationAddress));
                        await endpoint.Send(messageObject!,
                           type,
                           x => x.Headers.Set(Constants.OutboxMessageIdHeaderName, message.Id),
                           stoppingToken);
                     }
                     else
                     {
                        await bus.Publish(messageObject!,
                           type,
                           x => x.Headers.Set(Constants.OutboxMessageIdHeaderName, message.Id),
                           stoppingToken);
                     }

                     publishedMessageIds.Add(message.Id);
                  }
                  catch (Exception ex) when (ex is ArgumentException
                                                or JsonException
                                                or NotSupportedException
                                                or SerializationException
                                                or TypeLoadException
                                                or FileLoadException
                                                or BadImageFormatException
                                                or UriFormatException)
                  {
                     // Deterministic failures (malformed type name, undeserializable payload, message
                     // type rejected by the bus, bad destination URI) can never succeed on retry.
                     // Without dead-lettering they would be re-selected and re-thrown every tick
                     // forever, head-of-line blocking newer messages.
                     poisonedMessageIds.Add(message.Id);
                     OutboxPublisherLog.DeadLettered(logger, message.Id, ex);
                  }
                  catch (Exception ex)
                  {
                     OutboxPublisherLog.PublishFailed(logger, message.Id, ex);
                  }
               }

               if (publishedMessageIds.Count == 0 && poisonedMessageIds.Count == 0)
               {
                  await transactionScope.RollbackAsync(stoppingToken);
                  continue;
               }

               var utcNow = DateTime.UtcNow;

               if (publishedMessageIds.Count > 0)
               {
                  await dbContext.OutboxMessages
                                 .Where(b => publishedMessageIds.Contains(b.Id))
                                 .ExecuteUpdateAsync(x => x.SetProperty(m => m.State, MessageState.Done)
                                                           .SetProperty(m => m.UpdatedAt, utcNow),
                                    stoppingToken);
               }

               if (poisonedMessageIds.Count > 0)
               {
                  await dbContext.OutboxMessages
                                 .Where(b => poisonedMessageIds.Contains(b.Id))
                                 .ExecuteUpdateAsync(x => x.SetProperty(m => m.State, MessageState.Failed)
                                                           .SetProperty(m => m.UpdatedAt, utcNow),
                                    stoppingToken);
               }

               await transactionScope.CommitAsync(stoppingToken);
            }
            catch (Exception ex)
            {
               OutboxPublisherLog.IterationFailed(logger, ex);
               await transactionScope.RollbackAsync(CancellationToken.None);
            }
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

   [LoggerMessage(Level = LogLevel.Error, Message = "Cannot resolve type '{TypeName}' for outbox message {MessageId}")]
   public static partial void TypeResolutionFailed(ILogger logger, Guid messageId, string typeName);

   [LoggerMessage(Level = LogLevel.Error,
      Message = "Outbox message {MessageId} failed with a non-retryable error and was marked Failed")]
   public static partial void DeadLettered(ILogger logger, Guid messageId, Exception ex);
}