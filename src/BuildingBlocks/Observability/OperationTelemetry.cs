using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace BuildingBlocks.Observability;

public static class OperationTelemetry
{
    public static readonly ActivitySource Application = new("Mikroservis.Application");
    public static readonly ActivitySource Persistence = new("Mikroservis.Persistence");
    public static readonly ActivitySource Cache = new("Mikroservis.Cache");
    public static readonly Meter Meter = new("Mikroservis.Operations");
    public static readonly Counter<long> CacheHits = Meter.CreateCounter<long>("cache.hits");
    public static readonly Counter<long> CacheMisses = Meter.CreateCounter<long>("cache.misses");

    public static Activity? Database(string name, string system, string? id = null)
    {
        var activity = Persistence.StartActivity(name, ActivityKind.Client);
        activity?.SetTag("db.system.name", system);
        if (id is not null) activity?.SetTag("product.id", id);
        return activity;
    }
}
