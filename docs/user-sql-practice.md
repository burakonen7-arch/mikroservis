# Bağımsız User servisi: PostgreSQL, EF Core ve raw SQL

User servisi SQL sorguları çalışmak için oluşturuldu. Yalnızca PostgreSQL kullanır;
Product, Stock, Gateway, Redis ve RabbitMQ bağlantısı veya ortak proje bağımlılığı yoktur.
ORM olarak EF Core ve Npgsql PostgreSQL provider kullanır. Dapper, MediatR, JWT ve outbox içermez.

## Çalıştırma

Proje kökünde yalnızca bu servisi ve kendi veritabanını başlatın:

```powershell
docker compose -f compose.user.yaml up -d --build
```

- Swagger: http://localhost:5003/swagger/index.html
- API: http://localhost:5003/users
- PostgreSQL: `localhost:5433`, veritabanı `users`, kullanıcı `user_app`, parola `user_dev_password`.

Bu sabit parola yerel SQL çalışma ortamı içindir. Bağlantı ayarı
`ConnectionStrings__Database` ortam değişkeniyle değiştirilebilir.
Ana `compose.yaml` bu servisi başlatmaz; mevcut servislerin çalışması gerekmez.

API'yi Rider'da debug etmek için yalnızca veritabanını başlatın:

```powershell
docker compose -f compose.user.yaml up -d user-db
dotnet run --project src/User/User.Api/User.Api.csproj
```

Rider'daki **User Debug** ayarı da kullanılabilir. API Docker'da çalışıyorsa
5003 portunu boşaltmak için önce `docker compose -f compose.user.yaml stop user-api` çalıştırın.

## Onion / Clean bağımlılıkları

```text
User.Api            -> User.Application, User.Infrastructure (DI kurulumu)
User.Infrastructure -> User.Application -> User.Domain
```

- **Domain:** `UserAccount`, ad/e-posta/yaş/şehir doğrulamaları; paket bağımlılığı yok.
- **Application:** `UserService`, repository interface'i, command/query ve DTO'lar; yalnızca Domain'e bağımlı.
- **Infrastructure:** EF Core `UserDbContext`, fluent entity eşlemeleri, SQL dosyaları, repository ve DI kayıtları.
- **Api:** GET/POST controller, HTTP giriş modelleri, Swagger ve Problem Details.

`Directory.Build.targets` Domain ve Application bağımlılıklarını derlemede kontrol eder.
Veri akışı: Controller → UserService → IUserRepository → UserRepository → UserDbContext → PostgreSQL.
EF Core bağımlılığı yalnızca Infrastructure katmanındadır; Domain ve Application ORM'den bağımsızdır.

## Endpoint'ler

| Metot | Yol | İşlem |
|---|---|---|
| GET | `/users` | ID sırasıyla kullanıcı listesi; varsayılan 20 kayıt |
| GET | `/users/{id}` | Tek kullanıcı; bulunamazsa 404 |
| POST | `/users` | Kullanıcı ekleme; 201 ve Location başlığı |

Liste parametreleri: `search`, `city`, `minAge`, `maxAge`, `page`, `pageSize`.
Arama ad, soyad ve e-postada `ILIKE` kullanır; şehir filtresi büyük/küçük harfe duyarsız eşitliktir.
`page >= 1`, `pageSize` 1–100 ve yaş 0–120 olmalıdır. Geçersiz giriş 400,
aynı e-posta ile ikinci kayıt 409 döndürür. E-posta trim edilip küçük harfe çevrilir;
benzersizlik PostgreSQL constraint'iyle korunur.

```http
GET http://localhost:5003/users?pageSize=100
GET http://localhost:5003/users?city=Ankara&minAge=25&maxAge=45
GET http://localhost:5003/users?search=Ali&page=1&pageSize=10
GET http://localhost:5003/users/1
```

POST gövdesi:

```json
{
  "firstName": "Burak",
  "lastName": "Onen",
  "email": "burak@example.com",
  "age": 30,
  "city": "Istanbul"
}
```

Rider HTTP Client ile doğrudan çalıştırılabilecek istekler `src/User/User.Api/User.Api.http` dosyasındadır.

## SQL dosyaları ve 100 örnek kayıt

`src/User/User.Infrastructure/Sql/Queries` içindeki dosyalar API'nin gerçek sorgularıdır:

- `GetUsers.sql`: filtreleme, `ILIKE`, sıralama, `LIMIT / OFFSET`.
- `GetUserById.sql`: ID ile arama.

GET sorguları EF Core'un `FromSqlRaw(...)` metodu ile çalıştırılır ve entity'lere eşlenir;
`AsNoTracking()` okuma sonuçlarını takip etmez. POST, `DbSet.Add(...)` ve
`SaveChangesAsync(...)` ile kaydedilir; ID ve kayıt tarihi PostgreSQL tarafından üretilip entity'ye aktarılır.

Sorgular assembly'ye gömülür; değiştirdikten sonra API'yi yeniden derleyip başlatın.
`@search`, `@city`, `@id` gibi değerler tipli `NpgsqlParameter` nesneleriyle EF Core'a aktarılır;
kullanıcı girdisi SQL metnine birleştirilmez. Parametreler her SQL dosyasının başında yazılıdır.

`Sql/Init/001-schema.sql` tabloyu, `002-seed.sql` ise `generate_series(1, 100)` ile
100 kullanıcıyı oluşturur. Kayıtlar 10 şehir, farklı yaşlar ve son 100 güne dağılan
kayıt tarihleri içerir. Dummy e-postalar `user001@example.com`–`user100@example.com` şeklindedir.
Yeni, boş veritabanında başlangıç sayısı 100'dür; POST ile sayı artar.

Init dosyaları yalnızca PostgreSQL volume'u ilk oluşturulduğunda çalışır.
Yeniden başlatma ek kullanıcı üretmez; mevcut veriler volume'da kalır.
Şema dosyasını değiştirmek mevcut tabloyu otomatik değiştirmez; SQL konsolunda `ALTER TABLE` çalıştırabilirsiniz.

Rider Database penceresinde PostgreSQL bağlantısı açın veya terminalden SQL konsoluna girin:

```powershell
docker compose -f compose.user.yaml exec user-db psql -U user_app -d users
```

Pratik sorguları `src/User/User.Infrastructure/Sql/Exercises.sql` dosyasındadır.
Bu dosya otomatik çalışmaz; sorguları konsolda tek tek deneyebilirsiniz.

```powershell
docker compose -f compose.user.yaml down
```

Bu komut yalnızca User ortamını durdurur ve verileri korur.

Referanslar: [EF Core raw SQL sorguları](https://learn.microsoft.com/en-us/ef/core/querying/sql-queries),
[Npgsql EF Core provider](https://www.npgsql.org/efcore/),
[PostgreSQL generate_series](https://www.postgresql.org/docs/current/functions-srf.html).
