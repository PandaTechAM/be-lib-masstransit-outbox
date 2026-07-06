namespace MassTransit.EfCoreOutbox;

/// <summary>
///     Well-known constants shared by the outbox publisher and inbox consumer.
/// </summary>
public static class Constants
{
    /// <summary>
    ///     Message header carrying the originating outbox message ID, used by the inbox for idempotency.
    /// </summary>
    public const string OutboxMessageIdHeaderName = "OutboxMessageId";
}
