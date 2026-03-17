using MassTransit.EfCoreOutbox.Abstractions;
using MassTransit.EfCoreOutbox.Demo.Consumer.Context;
using MassTransit.PostgresOutbox.Demo.Shared.Events;
using Microsoft.EntityFrameworkCore.Storage;

namespace MassTransit.EfCoreOutbox.Demo.Consumer.Services;

public class EfCoreConsumeService(IServiceProvider sp)
   : InboxConsumer<ComplexObjectEvent, EfCoreConsumerContext>(sp)
{
   protected override Task ConsumeAsync(ComplexObjectEvent message,
      IDbContextTransaction dbContextTransaction,
      CancellationToken ct)
   {
      var original = ComplexObjectEvent.Init();
      var match = message.Equals(original);
      Console.WriteLine($"[EfCore Consumer] ComplexObjectEvent received — match: {match}");
      return Task.CompletedTask;
   }
}
