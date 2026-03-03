using MassTransit.PostgresOutbox.Demo.Context;
using MassTransit.PostgresOutbox.Demo.Services;
using MassTransit.PostgresOutbox.Demo.Shared.Extensions;
using MassTransit.PostgresOutbox.Extensions;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.AddMassTransit(typeof(Program).Assembly);
builder.AddPostgresContext<PublisherContext>(
   "Server=localhost;Port=5432;Database=nuget_publisher_demo;User Id=test;Password=test;Include Error Detail=True;");
builder.Services.AddOutboxInboxServices<PublisherContext>();
builder.Services.AddScoped<PublishService>();


var app = builder.Build();

app.MigrateDatabase<PublisherContext>();
app.UseSwagger();
app.UseSwaggerUI();

app.MapPost("/publish",
   async ([FromServices] PublishService service) =>
   {
      await service.PublishComplexEventAsync();
      return Results.Ok();
   });


app.Run();