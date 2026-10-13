using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;
using OpenTelemetry.Logs;
using Microsoft.Extensions.Logging;
namespace BuildingBlocks.Observability;

public static class Telemetry
{
    public static IServiceCollection AddTelemetry(this IServiceCollection services, string name)
    {
        services.AddOpenTelemetry().ConfigureResource(r => r.AddService(name))
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddSource("Mikroservis.Messaging")
                .AddSource("Mikroservis.Application", "Mikroservis.Persistence", "Mikroservis.Cache")
                .AddOtlpExporter())
            .WithMetrics(metrics => metrics
                .AddMeter("Mikroservis.Operations")
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddOtlpExporter());
        services.AddLogging(logging => logging.AddOpenTelemetry(options =>
        {
            options.SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(name));
            options.IncludeFormattedMessage = true;
            options.IncludeScopes = true;
            options.AddOtlpExporter();
        }));
        return services;
    }
}
