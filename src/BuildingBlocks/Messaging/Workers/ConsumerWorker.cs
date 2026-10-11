using System.Diagnostics;
using System.Text;
using BuildingBlocks.Messaging.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace BuildingBlocks.Messaging.Workers;

public sealed class ConsumerWorker(
    IServiceScopeFactory scopes,
    RabbitConnection rabbit,
    IConfiguration configuration,
    ILogger<ConsumerWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var queue = configuration["Messaging:Queue"]
            ?? throw new InvalidOperationException("Messaging:Queue is required.");
        var topic = configuration["Messaging:Topic"]
            ?? throw new InvalidOperationException("Messaging:Topic is required.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var channel = await (await rabbit.GetAsync(stoppingToken))
                    .CreateChannelAsync(cancellationToken: stoppingToken);
                await RabbitTopology.DeclareAsync(channel, queue, topic, stoppingToken);
                await channel.BasicQosAsync(0, 1, false, stoppingToken);
                var consumer = new AsyncEventingBasicConsumer(channel);
                consumer.ReceivedAsync += async (_, delivery) =>
                {
                    try
                    {
                        ActivityContext parent = default;
                        if (delivery.BasicProperties.Headers?.TryGetValue("traceparent", out var header) == true
                            && header is byte[] bytes)
                        {
                            ActivityContext.TryParse(Encoding.UTF8.GetString(bytes), null, out parent);
                        }

                        using var activity = RabbitTopology.Activities.StartActivity(
                            delivery.RoutingKey, ActivityKind.Consumer, parent);
                        await using var scope = scopes.CreateAsyncScope();
                        var handler = scope.ServiceProvider.GetRequiredService<IMessageHandler>();
                        await handler.HandleAsync(delivery.RoutingKey,
                            Encoding.UTF8.GetString(delivery.Body.Span), stoppingToken);
                        await channel.BasicAckAsync(delivery.DeliveryTag, false, stoppingToken);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
                    catch (Exception exception)
                    {
                        logger.LogError(exception, "Message handling failed: {MessageId}", delivery.BasicProperties.MessageId);
                        // Retry once; failed redeliveries go to the durable dead letter queue.
                        await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
                        await channel.BasicNackAsync(delivery.DeliveryTag, false,
                            requeue: !delivery.Redelivered, cancellationToken: stoppingToken);
                    }
                };

                await channel.BasicConsumeAsync(queue, false, consumer, stoppingToken);
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "RabbitMQ consumer connection failed");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }
}
