using EFCore.PostgresExtensions.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MassTransit.PostgresOutbox.Demo.Shared.Extensions;

public static class DatabaseExtensions
{
    public static WebApplicationBuilder AddSqliteContext<TContext>(this WebApplicationBuilder builder,
        string connectionString) where TContext : DbContext
    {
        builder.Services.AddDbContextPool<TContext>((sp, options) => { options.UseSqlite(connectionString); });

        return builder;
    }

    public static WebApplicationBuilder AddPostgresContext<TContext>(this WebApplicationBuilder builder,
        string connectionString) where TContext : DbContext
    {
        builder.Services.AddDbContextPool<TContext>((sp, options) =>
        {
            options.UseNpgsql(connectionString)
                .UseQueryLocks();
        });

        return builder;
    }

    public static WebApplication MigrateDatabase<TContext>(this WebApplication app)
        where TContext : DbContext
    {
        using var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TContext>();
        dbContext.Database.Migrate();
        return app;
    }
}
