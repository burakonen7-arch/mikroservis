using System.Text.Json;
using MediatR;
using BuildingBlocks.Messaging.Abstractions;
using Contracts.Events;
using Contracts.Messaging;
using Product.Application.Commands.ApplyStockProjection;

namespace Product.Infrastructure.Messaging;

public sealed class StockChangedHandler(ISender mediatr) : IMessageHandler
{
    public Task HandleAsync(string topic, string payload, CancellationToken cancellationToken)
    {
        if (topic != Topics.StockChanged)
            throw new ArgumentException("Unexpected topic.", nameof(topic));

        var message = JsonSerializer.Deserialize<StockChanged>(payload)
            ?? throw new ArgumentException("Invalid stock message.", nameof(payload));

        return mediatr.Send(
            new ApplyStockProjectionCommand(message.ProductId, message.Quantity, message.Version), cancellationToken);
    }
}

// StockChangedHandler: RabbitMQ üzerinden gelen stock.changed mesajının topic bilgisini doğrular ve JSON içeriğini çözer.
// Ürün servisindeki stok görünümünü güncellemek için ApplyStockProjectionCommand komutunu MediatR üzerinden gönderir.
