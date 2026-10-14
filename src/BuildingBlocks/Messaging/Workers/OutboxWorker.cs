using System.Diagnostics;
using System.Text;
using BuildingBlocks.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using BuildingBlocks.Observability;

namespace BuildingBlocks.Messaging.Workers;

public sealed class OutboxWorker<TContext>(
    IServiceScopeFactory scopes,
    RabbitConnection rabbit,
    ILogger<OutboxWorker<TContext>> logger) : BackgroundService where TContext : DbContext
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        // 1. Uygulama kapanana kadar gönderilmeyi bekleyen mesajları kontrol etmeye devam eder.
        {
            try
            {
                await DeliverPendingAsync(stoppingToken);
                // 2. Veritabanındaki bekleyen mesajları bulup gönderen aşağıdaki metodu çalıştırır.
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
            // 3. Bu tur bittiğinde veya hata oluştuğunda 2 saniye bekler, ardından bekleyen mesajları tekrar kontrol eder.
        }
    }

    private async Task DeliverPendingAsync(CancellationToken cancellationToken)
    // Bir gönderim turu: Bekleyen mesajları getir, RabbitMQ'ya gönder ve gönderilenleri işaretle.
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

        //rabbit üzerinden measj göndermek için kanal oluşturuluyor.
        await using var channel = await (await rabbit.GetAsync(cancellationToken)).CreateChannelAsync(
            new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true),
            cancellationToken);
  
        //mesajların ilgili kuyruklara nasıl yönlendirileceği exxhange ile belirleniyor.
        await channel.ExchangeDeclareAsync(RabbitTopology.Exchange, ExchangeType.Topic,
            durable: true, cancellationToken: cancellationToken);
      
        foreach (var message in batch)
        {
            //db'de tutulan outbox tablosundaki izleme kaydına bağlanmak için message'ın trace'i alınıyor
            //sıfırdan bir trace oluşturmak yerine, mevcuttta bulunan trace işlemi alınmaktadır
            //outbox tablosunda tutulan traceParent bilgisi okunuyor
            ActivityContext.TryParse(message.TraceParent, null, out var parent);
         
            //önceki trace bağlı, yeni bir span oluşturuluyor
            using var activity = RabbitTopology.Activities.StartActivity(message.Topic, ActivityKind.Producer, parent);
            
            //Rabbit üzerinden gelen mesajın içeriğinmi/detayını activitye eklemek için kullanılır.
            MessagingTelemetry.Enrich(activity, message.Topic, message.Payload, message.Id.ToString());
            activity?.SetTag("messaging.operation.name", "publish");
            
            //bu işlemler aslında, farklı servislerde gerçekleşen tüm adımların tek bir trace altında izlenmesini sağlamaktadır.
            //örn trace:xyz123 trace'inde productService ürün ekledi, rabbit'e mesaj gönderildi, stockService bu mesajı alıp 
            //stock düşürdü. bu 2 servis ve rabbit arasındaki tüm ilişkilerin hepsi aynı(xyz123) trace'i ile takip edilebilir.
            
            
            //rabbit ile gönderilecek mesajın özellikleri belirlenir.
            var properties = new BasicProperties
            {
                Persistent = true, // mesajın kalıcı olarak saklanması hedeflenir
                MessageId = message.Id.ToString(), // mesajın benzersiz kimliği
                ContentType = "application/json", // json formatı
                Headers = new Dictionary<string, object?>() //ek bilgiler eklemek için kullanılır
            };
            
            if (Activity.Current is { } current)
                properties.Headers["traceparent"] = current.Id;

            try
            {
                
                await channel.BasicPublishAsync(RabbitTopology.Exchange, message.Topic, mandatory: true,
                    basicProperties: properties, body: Encoding.UTF8.GetBytes(message.Payload),
                    cancellationToken: cancellationToken);
                
                message.SentAt = DateTime.UtcNow;
                
                await db.SaveChangesAsync(cancellationToken);
                
                logger.LogInformation("Published {Topic}. MessageId={MessageId}; ProductId={ProductId}",
                    message.Topic, message.Id, activity?.GetTagItem("product.id"));
            }
            catch (Exception exception)
            {
                activity?.SetStatus(ActivityStatusCode.Error, exception.GetType().Name);
                throw;
            }
        }
    }
}

// OutboxWorker: Veritabanındaki gönderilmemiş outbox mesajlarını düzenli aralıklarla RabbitMQ üzerinden yayımlar.
// Yayın onayı alındıktan sonra SentAt alanını kaydeder; başarısız gönderimler sonraki turda yeniden denenir.
// Yayın ile veritabanı kaydı ayrı adımlar olduğu için aynı mesaj tekrar teslim edilebilir.
