using System.Diagnostics;
using System.Text;
using BuildingBlocks.Messaging.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using BuildingBlocks.Observability;

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
                
                //mesaj gönderip almak için bir kanal oluşturulur
                await using var channel = await (await rabbit.GetAsync(stoppingToken))
                    .CreateChannelAsync(cancellationToken: stoppingToken);
                
                //gerekli mesajlaşma yapıları oluşturulur, kuyruk, exchange vs
                await RabbitTopology.DeclareAsync(channel, queue, topic, stoppingToken);
                
                //consumer'a aynı anda sadece 1 tane onaylanmamış mesaj gönderilmesini sağlar,
                //mesaj ACK ile onaylandıktan sonra tekrar yeni mesaj gönderimi başlar.
                await channel.BasicQosAsync(0, 1, false, stoppingToken);
                
                //consumer oluşuturulur
                var consumer = new AsyncEventingBasicConsumer(channel);
                consumer.ReceivedAsync += async (_, delivery) =>
                {
                    try
                    {
                        //RabbitMQ mesajının header'ında traceparent varsa bu bilgiyi okuyarak önceki trace ile bağlantı kurmak için hazırlıyoruz.
                        ActivityContext parent = default;
                    
                        if (delivery.BasicProperties.Headers?.TryGetValue("traceparent", out var header) == true
                            && header is byte[] bytes)
                        {
                            ActivityContext.TryParse(Encoding.UTF8.GetString(bytes), null, out parent);
                        }

                        //rabbitten gelen mesajın işlenmesini izlemek için activity başlatıyoruz
                        //activityKind.Consumer = mesajı alan ve işleyen taraf olduğunu belirtir. 
                        using var activity = RabbitTopology.Activities.StartActivity(delivery.RoutingKey, ActivityKind.Consumer, parent);
                        
                        //gelen mesajın içeriği
                        var payload = Encoding.UTF8.GetString(delivery.Body.Span);
                        
                        //izleme içine rabbitten gelen mesajı ekliyoruz. yani payload
                        MessagingTelemetry.Enrich(activity, delivery.RoutingKey, payload, delivery.BasicProperties.MessageId);
                        
                        //mesajın içinde bulunan bilgileri izlemeye ek detay olarak ekliyoruz
                        //mesajın süreci, kuyruk bilgisi ve teslim edilip yeniden gönderilip gönderilmediği bilgisi
                        activity?.SetTag("messaging.operation.name", "process");
                        activity?.SetTag("messaging.destination.subscription.name", queue);
                        activity?.SetTag("messaging.rabbitmq.message.redelivered", delivery.Redelivered);
                        
                        await using var scope = scopes.CreateAsyncScope();
                        var handler = scope.ServiceProvider.GetRequiredService<IMessageHandler>();
                        
                        try
                        {
                            await handler.HandleAsync(delivery.RoutingKey, payload, stoppingToken);
                        }
                        catch (Exception exception)
                        {
                            // hata oluşursa izleme sistemine bildirmek için
                            activity?.SetStatus(ActivityStatusCode.Error, exception.GetType().Name);
                            throw;
                        }
                        
                        //mesajın consumer tarafından başarılı bir şekilde işlendiğinin onayını rabbite bildirir.ACK
                        //rabbit de mesajı kuyruktan kaldırır.
                        await channel.BasicAckAsync(delivery.DeliveryTag, false, stoppingToken);
                        
                        logger.LogInformation("Consumed {Topic}. MessageId={MessageId}; ProductId={ProductId}",
                            delivery.RoutingKey, delivery.BasicProperties.MessageId, activity?.GetTagItem("product.id"));
                    
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
   
                    catch (Exception exception)
                    {
                        logger.LogError(exception, "Message handling failed: {MessageId}", delivery.BasicProperties.MessageId);
                        
                        //mesaj işlenirken hata oluşursa 2 saniye bekler,
                        await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
                        
                        //2 saniye sonra mesajın başarılıyla işlenemediği bilgisi rabbite gönderilir ve kuyruk sistemine göre
                        //ilk teslimatta başarısız olursa mesaj aynı kuyruğa geri alınır (retry).
                        //eeniden teslimatta da başarısız olursa mesaj reddedilir.
                        await channel.BasicNackAsync(delivery.DeliveryTag, false,
                            requeue: !delivery.Redelivered, cancellationToken: stoppingToken);
                    }
                };
                
                //ilgili kuyruğu dinlemeye başlar ve gelen mesajları consumer'a iletir.
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

