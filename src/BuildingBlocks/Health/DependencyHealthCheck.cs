using BuildingBlocks.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace BuildingBlocks.Health;

public sealed class DependencyHealthCheck<TContext>(
    TContext db,
    IDistributedCache redis,
    RabbitConnection rabbit,
    ILogger<DependencyHealthCheck<TContext>> logger) : IHealthCheck
    where TContext : DbContext
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!await db.Database.CanConnectAsync(cancellationToken))
                return HealthCheckResult.Unhealthy("Database is unavailable.");

            await redis.SetStringAsync("health", "ok", new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(10)
            }, cancellationToken);

            if (await redis.GetStringAsync("health", cancellationToken) != "ok")
                return HealthCheckResult.Unhealthy("Redis is unavailable.");
            if (!(await rabbit.GetAsync(cancellationToken)).IsOpen)
                return HealthCheckResult.Unhealthy("RabbitMQ is unavailable.");

            return HealthCheckResult.Healthy();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "A service dependency is not ready");
            return HealthCheckResult.Unhealthy("A service dependency is unavailable.", exception);
        }
    }
}

// DependencyHealthCheck: Veritabanı, Redis ve RabbitMQ erişimini kontrol ederek servisin hazır olma durumunu bildirir.
// RabbitMQ kontrolü, paylaşılan bağlantının açık olup olmadığını doğrular.
