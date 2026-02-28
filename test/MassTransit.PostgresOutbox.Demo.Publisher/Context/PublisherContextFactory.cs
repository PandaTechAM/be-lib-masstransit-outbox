using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MassTransit.PostgresOutbox.Demo.Context;

public class PublisherContextFactory : IDesignTimeDbContextFactory<PublisherContext>
{
   public PublisherContext CreateDbContext(string[] args)
   {
      var optionsBuilder = new DbContextOptionsBuilder<PublisherContext>();

      optionsBuilder
         .UseNpgsql();

      return new PublisherContext(optionsBuilder.Options);
   }
}