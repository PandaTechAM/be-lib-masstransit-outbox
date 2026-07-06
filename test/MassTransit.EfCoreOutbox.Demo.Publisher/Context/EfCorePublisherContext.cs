using MassTransit.EfCoreOutbox.Abstractions;
using MassTransit.EfCoreOutbox.Entities;
using MassTransit.EfCoreOutbox.Extensions;
using Microsoft.EntityFrameworkCore;

namespace MassTransit.EfCoreOutbox.Demo.Publisher.Context;

public class EfCorePublisherContext(DbContextOptions<EfCorePublisherContext> options) : DbContext(options),
    IOutboxDbContext, IInboxDbContext
{
    public DbSet<InboxMessage> InboxMessages { get; set; }
    public DbSet<OutboxMessage> OutboxMessages { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(EfCorePublisherContext).Assembly);
        modelBuilder.ConfigureInboxOutboxEntities();
    }
}
