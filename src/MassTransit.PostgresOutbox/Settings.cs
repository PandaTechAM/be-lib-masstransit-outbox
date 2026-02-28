namespace MassTransit.PostgresOutbox;

/// <summary>
///    Configuration settings for outbox/inbox background services.
/// </summary>
public class Settings
{
   /// <summary>
   ///    Number of days after which processed inbox messages are deleted. Default: 5.
   /// </summary>
   public int InboxRemovalBeforeInDays { get; set; } = 5;

   /// <summary>
   ///    How often the inbox cleanup job runs. Default: once per day.
   /// </summary>
   public TimeSpan InboxRemovalTimerPeriod { get; set; } = TimeSpan.FromDays(1);

   /// <summary>
   ///    Number of days after which published outbox messages are deleted. Default: 5.
   /// </summary>
   public int OutboxRemovalBeforeInDays { get; set; } = 5;

   /// <summary>
   ///    How often the outbox cleanup job runs. Default: once per day.
   /// </summary>
   public TimeSpan OutboxRemovalTimerPeriod { get; set; } = TimeSpan.FromDays(1);

   /// <summary>
   ///    Maximum number of outbox messages to publish per tick. Default: 100.
   /// </summary>
   public int PublisherBatchCount { get; set; } = 100;

   /// <summary>
   ///    How often the outbox publisher polls for new messages. Default: every 1 second.
   /// </summary>
   public TimeSpan PublisherTimerPeriod { get; set; } = TimeSpan.FromSeconds(1);
}