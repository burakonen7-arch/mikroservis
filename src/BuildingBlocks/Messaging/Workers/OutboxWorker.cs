using System.Diagnostics;
using System.Text;
using BuildingBlocks.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace BuildingBlocks.Messaging.Workers;

public sealed class OutboxWorker<TContext>(
    IServiceScopeFactory scopes,
    RabbitConnection rabbit,
    ILogger<OutboxWorker<TContext>> logger) : BackgroundService where TContext : DbContext
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DeliverPendingAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Outbox delivery failed; pending messages will retry");
            }

            await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
        }
    }

    private async Task DeliverPendingAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TContext>();
        var batch = await db.Set<OutboxMessage>()
            .Where(message => message.SentAt == null)
            .OrderBy(message => message.CreatedAt)
            .Take(50)
            .ToListAsync(cancellationToken);
        if (batch.Count == 0)
            return;

        await using var channel = await (await rabbit.GetAsync(cancellationToken)).CreateChannelAsync(
            new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true),
            cancellationToken);
        await channel.ExchangeDeclareAsync(RabbitTopology.Exchange, ExchangeType.Topic,
            durable: true, cancellationToken: cancellationToken);

        foreach (var message in batch)
        {
            ActivityContext.TryParse(message.TraceParent, null, out var parent);
            using var activity = RabbitTopology.Activities.StartActivity(message.Topic, ActivityKind.Producer, parent);
            var properties = new BasicProperties
            {
                Persistent = true,
                MessageId = message.Id.ToString(),
                ContentType = "application/json",
                Headers = new Dictionary<string, object?>()
            };
            if (Activity.Current is { } current)
                properties.Headers["traceparent"] = current.Id;

            await channel.BasicPublishAsync(RabbitTopology.Exchange, message.Topic, mandatory: true,
                basicProperties: properties, body: Encoding.UTF8.GetBytes(message.Payload),
                cancellationToken: cancellationToken);
            message.SentAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
