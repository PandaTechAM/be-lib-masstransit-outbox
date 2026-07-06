using MassTransit.EfCoreOutbox.Entities;
using Microsoft.EntityFrameworkCore;

namespace MassTransit.EfCoreOutbox.Abstractions;

/// <summary>
///     Marks a <see cref="DbContext" /> as owning the outbox table. Implement this to enable
///     outbox publishing for the context.
/// </summary>
public interface IOutboxDbContext
{
    /// <summary>
    ///     The outbox messages awaiting publication.
    /// </summary>
    DbSet<OutboxMessage> OutboxMessages { get; set; }
}
