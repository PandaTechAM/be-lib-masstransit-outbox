using MassTransit.SQLiteOutbox.Abstractions;
using MassTransit.SQLiteOutbox.Jobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MassTransit.SQLiteOutbox.Extensions;

public static class ServiceCollectionExtensions
{
   /// <summary>
   ///    Registers outbox publisher, outbox cleanup, and inbox cleanup background services.
   /// </summary>
   public static IServiceCollection AddOutboxInboxServices<TDbContext>(this IServiceCollection services,
      Settings? settings = null)
      where TDbContext : DbContext, IOutboxDbContext, IInboxDbContext
   {
      return services.AddSettings(settings)
                     .AddHostedService<OutboxMessagePublisherService<TDbContext>>()
                     .AddHostedService<OutboxMessageRemovalService<TDbContext>>()
                     .AddHostedService<InboxMessageRemovalService<TDbContext>>();
   }

   /// <summary>
   ///    Registers only the outbox publisher background service.
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
   ///    Registers only the outbox cleanup background service.
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
   ///    Registers only the inbox cleanup background service.
   /// </summary>
   public static IServiceCollection AddInboxRemovalJob<TDbContext>(this IServiceCollection services,
      Settings? settings = null)
      where TDbContext : DbContext, IInboxDbContext
   {
      services.AddSettings(settings);
      services.AddHostedService<InboxMessageRemovalService<TDbContext>>();
      return services;
   }

   private static IServiceCollection AddSettings(this IServiceCollection services, Settings? settings)
   {
      settings ??= new Settings();
      services.AddSingleton(settings);
      return services;
   }
}