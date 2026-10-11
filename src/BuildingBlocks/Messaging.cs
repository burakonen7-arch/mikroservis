using Contracts;
using System.Text;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
namespace BuildingBlocks;

public sealed class RabbitConnection(IConfiguration config) : IAsyncDisposable
{
    private readonly SemaphoreSlim gate = new(1);
    private IConnection? connection;
    public async Task<IConnection> Get(CancellationToken ct)
    {
        await gate.WaitAsync(ct);
        try { if (connection is null || !connection.IsOpen) connection = await new ConnectionFactory { Uri = new Uri(config["RabbitMQ:Uri"] ?? throw new InvalidOperationException("RabbitMQ:Uri required")), AutomaticRecoveryEnabled = true }.CreateConnectionAsync(ct); return connection; }
        finally { gate.Release(); }
    }
    public async ValueTask DisposeAsync() { if (connection is not null) await connection.DisposeAsync(); gate.Dispose(); }
}
public static class Messaging
{
    public const string Exchange = "microservices";
    public static readonly ActivitySource Activities = new("Mikroservis.Messaging");
    public static async Task Topology(IChannel channel, string queue, string topic, CancellationToken ct)
    {
        await channel.ExchangeDeclareAsync(Exchange, ExchangeType.Topic, durable: true, cancellationToken: ct);
        await channel.ExchangeDeclareAsync("deadletters", ExchangeType.Topic, durable: true, cancellationToken: ct);
        await channel.QueueDeclareAsync(queue + ".dead", durable: true, exclusive: false, autoDelete: false, cancellationToken: ct);
        await channel.QueueBindAsync(queue + ".dead", "deadletters", topic, cancellationToken: ct);
        await channel.QueueDeclareAsync(queue, durable: true, exclusive: false, autoDelete: false, arguments: new Dictionary<string, object?> { ["x-dead-letter-exchange"] = "deadletters" }, cancellationToken: ct);
        await channel.QueueBindAsync(queue, Exchange, topic, cancellationToken: ct);
    }
}
public sealed class OutboxWorker<T>(IServiceScopeFactory scopes, RabbitConnection rabbit, ILogger<OutboxWorker<T>> log) : BackgroundService where T : DbContext
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<T>();
                var batch = await db.Set<OutboxMessage>().Where(x => x.SentAt == null).OrderBy(x => x.CreatedAt).Take(50).ToListAsync(ct);
                if (batch.Count > 0)
                {
                    await using var channel = await (await rabbit.Get(ct)).CreateChannelAsync(new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true), ct);
                    await channel.ExchangeDeclareAsync(Messaging.Exchange, ExchangeType.Topic, durable: true, cancellationToken: ct);
                    foreach (var message in batch)
                    {
                        ActivityContext.TryParse(message.TraceParent, null, out var parent);
                        using var activity = Messaging.Activities.StartActivity(message.Topic, ActivityKind.Producer, parent);
                        var props = new BasicProperties { Persistent = true, MessageId = message.Id.ToString(), ContentType = "application/json", Headers = new Dictionary<string, object?>() };
                        if (Activity.Current is { } current) props.Headers["traceparent"] = current.Id;
                        await channel.BasicPublishAsync(Messaging.Exchange, message.Topic, mandatory: true, basicProperties: props, body: Encoding.UTF8.GetBytes(message.Payload), cancellationToken: ct);
                        message.SentAt = DateTime.UtcNow; await db.SaveChangesAsync(ct);
                    }
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception ex) { log.LogError(ex, "Outbox delivery failed; pending messages will retry"); }
            await Task.Delay(TimeSpan.FromSeconds(2), ct);
        }
    }
}
public sealed class ConsumerWorker(IServiceScopeFactory scopes, RabbitConnection rabbit, IConfiguration config, ILogger<ConsumerWorker> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await using var channel = await (await rabbit.Get(ct)).CreateChannelAsync(cancellationToken: ct);
                var queue = config["Messaging:Queue"]!; var topic = config["Messaging:Topic"]!;
                await Messaging.Topology(channel, queue, topic, ct); await channel.BasicQosAsync(0, 1, false, ct);
                var consumer = new AsyncEventingBasicConsumer(channel);
                consumer.ReceivedAsync += async (_, ea) =>
                {
                    try
                    {
                        ActivityContext parent = default;
                        if (ea.BasicProperties.Headers?.TryGetValue("traceparent", out var header) == true && header is byte[] bytes) ActivityContext.TryParse(Encoding.UTF8.GetString(bytes), null, out parent);
                        using var activity = Messaging.Activities.StartActivity(ea.RoutingKey, ActivityKind.Consumer, parent);
                        using var scope = scopes.CreateScope(); await scope.ServiceProvider.GetRequiredService<IMessageHandler>().Handle(ea.RoutingKey, Encoding.UTF8.GetString(ea.Body.Span), ct);
                        await channel.BasicAckAsync(ea.DeliveryTag, false, ct);
                    }
                    catch (Exception ex)
                    {
                        log.LogError(ex, "Message handling failed: {MessageId}", ea.BasicProperties.MessageId);
                        // Retry transient failures without a hot loop; poison messages go to the durable dead letter queue.
                        await Task.Delay(TimeSpan.FromSeconds(2), ct);
                        await channel.BasicNackAsync(ea.DeliveryTag, false, requeue: !ea.Redelivered, cancellationToken: ct);
                    }
                };
                await channel.BasicConsumeAsync(queue, false, consumer, ct); await Task.Delay(Timeout.Infinite, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception ex) { log.LogError(ex, "RabbitMQ consumer connection failed"); await Task.Delay(TimeSpan.FromSeconds(5), ct); }
        }
    }
}
