using MassTransit.SQLiteOutbox.Abstractions;
using MassTransit.SQLiteOutbox.Entities;
using MassTransit.SQLiteOutbox.Extensions;
using Microsoft.EntityFrameworkCore;

namespace MassTransit.SQLiteOutbox.Demo.Publisher.Context;

public class SqlitePublisherContext(DbContextOptions<SqlitePublisherContext> options) : DbContext(options),
   IOutboxDbContext, IInboxDbContext
{
   public DbSet<InboxMessage> InboxMessages { get; set; }
   public DbSet<OutboxMessage> OutboxMessages { get; set; }

   protected override void OnModelCreating(ModelBuilder modelBuilder)
   {
      base.OnModelCreating(modelBuilder);

      modelBuilder.ApplyConfigurationsFromAssembly(typeof(SqlitePublisherContext).Assembly);
      modelBuilder.ConfigureInboxOutboxEntities();
   }
}