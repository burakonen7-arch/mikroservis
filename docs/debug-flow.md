# Rider'da ürün ekleme ve tüm akışı izleme

Breakpoint'ler mevcut Rider projesine IDE üzerinden yerleştirildi. Tam liste, kategori ve
her durakta incelenecek değerler [debug-breakpoints.json](debug-breakpoints.json) dosyasındadır.
JSON bir referans listesidir; başka bir Rider projesinde breakpoint'leri otomatik kurmaz.
Tablolarda dosya adları src/ altındaki projelere göre kısaltılmıştır; tam yollar JSON'dadır.

## 1. Hazırlık

Proje kökünde:

~~~powershell
.\scripts\prepare-debug.ps1
~~~

Script MongoDB replica set, Oracle, Redis, RedisInsight, RabbitMQ, Aspire ve Collector'ı
hazırlar; Docker'daki Gateway/Product/Stock API'lerini durdurur ve .local/*-debug.env
dosyalarını oluşturur. Docker Desktop Linux motoru çalışır durumda olmalıdır. Script,
docker.exe PATH'te yoksa kullanıcıya veya Program Files'a kurulan Docker Desktop yolunu
da kontrol eder; motor erişilemiyorsa API'leri durdurmadan açıklayıcı hata verir.
Altyapı zaten çalışıyorsa yalnızca ayarları üretmek için -SkipInfrastructure kullanabilirsiniz.
Bu seçenek MongoDB, Redis veya diğer Docker servislerini başlatmaz.

Product başlarken localhost:27017 için connection refused / 10061 hatası varsa MongoDB
erişilebilir değildir. Önce Docker Desktop'ı ve bu hazırlık scriptini çalıştırın;
ardından Rider debug oturumunu yeniden başlatın. Hazırlık scripti MongoDB health
kontrolünü ve rs0 replica set primary durumunu bekler.

Rider'da solution configuration **Debug**, run configuration **Mikroservis Debug** olsun.
**Debug** ile Gateway, Product ve Stock oturumlarını birlikte açın. Komut satırında
aynı API'ler çalışıyorsa önceden scripts/stop-local.py ile durdurun. Aynı servisleri
Docker ve Rider'da birlikte çalıştırmak port çakışmasına veya mesajın breakpoint
koymadığınız süreç tarafından tüketilmesine neden olabilir.

| Ekran | Adres |
|---|---|
| Gateway Swagger | http://localhost:5000/swagger/index.html |
| Product Swagger | http://localhost:5001/swagger/index.html |
| Stock Swagger | http://localhost:5002/swagger/index.html |
| RabbitMQ yönetimi | http://localhost:15672 |
| RedisInsight | http://localhost:5540 |
| Aspire traces / logs / metrics | http://localhost:18888 |

Program.cs, DependencyInjection, telemetry provider kaydı ve RabbitMQ başlangıç
kurulumlarında breakpoint yoktur. OpenTelemetry span/tag/metrik durakları da kaldırıldı;
iş kuralları, veritabanı, cache ve mesaj durakları aktiftir. İlk ürün isteğinin giriş
durağı ProductsController.cs:41'dir.

Login için:

~~~powershell
.\scripts\get-dev-token.ps1 -Role admin
~~~

Token'ı Gateway Swagger **Authorize** alanına girin; üstteki listeden **Product API**
belgesini seçin. AuthController.cs:37 development login doğrulamasını ve rolü gösterir.
Login breakpoint'inde durursa Gateway oturumunda Resume yapın; token scripti varsayılan 120 saniye bekler.
Token 15 dakika geçerlidir; uzun debug oturumunda yenileyin. Token/parolaları paylaşmayın.

## 2. Ürün ekleme: HTTP → domain → MongoDB → outbox

Gateway üzerinden **POST /products** gönderin:

~~~json
{"name":"Debug Klavye","price":1250.50}
~~~

| Sıra | Dosya / satır | İncelenecek işlem |
|---|---|---|
| 1 | ProductsController.cs:41 | request.Name / Price; CreateProductCommand |
| 2 | CreateProductCommandHandler.cs:17 | Yeni ProductItem ve GUID |
| 3 | ProductItem.cs:24 | Ad/fiyat iş kuralları |
| 4 | ProductWriteRepository.cs:16 | Entity'yi context'e ekleme; henüz DB'ye yazılmaz |
| 5 | CreateProductCommandHandler.cs:19 | ProductCreatedEvent oluşturma |
| 6 | EfUnitOfWork.cs:13 | Olayı outbox'a ekleme |
| 7 | ProductOutboxMessageFactory.cs:13 | Application olayı → Contracts mesajı / topic |
| 8 | CreateProductCommandHandler.cs:20 → EfUnitOfWork.cs:21 | Ürün ve outbox birlikte kaydedilir |
| 9 | ProductsController.cs:42 | 201 ve ürün DTO'su |

Gateway YARP üzerinden Product'a yönlendirir; ürün oluşturan Gateway controller'ı yoktur.
HTTP forwarding/instrumentation kütüphane kodudur; HTTP span'larını Aspire'da inceleyebilir,
gerektiğinde Step Into ile external source'a ilerleyebilirsiniz.

Ürünün **Id** değerini not edin. EfUnitOfWork.cs:21 satırında **db.ChangeTracker.Entries()**
ile Added/Modified entity'leri inceleyin. SaveChangesAsync satırını **Step Over** ile
tamamladıktan sonra MongoDB'deki products ve outbox koleksiyonlarını yenileyin.
Publish henüz yapılmadıysa outbox SentAt değeri null'dır.

## 3. Product outbox → RabbitMQ → Stock

HTTP isteği tamamlanınca **Resume** ile arka plan worker'larını takip edin.

| Sıra | Dosya / satır | İncelenecek işlem |
|---|---|---|
| 1 | OutboxWorker.cs:58 | Bekleyen batch ve db; boş batch'te bu durak çalışmaz |
| 2 | OutboxWorker.cs:83 | product.created publish; payload, MessageId ve publisher confirm |
| 3 | OutboxWorker.cs:86 | Publish onayı alındı; SentAt atanacak |
| 4 | OutboxWorker.cs:87 | Gönderildi bilgisini MongoDB'ye kaydetme |
| 5 | OutboxWorker.cs:88 | Published logu |
| 6 | ConsumerWorker.cs:95 | **Stock oturumu:** payload, routing key ve ProductCreatedHandler çağrısı |
| 7 | ProductCreatedHandler.cs:17, :20 | JSON çözümü ve InitializeStockCommand |
| 8 | StockWriteRepository.cs:19 | Stok kaydı zaten var mı? |
| 9 | InitializeStockCommandHandler.cs:20 | Yeni stok: quantity 0, version 1 |
| 10 | InitializeStockCommandHandler.cs:22 | İlk StockChangedEvent |
| 11 | InitializeStockCommandHandler.cs:23 → EfUnitOfWork.cs:21 | Oracle stok ve outbox kaydı |
| 12 | RedisCache.cs:50 | Stok cache anahtarını silme |
| 13 | ConsumerWorker.cs:105, :106 | Başarılı işlem sonrası ACK ve Consumed logu |

StockOutboxMessageFactory.cs:13 durağında stock.changed olayını; OutboxWorker.cs:83
durağında kaydedilmiş JSON payload ve mesaj başlıklarını inceleyin.

RabbitMQ exchange **microservices**, topic **product.created**, kuyruk
**stock.product-created** olur. ACK öncesi durakta mesaj **Unacked** görünür.
Uygulamanın ana kuyruğundan Get messages ile mesaj çekmek yerine debugger'daki
payload/delivery değerlerini inceleyin. Daha önce demo.messages oluşturulduysa
mesaj kopyaları ayrıca bu kuyrukta görülebilir.

## 4. Stock outbox → RabbitMQ → Product stok projeksiyonu

Bu kez **Stock Debug** içindeki OutboxWorker.cs:83 satırında topic **stock.changed**
olur. Aynı publish, SentAt ve log duraklarından geçilir.

**Product Debug** oturumunda:

| Durak | İşlem |
|---|---|
| ConsumerWorker.cs:95 | Payload, routing key ve StockChangedHandler |
| StockChangedHandler.cs:17, :20 | JSON çözümü ve ApplyStockProjectionCommand |
| ApplyStockProjectionCommandHandler.cs:15 | Tracked ürünü MongoDB'den yükleme |
| ApplyStockProjectionCommandHandler.cs:18 | Ürün bulundu mu, olay sürümü daha yeni mi? |
| ProductItem.cs:37, :40 | Eski/tekrar olayları eleme ve quantity/version uygulama |
| EfUnitOfWork.cs:21 | Güncellenmiş ürünü kaydetme |
| ApplyStockProjectionCommandHandler.cs:22 → RedisCache.cs:50 | Ürün cache'ini temizleme |
| ConsumerWorker.cs:105, :106 | ACK ve consume logu |

Tamamlanınca ürün stok görünümü quantity **0**, stockVersion **1** olur. HTTP yanıtı
asenkron projeksiyondan önce dönmüşse ilk DTO'nun stockVersion değeri **0** olabilir.

Ortak worker breakpoint'leri her iki serviste çalışır. Debug oturumunun adına,
message.Topic, delivery.RoutingKey, db tipine ve ürün ID'sine bakın. Publish sonrası
SentAt yazımı, diğer servisin mesajı işlemesi ve ACK arasında süreçler genelinde
tek bir kesin zaman sıralaması yoktur.

## 5. GET: Redis miss → veritabanı → SET → hit

Projeksiyon tamamlanınca Gateway'de **GET /products/{id}** gönderin.

| Sıra | Dosya / satır | İşlem |
|---|---|---|
| 1 | ProductsController.cs:26 | GetProductByIdQuery |
| 2 | GetProductByIdQueryHandler.cs:16 | Cache key ve cache.Get |
| 3 | RedisCache.cs:18 | Gerçek Redis GET okuması |
| 4 | RedisCache.cs:19 | value; ilk okumada null beklenir |
| 5 | RedisCache.cs:23 | Cache hit/miss logu |
| 6 | GetProductByIdQueryHandler.cs:17 | cached null mı? |
| 7 | GetProductByIdQueryHandler.cs:20 | Miss ise repository okuması |
| 8 | ProductReadRepository.cs:13 | AsNoTracking MongoDB sorgusu |
| 9 | GetProductByIdQueryHandler.cs:24 | DTO'yu cache'e yazma |
| 10 | RedisCache.cs:39 | Gerçek Redis SET ve 30 saniye TTL |

Aynı GET'i kısa süre içinde tekrar gönderin. value JSON ve cached DTO olursa hit
dalından dönülür; repository ve SET duraklarına girilmez.

TTL **30 saniye**: breakpoint'te beklediğiniz süre de buna dahildir. İkinci istekte
30 saniye dolmadan GET satırını çalıştırın; gerekirse o turda fazla durakları geçici
kapatıp sonra açın. TTL dolduysa veya stock.changed cache'i sildiyse yeni miss normaldir.
Ürün POST'unda cache GET/SET çalışmaz; bunları bu ayrı GET istekleriyle görürsünüz.
Stok/projeksiyon command'ları cache DEL kullanır.

Stock için **GET /stocks/{id}** gönderin:
StocksController.cs:24 → GetStockByProductIdQueryHandler.cs:16, :17, :20, :24 →
StockReadRepository.cs:13 → ortak Redis durakları.

RedisInsight'taki gerçek anahtarlar uygulama prefix'i nedeniyle
**product:product:{id}** ve **stock:stock:{id}** şeklindedir. Debugger'daki key
ise product:{id} / stock:{id} olur. Value ve TTL alanlarını inceleyin.

## 6. Stok artırma

GET /stocks/{id} yanıtındaki güncel version ile **POST /stocks/{id}/adjustments** gönderin:

~~~json
{"delta":10,"expectedVersion":1}
~~~

StocksController.cs:40 → AdjustStockCommandHandler.cs:22 sürüm kontrolü →
:25 domain değişikliği → StockItem.cs:22, :27 miktar/sürüm hesabı →
AdjustStockCommandHandler.cs:26 outbox olayı → EfUnitOfWork.cs:21 Oracle kaydı →
AdjustStockCommandHandler.cs:28 cache silme akışını izleyin.

Stock outbox → RabbitMQ → Product projeksiyonu tekrar çalışır. Başlangıç quantity 0,
version 1 ise son değerler quantity **10**, version **2** olur. Son Product GET
güncellenmiş projeksiyonu okur ve cache'e yazar.

## 7. Telemetry, loglar ve export

İlgili duraklarda Watch / Evaluate için:

~~~csharp
System.Diagnostics.Activity.Current?.TraceId.ToString()
System.Diagnostics.Activity.Current?.SpanId.ToString()
System.Diagnostics.Activity.Current?.ParentSpanId.ToString()
System.Diagnostics.Activity.Current?.Tags
~~~

OpenTelemetry kodunda ayrı breakpoint yoktur. Yukarıdaki ifadeler mevcut iş akışı
duraklarında isteğe bağlı incelenebilir; tracing, metrics ve log export işlemleri
çalışmaya devam eder.

Log durakları OutboxWorker.cs:88, ConsumerWorker.cs:106 ve RedisCache.cs:23
satırlarıdır. Durak sırasında log henüz
yazılmamıştır; **Step Over** ile ilerleyip o servisin Debug console'unu inceleyin.

prepare-debug.ps1 debug oturumları için şu log seviyelerini de hazırlar:

| Kategori | Seviye / görülecek bilgi |
|---|---|
| Microsoft.EntityFrameworkCore.Database.Command | Information; provider'ın command logları, Oracle SQL komutları |
| Microsoft.AspNetCore.Hosting.Diagnostics | Information; HTTP istek başlangıcı/bitişi ve durum kodu |
| Microsoft.AspNetCore.Routing.EndpointMiddleware | Information; çalıştırılan endpoint |
| Yarp.ReverseProxy (Gateway) | Debug; proxy yönlendirme ayrıntıları |

Bu ayarlar .local/*-debug.env dosyalarındadır ve yeni debug süreci başladığında yüklenir.
MongoDB'de command loglarının kapsamı provider'a bağlıdır; repository span'ları
her MQL komutunun ayrı trace edilmesi anlamına gelmez. YARP log kategorileri için
[resmî tanılama rehberine](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/servers/yarp/diagnosing-yarp-issues?view=aspnetcore-10.0) bakabilirsiniz.

Veri hattı: **Activity / Meter / ILogger → OTLP exporter → Collector → Aspire**.
Collector'ın host gRPC portu 4317'dir. observability/collector.yaml içindeki traces,
metrics ve logs pipeline'ları ayrı ayrı tanımlıdır. Collector ayrı süreçtir;
C# breakpoint'i burada durdurmaz. Collector logları:

~~~powershell
docker compose logs -f --tail 50 otel-collector
~~~

Aspire **Traces** ekranında not ettiğiniz TraceId'yi arayın. Gateway HTTP → Product
command → database.SaveChanges → product.created producer/consumer → Stock command →
Oracle kaydı → stock.changed → Product projeksiyonu span'larını ve ilgili structured
logları inceleyin. Sonradan gönderilen GET ve stok artırma istekleri ayrı trace oluşturur;
ortak product.id ile eşleştirin. **Metrics** ekranında cache.hits / cache.misses sayaçlarını izleyin.

Span export'u activity kapandıktan, metrics/log export'u exporter batch'i çalıştıktan
sonra görünür. Henüz tamamlanmamış span dashboard'da bulunmayabilir; **Resume** ile
işlemi bitirip kısa süre sonra yenileyin. Trace süreleri breakpoint'te beklediğiniz
zamanı da içerir; gerçek performans ölçümü olarak değerlendirmeyin.

## 8. Hata yolları

- Eski expectedVersion ile adjustment: AdjustStockCommandHandler.cs:22 →
  ApiExceptionHandler.cs:14, :25 → **409**.
- Mevcut stoktan fazla azaltma: StockItem.cs:22 iş kuralı → hata telemetry'si → **400**.
  Hatalı JSON/model alanları controller'a ulaşmadan framework tarafından 400 dönebilir.
- DB concurrency: EfUnitOfWork.cs:26.
- Publish hatası: ilgili servisin Debug console'unda Outbox delivery failed logu.
- Consumer hatası: ConsumerWorker.cs:118 logu ve :125 NACK. Redelivered bir deneme sayacı
  değildir; requeue=false ise DLX üzerinden ilgili .dead kuyruğuna geçilir.
- Rate limit: ApiSecurity.cs:95 Retry-After / **429**.

## 9. Bağımsız User / PostgreSQL

[User rehberindeki](user-sql-practice.md) PostgreSQL ortamını ve **User Debug** oturumunu açın.
Compound run configuration User'ı içermez. Product/Stock/RabbitMQ/Redis akışına bağlı değildir.

GET: UsersController.cs:21 → UserRepository.cs:17 veya :31; filtreler, SQL parametreleri
ve EF Core FromSqlRaw sorgusu. POST: UsersController.cs:41 → UserService.cs:36 →
UserRepository.cs:44 SaveChanges → :45 PostgreSQL'in ürettiği ID/tarih.
E-posta çakışmasında UserRepository.cs:50 hata dönüşümünü inceleyin.

## İlerleme ve bitirme

**Step Over** mevcut satırı çalıştırır; **Step Into** çağrıya girer; **Resume** sonraki
breakpoint'e kadar devam eder. Async servis/worker geçişleri tek call stack değildir;
Debug penceresindeki doğru oturumu seçin.

Breakpoint bütün thread'leri durdurduğunda o servisin diğer işleri de bekler.
Uzun duraklamalarda HTTP timeout'u veya RabbitMQ bağlantısının yeniden kurulması görülebilir.
**Run → View Breakpoints** üzerinden o anda izlemediğiniz User veya diğer iş akışı dosyalarının
duraklarını geçici kapatabilirsiniz.

Breakpoint'ler .idea içinde yerel IDE ayarı olarak tutulur, Git'e eklenmez.
Rehber ve tam listedeki satırlar 1 tabanlıdır. Kod değişirse debugger'ın gösterdiği
gerçek statement'ı esas alın.

Debug bittikten sonra Gateway/Product/Stock oturumlarını Stop ile kapatıp Docker'a dönmek için:

~~~powershell
docker compose up -d product-api stock-api gateway-api
~~~

Rider'daki ilerleme ve async debug davranışları için [breakpoint belgeleri](https://www.jetbrains.com/help/rider/Using_Breakpoints.html),
[stepping](https://www.jetbrains.com/help/rider/Stepping_Through_the_Program.html) ve
[async debug](https://www.jetbrains.com/help/rider/Debugging_Multithreaded_Applications.html) kaynaklarına bakabilirsiniz.
