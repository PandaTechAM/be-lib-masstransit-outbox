using MassTransit.PostgresOutbox.Entities;
using Microsoft.EntityFrameworkCore;

namespace MassTransit.PostgresOutbox.Extensions;

public static class ModelBuilderExtensions
{
   extension(ModelBuilder modelBuilder)
   {
      public ModelBuilder ConfigureOutboxMessageEntity()
      {
         var entity = modelBuilder.Entity<OutboxMessage>();

         entity.HasKey(x => x.Id);
         entity.Property(x => x.Id)
               .ValueGeneratedNever();

         return modelBuilder;
      }

      public ModelBuilder ConfigureInboxMessageEntity()
      {
         var entity = modelBuilder.Entity<InboxMessage>();

         entity.HasKey(x => new
         {
            x.MessageId,
            x.ConsumerId
         });

         return modelBuilder;
      }

      public ModelBuilder ConfigureInboxOutboxEntities()
      {
         modelBuilder.ConfigureOutboxMessageEntity();
         modelBuilder.ConfigureInboxMessageEntity();

         return modelBuilder;
      }
   }
}