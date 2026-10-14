using System.ComponentModel.DataAnnotations;

namespace User.Api.Models;

public sealed class CreateUserRequest
{
    [Required, StringLength(100)]
    public string FirstName { get; init; } = string.Empty;

    [Required, StringLength(100)]
    public string LastName { get; init; } = string.Empty;

    [Required, EmailAddress, StringLength(254)]
    public string Email { get; init; } = string.Empty;

    [Required, Range(0, 120)]
    public int? Age { get; init; }

    [Required, StringLength(100)]
    public string City { get; init; } = string.Empty;
}
