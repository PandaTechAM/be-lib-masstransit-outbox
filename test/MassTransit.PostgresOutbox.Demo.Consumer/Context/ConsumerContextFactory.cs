using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MassTransit.PostgresOutbox.Demo.Consumer.Context;

public class ConsumerContextFactory : IDesignTimeDbContextFactory<ConsumerContext>
{
    public ConsumerContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ConsumerContext>();

        optionsBuilder
            .UseNpgsql();

        return new ConsumerContext(optionsBuilder.Options);
    }
}
