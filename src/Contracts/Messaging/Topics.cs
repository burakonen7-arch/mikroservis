namespace Contracts.Messaging;

public static class Topics
{
    public const string ProductCreated = "product.created";
    public const string StockChanged = "stock.changed";
}

// Topics: Ürün oluşturulma ve stok değişikliği mesajlarının ortak RabbitMQ routing key değerlerini tanımlar.
// Yayıncıların ve aboneliklerin aynı topic adlarını kullanmasını sağlar.
