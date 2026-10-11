using Shared.Application.Abstractions;

namespace Stock.Application.Events;

public sealed record StockChangedEvent(Guid ProductId, int Quantity, long Version) : IApplicationEvent;
