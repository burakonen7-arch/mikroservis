using System.Diagnostics;
using Contracts.Messaging;
using RabbitMQ.Client;

namespace BuildingBlocks.Messaging;

public static class RabbitTopology
{
    public const string Exchange = "microservices";
    public static readonly ActivitySource Activities = new("Mikroservis.Messaging");

    public static async Task EnsureSubscriptionsAsync(RabbitConnection rabbit, CancellationToken cancellationToken)
    {
        // Both subscriptions must exist before either service publishes its first event.
        await using var channel = await (await rabbit.GetAsync(cancellationToken))
            .CreateChannelAsync(cancellationToken: cancellationToken);
        await DeclareAsync(channel, "stock.product-created", Topics.ProductCreated, cancellationToken);
        await DeclareAsync(channel, "product.stock-changed", Topics.StockChanged, cancellationToken);
    }

    public static async Task DeclareAsync(
        IChannel channel, string queue, string topic, CancellationToken cancellationToken)
    {
        await channel.ExchangeDeclareAsync(Exchange, ExchangeType.Topic,
            durable: true, cancellationToken: cancellationToken);
        await channel.ExchangeDeclareAsync("deadletters", ExchangeType.Topic,
            durable: true, cancellationToken: cancellationToken);
        await channel.QueueDeclareAsync(queue + ".dead", durable: true, exclusive: false,
            autoDelete: false, cancellationToken: cancellationToken);
        await channel.QueueBindAsync(queue + ".dead", "deadletters", topic, cancellationToken: cancellationToken);
        await channel.QueueDeclareAsync(queue, durable: true, exclusive: false, autoDelete: false,
            arguments: new Dictionary<string, object?> { ["x-dead-letter-exchange"] = "deadletters" },
            cancellationToken: cancellationToken);
        await channel.QueueBindAsync(queue, Exchange, topic, cancellationToken: cancellationToken);
    }
}
