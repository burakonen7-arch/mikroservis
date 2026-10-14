# Gateway, JWT ve rate limiting

Gateway: **http://localhost:5000/swagger/index.html**. Üstteki belge listesinden Gateway login, Product veya Stock seçilir. Product ve Stock'un doğrudan Swagger adresleri 5001/5002 portlarında da kullanılabilir. İşlem endpoint'leri hem Gateway'de hem servislerde JWT gerektirir; health endpoint'leri anonimdir.

## Başlatma ve token

```powershell
.\scripts\prepare-debug.ps1
```

Bu komut eksik JWT/development parolalarını ignored `.env` dosyasına ekler; mevcut parolaları korur. Product, Stock ve Gateway için `.local/*-debug.env` dosyalarını üretir. Rider'da **Mikroservis Debug** üç servisi birlikte açar.

Komut satırında çalıştırmak için önce Rider oturumlarını durdurun:

```powershell
dotnet build Mikroservis.slnx -c Release --no-restore
py scripts/start-local.py
```

Host süreçlerini durdurmak: `py scripts/stop-local.py`. Linux'ta `python3` kullanın. Yerel script yalnızca kendi PID kayıtlarına ve bu workspace'in assembly yoluna uyan süreçleri durdurur. Rider'a geçmeden önce bu script'i çalıştırın.

Token almak için:

```powershell
.\scripts\get-dev-token.ps1 -Role admin
```

Çıktıdaki token'ı Swagger **Authorize** alanına yapıştırın. Script parolayı `.env` dosyasından okur. Token 15 dakika geçerlidir; süresi dolunca yeniden alınır. Token veya `.env` Git'e eklenmez.

Token almak için Gateway'in `localhost:5000` üzerinde çalışması gerekir. Rider'da
**Gateway Debug** veya üç servisi birlikte açan **Mikroservis Debug** yapılandırmasını kullanın.
Login breakpoint'inde durursa Gateway oturumunda **Resume** yapın. Script bu işlem için
varsayılan 120 saniye bekler; gerekirse `-TimeoutSec` parametresiyle süreyi değiştirebilirsiniz.

Swagger'daki `POST /auth/token` de kullanılabilir:

```json
{"username":"admin","password":".env dosyasındaki AUTH_ADMIN_PASSWORD değeri"}
```

| Kullanıcı / rol | Okuma | Ürün oluşturma/güncelleme/silme | Stok değiştirme | .env parola anahtarı |
|---|---|---|---|---|
| admin | Evet | Evet | Evet | AUTH_ADMIN_PASSWORD |
| reader | Evet | Hayır | Hayır | AUTH_READER_PASSWORD |
| catalog | Evet | Evet | Hayır | AUTH_CATALOG_PASSWORD |
| inventory | Evet | Hayır | Evet | AUTH_INVENTORY_PASSWORD |

Yerel token endpoint'i yalnızca Development ortamında çalışır; Production ortamında 404 döner. Bu bir kullanıcı kayıt/kimlik yönetimi servisi değildir. Üretimde kimlik sağlayıcısı ve ona uygun JWT issuer/audience/signature ayarları kullanılmalıdır. Mevcut doğrulama HS256 imzası, issuer, audience ve süreyi kontrol eder. Signing key environment/secret ayarında bulunur.

## Ürün oluşturma

`POST /products` için JWT ile yetkilendirme yaptıktan sonra ürün adı ve fiyatını gönderin:

```json
{"name":"Klavye","price":1250.50}
```

Her başarılı istek yeni bir ürün oluşturur ve 201 yanıtında ürün DTO'sunu döndürür. Ürün ve Outbox kaydı aynı MongoDB transaction'ında kaydedilir. Stok hareketlerinde `expectedVersion` kontrolü uygulanır.

## İstek sınırları

- Gateway iş endpoint'leri: kullanıcı (`sub`) başına 60 istek / 60 saniye.
- Product ve Stock: her servis için kullanıcı başına 120 istek / 60 saniye.
- Development login: IP başına 5 deneme / dakika.
- Limit aşıldığında 429 Problem Details ve `Retry-After` dönülür; istek sıraya alınmaz.

`RateLimiting:PermitLimit`, `WindowSeconds`, `LoginPermitLimit` konfigürasyonla değiştirilebilir. Sayaçlar süreç içinde tutulur; çok instance'lı dağıtım için ortak/distributed kota ayrıca tasarlanmalıdır. Gateway limiti dolduğunda doğrudan servis adresi, servisin kendi auth ve limit kontrollerini uygular.

## Aspire'da yeni bilgiler

Traces ekranında HTTP → Gateway → servis → command/query → veritabanı/Redis → RabbitMQ akışı görülebilir. Yeni span'lar:

- Command/query adları: `CreateProductCommand`, `GetProductByIdQuery`, `AdjustStockCommand` vb.
- MongoDB/Oracle okuma ve `database.SaveChanges` süreleri.
- `redis.GET`, `redis.SET`, `redis.DEL`; `cache.result=hit/miss`, anahtar ve TTL.
- Ürün kimliği/adı/fiyatı ve stok miktarı/sürümü alanları.
- Mesaj kimliği, routing key, consumer kuyruğu ve redelivery bilgisi.

Structured logs başarılı application işlemlerini, cache hit/miss ve mesaj publish/consume sonuçlarını trace bağlantısıyla gösterir. Metrics'te `cache.hits` ve `cache.misses` sayaçları vardır. Ham HTTP/mesaj gövdeleri, JWT veya parolalar trace'e yazılmaz. Veritabanı span'ları repository/Unit of Work düzeyindedir; her SQL/MQL komutu ayrı instrument edilmez.

İlgili kaynaklar: [JWT](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/configure-jwt-bearer-authentication?view=aspnetcore-10.0), [YARP](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/servers/yarp/yarp-overview?view=aspnetcore-10.0), [rate limiting](https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit?view=aspnetcore-10.0).
