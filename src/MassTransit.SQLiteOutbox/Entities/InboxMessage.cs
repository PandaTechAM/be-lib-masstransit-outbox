using MassTransit.SQLiteOutbox.Enums;

namespace MassTransit.SQLiteOutbox.Entities;

/// <summary>
///    Represents a message tracked in the inbox table for idempotent consumption.
///    The composite key of <see cref="MessageId" /> and <see cref="ConsumerId" /> ensures
///    each consumer processes each message at most once.
/// </summary>
public class InboxMessage
{
   /// <summary>
   ///    The original message identifier, propagated from the outbox or MassTransit.
   /// </summary>
   public required Guid MessageId { get; set; }

   /// <summary>
   ///    Fully qualified type name of the consumer, used to track per-consumer processing.
   /// </summary>
   public required string ConsumerId { get; set; }

   /// <summary>
   ///    Current processing state of the message.
   /// </summary>
   public MessageState State { get; set; } = MessageState.New;

   /// <summary>
   ///    Timestamp when the inbox record was created.
   /// </summary>
   public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

   /// <summary>
   ///    Timestamp when the message was last updated (e.g., marked as done or lease released).
   /// </summary>
   public DateTime? UpdatedAt { get; set; }

   /// <summary>
   ///    Lease expiry timestamp. Used instead of PostgreSQL FOR UPDATE SKIP LOCKED
   ///    to prevent concurrent processing of the same message.
   /// </summary>
   public DateTime? LeasedUntil { get; set; }
}