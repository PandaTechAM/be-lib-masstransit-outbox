using System.Text.Json;
using MassTransit.PostgresOutbox.Abstractions;
using MassTransit.PostgresOutbox.Entities;
using MassTransit.PostgresOutbox.Enums;

namespace MassTransit.PostgresOutbox.Extensions;

public static class OutboxDbContextExtensions
{
   public static Guid AddToOutbox<T>(this IOutboxDbContext dbContext, T message)
   {
      var entity = new OutboxMessage
      {
         Id = Guid.NewGuid(),
         CreatedAt = DateTime.UtcNow,
         State = MessageState.New,
         UpdatedAt = null,
         Payload = JsonSerializer.Serialize(message),
         Type = typeof(T).AssemblyQualifiedName!
      };

      dbContext.OutboxMessages.Add(entity);

      return entity.Id;
   }
   public static IReadOnlyList<Guid> AddToOutboxRange<T>(this IOutboxDbContext dbContext, params T[] messages)
   {
      var utcNow = DateTime.UtcNow;

      var entities = messages.Select(message => new OutboxMessage
      {
         Id = Guid.NewGuid(),
         CreatedAt = utcNow,
         State = MessageState.New,
         UpdatedAt = null,
         Payload = JsonSerializer.Serialize(message),
         Type = typeof(T).AssemblyQualifiedName!
      }).ToList();

      dbContext.OutboxMessages.AddRange(entities);

      return entities.Select(x => x.Id).ToArray();
   }
}