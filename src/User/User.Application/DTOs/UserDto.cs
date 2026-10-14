using User.Domain.Entities;

namespace User.Application.DTOs;

public sealed record UserDto(
    long Id, string FirstName, string LastName, string Email, int Age, string City, DateTime CreatedAt)
{
    public static UserDto FromEntity(UserAccount user) => new(
        user.Id, user.FirstName, user.LastName, user.Email, user.Age, user.City, user.CreatedAt);
}
