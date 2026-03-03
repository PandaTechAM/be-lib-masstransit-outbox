using MassTransit.SQLiteOutbox.Entities;
using Microsoft.EntityFrameworkCore;

namespace MassTransit.SQLiteOutbox.Extensions;

public static class ModelBuilderExtensions
{
   /// <summary>
   ///    Configures the OutboxMessage entity (primary key, value generation, lease index).
   /// </summary>
   public static ModelBuilder ConfigureOutboxMessageEntity(this ModelBuilder modelBuilder)
   {
      var entity = modelBuilder.Entity<OutboxMessage>();

      entity.HasKey(x => x.Id);
      entity.Property(x => x.Id)
            .ValueGeneratedNever();

      entity.HasIndex(x => new
      {
         x.State,
         x.LeasedUntil,
         x.Id
      });

      return modelBuilder;
   }

   /// <summary>
   ///    Configures the InboxMessage entity (composite primary key, lease index).
   /// </summary>
   public static ModelBuilder ConfigureInboxMessageEntity(this ModelBuilder modelBuilder)
   {
      var entity = modelBuilder.Entity<InboxMessage>();

      entity.HasKey(x => new
      {
         x.MessageId,
         x.ConsumerId
      });

      entity.HasIndex(x => new
      {
         x.State,
         x.LeasedUntil
      });

      return modelBuilder;
   }

   /// <summary>
   ///    Configures both inbox and outbox entity mappings. Call this in
   ///    <see cref="DbContext.OnModelCreating" /> for any context implementing
   ///    <see cref="IOutboxDbContext" /> and/or <see cref="IInboxDbContext" />.
   /// </summary>
   public static ModelBuilder ConfigureInboxOutboxEntities(this ModelBuilder modelBuilder)
   {
      modelBuilder.ConfigureOutboxMessageEntity();
      modelBuilder.ConfigureInboxMessageEntity();

      return modelBuilder;
   }
}