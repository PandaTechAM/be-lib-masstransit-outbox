using MassTransit.PostgresOutbox.Enums;

namespace MassTransit.PostgresOutbox.Entities;

/// <summary>
///     Represents a consumed message tracked in the inbox table for idempotency and retry.
/// </summary>
public class InboxMessage
{
    /// <summary>
    ///     Identifier of the consumed message. Part of the composite primary key.
    /// </summary>
    public required Guid MessageId { get; set; }

    /// <summary>
    ///     Identifier of the consumer that processed the message. Part of the composite primary key.
    /// </summary>
    public required string ConsumerId { get; set; }

    /// <summary>
    ///     Current processing state of the message.
    /// </summary>
    public MessageState State { get; set; } = MessageState.New;

    /// <summary>
    ///     Timestamp when the message was first recorded in the inbox.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    ///     Timestamp when the message was last updated.
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    ///     JSON-serialized message payload. Populated only when inbox retry is enabled.
    /// </summary>
    public string? Payload { get; set; }

    /// <summary>
    ///     Version-agnostic type name of the message. Populated only when inbox retry is enabled.
    /// </summary>
    public string? Type { get; set; }

    /// <summary>
    ///     Optional originating destination endpoint address. Populated only when inbox retry is enabled.
    /// </summary>
    public string? DestinationAddress { get; set; }

    /// <summary>
    ///     Number of retry attempts made so far.
    /// </summary>
    public int RetryCount { get; set; }

    /// <summary>
    ///     Timestamp of the next scheduled retry, or <c>null</c> when no retry is pending.
    /// </summary>
    public DateTime? NextRetryAt { get; set; }

    /// <summary>
    ///     Details of the most recent processing error (truncated to 4000 characters).
    /// </summary>
    public string? LastError { get; set; }
}
