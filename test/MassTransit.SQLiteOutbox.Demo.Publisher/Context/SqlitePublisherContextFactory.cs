using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MassTransit.SQLiteOutbox.Demo.Publisher.Context;

public class SqlitePublisherContextFactory : IDesignTimeDbContextFactory<SqlitePublisherContext>
{
   public SqlitePublisherContext CreateDbContext(string[] args)
   {
      var optionsBuilder = new DbContextOptionsBuilder<SqlitePublisherContext>();

      optionsBuilder.UseSqlite("Data Source=sqlite_publisher.db");

      return new SqlitePublisherContext(optionsBuilder.Options);
   }
}