using User.Application.DTOs;
using User.Application.Queries;
using User.Domain.Entities;

namespace User.Application.Abstractions;

public interface IUserRepository
{
    Task<IReadOnlyList<UserDto>> GetUsersAsync(UserSearchQuery query, CancellationToken cancellationToken);
    Task<UserDto?> GetByIdAsync(long id, CancellationToken cancellationToken);
    Task<UserDto> CreateAsync(UserAccount user, CancellationToken cancellationToken);
}
