using System.Text.Json;
using MassTransit.PostgresOutbox.Abstractions;
using MassTransit.PostgresOutbox.Entities;
using MassTransit.PostgresOutbox.Enums;
using Microsoft.EntityFrameworkCore;

namespace MassTransit.PostgresOutbox.Extensions;

/// <summary>
///     Extension methods for enqueuing messages into the outbox from an <see cref="IOutboxDbContext" />.
/// </summary>
public static class OutboxDbContextExtensions
{
    /// <summary>
    ///     Adds a message to the outbox, serialized as JSON. The message is persisted
    ///     when <c>SaveChangesAsync</c> is called on the owning <see cref="DbContext" />.
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
    ///     Adds multiple messages of the same type to the outbox in a single batch.
    ///     All messages share the same <c>CreatedAt</c> timestamp.
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
    ///     Adds a message to the outbox targeted at a specific endpoint. The background publisher
    ///     will use MassTransit <c>Send</c> instead of <c>Publish</c> for this message.
    /// </summary>
    /// <typeparam name="T">The message type. Must be serializable by <see cref="System.Text.Json" />.</typeparam>
    /// <param name="dbContext">The DbContext implementing <see cref="IOutboxDbContext" />.</param>
    /// <param name="message">The message to enqueue.</param>
    /// <param name="destination">The target endpoint URI (e.g. <c>new Uri("queue:my-endpoint")</c>).</param>
    /// <returns>The generated outbox message ID, usable for correlation or diagnostics.</returns>
    public static Guid AddToOutbox<T>(this IOutboxDbContext dbContext, T message, Uri destination)
    {
        var entity = new OutboxMessage
        {
            CreatedAt = DateTime.UtcNow,
            State = MessageState.New,
            UpdatedAt = null,
            Payload = JsonSerializer.Serialize(message),
            Type = GetVersionAgnosticTypeName<T>(),
            DestinationAddress = destination.ToString()
        };

        dbContext.OutboxMessages.Add(entity);

        return entity.Id;
    }

    /// <summary>
    ///     Adds a message to the outbox for each specified destination endpoint. The background publisher
    ///     will use MassTransit <c>Send</c> instead of <c>Publish</c> for these messages.
    ///     All messages share the same <c>CreatedAt</c> timestamp and serialized payload.
    /// </summary>
    /// <typeparam name="T">The message type. Must be serializable by <see cref="System.Text.Json" />.</typeparam>
    /// <param name="dbContext">The DbContext implementing <see cref="IOutboxDbContext" />.</param>
    /// <param name="message">The message to enqueue.</param>
    /// <param name="destinations">The target endpoint URIs (e.g. <c>new Uri("queue:my-endpoint")</c>).</param>
    /// <returns>The generated outbox message IDs in the same order as <paramref name="destinations" />.</returns>
    public static IReadOnlyList<Guid> AddToOutbox<T>(this IOutboxDbContext dbContext, T message,
        IEnumerable<Uri> destinations)
    {
        var utcNow = DateTime.UtcNow;
        var typeName = GetVersionAgnosticTypeName<T>();
        var payload = JsonSerializer.Serialize(message);

        var entities = destinations.Select(destination => new OutboxMessage
            {
                CreatedAt = utcNow,
                State = MessageState.New,
                UpdatedAt = null,
                Payload = payload,
                Type = typeName,
                DestinationAddress = destination.ToString()
            })
            .ToArray();

        dbContext.OutboxMessages.AddRange(entities);

        return Array.ConvertAll(entities, e => e.Id);
    }

    /// <summary>
    ///     Returns "Namespace.TypeName, AssemblyName" without version/culture/token.
    ///     Type.GetType() resolves this format regardless of strong naming.
    ///     Backward compatible: old AssemblyQualifiedName rows still resolve fine.
    /// </summary>
    private static string GetVersionAgnosticTypeName<T>()
    {
        var type = typeof(T);
        return $"{type.FullName}, {type.Assembly.GetName().Name}";
    }
}
