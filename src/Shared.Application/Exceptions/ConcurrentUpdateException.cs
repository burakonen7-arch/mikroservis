namespace Shared.Application.Exceptions;

public sealed class ConcurrentUpdateException : Exception
{
    public ConcurrentUpdateException(string message) : base(message) { }

    public ConcurrentUpdateException(string message, Exception innerException)
        : base(message, innerException) { }
}
