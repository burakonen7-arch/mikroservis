using System.Net.Mail;

namespace User.Domain.Entities;

public sealed class UserAccount
{
    public long Id { get; private set; }
    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public int Age { get; private set; }
    public string City { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }

    private UserAccount() { }

    public UserAccount(string firstName, string lastName, string email, int age, string city)
    {
        FirstName = RequiredText(firstName, 100, nameof(firstName));
        LastName = RequiredText(lastName, 100, nameof(lastName));
        City = RequiredText(city, 100, nameof(city));
        Email = RequiredText(email, 254, nameof(email)).ToLowerInvariant();
        if (!MailAddress.TryCreate(Email, out var address) || address.Address != Email)
            throw new ArgumentException("A valid email address is required.", nameof(email));
        if (age is < 0 or > 120)
            throw new ArgumentException("Age must be between 0 and 120.", nameof(age));
        Age = age;
    }

    private static string RequiredText(string value, int maximumLength, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > maximumLength)
            throw new ArgumentException($"{parameterName} must contain 1–{maximumLength} characters.", parameterName);
        return value.Trim();
    }
}
