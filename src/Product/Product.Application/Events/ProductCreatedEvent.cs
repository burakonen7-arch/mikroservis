using Shared.Application.Abstractions;

namespace Product.Application.Events;

public sealed record ProductCreatedEvent(Guid ProductId, string Name) : IApplicationEvent;

// ProductCreatedEvent: Ürün oluşturulduğunda outbox akışına eklenen uygulama olayıdır.
// Ürün kimliği ve adını taşır; ürün mesaj fabrikası bu olayı RabbitMQ mesajına dönüştürür.
