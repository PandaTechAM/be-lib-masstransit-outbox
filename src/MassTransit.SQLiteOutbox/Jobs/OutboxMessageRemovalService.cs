using MassTransit.SQLiteOutbox.Abstractions;
using MassTransit.SQLiteOutbox.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MassTransit.SQLiteOutbox.Jobs;

internal class OutboxMessageRemovalService<TDbContext>(
   IServiceScopeFactory serviceScopeFactory,
   ILogger<OutboxMessageRemovalService<TDbContext>> logger,
   Settings settings)
   : BackgroundService
   where TDbContext : DbContext, IOutboxDbContext
{
   private readonly int _beforeInDays = settings.OutboxRemovalBeforeInDays;
   private readonly PeriodicTimer _timer = new(settings.OutboxRemovalTimerPeriod);

   protected override async Task ExecuteAsync(CancellationToken stoppingToken)
   {
      while (await _timer.WaitForNextTickAsync(stoppingToken)
                         .ConfigureAwait(false))
      {
         OutboxRemovalLog.IterationStarted(logger);

         using var scope = serviceScopeFactory.CreateScope();
         await using var dbContext = scope.ServiceProvider.GetRequiredService<TDbContext>();

         try
         {
            var daysBefore = DateTime.UtcNow.AddDays(-_beforeInDays);

            var deleted = await dbContext.OutboxMessages
                                         .Where(x => x.State == MessageState.Done)
                                         .Where(x => x.UpdatedAt < daysBefore)
                                         .ExecuteDeleteAsync(stoppingToken)
                                         .ConfigureAwait(false);

            OutboxRemovalLog.IterationCompleted(logger, deleted);
         }
         catch (Exception ex)
         {
            OutboxRemovalLog.IterationFailed(logger, ex);
         }
      }
   }
}

internal static partial class OutboxRemovalLog
{
   [LoggerMessage(Level = LogLevel.Debug, Message = "Outbox removal started iteration")]
   public static partial void IterationStarted(ILogger logger);

   [LoggerMessage(Level = LogLevel.Debug, Message = "Outbox removal completed: {DeletedCount} messages deleted")]
   public static partial void IterationCompleted(ILogger logger, int deletedCount);

   [LoggerMessage(Level = LogLevel.Error, Message = "Outbox removal iteration failed")]
   public static partial void IterationFailed(ILogger logger, Exception ex);
}