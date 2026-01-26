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
      while (await _timer.WaitForNextTickAsync(stoppingToken))
      {
         logger.LogDebug($"{nameof(InboxMessageRemovalService<>)} started iteration");

         using var scope = serviceScopeFactory.CreateScope();
         await using var dbContext = scope.ServiceProvider.GetRequiredService<TDbContext>();

         try
         {
            var daysBefore = DateTime.UtcNow.AddDays(-_beforeInDays);

            await dbContext.InboxMessages
                           .Where(x => x.State == MessageState.Done)
                           .Where(x => x.UpdatedAt < daysBefore)
                           .ExecuteDeleteAsync(stoppingToken);
         }
         catch (Exception ex)
         {
            logger.LogError(ex, ex.Message);
         }

         logger.LogDebug($"{nameof(InboxMessageRemovalService<TDbContext>)} finished iteration");
      }
   }
}