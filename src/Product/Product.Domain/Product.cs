namespace Product.Domain;

public sealed class ProductItem
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = "";
    public decimal Price { get; private set; }
    public int AvailableQuantity { get; private set; }
    public long StockVersion { get; private set; }
    private ProductItem() { }
    public ProductItem(Guid id, string name, decimal price)
    {
        if (id == Guid.Empty || string.IsNullOrWhiteSpace(name) || name.Trim().Length > 200 || price < 0 || decimal.Round(price, 2) != price) throw new ArgumentException("Name (1–200 characters) and nonnegative price with at most two decimals are required.");
        Id = id; Name = name.Trim(); Price = price;
    }
    public bool ApplyStock(int quantity, long version)
    {
        if (quantity < 0 || version < 1) throw new ArgumentException("Invalid stock event.");
        if (version <= StockVersion) return false;
        AvailableQuantity = quantity; StockVersion = version; return true;
    }
}
