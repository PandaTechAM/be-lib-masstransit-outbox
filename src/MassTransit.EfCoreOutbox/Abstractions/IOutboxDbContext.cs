using MassTransit.EfCoreOutbox.Entities;
using Microsoft.EntityFrameworkCore;

namespace MassTransit.EfCoreOutbox.Abstractions;

public interface IOutboxDbContext
{
   DbSet<OutboxMessage> OutboxMessages { get; set; }
}
