using MassTransit.PostgresOutbox.Enums;

namespace MassTransit.PostgresOutbox.Entities;

public class OutboxMessage
{
   public Guid Id { get; private set; } = Guid.CreateVersion7();
   public MessageState State { get; set; } = MessageState.New;
   public required string Payload { get; set; }
   public required string Type { get; set; }
   public string? DestinationAddress { get; set; }
   public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
   public DateTime? UpdatedAt { get; set; }
}