using MassTransit.EfCoreOutbox.Entities;
using Microsoft.EntityFrameworkCore;

namespace MassTransit.EfCoreOutbox.Abstractions;

/// <summary>
///     Marks a <see cref="DbContext" /> as owning the inbox table. Implement this to enable
///     idempotent inbox consumption for the context.
/// </summary>
public interface IInboxDbContext
{
    /// <summary>
    ///     The inbox messages tracked for idempotency and retry.
    /// </summary>
    DbSet<InboxMessage> InboxMessages { get; set; }
}
