using MassTransit.PostgresOutbox.Enums;

namespace MassTransit.PostgresOutbox.Entities;

public class InboxMessage
{
   public required Guid MessageId { get; set; }
   public required string ConsumerId { get; set; }
   public MessageState State { get; set; } = MessageState.New;
   public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
   public DateTime? UpdatedAt { get; set; }
   public string? Payload { get; set; }
   public string? Type { get; set; }
   public string? DestinationAddress { get; set; }
   public int RetryCount { get; set; }
   public DateTime? NextRetryAt { get; set; }
   public string? LastError { get; set; }
}