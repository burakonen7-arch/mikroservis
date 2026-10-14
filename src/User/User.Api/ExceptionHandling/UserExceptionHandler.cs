using Microsoft.AspNetCore.Diagnostics;
using User.Application.Exceptions;

namespace User.Api.ExceptionHandling;

public sealed class UserExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title) = exception switch
        {
            ArgumentException => (StatusCodes.Status400BadRequest, "Invalid request"),
            DuplicateEmailException => (StatusCodes.Status409Conflict, "Email already exists"),
            _ => (0, string.Empty)
        };
        if (status == 0)
            return false;
        await Results.Problem(statusCode: status, title: title, detail: exception.Message).ExecuteAsync(context);
        return true;
    }
}
