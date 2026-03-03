using MassTransit.PostgresOutbox.Demo.Shared.Events;
using MassTransit.SQLiteOutbox.Demo.Publisher.Context;
using MassTransit.SQLiteOutbox.Extensions;

namespace MassTransit.SQLiteOutbox.Demo.Publisher.Services;

public class SqlitePublishService(SqlitePublisherContext dbContext)
{
   public async Task PublishComplexEventAsync()
   {
      var complexEvent = ComplexObjectEvent.Init();
      dbContext.AddToOutbox(complexEvent);
      await dbContext.SaveChangesAsync();
   }
}