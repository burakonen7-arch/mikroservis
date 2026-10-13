namespace Contracts.Events;

public sealed record ProductCreated(Guid EventId, Guid ProductId, string Name);

// ProductCreated: RabbitMQ üzerinden stok servisine iletilen ürün oluşturulma mesajının veri sözleşmesidir.
// Olay kimliğini, ürün kimliğini ve ürün adını taşır.
