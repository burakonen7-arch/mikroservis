using User.Application.Abstractions;
using User.Application.Commands;
using User.Application.DTOs;
using User.Application.Queries;
using User.Domain.Entities;

namespace User.Application.Services;

public sealed class UserService(IUserRepository users)
{
    public Task<IReadOnlyList<UserDto>> GetUsersAsync(UserSearchQuery query, CancellationToken cancellationToken)
    {
        if (query.Page < 1 || query.PageSize is < 1 or > 100)
            throw new ArgumentException("Page must be positive and pageSize must be between 1 and 100.");
        if (query.MinAge is < 0 or > 120 || query.MaxAge is < 0 or > 120 || query.MinAge > query.MaxAge)
            throw new ArgumentException("Age filters must be between 0 and 120, with minAge <= maxAge.");
        if (query.Search?.Trim().Length > 200 || query.City?.Trim().Length > 100)
            throw new ArgumentException("Search must be at most 200 characters and city at most 100 characters.");

        return users.GetUsersAsync(query with
        {
            Search = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim(),
            City = string.IsNullOrWhiteSpace(query.City) ? null : query.City.Trim()
        }, cancellationToken);
    }

    public Task<UserDto?> GetByIdAsync(long id, CancellationToken cancellationToken)
    {
        if (id < 1)
            throw new ArgumentException("User ID must be positive.", nameof(id));
        return users.GetByIdAsync(id, cancellationToken);
    }

    public Task<UserDto> CreateAsync(CreateUserCommand command, CancellationToken cancellationToken)
    {
        var user = new UserAccount(command.FirstName, command.LastName, command.Email, command.Age, command.City);
        return users.CreateAsync(user, cancellationToken);
    }
}
