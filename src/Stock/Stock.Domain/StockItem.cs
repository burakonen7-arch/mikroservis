namespace Stock.Domain;

public sealed class StockItem
{
    public Guid ProductId { get; private set; }
    public int Quantity { get; private set; }
    public long Version { get; private set; }
    private StockItem() { }
    public StockItem(Guid productId) { if (productId == Guid.Empty) throw new ArgumentException("Product ID required."); ProductId = productId; Version = 1; }
    public void Adjust(int delta) { var next = checked(Quantity + delta); if (next < 0) throw new ArgumentException("Insufficient stock."); Quantity = next; Version = checked(Version + 1); }
}
