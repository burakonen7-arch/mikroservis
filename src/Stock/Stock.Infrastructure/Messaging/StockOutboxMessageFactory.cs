using BuildingBlocks.Persistence;
using Contracts.Events;
using Contracts.Messaging;
using Shared.Application.Abstractions;
using Stock.Application.Events;

namespace Stock.Infrastructure.Messaging;

public sealed class StockOutboxMessageFactory : IOutboxMessageFactory
{
    public OutboxMessage Create(IApplicationEvent applicationEvent) => applicationEvent switch
    {
        StockChangedEvent message => OutboxMessage.Create(Topics.StockChanged,
            new StockChanged(Guid.NewGuid(), message.ProductId, message.Quantity, message.Version)),
        _ => throw new InvalidOperationException($"Unsupported stock event: {applicationEvent.GetType().Name}.")
    };
}
