namespace MassTransit.PostgresOutbox.Enums;

/// <summary>
///     Processing state of an outbox or inbox message.
/// </summary>
public enum MessageState
{
    /// <summary>
    ///     Not yet processed; awaiting publication or consumption.
    /// </summary>
    New = 1,

    /// <summary>
    ///     Successfully processed.
    /// </summary>
    Done = 2,

    /// <summary>
    ///     Permanently failed after exhausting all retries.
    /// </summary>
    Failed = 3
}
