namespace Stock.Domain.Entities;

public sealed class StockItem
{
    public Guid ProductId { get; private set; }
    public int Quantity { get; private set; }
    public long Version { get; private set; }

    private StockItem() { }

    public StockItem(Guid productId)
    {
        if (productId == Guid.Empty)
            throw new ArgumentException("Product ID is required.", nameof(productId));

        ProductId = productId;
        Version = 1;
    }

    public void Adjust(int delta)
    {
        var nextQuantity = checked(Quantity + delta);
        if (nextQuantity < 0)
            throw new ArgumentException("Insufficient stock.", nameof(delta));
        var nextVersion = checked(Version + 1);

        Quantity = nextQuantity;
        Version = nextVersion;
    }
}
