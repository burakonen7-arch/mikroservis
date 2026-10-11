namespace Shared.Application.Abstractions;

public interface ICache
{
    Task<T?> Get<T>(string key, CancellationToken ct);
    Task Put<T>(string key, T value, CancellationToken ct);
    Task Remove(string key, CancellationToken ct);
}
