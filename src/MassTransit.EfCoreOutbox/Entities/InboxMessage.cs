using MassTransit.EfCoreOutbox.Enums;

namespace MassTransit.EfCoreOutbox.Entities;

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
   ///    Timestamp when the message was last updated.
   /// </summary>
   public DateTime? UpdatedAt { get; set; }

   /// <summary>
   ///    Lease expiry timestamp. Prevents concurrent processing of the same message
   ///    across multiple application instances.
   /// </summary>
   public DateTime? LeasedUntil { get; set; }

   /// <summary>
   ///    JSON-serialized message payload. Stored only when <see cref="Settings.InboxRetryEnabled" /> is <c>true</c>.
   /// </summary>
   public string? Payload { get; set; }

   /// <summary>
   ///    Version-agnostic type name of the message. Stored only when retry is enabled.
   /// </summary>
   public string? Type { get; set; }

   /// <summary>
   ///    The endpoint address to re-dispatch the message to on retry. Stored only when retry is enabled.
   /// </summary>
   public string? DestinationAddress { get; set; }

   /// <summary>
   ///    Number of failed processing attempts.
   /// </summary>
   public int RetryCount { get; set; }

   /// <summary>
   ///    Timestamp when the next retry should occur. <c>null</c> when no retry is pending.
   /// </summary>
   public DateTime? NextRetryAt { get; set; }

   /// <summary>
   ///    The last error message, truncated to 4000 characters.
   /// </summary>
   public string? LastError { get; set; }
}
