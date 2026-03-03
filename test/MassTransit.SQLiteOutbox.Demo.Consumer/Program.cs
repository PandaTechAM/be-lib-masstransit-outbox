using MassTransit.PostgresOutbox.Demo.Shared.Extensions;
using MassTransit.SQLiteOutbox.Demo.Consumer.Context;
using MassTransit.SQLiteOutbox.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();
builder.AddMassTransit(typeof(Program).Assembly);

builder.AddSqliteContext<SqliteConsumerContext>("Data Source=sqlite_consumer.db");
builder.Services.AddOutboxInboxServices<SqliteConsumerContext>();

var app = builder.Build();
app.MapOpenApi();

// EnsureCreated for quick testing; switch to MigrateDatabase once migrations are generated
app.MigrateDatabase<SqliteConsumerContext>();

app.Run();