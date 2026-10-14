using System.Diagnostics;
using System.Text.Json;

namespace BuildingBlocks.Observability;

public static class MessagingTelemetry
{
    public static void Enrich(Activity? activity, string topic, string payload, string? messageId)
    {
        if (activity is null) return;
        
        activity.SetTag("messaging.system", "rabbitmq");
        activity.SetTag("messaging.destination.name", "microservices");
        activity.SetTag("messaging.rabbitmq.destination.routing_key", topic);
        activity.SetTag("messaging.message.id", messageId);
        
        try
        {
            using var json = JsonDocument.Parse(payload);
            if (json.RootElement.TryGetProperty("ProductId", out var id)) activity.SetTag("product.id", id.GetString());
            if (json.RootElement.TryGetProperty("Name", out var name)) activity.SetTag("product.name", name.GetString());
            if (json.RootElement.TryGetProperty("Quantity", out var quantity)) activity.SetTag("stock.quantity", quantity.GetInt32());
            if (json.RootElement.TryGetProperty("Version", out var version)) activity.SetTag("stock.version", version.GetInt64());
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or FormatException)
        {
            // Diagnostics must not replace the consumer's message validation.
        }
    }
}
