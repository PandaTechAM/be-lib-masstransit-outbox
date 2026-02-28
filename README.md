# Pandatech MassTransit Outbox

Outbox and Inbox pattern implementation for [MassTransit](https://masstransit-project.com/) with **multiple DbContext
support**. Built specifically for modular monolith and microservice architectures where MassTransit's built-in outbox
falls short — it only supports a single `DbContext`. These packages let you reliably publish and consume messages across
hundreds of modules, each with their own `DbContext`.

Two database providers are available. Both packages are **wire-compatible** — a service using PostgreSQL for its outbox
can publish to a service using SQLite for its inbox, and vice versa.

| Package                                | Concurrency strategy        | 
|----------------------------------------|-----------------------------|
| `Pandatech.MassTransit.PostgresOutbox` | `FOR UPDATE SKIP LOCKED`    | 
| `Pandatech.MassTransit.SqliteOutbox`   | Lease-based (`LeasedUntil`) | 

## Features

- **Multiple DbContext support** — operate across hundreds of modules, each with its own `DbContext`
- **Outbox pattern** — messages are persisted atomically with your domain changes, then published by a background
  service
- **Inbox pattern** — idempotent message consumption prevents duplicate processing
- **Background cleanup** — processed messages are automatically removed after a configurable retention period
- **Zero-allocation logging** — uses `[LoggerMessage]` source generators throughout
- **Multi-TFM** — targets `net8.0`, `net9.0`, and `net10.0`

## Installation

```bash
# PostgreSQL
dotnet add package Pandatech.MassTransit.PostgresOutbox

# SQLite
dotnet add package Pandatech.MassTransit.SqliteOutbox
```

## Quick start

The API surface is identical for both providers. Examples below use the PostgreSQL package — replace the namespace with
`MassTransit.SqliteOutbox` for SQLite.

### 1. Configure your DbContext

Your `DbContext` must implement `IOutboxDbContext`, `IInboxDbContext`, or both, and call `ConfigureInboxOutboxEntities`
in `OnModelCreating`:

```csharp
using MassTransit.PostgresOutbox.Abstractions;
using MassTransit.PostgresOutbox.Extensions;

public class AppDbContext : DbContext, IOutboxDbContext, IInboxDbContext
{
    public DbSet<OutboxMessage> OutboxMessages { get; set; }
    public DbSet<InboxMessage> InboxMessages { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ConfigureInboxOutboxEntities();
    }
}
```

**PostgreSQL only** — enable `UseQueryLocks()` for the `FOR UPDATE` feature:

```csharp
builder.Services.AddDbContextPool<AppDbContext>(options =>
    options.UseNpgsql(connectionString)
           .UseQueryLocks());
```

### 2. Register services

```csharp
using MassTransit.PostgresOutbox.Extensions;

// Registers outbox publisher + outbox cleanup + inbox cleanup background services
services.AddOutboxInboxServices<AppDbContext>();

// Or register individually:
// services.AddOutboxPublisherJob<AppDbContext>();
// services.AddOutboxRemovalJob<AppDbContext>();
// services.AddInboxRemovalJob<AppDbContext>();
```

You can optionally pass a `Settings` object to override defaults:

```csharp
services.AddOutboxInboxServices<AppDbContext>(new Settings
{
    PublisherTimerPeriod = TimeSpan.FromSeconds(2),
    PublisherBatchCount = 50,
    OutboxRemovalBeforeInDays = 7,
    InboxRemovalBeforeInDays = 7
});
```

**SQLite only** — the `Settings` class has an additional `LeaseDuration` property (default: 5 minutes) that controls
how long a message is leased before it becomes available for reprocessing after a crash.

### 3. Publish messages (outbox)

Add your message to the outbox within the same `SaveChanges` call as your domain changes:

```csharp
dbContext.Orders.Add(new Order
{
    Amount = 555,
    CreatedAt = DateTime.UtcNow,
});

dbContext.AddToOutbox(new OrderCreatedEvent { OrderId = orderId });

// Or batch:
// dbContext.AddToOutboxRange(event1, event2, event3);

await dbContext.SaveChangesAsync();
```

The background publisher will pick up and publish the message, then mark it as done.

### 4. Consume messages (inbox)

Create a consumer that inherits from `InboxConsumer<TMessage, TDbContext>`:

```csharp
using MassTransit.PostgresOutbox.Abstractions;

public class OrderCreatedConsumer : InboxConsumer<OrderCreatedEvent, AppDbContext>
{
    private readonly AppDbContext _context;

    public OrderCreatedConsumer(AppDbContext dbContext, IServiceProvider sp) : base(sp)
    {
        _context = dbContext;
    }

    public override async Task Consume(OrderCreatedEvent message, IDbContextTransaction transaction)
    {
        // Your idempotent processing logic here.
        // The transaction is managed by InboxConsumer — just do your work.
    }
}
```

The base class handles idempotency (deduplication by `MessageId` + `ConsumerId`) and concurrency. In PostgreSQL this
uses `FOR UPDATE SKIP LOCKED`; in SQLite it uses atomic lease acquisition.

## How it works

**Outbox flow:**
Your code calls `AddToOutbox()` + `SaveChanges()` → message is persisted in the `OutboxMessages` table atomically with
your domain changes → a background `HostedService` polls for new messages, publishes them via MassTransit, and marks
them as done → a cleanup service deletes old processed messages.

**Inbox flow:**
MassTransit delivers a message to your `InboxConsumer` → the base class inserts or finds the `InboxMessage` row →
acquires an exclusive lock (PostgreSQL) or lease (SQLite) → calls your `Consume` method → marks the message as done
and commits → if your code throws, the transaction rolls back and the message is retried.

## Cross-provider compatibility

Both packages serialize messages identically (`System.Text.Json`, same MassTransit header convention), so they are
fully wire-compatible. A modular monolith can have some modules using PostgreSQL and others using SQLite — messages
flow seamlessly between them via the shared message broker.

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