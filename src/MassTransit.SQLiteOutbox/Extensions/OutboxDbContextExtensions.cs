using System.Text.Json;
using MassTransit.SQLiteOutbox.Abstractions;
using MassTransit.SQLiteOutbox.Entities;
using MassTransit.SQLiteOutbox.Enums;

namespace MassTransit.SQLiteOutbox.Extensions;

public static class OutboxDbContextExtensions
{
   /// <summary>
   ///    Adds a message to the outbox, serialized as JSON. The message is persisted
   ///    when <c>SaveChangesAsync</c> is called on the owning <see cref="DbContext" />.
   /// </summary>
   /// <typeparam name="T">The message type. Must be serializable by <see cref="System.Text.Json" />.</typeparam>
   /// <param name="dbContext">The DbContext implementing <see cref="IOutboxDbContext" />.</param>
   /// <param name="message">The message to enqueue.</param>
   /// <returns>The generated outbox message ID, usable for correlation or diagnostics.</returns>
   public static Guid AddToOutbox<T>(this IOutboxDbContext dbContext, T message)
   {
      var entity = new OutboxMessage
      {
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
   ///    Adds multiple messages of the same type to the outbox in a single batch.
   ///    All messages share the same <c>CreatedAt</c> timestamp.
   /// </summary>
   /// <typeparam name="T">The message type. Must be serializable by <see cref="System.Text.Json" />.</typeparam>
   /// <param name="dbContext">The DbContext implementing <see cref="IOutboxDbContext" />.</param>
   /// <param name="messages">The messages to enqueue.</param>
   /// <returns>The generated outbox message IDs in the same order as <paramref name="messages" />.</returns>
   public static IReadOnlyList<Guid> AddToOutboxRange<T>(this IOutboxDbContext dbContext, IEnumerable<T> messages)
   {
      var utcNow = DateTime.UtcNow;
      var typeName = GetVersionAgnosticTypeName<T>();

      var entities = messages.Select(message => new OutboxMessage
                             {
                                CreatedAt = utcNow,
                                State = MessageState.New,
                                UpdatedAt = null,
                                Payload = JsonSerializer.Serialize(message),
                                Type = typeName
                             })
                             .ToArray();

      dbContext.OutboxMessages.AddRange(entities);

      return Array.ConvertAll(entities, e => e.Id);
   }

   /// <summary>
   ///    Convenience overload that accepts individual messages as arguments.
   /// </summary>
   /// <typeparam name="T">The message type. Must be serializable by <see cref="System.Text.Json" />.</typeparam>
   /// <param name="dbContext">The DbContext implementing <see cref="IOutboxDbContext" />.</param>
   /// <param name="messages">The messages to enqueue.</param>
   /// <returns>The generated outbox message IDs in the same order as <paramref name="messages" />.</returns>
   public static IReadOnlyList<Guid> AddToOutboxRange<T>(this IOutboxDbContext dbContext, params T[] messages)
      => AddToOutboxRange(dbContext, messages.AsEnumerable());

   /// <summary>
   ///    Returns "Namespace.TypeName, AssemblyName" without version/culture/token.
   ///    Type.GetType() resolves this format regardless of strong naming.
   /// </summary>
   private static string GetVersionAgnosticTypeName<T>()
   {
      var type = typeof(T);
      return $"{type.FullName}, {type.Assembly.GetName().Name}";
   }
}