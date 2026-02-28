using MassTransit.PostgresOutbox.Demo.Shared.Extensions;
using MassTransit.SQLiteOutbox.Demo.Publisher.Context;
using MassTransit.SQLiteOutbox.Demo.Publisher.Services;
using MassTransit.SQLiteOutbox.Extensions;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.AddMassTransit(typeof(Program).Assembly);
builder.AddSqliteContext<SqlitePublisherContext>("Data Source=sqlite_publisher.db");
builder.Services.AddOutboxInboxServices<SqlitePublisherContext>();
builder.Services.AddScoped<SqlitePublishService>();

var app = builder.Build();

app.MigrateDatabase<SqlitePublisherContext>();

app.UseSwagger();
app.UseSwaggerUI();

app.MapPost("/publish",
   async ([FromServices] SqlitePublishService service) =>
   {
      await service.PublishComplexEventAsync();
      return Results.Ok();
   });

app.Run();