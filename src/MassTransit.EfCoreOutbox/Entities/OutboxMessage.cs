using MassTransit.EfCoreOutbox.Enums;

namespace MassTransit.EfCoreOutbox.Entities;

/// <summary>
///    Represents a message persisted in the outbox table, awaiting publication by the background publisher.
/// </summary>
public class OutboxMessage
{
   /// <summary>
   ///    Unique identifier for this outbox message.
   /// </summary>
   public Guid Id { get; private set; } = Guid.CreateVersion7();

   /// <summary>
   ///    Current processing state of the message.
   /// </summary>
   public MessageState State { get; set; } = MessageState.New;

   /// <summary>
   ///    JSON-serialized message payload.
   /// </summary>
   public required string Payload { get; set; }

   /// <summary>
   ///    Version-agnostic type name of the message, used for deserialization.
   /// </summary>
   public required string Type { get; set; }

   /// <summary>
   ///    Optional destination endpoint address. When set, the message is sent directly
   ///    to this endpoint instead of being published to all subscribers.
   /// </summary>
   public string? DestinationAddress { get; set; }

   /// <summary>
   ///    Timestamp when the message was added to the outbox.
   /// </summary>
   public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

   /// <summary>
   ///    Timestamp when the message was last updated (e.g., marked as done).
   /// </summary>
   public DateTime? UpdatedAt { get; set; }

   /// <summary>
   ///    Lease expiry timestamp. Prevents concurrent processing of the same message
   ///    across multiple application instances.
   /// </summary>
   public DateTime? LeasedUntil { get; set; }
}
