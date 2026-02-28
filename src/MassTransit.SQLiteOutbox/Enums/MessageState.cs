namespace MassTransit.SQLiteOutbox.Enums;

/// <summary>
///    Represents the processing state of an outbox or inbox message.
/// </summary>
public enum MessageState
{
   /// <summary>
   ///    The message has been persisted but not yet processed.
   /// </summary>
   New = 1,

   /// <summary>
   ///    The message has been successfully processed (published or consumed).
   /// </summary>
   Done = 2
}