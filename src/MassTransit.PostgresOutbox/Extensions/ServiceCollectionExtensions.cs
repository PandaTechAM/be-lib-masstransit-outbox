using MassTransit.PostgresOutbox.Abstractions;
using MassTransit.PostgresOutbox.Jobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MassTransit.PostgresOutbox.Extensions;

public static class ServiceCollectionExtensions
{
   extension(IServiceCollection services)
   {
      public IServiceCollection AddOutboxInboxServices<TDbContext>(Settings? settings = null)
         where TDbContext : DbContext, IOutboxDbContext, IInboxDbContext
      {
         return services.AddSettings(settings)
                        .AddHostedService<OutboxMessagePublisherService<TDbContext>>()
                        .AddHostedService<OutboxMessageRemovalService<TDbContext>>()
                        .AddHostedService<InboxMessageRemovalService<TDbContext>>();
      }

      public IServiceCollection AddOutboxPublisherJob<TDbContext>(Settings? settings = null)
         where TDbContext : DbContext, IOutboxDbContext
      {
         services.AddSettings(settings);
         services.AddHostedService<OutboxMessagePublisherService<TDbContext>>();
         return services;
      }

      public IServiceCollection AddOutboxRemovalJob<TDbContext>(Settings? settings = null)
         where TDbContext : DbContext, IOutboxDbContext
      {
         services.AddSettings(settings);
         services.AddHostedService<OutboxMessageRemovalService<TDbContext>>();
         return services;
      }

      public IServiceCollection AddInboxRemovalJob<TDbContext>(Settings? settings = null)
         where TDbContext : DbContext, IInboxDbContext
      {
         services.AddSettings(settings);
         services.AddHostedService<InboxMessageRemovalService<TDbContext>>();
         return services;
      }

      public IServiceCollection AddSettings(Settings? settings)
      {
         settings ??= new Settings();
         services.AddSingleton(settings);
         return services;
      }
   }
}