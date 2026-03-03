using MassTransit.PostgresOutbox.Demo.Consumer.Context;
using MassTransit.PostgresOutbox.Demo.Shared.Extensions;
using MassTransit.PostgresOutbox.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();
builder.AddMassTransit(typeof(Program).Assembly);

builder.AddPostgresContext<ConsumerContext>(
   "Server=localhost;Port=5432;Database=nuget_consumer_demo;User Id=test;Password=test;Include Error Detail=True;");
builder.Services.AddOutboxInboxServices<ConsumerContext>();


var app = builder.Build();
app.MapOpenApi();
app.MigrateDatabase<ConsumerContext>();


app.Run();