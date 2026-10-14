# Rider ile akışı adım adım izleme

## Başlatma

Proje terminalinde:

```powershell
.\scripts\prepare-debug.ps1
```

Script MongoDB, Oracle, Redis, RabbitMQ ve telemetry collector'ı hazırlar;
5001/5002 portlarını ve mesaj tüketimini Rider'a bırakmak için Docker'daki iki API'yi durdurur.
Yerel bağlantı ayarlarını `.env` parolalarından `.local/product-debug.env` ve
`.local/stock-debug.env` dosyalarına üretir. `.local` Git tarafından ignore edilir.

Rider'da solution configuration **Debug** olsun. Run configuration listesinden
**Mikroservis Debug** seçip **Debug** başlatın. Bu ayar hem **Product Debug** hem
**Stock Debug** ve **Gateway Debug** oturumlarını açar. Komut satırıyla açılan servisler varsa önce `py scripts/stop-local.py` çalıştırın. Gateway/JWT kullanımı için [rehber](gateway-security.md).

`RabbitMQ:Uri is required` hatası, seçilen run configuration'ın bağlantı ayarlarını
yüklemediğini gösterir. Tek servis için paylaşılan **Product Debug** veya **Stock Debug**
yapılandırmasını kullanın. Rider'ın otomatik oluşturduğu **Product.Api / Stock.Api**
yapılandırmalarını kullanıyorsanız **Run → Edit Configurations → Environment files**
alanına sırasıyla `$PROJECT_DIR$/.local/product-debug.env` ve
`$PROJECT_DIR$/.local/stock-debug.env` ekleyin. Bu dosyalar yalnızca Docker Compose
tarafından kullanılan kökteki `.env` dosyasından farklıdır; bağlantıları ve OTEL
endpoint'ini API sürecine aktarır. Parolaları `appsettings.json` içine yazmayın.

- Product: http://localhost:5001/swagger/index.html
- Gateway: http://localhost:5000/swagger/index.html
- Stock: http://localhost:5002/swagger/index.html
- RabbitMQ: http://localhost:15672

Breakpoint'ler mevcut Rider oturumuna eklendi. Başka bir workspace/IDE'de aşağıdaki
tablodaki metodlara breakpoint koyabilirsiniz. Kod değişiklikleri nedeniyle
satır numaraları değişebilir; controller/handler/worker metotlarını esas alın.

## Bir ürün oluştururken duraklar

Product Swagger'da `POST /products` isteğini şu gövdeyle gönderin:

Önce `scripts/get-dev-token.ps1` ile token alıp Swagger Authorize alanına girin.

```json
{"name":"Debug Klavye","price":1250.50}
```

| Sıra | Breakpoint dosyası ve satır | İncelenecek değer / işlem |
|---|---|---|
| 1 | `src/Product/Product.Api/Controllers/ProductsController.cs:34` | HTTP `request`, MediatR command gönderimi |
| 2 | `src/Product/Product.Application/Commands/CreateProduct/CreateProductCommandHandler.cs:17` | `command.Name`, `command.Price`; yeni `product` |
| 3 | Aynı dosya `:20` | Ürün ve outbox birlikte MongoDB'ye kaydedilmeden önce |
| 4 | `src/BuildingBlocks/Messaging/Workers/OutboxWorker.cs:78` | `message.Topic`, `message.Payload`, `properties.MessageId`; RabbitMQ yayını |
| 5 | Aynı dosya `:82` | Yayın onayı sonrası `SentAt` işaretlenmesi |
| 6 | `src/BuildingBlocks/Messaging/Workers/ConsumerWorker.cs:87` | Stock oturumunda `delivery.RoutingKey`, `delivery.BasicProperties.MessageId`; tüketici handler'ı |
| 7 | `src/Stock/Stock.Infrastructure/Messaging/ProductCreatedHandler.cs:17` | JSON `payload` içinden ProductCreated mesajını çözme |
| 8 | `src/Stock/Stock.Application/Commands/InitializeStock/InitializeStockCommandHandler.cs:20` | `command.ProductId`; sıfır stok kaydını oluşturma |
| 9 | Aynı dosya `:23` | Stok ve `stock.changed` outbox kaydını Oracle'a yazma |
| 10 | `src/BuildingBlocks/Messaging/Workers/ConsumerWorker.cs:92` | Stock tüketicisi başarılı işlemden sonra ACK verir |
| 11 | `OutboxWorker.cs:78` ve `:82` | Bu kez Stock oturumunda `stock.changed` yayınlanır |
| 12 | `ConsumerWorker.cs:87` | Bu kez Product oturumunda `stock.changed` tüketilir |
| 13 | `src/Product/Product.Infrastructure/Messaging/StockChangedHandler.cs:17` | Stok mesajının `payload` içeriği |
| 14 | `src/Product/Product.Application/Commands/ApplyStockProjection/ApplyStockProjectionCommandHandler.cs:15` | `command.Quantity`, `command.Version`; MongoDB ürün stok görünümünü güncelleme |
| 15 | `ConsumerWorker.cs:92` | Product tüketicisi ACK verir |

**Step Over** mevcut satırı çalıştırıp sonraki satıra geçer. **Step Into** çağrılan
metoda girer. **Resume** sıradaki breakpoint'e kadar devam eder. Kısayollar Rider keymap'ine bağlıdır.
Breakpoint satırı henüz çalışmamıştır; Mongo/Oracle yazısını görmek için `SaveChangesAsync`
satırını Step Over ile tamamlayın veya sonraki breakpoint'e ilerleyin.

## Önemli: servisler ve worker'lar ayrı çalışır

HTTP isteği → outbox worker → RabbitMQ → diğer servisin tüketicisi tek bir çağrı
stack'i değildir. HTTP isteğini tamamlayınca **Resume** ile worker breakpoint'lerini
bekleyin. Debug penceresindeki **Product Debug / Stock Debug** sekmelerinden hangi
servisin durduğunu takip edin. Ortak `OutboxWorker` / `ConsumerWorker` kodunda iki
servis de durabilir; `message.Topic`, `delivery.RoutingKey` ve oturum adıyla ayırt edin.

Breakpoint tüm thread'leri durdurduğunda o serviste Swagger isteği de bekleyebilir.
Uzun süre beklemek HTTP isteğini zaman aşımına uğratabilir; Resume ile akışı tamamlayın.
Outbox worker iki saniyede bir tarar; yayın breakpoint'i sadece bekleyen mesaj olduğunda çalışır.
Oracle stok kaydının kaydedilmesi, Stock'un ACK vermesi ve kendi outbox worker'ının
yayını farklı task'larda yürüdüğünden bu durakların birbirine göre zamanı değişebilir.

## Stok eklerken

Ürün oluşturma yanıtındaki `id` ile Stock Swagger'da `GET /stocks/{id}` çağırın.
Son yanıttaki `version` değerini kullanarak `POST /stocks/{id}/adjustments` gönderin:

```json
{"delta":10,"expectedVersion":1}
```

Ek breakpoint'ler:

- `src/Stock/Stock.Api/Controllers/StocksController.cs:34`: HTTP isteğinden AdjustStock command'ine geçiş.
- `src/Stock/Stock.Application/Commands/AdjustStock/AdjustStockCommandHandler.cs:25`:
  `stock.Adjust(command.Delta)` öncesi miktar/sürüm. Step Over sonrası yeni değerleri inceleyin.

Ardından yine Stock outbox → RabbitMQ → Product tüketicisi → MongoDB projeksiyonu
duraklarına ulaşılır. Son Product yanıtında `availableQuantity: 10`, `stockVersion: 2` beklenir.

## Veritabanları ve RabbitMQ

Bağlantı bilgileri ve sorgular [yerel demo rehberinde](local-demo.md).
RabbitMQ'da `demo.messages` kuyruğunda olay kopyaları görüntülenebilir.
Stock `ConsumerWorker` ACK breakpoint'inde dururken ana stok kuyruğundaki mesaj
**Unacked** olarak görünür. Product tüketicisinde de aynı şekilde izleyebilirsiniz.

## Docker çalışmasına dönme

Önce Rider'daki iki debug oturumunu **Stop** ile kapatın; ardından:

```powershell
docker compose up -d product-api stock-api
```

Referanslar: [Rider çoklu debug](https://www.jetbrains.com/help/rider/Run_Debug_Multiple.html),
[Run/debug ayarları](https://www.jetbrains.com/help/rider/Run_Debug_Configuration.html).
