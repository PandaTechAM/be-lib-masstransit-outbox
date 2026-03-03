using MassTransit.PostgresOutbox.Abstractions;
using MassTransit.PostgresOutbox.Demo.Consumer.Context;
using MassTransit.PostgresOutbox.Demo.Shared.Events;
using Microsoft.EntityFrameworkCore.Storage;

namespace MassTransit.PostgresOutbox.Demo.Consumer.Services;

public class ConsumeService(ConsumerContext dbContext, IServiceProvider sp)
   : InboxConsumer<ComplexObjectEvent, ConsumerContext>(sp)
{
   protected override Task ConsumeAsync(ComplexObjectEvent message,
      IDbContextTransaction dbContextTransaction,
      CancellationToken ct)
   {
      var original = ComplexObjectEvent.Init();
      var match = message.Equals(original);
      Console.WriteLine($"Match: {match}");
      return Task.CompletedTask;
   }
}