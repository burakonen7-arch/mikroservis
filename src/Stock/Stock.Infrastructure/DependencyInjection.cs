using BuildingBlocks;
using BuildingBlocks.Messaging;
using BuildingBlocks.Messaging.Abstractions;
using BuildingBlocks.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Stock.Application.Abstractions;
using Stock.Application.Commands.AdjustStock;
using Stock.Infrastructure.Messaging;
using Stock.Infrastructure.Repositories;
using BuildingBlocks.Observability;
using Stock.Infrastructure.Observability;

namespace Stock.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddStockInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<StockDbContext>(options =>
            options.UseOracle(configuration.GetConnectionString("Database")
                ?? throw new InvalidOperationException("ConnectionStrings:Database is required.")));
        services.AddScoped<IStockReadRepository, StockReadRepository>();
        services.AddScoped<IStockWriteRepository, StockWriteRepository>();
        services.AddScoped<IRequestTelemetryEnricher, StockTelemetryEnricher>();
        services.AddMediatR(options =>
        {
            options.RegisterServicesFromAssemblyContaining<AdjustStockCommandHandler>();
            options.AddOpenBehavior(typeof(RequestTelemetryBehavior<,>));
        });
        services.AddScoped<IMessageHandler, ProductCreatedHandler>();
        services.AddScoped<IOutboxMessageFactory, StockOutboxMessageFactory>();
        services.AddServiceInfrastructure<StockDbContext>(configuration, "stock-service", "stock:");
        return services;
    }

    public static async Task InitializeStockInfrastructureAsync(
        this IServiceProvider services, IConfiguration configuration, CancellationToken cancellationToken = default)
    {
        // Production deployments apply migrations separately.
        if (configuration.GetValue<bool>("Database:Initialize"))
        {
            await using var scope = services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<StockDbContext>();
            await db.Database.MigrateAsync(cancellationToken);
        }

        await RabbitTopology.EnsureSubscriptionsAsync(services.GetRequiredService<RabbitConnection>(), cancellationToken);
    }
}

// DependencyInjection: Stok servisinin veritabanı, repository ve MediatR bağımlılıklarını kaydeder.
// ProductCreatedHandler ile stok outbox fabrikasını bağlar; başlangıçta RabbitMQ aboneliklerini hazırlar.
