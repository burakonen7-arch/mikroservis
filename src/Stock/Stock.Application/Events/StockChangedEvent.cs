using Shared.Application.Abstractions;

namespace Stock.Application.Events;

public sealed record StockChangedEvent(Guid ProductId, int Quantity, long Version) : IApplicationEvent;

// StockChangedEvent: Stok oluşturulduğunda veya miktarı değiştiğinde outbox akışına eklenen uygulama olayıdır.
// Ürün kimliği, stok miktarı ve sürümünü taşır; stok mesaj fabrikası bu olayı RabbitMQ mesajına dönüştürür.
