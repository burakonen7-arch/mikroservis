namespace Contracts;

public sealed record ProductCreated(Guid EventId, Guid ProductId, string Name);
public sealed record StockChanged(Guid EventId, Guid ProductId, int Quantity, long Version);
public static class Topics { public const string ProductCreated = "product.created"; public const string StockChanged = "stock.changed"; }
