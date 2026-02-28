using MassTransit.PostgresOutbox.Demo.Shared.Events;
using MassTransit.SQLiteOutbox.Abstractions;
using MassTransit.SQLiteOutbox.Demo.Consumer.Context;
using Microsoft.EntityFrameworkCore.Storage;

namespace MassTransit.SQLiteOutbox.Demo.Consumer.Services;

public class SqliteConsumeService(SqliteConsumerContext dbContext, IServiceProvider sp)
   : InboxConsumer<ComplexObjectEvent, SqliteConsumerContext>(sp)
{
   protected override Task Consume(ComplexObjectEvent message, IDbContextTransaction dbContextTransaction)
   {
      var original = ComplexObjectEvent.Init();
      var match = message.Equals(original);
      Console.WriteLine($"[SQLite Consumer] Match: {match}");
      return Task.CompletedTask;
   }
}