using MassTransit.SQLiteOutbox.Enums;

namespace MassTransit.SQLiteOutbox.Entities;

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
   ///    Assembly-qualified type name of the message, used for deserialization.
   /// </summary>
   public required string Type { get; set; }

   /// <summary>
   ///    Timestamp when the message was added to the outbox.
   /// </summary>
   public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

   /// <summary>
   ///    Timestamp when the message was last updated (e.g., marked as done).
   /// </summary>
   public DateTime? UpdatedAt { get; set; }

   /// <summary>
   ///    Lease expiry timestamp. Used instead of PostgreSQL FOR UPDATE SKIP LOCKED
   ///    to prevent concurrent processing of the same message.
   /// </summary>
   public DateTime? LeasedUntil { get; set; }
}