using BuildingBlocks;
using BuildingBlocks.Messaging;
using BuildingBlocks.Messaging.Abstractions;
using BuildingBlocks.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Product.Application.Abstractions;
using Product.Application.Commands.CreateProduct;
using Product.Infrastructure.Messaging;
using Product.Infrastructure.Repositories;
using BuildingBlocks.Observability;
using Product.Infrastructure.Observability;

namespace Product.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddProductInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<ProductDbContext>(options =>
            options.UseMongoDB(configuration.GetConnectionString("Database")
                ?? throw new InvalidOperationException("ConnectionStrings:Database is required."), "products"));
        
        services.AddScoped<IProductReadRepository, ProductReadRepository>();
        services.AddScoped<IProductWriteRepository, ProductWriteRepository>();
        services.AddScoped<IRequestTelemetryEnricher, ProductTelemetryEnricher>();
        services.AddMediatR(options =>
        {
            options.RegisterServicesFromAssemblyContaining<CreateProductCommandHandler>();
            options.AddOpenBehavior(typeof(RequestTelemetryBehavior<,>));
        });
        services.AddScoped<IMessageHandler, StockChangedHandler>();
        services.AddScoped<IOutboxMessageFactory, ProductOutboxMessageFactory>();
        services.AddServiceInfrastructure<ProductDbContext>(configuration, "product-service", "product:");
        return services;
    }

    public static async Task InitializeProductInfrastructureAsync(
        this IServiceProvider services, IConfiguration configuration, CancellationToken cancellationToken = default)
    {
        // Database initialization is opt-in for local development.
        if (configuration.GetValue<bool>("Database:Initialize"))
        {
            await using var scope = services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ProductDbContext>();
            await db.Database.EnsureCreatedAsync(cancellationToken);
        }

        await RabbitTopology.EnsureSubscriptionsAsync(services.GetRequiredService<RabbitConnection>(), cancellationToken);
    }
}

// DependencyInjection: Ürün servisinin veritabanı, repository ve MediatR bağımlılıklarını kaydeder.
// StockChangedHandler ile ürün outbox fabrikasını bağlar; başlangıçta RabbitMQ aboneliklerini hazırlar.
