using MassTransit.EfCoreOutbox.Demo.Publisher.Context;
using MassTransit.EfCoreOutbox.Extensions;
using MassTransit.PostgresOutbox.Demo.Shared.Events;

namespace MassTransit.EfCoreOutbox.Demo.Publisher.Services;

public class EfCorePublishService(EfCorePublisherContext dbContext)
{
    public async Task PublishComplexEventAsync()
    {
        var complexEvent = ComplexObjectEvent.Init();
        dbContext.AddToOutbox(complexEvent);
        await dbContext.SaveChangesAsync();
    }

    public async Task PublishFlakyEventAsync()
    {
        var flakyEvent = FlakyEvent.Init();
        dbContext.AddToOutbox(flakyEvent);
        await dbContext.SaveChangesAsync();
    }
}
