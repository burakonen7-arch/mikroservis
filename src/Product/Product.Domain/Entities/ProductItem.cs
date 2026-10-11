namespace Product.Domain.Entities;

public sealed class ProductItem
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public decimal Price { get; private set; }
    public int AvailableQuantity { get; private set; }
    public long StockVersion { get; private set; }

    private ProductItem() { }

    public ProductItem(Guid id, string name, decimal price)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Product ID is required.", nameof(id));
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 200)
            throw new ArgumentException("Name must contain 1–200 characters.", nameof(name));
        if (price < 0 || decimal.Round(price, 2) != price)
            throw new ArgumentException("Price must be nonnegative with at most two decimals.", nameof(price));

        Id = id;
        Name = name.Trim();
        Price = price;
    }

    public bool ApplyStock(int quantity, long version)
    {
        if (quantity < 0 || version < 1)
            throw new ArgumentException("Invalid stock event.");
        if (version <= StockVersion)
            return false;

        AvailableQuantity = quantity;
        StockVersion = version;
        return true;
    }
}
