using MassTransit.EfCoreOutbox;
using MassTransit.EfCoreOutbox.Demo.Consumer.Context;
using MassTransit.EfCoreOutbox.Extensions;
using MassTransit.PostgresOutbox.Demo.Shared.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();
builder.AddMassTransit(typeof(Program).Assembly);

builder.AddSqliteContext<EfCoreConsumerContext>("Data Source=efcore_consumer.db");
builder.Services.AddOutboxInboxServices<EfCoreConsumerContext>(new Settings
{
    InboxRetryEnabled = true,
    InboxRetryIntervals =
    [
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(7),
        TimeSpan.FromSeconds(7),
        TimeSpan.FromSeconds(7),
        TimeSpan.FromSeconds(8),
        TimeSpan.FromSeconds(9)
    ],
    InboxRetryPollInterval = TimeSpan.FromSeconds(3)
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<EfCoreConsumerContext>();
    await db.Database.EnsureCreatedAsync();
}

app.MapOpenApi();

app.Run();
