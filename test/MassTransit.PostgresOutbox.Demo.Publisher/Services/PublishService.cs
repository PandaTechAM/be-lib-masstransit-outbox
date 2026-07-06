using MassTransit.PostgresOutbox.Demo.Context;
using MassTransit.PostgresOutbox.Demo.Shared.Events;
using MassTransit.PostgresOutbox.Extensions;

namespace MassTransit.PostgresOutbox.Demo.Services;

public class PublishService(PublisherContext dbContext)
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
