using MassTransit.SQLiteOutbox.Entities;
using Microsoft.EntityFrameworkCore;

namespace MassTransit.SQLiteOutbox.Abstractions;

/// <summary>
///    Marker interface for a <see cref="DbContext" /> that supports the outbox pattern.
///    Implement this on your <see cref="DbContext" /> to enable outbox message publishing.
/// </summary>
public interface IOutboxDbContext
{
   /// <summary>
   ///    The outbox message store. Mapped to the OutboxMessages table.
   /// </summary>
   DbSet<OutboxMessage> OutboxMessages { get; set; }
}