using BuildingBlocks.Caching;
using BuildingBlocks.Health;
using BuildingBlocks.Messaging;
using BuildingBlocks.Messaging.Workers;
using BuildingBlocks.Observability;
using BuildingBlocks.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shared.Application.Abstractions;

namespace BuildingBlocks;

public static class DependencyInjection
{
    public static IServiceCollection AddServiceInfrastructure<TContext>(
        this IServiceCollection services, IConfiguration configuration, string serviceName, string cachePrefix)
        where TContext : DbContext
    {
        services.AddScoped<EfUnitOfWork<TContext>>();
        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<EfUnitOfWork<TContext>>());
        services.AddScoped<IOutbox>(provider => provider.GetRequiredService<EfUnitOfWork<TContext>>());
        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = configuration.GetConnectionString("Redis")
                ?? throw new InvalidOperationException("ConnectionStrings:Redis is required.");
            options.InstanceName = cachePrefix;
        });
        services.AddScoped<ICache, RedisCache>();
        services.AddHealthChecks().AddCheck<DependencyHealthCheck<TContext>>("dependencies", tags: ["ready"]);
        services.AddSingleton<RabbitConnection>();
        services.AddHostedService<ConsumerWorker>();
        services.AddHostedService<OutboxWorker<TContext>>();
        services.AddTelemetry(serviceName);
        return services;
    }
}

// DependencyInjection: Servislerin ortak altyapı bağımlılıklarını DI konteynerine kaydeder.
// RabbitMQ bağlantısını, tüketici ve outbox worker servislerini kurar; IOutbox ve IUnitOfWork için aynı scoped nesneyi kullanır.
