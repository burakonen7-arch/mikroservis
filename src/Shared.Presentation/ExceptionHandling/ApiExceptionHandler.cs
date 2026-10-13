using Shared.Application.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;

namespace Shared.Presentation.ExceptionHandling;

public sealed class ApiExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (statusCode, title, detail) = exception switch
        {
            IdempotencyConflictException => (StatusCodes.Status409Conflict, "Idempotency conflict", exception.Message),
            ArgumentException => (StatusCodes.Status400BadRequest, "Invalid request", exception.Message),
            OverflowException => (StatusCodes.Status400BadRequest, "Stock quantity overflow", "The stock quantity or version exceeds its supported range."),
            ConcurrentUpdateException => (StatusCodes.Status409Conflict, "Concurrent update", "Reload the resource and retry with the current version."),
            _ => (0, string.Empty, string.Empty)
        };

        if (statusCode == 0)
            return false;

        await Results.Problem(statusCode: statusCode, title: title, detail: detail).ExecuteAsync(httpContext);
        return true;
    }
}
