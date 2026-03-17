# CLAUDE.md

## Project overview

MassTransit outbox/inbox library with two NuGet packages:
- `Pandatech.MassTransit.PostgresOutbox` — PostgreSQL-specific, uses `FOR UPDATE SKIP LOCKED`
- `Pandatech.MassTransit.EfCoreOutbox` — provider-agnostic, uses lease-based concurrency

Packages are independent — no shared base library. Code duplication between them is intentional.

## Build & test

```bash
dotnet build
dotnet test
```

Solution file: `MassTransit.Outbox.slnx` (modern .slnx format).
Multi-TFM: `net9.0` and `net10.0`. SDK version in `global.json`.
Version is shared via `Directory.Build.props`.

## C# coding conventions

**Indentation:** 3 spaces for `.cs` files (set in `.editorconfig`).

**Braces:** Always required for `if`, `for`, `foreach`, `while`, `lock`, `using` — even single-line bodies. This is enforced as a warning.

```csharp
// correct
if (condition)
{
   return;
}

// wrong — never omit braces
if (condition)
   return;
```

**Namespaces:** File-scoped only (enforced as error).

```csharp
namespace MassTransit.PostgresOutbox.Jobs;
```

**Primary constructors:** Preferred for classes with injected dependencies (suggestion level).

```csharp
internal class OutboxMessagePublisherService<TDbContext>(
   IServiceScopeFactory serviceScopeFactory,
   IBus bus,
   ILogger<OutboxMessagePublisherService<TDbContext>> logger,
   Settings settings)
   : BackgroundService
   where TDbContext : DbContext, IOutboxDbContext
```

**`var` everywhere:** Use `var` for all local variable declarations — built-in types, apparent types, and non-apparent types.

**Method chaining:** Chop always (each `.Method()` on its own line, aligned).

```csharp
var messages = await dbContext.OutboxMessages
                              .Where(x => x.State == MessageState.New)
                              .OrderBy(x => x.Id)
                              .Take(_batchCount)
                              .ToListAsync(stoppingToken);
```

**LINQ expressions:** Chop always.

**Object/collection initializers:** Chop always (each property on its own line).

**Expression bodies:** Preferred for properties, indexers, accessors, lambdas. Not used for methods, constructors, operators, or local functions.

**Nullability:** Enabled project-wide. Null-related diagnostics (CS8600–CS8762) are treated as errors.

**Cancellation token forwarding:** CA2016 is an error — always forward cancellation tokens.

**Logging:** Use `[LoggerMessage]` source-generated logging, not `ILogger.Log*()` methods.

**No `this.` qualifier** — omit unless required for disambiguation.

## Architecture notes

- Each package is self-contained with identical directory structure: `Abstractions/`, `Entities/`, `Enums/`, `Extensions/`, `Jobs/`
- Background services use `BackgroundService` + `PeriodicTimer`
- Settings are registered as a singleton, not `IOptions<T>`
- `ExecuteUpdateAsync` / `ExecuteDeleteAsync` for bulk operations (bypasses change tracker)
- Type serialization uses version-agnostic format: `"Namespace.Type, Assembly"`
- Message IDs use `Guid.CreateVersion7()`
- Failed inbox messages are intentionally kept (not auto-deleted) for investigation
