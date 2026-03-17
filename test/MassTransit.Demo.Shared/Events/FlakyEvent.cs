namespace MassTransit.PostgresOutbox.Demo.Shared.Events;

public class FlakyEvent
{
   public Guid Id { get; init; }
   public string Message { get; init; } = string.Empty;
   public DateTime CreatedAt { get; init; }

   public static FlakyEvent Init()
   {
      return new FlakyEvent
      {
         Id = Guid.NewGuid(),
         Message = "I fail 80% of the time",
         CreatedAt = DateTime.UtcNow
      };
   }
}
