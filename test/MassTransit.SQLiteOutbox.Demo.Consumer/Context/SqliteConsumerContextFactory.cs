using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MassTransit.SQLiteOutbox.Demo.Consumer.Context;

public class SqliteConsumerContextFactory : IDesignTimeDbContextFactory<SqliteConsumerContext>
{
   public SqliteConsumerContext CreateDbContext(string[] args)
   {
      var optionsBuilder = new DbContextOptionsBuilder<SqliteConsumerContext>();

      optionsBuilder.UseSqlite("Data Source=sqlite_consumer.db");

      return new SqliteConsumerContext(optionsBuilder.Options);
   }
}