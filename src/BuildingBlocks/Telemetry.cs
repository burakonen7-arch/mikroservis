using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;
using OpenTelemetry.Logs;
using Microsoft.Extensions.Logging;
namespace BuildingBlocks;

public static class Telemetry
{
    public static IServiceCollection AddTelemetry(this IServiceCollection services, string name)
    {
        services.AddOpenTelemetry().ConfigureResource(r => r.AddService(name))
         .WithTracing(t => t.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation().AddSource("Mikroservis.Messaging").AddOtlpExporter())
         .WithMetrics(m => m.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation().AddOtlpExporter());
        services.AddLogging(l => l.AddOpenTelemetry(o => { o.SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(name)); o.IncludeFormattedMessage = true; o.IncludeScopes = true; o.AddOtlpExporter(); }));
        return services;
    }
}
