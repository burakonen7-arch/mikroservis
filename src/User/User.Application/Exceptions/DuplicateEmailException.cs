namespace User.Application.Exceptions;

public sealed class DuplicateEmailException(Exception innerException)
    : Exception("A user with this email address already exists.", innerException);
