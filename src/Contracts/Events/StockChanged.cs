namespace Contracts.Events;

public sealed record StockChanged(Guid EventId, Guid ProductId, int Quantity, long Version);

// StockChanged: RabbitMQ üzerinden ürün servisine iletilen stok değişikliği mesajının veri sözleşmesidir.
// Olay kimliğini, ürün kimliğini, stok miktarını ve sıralama kontrolünde kullanılan sürümü taşır.
