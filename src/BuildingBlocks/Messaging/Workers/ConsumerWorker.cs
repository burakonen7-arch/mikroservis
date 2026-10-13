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
        // 1. Stok servisi örneğinde "stock.product-created" kuyruğunu seçer.
        // Ayar yoksa hangi kuyruğun dinleneceği bilinmediği için hata verir.
        
        var topic = configuration["Messaging:Topic"]
            ?? throw new InvalidOperationException("Messaging:Topic is required.");
        // 2. Bu kuyruk için "product.created" yönlendirme kuralını okur.
        // Buradaki topic, topology kurulurken binding key olarak kullanılacaktır.

        while (!stoppingToken.IsCancellationRequested)
        // 3. Servis durdurulmadıkça dinleme oturumunu sürdürür; dış try hata verirse yeni oturum açmayı dener.
        // Bu döngü her mesaj için dönmez; mesajlar aşağıdaki ReceivedAsync callback'inde işlenir.
        {
            try
            {
                await using var channel = await (await rabbit.GetAsync(stoppingToken))
                    .CreateChannelAsync(cancellationToken: stoppingToken);
                // 4. Bağlantıyı alır ve bu consumer için bir RabbitMQ kanalı açar.
                // Kanal, bağlantı üzerinde abonelik ve ACK/NACK işlemlerini taşır.
                // await using, bu try bloğundan çıkıldığında kanalı asenkron olarak temizler.
                
                await RabbitTopology.DeclareAsync(channel, queue, topic, stoppingToken);
                // 5. Ana exchange, normal kuyruk, dead-letter exchange ve .dead kuyruğunu hazırlar.
                // Binding sayesinde product.created mesajları stock.product-created kuyruğuna yönlenir.
                
                await channel.BasicQosAsync(0, 1, false, stoppingToken);
                // 6. prefetchCount=1: Bu consumer'a aynı anda en fazla bir onaylanmamış mesaj teslim edilir.
                // İlk ürün mesajı ACK/NACK almadan sıradaki mesaj teslim edilmez.
                // İlk 0, byte boyutu limiti koymaz; false, prefetch sınırını consumer düzeyinde uygular.
                
                var consumer = new AsyncEventingBasicConsumer(channel);
                // 7. Kanal üzerinden teslim edilen mesajları asenkron işleyecek consumer nesnesini oluşturur.
            
                consumer.ReceivedAsync += async (_, delivery) =>
                // 8. Her teslimatta çalışacak callback'i tanımlar; henüz kuyruğa abone olunmuş değildir.
                // delivery; mesaj gövdesini, routing key'i, başlıkları ve teslimat bilgilerini taşır.
                {
                    try
                    {
                        ActivityContext parent = default;
                        // 9. Ürün servisindeki önceki izleme adımını temsil edecek bağlamı boş başlatır.
                    
                        if (delivery.BasicProperties.Headers?.TryGetValue("traceparent", out var header) == true
                            && header is byte[] bytes)
                        // Mesajda byte dizisi olarak traceparent başlığı varsa gönderenin izleme bağlamını okur.
                        {
                            ActivityContext.TryParse(Encoding.UTF8.GetString(bytes), null, out parent);
                            // Başlığı UTF-8 metnine çevirip çözümler; geçerliyse tüketici izini yayıncı izine bağlayabilir.
                        }

                        using var activity = RabbitTopology.Activities.StartActivity(
                            delivery.RoutingKey, ActivityKind.Consumer, parent);
                        var payload = Encoding.UTF8.GetString(delivery.Body.Span);
                        MessagingTelemetry.Enrich(activity, delivery.RoutingKey, payload, delivery.BasicProperties.MessageId);
                        activity?.SetTag("messaging.operation.name", "process");
                        activity?.SetTag("messaging.destination.subscription.name", queue);
                        activity?.SetTag("messaging.rabbitmq.message.redelivered", delivery.Redelivered);
                        // 10. "product.created" teslimatının işlenmesini bir consumer izleme adımı olarak başlatır.
                        // İzleme dinleyicisi etkinse bu adım kaydedilir; using bitişte activity'yi kapatır.
                        
                        await using var scope = scopes.CreateAsyncScope();
                        // 11. Bu mesaj için ayrı DI scope açar; scoped DbContext gibi bağımlılıklar burada yaşar.
                        // Mesaj işlemesi bitince scope içindeki kaynaklar temizlenir.
                    
                        var handler = scope.ServiceProvider.GetRequiredService<IMessageHandler>();
                        // 12. Stok servisinde kayıtlı ProductCreatedHandler örneğini bağımlılıklarıyla birlikte alır.
                        // Aynı worker ürün servisinde çalışırsa orada kayıtlı StockChangedHandler alınır.
                        
                        try
                        {
                            await handler.HandleAsync(delivery.RoutingKey, payload, stoppingToken);
                        }
                        catch (Exception exception)
                        {
                            activity?.SetStatus(ActivityStatusCode.Error, exception.GetType().Name);
                            throw;
                        }
                        // 13. Routing key'i ve UTF-8'e çevrilen JSON gövdesini handler'a verir ve sonucu bekler.
                        // Senaryoda ProductCreatedHandler, InitializeStockCommand göndererek stok kaydını oluşturur.
                        
                        await channel.BasicAckAsync(delivery.DeliveryTag, false, stoppingToken);
                        logger.LogInformation("Consumed {Topic}. MessageId={MessageId}; ProductId={ProductId}",
                            delivery.RoutingKey, delivery.BasicProperties.MessageId, activity?.GetTagItem("product.id"));
                        // 14. İşlem başarılı olunca RabbitMQ'ya "bu teslimatı tamamladım" onayı gönderir.
                        // DeliveryTag bu kanaldaki teslimatı tanımlar; false yalnızca bu teslimatı onaylar.
                        // RabbitMQ mesajı bu kuyruk için tamamlanmış sayar ve sıradaki mesajı teslim edebilir.
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
                    // Servis kapanırken iptal edilen işlemi normal işleme hatası olarak loglamaz.
                    // ACK verilmemiş mesaj, kanal kapandığında RabbitMQ tarafından yeniden kuyruğa alınabilir.
                    catch (Exception exception)
                    // 15. Örneğin stok kaydı sırasında veritabanı hatası oluşursa bu hata yoluna girer.
                    {
                        logger.LogError(exception, "Message handling failed: {MessageId}", delivery.BasicProperties.MessageId);
                        // Hatayı mesaj kimliğiyle kaydeder; hangi ürün mesajının başarısız olduğu araştırılabilir.
                        // Retry once; failed redeliveries go to the durable dead letter queue.
                       
                        await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
                        // NACK göndermeden önce consumer içinde 2 saniye bekler; ayrı bir retry kuyruğu kullanmaz.
                        
                        await channel.BasicNackAsync(delivery.DeliveryTag, false,
                            requeue: !delivery.Redelivered, cancellationToken: stoppingToken);
                        // 16. İlk teslimat hatalıysa Redelivered=false olur: requeue=true ile mesaj kuyruğa geri döner.
                        // Yeniden teslim edilen mesaj da hata verirse requeue=false ile reddedilir.
                        // Kuyruğun dead-letter ayarı mesajı deadletters exchange'ine, binding de .dead kuyruğuna taşır.
                        // false yalnızca bu teslimatı reddeder. Redelivered bir deneme sayacı değildir;
                        // bağlantı kopması gibi nedenlerle yeniden teslim edilen mesajlarda da true olabilir.
                    }
                };

                await channel.BasicConsumeAsync(queue, false, consumer, stoppingToken);
                // 17. Consumer'ı stock.product-created kuyruğuna abone eder; mesaj teslimleri artık başlayabilir.
                // false, autoAck'i kapatır: mesajlar alınır alınmaz değil, yukarıdaki ACK ile onaylanır.
                
                await Task.Delay(Timeout.Infinite, stoppingToken);
                // 18. Servis durdurulana kadar bu oturumu açık tutar; callback gelen mesajları işlemeye devam eder.
                // Bu bekleyiş kendi başına kanal kopmasını izlemez; iptal edildiğinde dış kapanış yoluna geçilir.
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            // Uygulama kapanırken kanal açma veya bekleme işlemi iptal edilirse normal kapanış olarak ele alır.
            {
                break;
                // Dış döngüyü bitirerek arka plan servisini durdurur.
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "RabbitMQ consumer connection failed");
                // Kanal açma, topology kurma veya abonelik başlatma gibi dış try içindeki hataları loglar.
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                // 5 saniye bekler; ardından while döngüsü bağlantı/kanal ve abonelik kurulumunu yeniden dener.
            }
        }
    }
}

// ConsumerWorker: Yapılandırılan RabbitMQ kuyruğunu dinler ve mesajları IMessageHandler uygulamasına iletir.
// Başarılı işlemleri onaylar; hatalı mesajı bir kez yeniden kuyruğa alır, tekrar başarısız olursa dead-letter kuyruğuna yönlendirir.
