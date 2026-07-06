using MassTransit.PostgresOutbox.Abstractions;
using MassTransit.PostgresOutbox.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MassTransit.PostgresOutbox.Jobs;

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
        while (await _timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = serviceScopeFactory.CreateScope();
                await using var dbContext = scope.ServiceProvider.GetRequiredService<TDbContext>();

                var daysBefore = DateTime.UtcNow.AddDays(-_beforeInDays);

                await dbContext.OutboxMessages
                    .Where(x => x.State == MessageState.Done)
                    .Where(x => x.UpdatedAt < daysBefore)
                    .ExecuteDeleteAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                OutboxRemovalLog.IterationFailed(logger, ex);
            }
        }
    }

    public override void Dispose()
    {
        _timer.Dispose();
        base.Dispose();
    }
}

internal static partial class OutboxRemovalLog
{
    [LoggerMessage(Level = LogLevel.Error, Message = "Outbox removal iteration failed")]
    public static partial void IterationFailed(ILogger logger, Exception ex);
}
