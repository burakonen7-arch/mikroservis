namespace Shared.Application.Exceptions;

public sealed class IdempotencyConflictException(string message) : Exception(message);
