using System.Text.Json;
using MediatR;
using BuildingBlocks.Messaging.Abstractions;
using Contracts.Events;
using Contracts.Messaging;
using Stock.Application.Commands.InitializeStock;

namespace Stock.Infrastructure.Messaging;

public sealed class ProductCreatedHandler(ISender mediatr) : IMessageHandler
{
    public Task HandleAsync(string topic, string payload, CancellationToken cancellationToken)
    {
        if (topic != Topics.ProductCreated)
            throw new ArgumentException("Unexpected topic.", nameof(topic));

        var message = JsonSerializer.Deserialize<ProductCreated>(payload)
            ?? throw new ArgumentException("Invalid product message.", nameof(payload));

        return mediatr.Send(new InitializeStockCommand(message.ProductId), cancellationToken);
    }
}

// ProductCreatedHandler: RabbitMQ üzerinden gelen product.created mesajının topic bilgisini doğrular ve JSON içeriğini çözer.
// Yeni ürün için stok kaydı oluşturmak üzere InitializeStockCommand komutunu MediatR üzerinden gönderir.
