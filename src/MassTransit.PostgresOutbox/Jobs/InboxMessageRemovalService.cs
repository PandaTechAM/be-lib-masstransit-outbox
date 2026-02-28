using MassTransit.PostgresOutbox.Abstractions;
using MassTransit.PostgresOutbox.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MassTransit.PostgresOutbox.Jobs;

internal class InboxMessageRemovalService<TDbContext>(
   IServiceScopeFactory serviceScopeFactory,
   ILogger<InboxMessageRemovalService<TDbContext>> logger,
   Settings settings)
   : BackgroundService
   where TDbContext : DbContext, IInboxDbContext
{
   private readonly int _beforeInDays = settings.InboxRemovalBeforeInDays;
   private readonly PeriodicTimer _timer = new(settings.InboxRemovalTimerPeriod);

   protected override async Task ExecuteAsync(CancellationToken stoppingToken)
   {
      while (await _timer.WaitForNextTickAsync(stoppingToken)
                         .ConfigureAwait(false))
      {
         InboxRemovalLog.IterationStarted(logger);

         using var scope = serviceScopeFactory.CreateScope();
         await using var dbContext = scope.ServiceProvider.GetRequiredService<TDbContext>();

         try
         {
            var daysBefore = DateTime.UtcNow.AddDays(-_beforeInDays);

            var deleted = await dbContext.InboxMessages
                                         .Where(x => x.State == MessageState.Done)
                                         .Where(x => x.UpdatedAt < daysBefore)
                                         .ExecuteDeleteAsync(stoppingToken)
                                         .ConfigureAwait(false);

            InboxRemovalLog.IterationCompleted(logger, deleted);
         }
         catch (Exception ex)
         {
            InboxRemovalLog.IterationFailed(logger, ex);
         }
      }
   }
}

internal static partial class InboxRemovalLog
{
   [LoggerMessage(Level = LogLevel.Debug, Message = "Inbox removal started iteration")]
   public static partial void IterationStarted(ILogger logger);

   [LoggerMessage(Level = LogLevel.Debug, Message = "Inbox removal completed: {DeletedCount} messages deleted")]
   public static partial void IterationCompleted(ILogger logger, int deletedCount);

   [LoggerMessage(Level = LogLevel.Error, Message = "Inbox removal iteration failed")]
   public static partial void IterationFailed(ILogger logger, Exception ex);
}