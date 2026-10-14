using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Observability;

public sealed class RequestTelemetryBehavior<TRequest, TResponse>(
    IRequestTelemetryEnricher enricher,
    ILogger<RequestTelemetryBehavior<TRequest, TResponse>> logger) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var operation = typeof(TRequest).Name;
        using var activity = OperationTelemetry.Application.StartActivity(operation);
        enricher.Enrich(activity, request, null);
        var started = Stopwatch.GetTimestamp();
        try
        {
            var response = await next();
            enricher.Enrich(activity, request, response);
            var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            logger.LogInformation("Operation {Operation} completed in {DurationMs} ms. ProductId={ProductId}",
                operation, elapsed, activity?.GetTagItem("product.id"));
            return response;
        }
        catch (Exception exception)
        {
            activity?.SetStatus(ActivityStatusCode.Error, exception.GetType().Name);
            logger.LogError(exception, "Operation {Operation} failed. ProductId={ProductId}", operation, activity?.GetTagItem("product.id"));
            throw;
        }
    }
}
