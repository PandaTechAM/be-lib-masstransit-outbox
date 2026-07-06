using MassTransit.EfCoreOutbox.Abstractions;
using MassTransit.EfCoreOutbox.Entities;
using MassTransit.EfCoreOutbox.Extensions;
using Microsoft.EntityFrameworkCore;

namespace MassTransit.EfCoreOutbox.Demo.Consumer.Context;

public class EfCoreConsumerContext(DbContextOptions<EfCoreConsumerContext> options) : DbContext(options),
    IOutboxDbContext, IInboxDbContext
{
    public DbSet<InboxMessage> InboxMessages { get; set; }
    public DbSet<OutboxMessage> OutboxMessages { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(EfCoreConsumerContext).Assembly);
        modelBuilder.ConfigureInboxOutboxEntities();
    }
}
