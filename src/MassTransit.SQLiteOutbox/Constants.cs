namespace MassTransit.SQLiteOutbox;

/// <summary>
///    Shared constants used across the outbox and inbox infrastructure.
/// </summary>
public static class Constants
{
   /// <summary>
   ///    MassTransit header name used to propagate the outbox message ID to consumers.
   ///    This allows the inbox to correlate incoming messages with their outbox origin.
   /// </summary>
   public const string OutboxMessageIdHeaderName = "OutboxMessageId";
}