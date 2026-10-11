namespace Contracts.Events;

public sealed record StockChanged(Guid EventId, Guid ProductId, int Quantity, long Version);
