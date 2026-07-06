using MassTransit.PostgresOutbox.Abstractions;
using MassTransit.PostgresOutbox.Demo.Consumer.Context;
using MassTransit.PostgresOutbox.Demo.Shared.Events;
using Microsoft.EntityFrameworkCore.Storage;

namespace MassTransit.PostgresOutbox.Demo.Consumer.Services;

public class FlakyConsumeService(IServiceProvider sp)
    : InboxConsumer<FlakyEvent, ConsumerContext>(sp)
{
    private static readonly Random Random = new();

    protected override Task ConsumeAsync(FlakyEvent message,
        IDbContextTransaction dbContextTransaction,
        CancellationToken ct)
    {
        if (Random.NextDouble() < 0.65)
        {
            Console.WriteLine($"[Postgres Consumer] FlakyEvent {message.Id} — FAILED (simulated)");
            throw new InvalidOperationException("Simulated random failure (65% chance)");
        }

        Console.WriteLine($"[Postgres Consumer] FlakyEvent {message.Id} — SUCCESS");
        return Task.CompletedTask;
    }
}
