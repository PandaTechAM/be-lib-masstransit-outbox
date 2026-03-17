using MassTransit.EfCoreOutbox.Abstractions;
using MassTransit.EfCoreOutbox.Jobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MassTransit.EfCoreOutbox.Extensions;

public static class ServiceCollectionExtensions
{
   /// <summary>
   ///    Registers outbox publisher, outbox cleanup, inbox cleanup, and inbox retry
   ///    as hosted background services for the specified <typeparamref name="TDbContext" />.
   /// </summary>
   /// <typeparam name="TDbContext">
   ///    A <see cref="DbContext" /> implementing both <see cref="IOutboxDbContext" /> and <see cref="IInboxDbContext" />.
   /// </typeparam>
   /// <param name="services"></param>
   /// <param name="settings">Optional settings override. Uses defaults when <c>null</c>.</param>
   public static IServiceCollection AddOutboxInboxServices<TDbContext>(this IServiceCollection services,
      Settings? settings = null)
      where TDbContext : DbContext, IOutboxDbContext, IInboxDbContext
   {
      settings ??= new Settings();

      services.AddSettings(settings)
              .AddHostedService<OutboxMessagePublisherService<TDbContext>>()
              .AddHostedService<OutboxMessageRemovalService<TDbContext>>()
              .AddHostedService<InboxMessageRemovalService<TDbContext>>();

      if (settings.InboxRetryEnabled)
      {
         services.AddHostedService<InboxMessageRetryService<TDbContext>>();
      }

      return services;
   }

   /// <summary>
   ///    Registers only the outbox publisher background service for the specified <typeparamref name="TDbContext" />.
   /// </summary>
   public static IServiceCollection AddOutboxPublisherJob<TDbContext>(this IServiceCollection services,
      Settings? settings = null)
      where TDbContext : DbContext, IOutboxDbContext
   {
      services.AddSettings(settings);
      services.AddHostedService<OutboxMessagePublisherService<TDbContext>>();
      return services;
   }

   /// <summary>
   ///    Registers only the outbox message cleanup background service.
   /// </summary>
   public static IServiceCollection AddOutboxRemovalJob<TDbContext>(this IServiceCollection services,
      Settings? settings = null)
      where TDbContext : DbContext, IOutboxDbContext
   {
      services.AddSettings(settings);
      services.AddHostedService<OutboxMessageRemovalService<TDbContext>>();
      return services;
   }

   /// <summary>
   ///    Registers only the inbox message cleanup background service.
   /// </summary>
   public static IServiceCollection AddInboxRemovalJob<TDbContext>(this IServiceCollection services,
      Settings? settings = null)
      where TDbContext : DbContext, IInboxDbContext
   {
      services.AddSettings(settings);
      services.AddHostedService<InboxMessageRemovalService<TDbContext>>();
      return services;
   }

   /// <summary>
   ///    Registers only the inbox message retry background service.
   /// </summary>
   public static IServiceCollection AddInboxRetryJob<TDbContext>(this IServiceCollection services,
      Settings? settings = null)
      where TDbContext : DbContext, IInboxDbContext
   {
      settings ??= new Settings();
      services.AddSettings(settings);

      if (settings.InboxRetryEnabled)
      {
         services.AddHostedService<InboxMessageRetryService<TDbContext>>();
      }

      return services;
   }

   private static IServiceCollection AddSettings(this IServiceCollection services, Settings? settings)
   {
      settings ??= new Settings();
      services.AddSingleton(settings);
      return services;
   }
}
