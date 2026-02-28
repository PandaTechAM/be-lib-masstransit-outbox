using System.Text.Json;
using MassTransit.PostgresOutbox.Abstractions;
using MassTransit.PostgresOutbox.Entities;
using MassTransit.PostgresOutbox.Enums;

namespace MassTransit.PostgresOutbox.Extensions;

public static class OutboxDbContextExtensions
{
   /// <summary>
   ///    Adds a message to the outbox. Call SaveChanges/SaveChangesAsync to persist.
   /// </summary>
   /// <returns>The generated outbox message ID.</returns>
   public static Guid AddToOutbox<T>(this IOutboxDbContext dbContext, T message)
   {
      var entity = new OutboxMessage
      {
         Id = Guid.NewGuid(),
         CreatedAt = DateTime.UtcNow,
         State = MessageState.New,
         UpdatedAt = null,
         Payload = JsonSerializer.Serialize(message),
         Type = GetVersionAgnosticTypeName<T>()
      };

      dbContext.OutboxMessages.Add(entity);

      return entity.Id;
   }

   /// <summary>
   ///    Adds multiple messages of the same type to the outbox. Call SaveChanges/SaveChangesAsync to persist.
   /// </summary>
   /// <returns>The generated outbox message IDs.</returns>
   public static IReadOnlyList<Guid> AddToOutboxRange<T>(this IOutboxDbContext dbContext, params T[] messages)
   {
      var utcNow = DateTime.UtcNow;
      var typeName = GetVersionAgnosticTypeName<T>();

      var entities = messages.Select(message => new OutboxMessage
                             {
                                Id = Guid.NewGuid(),
                                CreatedAt = utcNow,
                                State = MessageState.New,
                                UpdatedAt = null,
                                Payload = JsonSerializer.Serialize(message),
                                Type = typeName
                             })
                             .ToList();

      dbContext.OutboxMessages.AddRange(entities);

      return entities.Select(x => x.Id)
                     .ToArray();
   }

   /// <summary>
   ///    Returns "Namespace.TypeName, AssemblyName" without version/culture/token.
   ///    Type.GetType() resolves this format regardless of strong naming.
   ///    Backward compatible: old AssemblyQualifiedName rows still resolve fine.
   /// </summary>
   private static string GetVersionAgnosticTypeName<T>()
   {
      var type = typeof(T);
      return $"{type.FullName}, {type.Assembly.GetName().Name}";
   }
}