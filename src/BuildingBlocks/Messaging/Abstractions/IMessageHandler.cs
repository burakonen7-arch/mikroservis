namespace BuildingBlocks.Messaging.Abstractions;

public interface IMessageHandler
{
    Task HandleAsync(string topic, string payload, CancellationToken cancellationToken);
}
