using System.Text.Json;
using MassTransit.PostgresOutbox.Abstractions;
using MassTransit.PostgresOutbox.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MassTransit.PostgresOutbox.Jobs;

internal class InboxMessageRetryService<TDbContext>(
   IServiceScopeFactory serviceScopeFactory,
   IBus bus,
   ILogger<InboxMessageRetryService<TDbContext>> logger,
   Settings settings)
   : BackgroundService
   where TDbContext : DbContext, IInboxDbContext
{
   private readonly int _batchCount = settings.InboxRetryBatchCount;
   private readonly PeriodicTimer _timer = new(settings.InboxRetryPollInterval);

   protected override async Task ExecuteAsync(CancellationToken stoppingToken)
   {
      while (await _timer.WaitForNextTickAsync(stoppingToken))
      {
         using var scope = serviceScopeFactory.CreateScope();
         await using var dbContext = scope.ServiceProvider.GetRequiredService<TDbContext>();

         try
         {
            var utcNow = DateTime.UtcNow;

            var messages = await dbContext.InboxMessages
                                          .Where(x => x.State == MessageState.New)
                                          .Where(x => x.RetryCount > 0)
                                          .Where(x => x.NextRetryAt <= utcNow)
                                          .OrderBy(x => x.NextRetryAt)
                                          .Take(_batchCount)
                                          .ToListAsync(stoppingToken);

            if (messages.Count == 0)
            {
               continue;
            }

            foreach (var message in messages)
            {
               try
               {
                  if (string.IsNullOrEmpty(message.Payload) || 
                      string.IsNullOrEmpty(message.Type) ||
                      string.IsNullOrEmpty(message.DestinationAddress))
                  {
                     InboxRetryLog.MissingRetryData(logger, message.MessageId, message.ConsumerId);
                     message.State = MessageState.Failed;
                     message.LastError = "Missing retry data";
                     message.UpdatedAt = utcNow;
                     continue;
                  }

                  var type = Type.GetType(message.Type);

                  if (type is null)
                  {
                     InboxRetryLog.TypeResolutionFailed(logger, message.MessageId, message.Type);
                     message.State = MessageState.Failed;
                     message.LastError = "Cannot resolve type: " + message.Type;
                     message.UpdatedAt = utcNow;
                     continue;
                  }

                  var messageObject = JsonSerializer.Deserialize(message.Payload, type);
                 
                  var endpoint = await bus.GetSendEndpoint(new Uri(message.DestinationAddress));

                  await endpoint.Send(messageObject!, type,
                     Pipe.Execute<SendContext>(ctx =>
                        ctx.Headers.Set(Constants.OutboxMessageIdHeaderName, message.MessageId)),
                     stoppingToken);
               }
               catch (Exception ex)
               {
                  InboxRetryLog.DispatchFailed(logger, message.MessageId, message.ConsumerId, ex);
               }
            }

            await dbContext.SaveChangesAsync(stoppingToken);
         }
         catch (Exception ex)
         {
            InboxRetryLog.IterationFailed(logger, ex);
         }
      }
   }

   public override void Dispose()
   {
      _timer.Dispose();
      base.Dispose();
   }
}

internal static partial class InboxRetryLog
{
   [LoggerMessage(Level = LogLevel.Error, Message = "Inbox retry iteration failed")]
   public static partial void IterationFailed(ILogger logger, Exception ex);

   [LoggerMessage(Level = LogLevel.Error,
      Message = "Inbox retry dispatch failed for message {MessageId} consumer {ConsumerId}")]
   public static partial void DispatchFailed(ILogger logger, Guid messageId, string consumerId, Exception ex);

   [LoggerMessage(Level = LogLevel.Error,
      Message = "Cannot resolve type '{TypeName}' for inbox retry message {MessageId}")]
   public static partial void TypeResolutionFailed(ILogger logger, Guid messageId, string typeName);

   [LoggerMessage(Level = LogLevel.Error,
      Message = "Missing retry data for inbox message {MessageId} consumer {ConsumerId}")]
   public static partial void MissingRetryData(ILogger logger, Guid messageId, string consumerId);
}
