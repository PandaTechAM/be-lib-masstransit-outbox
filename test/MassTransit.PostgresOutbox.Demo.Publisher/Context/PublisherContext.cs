using MassTransit.PostgresOutbox.Abstractions;
using MassTransit.PostgresOutbox.Entities;
using MassTransit.PostgresOutbox.Extensions;
using Microsoft.EntityFrameworkCore;

namespace MassTransit.PostgresOutbox.Demo.Context;

public class PublisherContext(DbContextOptions<PublisherContext> options) : DbContext(options)
   , IOutboxDbContext, IInboxDbContext
{
   public DbSet<InboxMessage> InboxMessages { get; set; }
   public DbSet<OutboxMessage> OutboxMessages { get; set; }

   protected override void OnModelCreating(ModelBuilder modelBuilder)
   {
      base.OnModelCreating(modelBuilder);

      modelBuilder.ApplyConfigurationsFromAssembly(typeof(PublisherContext).Assembly);
      modelBuilder.ConfigureInboxOutboxEntities();
   }
}