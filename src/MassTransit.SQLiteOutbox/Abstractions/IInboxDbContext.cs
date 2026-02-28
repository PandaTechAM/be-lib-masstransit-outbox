using MassTransit.SQLiteOutbox.Entities;
using Microsoft.EntityFrameworkCore;

namespace MassTransit.SQLiteOutbox.Abstractions;

/// <summary>
///    Marker interface for a <see cref="DbContext" /> that supports the inbox pattern.
///    Implement this on your <see cref="DbContext" /> to enable idempotent message consumption.
/// </summary>
public interface IInboxDbContext
{
   /// <summary>
   ///    The inbox message store. Mapped to the InboxMessages table.
   /// </summary>
   DbSet<InboxMessage> InboxMessages { get; set; }
}