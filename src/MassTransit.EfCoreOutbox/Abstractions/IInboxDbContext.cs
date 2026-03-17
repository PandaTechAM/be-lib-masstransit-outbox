using MassTransit.EfCoreOutbox.Entities;
using Microsoft.EntityFrameworkCore;

namespace MassTransit.EfCoreOutbox.Abstractions;

public interface IInboxDbContext
{
   DbSet<InboxMessage> InboxMessages { get; set; }
}
