using System.Diagnostics;

namespace BuildingBlocks.Observability;

public interface IRequestTelemetryEnricher
{
    void Enrich(Activity? activity, object request, object? response);
}
