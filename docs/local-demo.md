# Yerel ortamda veritabanı ve RabbitMQ demosu

Proje kökünde PowerShell ile çalıştırın:

```powershell
docker desktop start
.\scripts\prepare-debug.ps1 -SkipInfrastructure
docker compose up -d --build
```

Mevcut `.env` dosyasındaki parolalar kullanılır. API'lerin hazır olduğunu kontrol edin:

```powershell
Invoke-RestMethod http://localhost:5001/health/ready
Invoke-RestMethod http://localhost:5002/health/ready
```

## API ekranları

JWT ve Idempotency-Key kullanımı: [Gateway rehberi](gateway-security.md).

- Gateway Swagger: http://localhost:5000/swagger/index.html
- Product Swagger: http://localhost:5001/swagger/index.html
- Stock Swagger: http://localhost:5002/swagger/index.html
- RabbitMQ: http://localhost:15672

## Rider'da MongoDB ve Oracle

Database penceresinde **New / + → Connect to Database** ile bağlantı ekleyin.
Gerekirse **Download missing driver files**, ardından **Test Connection** seçin.

| Ayar | MongoDB | Oracle |
|---|---|---|
| Host | localhost | localhost |
| Port | 27017 | 1521 |
| Veritabanı / Service | products | FREEPDB1 |
| Kullanıcı | Kimlik doğrulama yok | stock |
| Parola | Yok | `.env` içindeki `ORACLE_APP_PASSWORD` değeri |
| Bağlantı türü | URL / connection string | Service name |

MongoDB bağlantı URL'si:

```text
mongodb://localhost:27017/products?replicaSet=rs0&directConnection=true
```

`directConnection=true`, replica set üyesinin Docker ağındaki `mongo` adresine
yönlenmeden host üzerindeki bağlantıyı kullanmasını sağlar.
MongoDB'de `products` veritabanını ve `products`, `outbox` koleksiyonlarını açın.
Ürün belgesindeki `AvailableQuantity` ve `StockVersion` alanlarını inceleyin.

Oracle URL'si:

```text
jdbc:oracle:thin:@//localhost:1521/FREEPDB1
```

Kullanıcı ve parolayı ayrı alanlara girin. **Schemas** sekmesinde `STOCK`
şemasını seçin. `STOCKS`, `OUTBOX` ve `__EFMigrationsHistory` tablolarını görebilirsiniz.
Oracle sorgu konsolunda:

```sql
SELECT RAWTOHEX("ProductId") AS PRODUCT_ID_RAW, "Quantity", "Version"
FROM STOCK.STOCKS;

SELECT "Topic", "Payload", "CreatedAt", "SentAt"
FROM STOCK.OUTBOX
ORDER BY "CreatedAt" DESC;
```

Oracle'daki `ProductId`, `RAW(16)` olarak saklanır; `RAWTOHEX` çıktısı .NET Guid
byte sırasını kullandığından API'deki UUID metniyle birebir görünmez.
Outbox `Payload` içindeki `ProductId` ile aynı işlemi kolayca eşleştirebilirsiniz.
`SentAt` dolu olması, outbox worker'ın yayın onayından sonra kaydı işaretlediğini gösterir.

## Tek komutla örnek işlem

```powershell
.\scripts\demo.ps1
```

Her çalıştırma yeni bir ürün oluşturur, stok kaydının oluşmasını bekler, 10 adet
stok ekler ve Product stok projeksiyonunun aynı miktara/sürüme ulaşmasını doğrular.
Ürün kimliği ve sonuç `.local/demo-result.json` dosyasına kaydedilir.

Akış:

1. `POST /products`: MongoDB'ye ürün ve `product.created` outbox kaydı yazılır.
2. Product worker, `microservices` exchange'ine `product.created` yayınlar.
3. Stock tüketicisi Oracle'a miktar 0, sürüm 1 stok kaydı yazar ve `stock.changed` üretir.
4. `POST /stocks/{id}/adjustments`: Oracle'da miktar 10, sürüm 2 olur ve yeni `stock.changed` üretilir.
5. Product tüketicisi MongoDB ürün belgesindeki stok görünümünü günceller.

## RabbitMQ'da mesaj içeriklerini görmek

Kullanıcı: `mikroservis`. Parola: `.env` içindeki `RABBITMQ_PASSWORD` değeri.

Demo scripti `demo.messages` kuyruğunu oluşturur; `microservices` exchange'ine
`product.created` ve `stock.changed` routing key'leriyle bağlar. Bu kuyruğun tüketicisi
yoktur; olayların kopyaları birikir. Servis kuyrukları kendi kopyalarını tüketmeye devam eder.
Demo kuyruğu en fazla 100 mesaj tutar; mesajlar 24 saat sonra sona erer.

1. http://localhost:15672/#/queues/%2F/demo.messages adresini açın.
2. **Get messages** bölümüne gidin.
3. **Messages** alanına `10`, **Ack mode** alanına **requeue true** girin.
4. **Get Message(s)** ile routing key, properties/header ve JSON payload'u görün.

`requeue true`, görüntülediğiniz mesajları kuyruğa geri koyar. Bu işlem mesajları
tekrar teslim edilmiş olarak işaretleyebilir. Her yeni demo normalde üç olay üretir:

| Routing key | Payload içeriği |
|---|---|
| product.created | ProductId, Name, EventId |
| stock.changed | Quantity: 0, Version: 1 |
| stock.changed | Quantity: 10, Version: 2 |

Asıl servis kuyrukları `stock.product-created` ve `product.stock-changed` isimlerindedir.
Tüketim hızlı olduğundan bunlarda **Ready = 0** görmeniz normaldir.
Exchange ekranında `microservices` bağlantılarını ve kuyrukların mesaj hızlarını inceleyebilirsiniz.

## Swagger üzerinden elle işlem

1. Product Swagger'da `POST /products` için `{"name":"Klavye","price":1250.50}` gönderin.
2. Yanıttaki `id` ile Stock Swagger'da `GET /stocks/{id}` çağırın.
   Mesaj tüketimi asenkron olduğu için ilk çağrı 404 dönerse kısa süre sonra tekrar deneyin.
3. `POST /stocks/{id}/adjustments` için `{"delta":10,"expectedVersion":1}` gönderin.
   Sonraki hareketlerde `expectedVersion` için son Stock yanıtındaki `version` değerini kullanın.
4. Product Swagger'da `GET /products/{id}` çağırın; `availableQuantity` ve `stockVersion` değişir.
5. MongoDB ve Oracle ekranlarını yenileyin, ardından `demo.messages` mesajlarını görüntüleyin.

Demo scriptini bir kez çalıştırdıktan sonra elle başlatılan işlemler de gözlem kuyruğuna kopyalanır.

## Loglar ve kapatma

```powershell
docker compose logs -f --tail 50 product-api stock-api
docker compose down
```

`down` verileri named volume'larda korur. Sonraki açılışta mevcut ürün ve stoklar durur.

Bağlantı ekranları: [Rider MongoDB](https://www.jetbrains.com/help/rider/MongoDB.html),
[Rider Oracle](https://www.jetbrains.com/help/rider/Oracle.html).
Mesaj görüntüleme seçenekleri: [RabbitMQ HTTP API](https://www.rabbitmq.com/docs/http-api-reference).
