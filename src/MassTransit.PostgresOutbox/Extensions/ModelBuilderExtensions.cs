using MassTransit.PostgresOutbox.Entities;
using Microsoft.EntityFrameworkCore;

namespace MassTransit.PostgresOutbox.Extensions;

public static class ModelBuilderExtensions
{
   /// <summary>
   ///    Configures the OutboxMessage entity (primary key, value generation).
   /// </summary>
   public static ModelBuilder ConfigureOutboxMessageEntity(this ModelBuilder modelBuilder)
   {
      var entity = modelBuilder.Entity<OutboxMessage>();

      entity.HasKey(x => x.Id);
      entity.Property(x => x.Id)
            .ValueGeneratedNever();

      return modelBuilder;
   }

   /// <summary>
   ///    Configures the InboxMessage entity (composite primary key).
   /// </summary>
   public static ModelBuilder ConfigureInboxMessageEntity(this ModelBuilder modelBuilder)
   {
      var entity = modelBuilder.Entity<InboxMessage>();

      entity.HasKey(x => new
      {
         x.MessageId,
         x.ConsumerId
      });

      return modelBuilder;
   }

   /// <summary>
   ///    Configures both Inbox and Outbox entities. Call this in your DbContext.OnModelCreating.
   /// </summary>
   public static ModelBuilder ConfigureInboxOutboxEntities(this ModelBuilder modelBuilder)
   {
      modelBuilder.ConfigureOutboxMessageEntity();
      modelBuilder.ConfigureInboxMessageEntity();

      return modelBuilder;
   }
}