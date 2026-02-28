using MassTransit.SQLiteOutbox.Abstractions;
using MassTransit.SQLiteOutbox.Entities;
using MassTransit.SQLiteOutbox.Extensions;
using Microsoft.EntityFrameworkCore;

namespace MassTransit.SQLiteOutbox.Demo.Consumer.Context;

public class SqliteConsumerContext(DbContextOptions<SqliteConsumerContext> options) : DbContext(options),
   IOutboxDbContext, IInboxDbContext
{
   public DbSet<InboxMessage> InboxMessages { get; set; }
   public DbSet<OutboxMessage> OutboxMessages { get; set; }

   protected override void OnModelCreating(ModelBuilder modelBuilder)
   {
      base.OnModelCreating(modelBuilder);

      modelBuilder.ApplyConfigurationsFromAssembly(typeof(SqliteConsumerContext).Assembly);
      modelBuilder.ConfigureInboxOutboxEntities();
   }
}