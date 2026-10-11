namespace Contracts;

public sealed class OutboxMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Topic { get; set; } = "";
    public string Payload { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SentAt { get; set; }
    public string? TraceParent { get; set; }
    public static OutboxMessage Create<T>(string topic, T value) => new() { TraceParent = System.Diagnostics.Activity.Current?.Id, Topic = topic, Payload = System.Text.Json.JsonSerializer.Serialize(value) };
}
public interface IMessageHandler { Task Handle(string topic, string payload, CancellationToken ct); }
public interface ICache
{
    Task<T?> Get<T>(string key, CancellationToken ct);
    Task Put<T>(string key, T value, CancellationToken ct);
    Task Remove(string key, CancellationToken ct);
}
