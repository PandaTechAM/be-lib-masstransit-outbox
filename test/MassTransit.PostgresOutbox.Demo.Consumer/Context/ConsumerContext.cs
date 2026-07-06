using MassTransit.PostgresOutbox.Abstractions;
using MassTransit.PostgresOutbox.Entities;
using MassTransit.PostgresOutbox.Extensions;
using Microsoft.EntityFrameworkCore;

namespace MassTransit.PostgresOutbox.Demo.Consumer.Context;

public class ConsumerContext(DbContextOptions<ConsumerContext> options) : DbContext(options)
    , IOutboxDbContext, IInboxDbContext
{
    public DbSet<InboxMessage> InboxMessages { get; set; }
    public DbSet<OutboxMessage> OutboxMessages { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ConsumerContext).Assembly);
        modelBuilder.ConfigureInboxOutboxEntities();
    }
}
