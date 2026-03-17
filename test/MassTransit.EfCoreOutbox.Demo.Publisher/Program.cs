using MassTransit.EfCoreOutbox.Demo.Publisher.Context;
using MassTransit.EfCoreOutbox.Demo.Publisher.Services;
using MassTransit.EfCoreOutbox.Extensions;
using MassTransit.PostgresOutbox.Demo.Shared.Extensions;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.AddMassTransit(typeof(Program).Assembly);
builder.AddSqliteContext<EfCorePublisherContext>("Data Source=efcore_publisher.db");
builder.Services.AddOutboxInboxServices<EfCorePublisherContext>();
builder.Services.AddScoped<EfCorePublishService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
   var db = scope.ServiceProvider.GetRequiredService<EfCorePublisherContext>();
   await db.Database.EnsureCreatedAsync();
}

app.UseSwagger();
app.UseSwaggerUI();

app.MapPost("/publish",
   async ([FromServices] EfCorePublishService service) =>
   {
      await service.PublishComplexEventAsync();
      Console.WriteLine("[EfCore Publisher] Published complex event");
      return Results.Ok();
   });

app.MapPost("/publish-flaky",
   async ([FromServices] EfCorePublishService service) =>
   {
      await service.PublishFlakyEventAsync();
      Console.WriteLine("[EfCore Publisher] Published flaky event (80% fail rate on consumer)");
      return Results.Ok();
   });

app.Run();
