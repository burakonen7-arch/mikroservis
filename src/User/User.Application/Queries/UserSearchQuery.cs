namespace User.Application.Queries;

public sealed record UserSearchQuery(
    string? Search = null, string? City = null, int? MinAge = null, int? MaxAge = null,
    int Page = 1, int PageSize = 20);
