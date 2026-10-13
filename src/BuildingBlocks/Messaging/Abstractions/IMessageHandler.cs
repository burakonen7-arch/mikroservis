namespace BuildingBlocks.Messaging.Abstractions;

public interface IMessageHandler
{
    Task HandleAsync(string topic, string payload, CancellationToken cancellationToken);
}

// IMessageHandler: RabbitMQ tüketicisinin aldığı topic ve JSON içeriğini işleyecek servis bileşeninin sözleşmesidir.
