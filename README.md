# Pandatech MassTransit Outbox

Outbox and inbox pattern implementation for [MassTransit](https://masstransit-project.com/) with **multiple DbContext
support**.

MassTransit's built-in outbox only supports a single `DbContext`. These packages let you reliably publish and consume
messages across many modules, each with its own `DbContext` — designed for modular monolith and microservice
architectures.

| Package                                | Provider             | Concurrency strategy        |
|----------------------------------------|----------------------|-----------------------------|
| `Pandatech.MassTransit.PostgresOutbox` | PostgreSQL           | `FOR UPDATE SKIP LOCKED`    |
| `Pandatech.MassTransit.EfCoreOutbox`   | Any EF Core provider | Lease-based (`LeasedUntil`) |

## Why these packages exist

**Why not MassTransit's built-in outbox?** MassTransit's transactional outbox is tied to a single `DbContext`. In a
modular monolith where each module has its own database context, that limitation forces an architectural compromise. We
built our own to support any number of `DbContext` instances natively.

**Why PostgreSQL first?** PostgreSQL's `FOR UPDATE SKIP LOCKED` gives us true row-level locking with zero contention —
the most efficient concurrency strategy for outbox polling. It was the natural starting point for a production-grade
implementation.

**Why a separate EF Core package?** Not every service runs on PostgreSQL. The EF Core package uses a lease-based
concurrency strategy that works with any relational database supported by EF Core — SQLite, SQL Server, MySQL, or
PostgreSQL itself. If you're on PostgreSQL and want maximum efficiency, use the PostgreSQL package. For everything else,
use the EF Core package.

## Features

- **Multiple DbContext support** — each module gets its own `DbContext`, outbox, and inbox
- **Outbox pattern** — messages are persisted atomically with your domain changes, then published by a background
  service
- **Inbox pattern** — idempotent message consumption prevents duplicate processing
- **Database-backed inbox retry** — failed messages are retried with configurable delay intervals, surviving process
  restarts and deployments
- **Background cleanup** — processed messages are automatically removed after a configurable retention period
- **Zero-allocation logging** — uses `[LoggerMessage]` source generators throughout
- **Multi-TFM** — targets `net9.0` and `net10.0`
- **Wire-compatible** — both packages serialize messages identically (`System.Text.Json`, same MassTransit header
  convention), so they interoperate seamlessly via the shared message broker

## Installation

```bash
# PostgreSQL (recommended when running on PostgreSQL)
dotnet add package Pandatech.MassTransit.PostgresOutbox

# EF Core (any relational database provider)
dotnet add package Pandatech.MassTransit.EfCoreOutbox
```

## Quick start

The API surface is identical for both packages. Examples below use the PostgreSQL package — replace the namespace with
`MassTransit.EfCoreOutbox` for the EF Core package.

### 1. Configure your DbContext

Implement `IOutboxDbContext`, `IInboxDbContext`, or both, and call `ConfigureInboxOutboxEntities` in `OnModelCreating`:

```csharp
using MassTransit.PostgresOutbox.Abstractions;
using MassTransit.PostgresOutbox.Extensions;

public class OrdersDbContext : DbContext, IOutboxDbContext, IInboxDbContext
{
    public DbSet<OutboxMessage> OutboxMessages { get; set; }
    public DbSet<InboxMessage> InboxMessages { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ConfigureInboxOutboxEntities();
    }
}
```

**PostgreSQL only** — enable `UseQueryLocks()` for the `FOR UPDATE SKIP LOCKED` feature:

```csharp
builder.Services.AddDbContextPool<OrdersDbContext>(options =>
    options.UseNpgsql(connectionString)
           .UseQueryLocks());
```

### 2. Register services

```csharp
using MassTransit.PostgresOutbox.Extensions;

// Registers outbox publisher + outbox cleanup + inbox cleanup background services
services.AddOutboxInboxServices<OrdersDbContext>();
```

To customize behavior, pass a `Settings` object:

```csharp
services.AddOutboxInboxServices<OrdersDbContext>(new Settings
{
    PublisherTimerPeriod = TimeSpan.FromSeconds(2),
    PublisherBatchCount = 50,
    OutboxRemovalBeforeInDays = 7,
    InboxRemovalBeforeInDays = 7,
    InboxRetryEnabled = true,
    InboxRetryIntervals =
    [
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(30),
        TimeSpan.FromHours(2),
        TimeSpan.FromHours(6),
        TimeSpan.FromHours(12),
        TimeSpan.FromHours(24)
    ]
});
```

You can also register services individually:

```csharp
services.AddOutboxPublisherJob<OrdersDbContext>();
services.AddOutboxRemovalJob<OrdersDbContext>();
services.AddInboxRemovalJob<OrdersDbContext>();
services.AddInboxRetryJob<OrdersDbContext>();
```

> **EF Core only** — `Settings` has an additional `LeaseDuration` property (default: 5 minutes) that controls how long a
> message is leased before becoming available for reprocessing after a crash.

### 3. Publish messages (outbox)

Add your message to the outbox within the same `SaveChangesAsync` call as your domain changes:

```csharp
dbContext.Orders.Add(new Order
{
    Amount = 555,
    CreatedAt = DateTime.UtcNow
});

dbContext.AddToOutbox(new OrderCreatedEvent { OrderId = orderId });

await dbContext.SaveChangesAsync();
```

To add multiple messages at once:

```csharp
dbContext.AddToOutboxRange(event1, event2, event3);
await dbContext.SaveChangesAsync();
```

Both methods return the generated outbox message ID(s) for correlation if needed.

The background publisher picks up new messages, publishes them via MassTransit, and marks them as done.

Transient publish failures (e.g. broker unavailable) leave the message in `New` state and it is retried on the next
tick. Non-retryable failures (unresolvable or malformed message type, undeserializable payload, message type rejected
by the bus, invalid destination URI) mark the message as `Failed` so it cannot head-of-line block newer messages;
`Failed` outbox messages are kept for manual review and can be re-enqueued by setting their state back to `New`.

### 3a. Send to specific endpoints (targeted delivery)

When you need to deliver a message to a specific endpoint rather than broadcasting to all subscribers, use the
`AddToOutbox` overloads that accept a destination `Uri`. The background publisher will use MassTransit `Send` instead of
`Publish` for these messages.

**Single destination:**

```csharp
dbContext.AddToOutbox(
    new ConfigUpdatedEvent { DeviceId = deviceId },
    new Uri("queue:device-42"));

await dbContext.SaveChangesAsync();
```

**Multiple destinations (same message):**

```csharp
var destinations = deviceIds.Select(id => new Uri($"queue:device-{id}"));

dbContext.AddToOutbox(new ConfigUpdatedEvent { Version = 5 }, destinations);

await dbContext.SaveChangesAsync();
```

One outbox row is created per destination. All rows share the same serialized payload and are delivered independently.
Messages without a destination continue to use `Publish` — existing code is unaffected.

### 4. Consume messages (inbox)

Create a consumer that inherits from `InboxConsumer<TMessage, TDbContext>`:

```csharp
using MassTransit.PostgresOutbox.Abstractions;
using Microsoft.EntityFrameworkCore.Storage;

public class OrderCreatedConsumer(IServiceProvider sp)
    : InboxConsumer<OrderCreatedEvent, OrdersDbContext>(sp)
{
    protected override async Task ConsumeAsync(
        OrderCreatedEvent message,
        IDbContextTransaction transaction,
        CancellationToken ct)
    {
        // Your idempotent processing logic here.
        // The transaction is managed by InboxConsumer — just do your work.
    }
}
```

The base class handles deduplication (by `MessageId` + `ConsumerId`) and concurrency. In PostgreSQL this uses
`FOR UPDATE SKIP LOCKED`; in the EF Core package it uses atomic lease acquisition.

## How it works

### Outbox flow

Your code calls `AddToOutbox()` + `SaveChangesAsync()` → the message is persisted in the `OutboxMessages` table
atomically with your domain changes → a background `HostedService` polls for new messages, publishes (or sends, when a
destination is specified) them via MassTransit, and marks them as done → a cleanup service deletes old processed messages.

### Inbox flow

MassTransit delivers a message to your `InboxConsumer` → the base class inserts or finds the `InboxMessage` row →
acquires an exclusive lock (PostgreSQL) or lease (EF Core) → calls your `ConsumeAsync` method → marks the message as
done and commits → if your code throws, the transaction rolls back and the message is retried by MassTransit's in-memory
retry/redelivery (default) or by the database-backed retry service (when enabled).

### Inbox retry

When `InboxRetryEnabled = true`, the consumer **never re-throws** — instead it records the failure in the database with
a `RetryCount` and a `NextRetryAt` timestamp computed from the `InboxRetryIntervals` array. A background service polls
for due messages and re-dispatches them through MassTransit. This means retries survive process restarts, deployments,
and crashes.

When `InboxRetryEnabled = false` (default), the consumer re-throws the exception so MassTransit's own
`UseMessageRetry` / `UseDelayedRedelivery` policies handle it instead.

> **These two mechanisms are mutually exclusive.** When inbox retry is enabled the consumer never throws, so
> MassTransit's
> in-memory retry and redelivery filters will not trigger. Choose one or the other.

Configuration mirrors MassTransit's `UseDelayedRedelivery(r => r.Intervals(…))` API:

```csharp
services.AddOutboxInboxServices<OrdersDbContext>(new Settings
{
    InboxRetryEnabled = true,
    InboxRetryIntervals =
    [
        TimeSpan.FromSeconds(10),
        TimeSpan.FromMinutes(2),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(30),
        TimeSpan.FromHours(2),
        TimeSpan.FromHours(6),
        TimeSpan.FromHours(12),
        TimeSpan.FromHours(24)
    ]
});
```

Once all intervals are exhausted the message is marked as `Failed` and will not be retried again.

## Settings reference

| Property                    | Default     | Description                                                     |
|-----------------------------|-------------|-----------------------------------------------------------------|
| `PublisherTimerPeriod`      | 1 second    | How often the publisher polls for new outbox messages           |
| `PublisherBatchCount`       | 100         | Max messages published per tick                                 |
| `OutboxRemovalBeforeInDays` | 5           | Days to retain processed outbox messages                        |
| `OutboxRemovalTimerPeriod`  | 1 day       | How often outbox cleanup runs                                   |
| `InboxRemovalBeforeInDays`  | 5           | Days to retain processed inbox messages                         |
| `InboxRemovalTimerPeriod`   | 1 day       | How often inbox cleanup runs                                    |
| `InboxRetryEnabled`         | `false`     | Enable database-backed inbox retry                              |
| `InboxRetryIntervals`       | 10 s → 24 h | Delay before each retry attempt; array length = max retry count |
| `InboxRetryPollInterval`    | 5 seconds   | How often the retry service polls for due messages              |
| `InboxRetryBatchCount`      | 50          | Max messages retried per tick                                   |
| `LeaseDuration` *(EF Core)* | 5 minutes   | How long a message lease is held                                |

## License

MIT
