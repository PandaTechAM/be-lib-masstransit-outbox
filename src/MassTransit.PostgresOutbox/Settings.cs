namespace MassTransit.PostgresOutbox;

/// <summary>
///     Configuration settings for outbox/inbox background services.
/// </summary>
public class Settings
{
    /// <summary>
    ///     Number of days after which processed inbox messages are deleted. Default: 5.
    /// </summary>
    public int InboxRemovalBeforeInDays { get; set; } = 5;

    /// <summary>
    ///     How often the inbox cleanup job runs. Default: once per day.
    /// </summary>
    public TimeSpan InboxRemovalTimerPeriod { get; set; } = TimeSpan.FromDays(1);

    /// <summary>
    ///     Number of days after which published outbox messages are deleted. Default: 5.
    /// </summary>
    public int OutboxRemovalBeforeInDays { get; set; } = 5;

    /// <summary>
    ///     How often the outbox cleanup job runs. Default: once per day.
    /// </summary>
    public TimeSpan OutboxRemovalTimerPeriod { get; set; } = TimeSpan.FromDays(1);

    /// <summary>
    ///     Maximum number of outbox messages to publish per tick. Default: 100.
    /// </summary>
    public int PublisherBatchCount { get; set; } = 100;

    /// <summary>
    ///     How often the outbox publisher polls for new messages. Default: every 1 second.
    /// </summary>
    public TimeSpan PublisherTimerPeriod { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    ///     Enables database-backed inbox retry. When <c>true</c>, failed messages are retried
    ///     by the background service using the configured delay intervals.
    ///     When <c>false</c>, the consumer re-throws the exception so MassTransit's in-memory
    ///     retry / redelivery policies can handle it instead.
    ///     <para>
    ///         These two mechanisms are mutually exclusive: when inbox retry is enabled the consumer
    ///         never throws, so MassTransit retry / redelivery filters will not trigger.
    ///     </para>
    ///     Default: <c>false</c>.
    /// </summary>
    public bool InboxRetryEnabled { get; set; } = false;

    /// <summary>
    ///     The retry intervals for failed inbox messages. Each element defines the delay
    ///     before the corresponding retry attempt. The number of elements determines the
    ///     maximum number of retries; once exhausted the message is marked as <c>Failed</c>.
    ///     <para>
    ///         Mirrors the MassTransit <c>UseDelayedRedelivery(r => r.Intervals(…))</c> API.
    ///     </para>
    /// </summary>
    public TimeSpan[] InboxRetryIntervals { get; set; } =
    [
        TimeSpan.FromSeconds(10),
        TimeSpan.FromMinutes(2),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(30),
        TimeSpan.FromHours(2),
        TimeSpan.FromHours(6),
        TimeSpan.FromHours(12),
        TimeSpan.FromHours(24)
    ];

    /// <summary>
    ///     How often the retry service polls for due retries. Default: every 5 seconds.
    /// </summary>
    public TimeSpan InboxRetryPollInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    ///     Maximum number of inbox messages to retry per tick. Default: 50.
    /// </summary>
    public int InboxRetryBatchCount { get; set; } = 50;
}
