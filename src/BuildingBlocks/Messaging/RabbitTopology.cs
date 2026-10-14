using System.Diagnostics;
using Contracts.Messaging;
using RabbitMQ.Client;

namespace BuildingBlocks.Messaging;

public static class RabbitTopology
{
    public const string Exchange = "microservices";
    
    
    public static readonly ActivitySource Activities = new("Mikroservis.Messaging");
    //izleme işlemi için kaynak oluşturulur. Daha sonra bu kaynak dinlemeye alınır
    //.StartActivity ile yukarıdaki kaynak üzerinden yeni bir span oluşturuluyor. ve izleme başlatılıyor.
    
    
    public static async Task EnsureSubscriptionsAsync(RabbitConnection rabbit, CancellationToken cancellationToken)
    {
        await using var channel = await (await rabbit.GetAsync(cancellationToken))
            .CreateChannelAsync(cancellationToken: cancellationToken);
        
        //ürün oluştuğunda gönderilecek mesajların işleneceği stock.product-created kuyruğu ve gerekli exchange işlemi
        await DeclareAsync(channel, "stock.product-created", Topics.ProductCreated, cancellationToken);
        
        //stock değiştiğinde gönderilecek mesakların işleneceği product.stock-change kuyruğu ve exhcnage bilgisi
        await DeclareAsync(channel, "product.stock-changed", Topics.StockChanged, cancellationToken);
    }

    public static async Task DeclareAsync(
        IChannel channel, string queue, string topic, CancellationToken cancellationToken)
    {
        //normal exchange
        await channel.ExchangeDeclareAsync(Exchange, ExchangeType.Topic,
            durable: true, cancellationToken: cancellationToken);
        
        //DLQ için exchange belirleniyor
        await channel.ExchangeDeclareAsync("deadletters", ExchangeType.Topic,
            durable: true, cancellationToken: cancellationToken);
        
        //DLQ için kuyruk
        await channel.QueueDeclareAsync(queue + ".dead", durable: true, exclusive: false,
            autoDelete: false, cancellationToken: cancellationToken);
        
        //DLQ için oluşturulan kuyruk ve exchange bind ediliyor
        await channel.QueueBindAsync(queue + ".dead", "deadletters", topic, cancellationToken: cancellationToken);
        
        //normal queue
        await channel.QueueDeclareAsync(queue, durable: true, exclusive: false, autoDelete: false,
            arguments: new Dictionary<string, object?> { ["x-dead-letter-exchange"] = "deadletters" },
            cancellationToken: cancellationToken);
        //normal bind
        await channel.QueueBindAsync(queue, Exchange, topic, cancellationToken: cancellationToken);
    }
}

// RabbitTopology: RabbitMQ exchange, kuyruk ve topic yönlendirme bağlarını oluşturur.
// İlk olay yayımlanmadan iki servisin aboneliklerini hazırlar; işlenemeyen mesajlar için dead-letter kuyruklarını tanımlar.
