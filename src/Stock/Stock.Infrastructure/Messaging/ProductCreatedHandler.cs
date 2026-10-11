using System.Text.Json;
using BuildingBlocks.Messaging.Abstractions;
using Contracts.Events;
using Contracts.Messaging;
using Stock.Application.Abstractions;

namespace Stock.Infrastructure.Messaging;

public sealed class ProductCreatedHandler(IStockService stocks) : IMessageHandler
{
    public Task HandleAsync(string topic, string payload, CancellationToken cancellationToken)
    {
        if (topic != Topics.ProductCreated)
            throw new ArgumentException("Unexpected topic.", nameof(topic));

        var message = JsonSerializer.Deserialize<ProductCreated>(payload)
            ?? throw new ArgumentException("Invalid product message.", nameof(payload));

        return stocks.InitializeAsync(message.ProductId, cancellationToken);
    }
}
