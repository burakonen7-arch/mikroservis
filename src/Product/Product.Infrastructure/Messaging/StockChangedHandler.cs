using System.Text.Json;
using BuildingBlocks.Messaging.Abstractions;
using Contracts.Events;
using Contracts.Messaging;
using Product.Application.Abstractions;

namespace Product.Infrastructure.Messaging;

public sealed class StockChangedHandler(IProductService products) : IMessageHandler
{
    public Task HandleAsync(string topic, string payload, CancellationToken cancellationToken)
    {
        if (topic != Topics.StockChanged)
            throw new ArgumentException("Unexpected topic.", nameof(topic));

        var message = JsonSerializer.Deserialize<StockChanged>(payload)
            ?? throw new ArgumentException("Invalid stock message.", nameof(payload));

        return products.ApplyStockAsync(message.ProductId, message.Quantity, message.Version, cancellationToken);
    }
}
