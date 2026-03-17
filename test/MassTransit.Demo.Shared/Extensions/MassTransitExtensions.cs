using System.Reflection;
using Microsoft.AspNetCore.Builder;

namespace MassTransit.PostgresOutbox.Demo.Shared.Extensions;

public static class MassTransitExtensions
{
   public static WebApplicationBuilder AddMassTransit(this WebApplicationBuilder builder, params Assembly[] assemblies)
   {
      builder.Services.AddMassTransit(x =>
      {
         x.AddConsumers(assemblies);
         x.SetKebabCaseEndpointNameFormatter();


         x.UsingRabbitMq((context, cfg) =>
         {
            cfg.Host("amqp://test:test@localhost:5672/");
            cfg.ConfigureEndpoints(context);
         });
      });


      return builder;
   }
}