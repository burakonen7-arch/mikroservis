using BuildingBlocks;
using BuildingBlocks.Messaging;
using BuildingBlocks.Messaging.Abstractions;
using BuildingBlocks.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Stock.Application.Abstractions;
using Stock.Application.Services;
using Stock.Infrastructure.Messaging;
using Stock.Infrastructure.Repositories;

namespace Stock.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddStockInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<StockDbContext>(options =>
            options.UseOracle(configuration.GetConnectionString("Database")
                ?? throw new InvalidOperationException("ConnectionStrings:Database is required.")));
        services.AddScoped<IStockRepository, StockRepository>();
        services.AddScoped<IStockService, StockService>();
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
