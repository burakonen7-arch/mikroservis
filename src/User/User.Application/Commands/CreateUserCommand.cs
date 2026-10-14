namespace User.Application.Commands;

public sealed record CreateUserCommand(string FirstName, string LastName, string Email, int Age, string City);
