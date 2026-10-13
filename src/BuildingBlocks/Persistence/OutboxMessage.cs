using System.Diagnostics;
using System.Text.Json;

namespace BuildingBlocks.Persistence;

public sealed class OutboxMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Topic { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SentAt { get; set; }
    public string? TraceParent { get; set; }
    public static OutboxMessage Create<T>(string topic, T value) => new()
    {
        TraceParent = Activity.Current?.Id,
        Topic = topic,
        Payload = JsonSerializer.Serialize(value)
    };
}

// OutboxMessage: RabbitMQ üzerinden yayımlanacak olayın topic, JSON içerik, oluşturulma ve gönderilme bilgilerini saklar.
// TraceParent alanı, olayın yayımlanması sırasında dağıtık izleme bağlamının sürdürülmesini sağlar.
