# Pandatech MassTransit Outbox

Outbox and inbox pattern implementation for [MassTransit](https://masstransit-project.com/) with **multiple DbContext
support**.

MassTransit's built-in outbox only supports a single `DbContext`. These packages let you reliably publish and consume
messages across many modules, each with its own `DbContext` — designed for modular monolith and microservice
architectures.

| Package                                | Provider   | Concurrency strategy        |
|----------------------------------------|------------|-----------------------------|
| `Pandatech.MassTransit.PostgresOutbox` | PostgreSQL | `FOR UPDATE SKIP LOCKED`    |
| `Pandatech.MassTransit.SQLiteOutbox`   | SQLite     | Lease-based (`LeasedUntil`) |

Both packages are **wire-compatible** — a service using PostgreSQL for its outbox can publish to a service using SQLite
for its inbox, and vice versa.

## Features

- **Multiple DbContext support** — each module gets its own `DbContext`, outbox, and inbox
- **Outbox pattern** — messages are persisted atomically with your domain changes, then published by a background
  service
- **Inbox pattern** — idempotent message consumption prevents duplicate processing
- **Background cleanup** — processed messages are automatically removed after a configurable retention period
- **Zero-allocation logging** — uses `[LoggerMessage]` source generators throughout
- **Multi-TFM** — targets `net9.0`, and `net10.0`

## Installation

```bash
# PostgreSQL
dotnet add package Pandatech.MassTransit.PostgresOutbox

# SQLite
dotnet add package Pandatech.MassTransit.SqliteOutbox
```

## Quick start

The API surface is identical for both providers. Examples below use the PostgreSQL package — replace the namespace with
`MassTransit.SQLiteOutbox` for SQLite.

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
    InboxRemovalBeforeInDays = 7
});
```

You can also register services individually:

```csharp
services.AddOutboxPublisherJob<OrdersDbContext>();
services.AddOutboxRemovalJob<OrdersDbContext>();
services.AddInboxRemovalJob<OrdersDbContext>();
```

> **SQLite only** — `Settings` has an additional `LeaseDuration` property (default: 5 minutes) that controls how long a
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
`FOR UPDATE SKIP LOCKED`; in SQLite it uses atomic lease acquisition.

## How it works

### Outbox flow

Your code calls `AddToOutbox()` + `SaveChangesAsync()` → the message is persisted in the `OutboxMessages` table
atomically with your domain changes → a background `HostedService` polls for new messages, publishes them via
MassTransit, and marks them as done → a cleanup service deletes old processed messages.

### Inbox flow

MassTransit delivers a message to your `InboxConsumer` → the base class inserts or finds the `InboxMessage` row →
acquires an exclusive lock (PostgreSQL) or lease (SQLite) → calls your `ConsumeAsync` method → marks the message as done
and commits → if your code throws, the transaction rolls back and the message is retried.

## Cross-provider compatibility

Both packages serialize messages identically (`System.Text.Json`, same MassTransit header convention), so they are fully
wire-compatible. A modular monolith can have some modules using PostgreSQL and others using SQLite — messages flow
seamlessly between them via the shared message broker.

## Settings reference

| Property                        | Default   | Description                                           |
|---------------------------------|-----------|-------------------------------------------------------|
| `PublisherTimerPeriod`          | 1 second  | How often the publisher polls for new outbox messages |
| `PublisherBatchCount`           | 100       | Max messages published per tick                       |
| `OutboxRemovalBeforeInDays`     | 5         | Days to retain processed outbox messages              |
| `OutboxRemovalTimerPeriod`      | 1 day     | How often outbox cleanup runs                         |
| `InboxRemovalBeforeInDays`      | 5         | Days to retain processed inbox messages               |
| `InboxRemovalTimerPeriod`       | 1 day     | How often inbox cleanup runs                          |
| `LeaseDuration` *(SQLite only)* | 5 minutes | How long a message lease is held                      |

## License

MIT