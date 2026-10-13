namespace Product.Infrastructure.Idempotency;

public sealed class ProductCreationRequest
{
    public string Id { get; set; } = string.Empty;
    public string Fingerprint { get; set; } = string.Empty;
    public Guid ProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int AvailableQuantity { get; set; }
    public long StockVersion { get; set; }
    public DateTime CreatedAt { get; set; }
}
