using MassTransit.EfCoreOutbox.Abstractions;
using MassTransit.EfCoreOutbox.Demo.Consumer.Context;
using MassTransit.PostgresOutbox.Demo.Shared.Events;
using Microsoft.EntityFrameworkCore.Storage;

namespace MassTransit.EfCoreOutbox.Demo.Consumer.Services;

public class EfCoreFlakyConsumeService(IServiceProvider sp)
    : InboxConsumer<FlakyEvent, EfCoreConsumerContext>(sp)
{
    private static readonly Random Random = new();

    protected override Task ConsumeAsync(FlakyEvent message,
        IDbContextTransaction dbContextTransaction,
        CancellationToken ct)
    {
        if (Random.NextDouble() < 0.65)
        {
            Console.WriteLine($"[EfCore Consumer] FlakyEvent {message.Id} — FAILED (simulated)");
            throw new InvalidOperationException("Simulated random failure (65% chance)");
        }

        Console.WriteLine($"[EfCore Consumer] FlakyEvent {message.Id} — SUCCESS");
        return Task.CompletedTask;
    }
}
