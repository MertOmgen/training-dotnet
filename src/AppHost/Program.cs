// =============================================================================
// AppHost — .NET Aspire Orkestratörü
// =============================================================================
// 📚 EĞİTİCİ NOT (Tech-Tutor):
//
// Bu dosya, tüm LMS mikroservis altyapısının merkezi tanım noktasıdır.
// Normalde docker-compose.yml ile yapılan şeyi burada C# kodu ile yapıyoruz.
//
// Aspire Resource Modeli:
// ┌──────────────────────────────────────────────────────────────────────┐
// │ IResourceBuilder<T>: Her kaynak (DB, servis, konteyner) bu tip ile  │
// │ temsil edilir. WithReference(), WaitFor(), WithEnvironment() gibi   │
// │ fluent API metotları ile zenginleştirilir.                           │
// │                                                                      │
// │ Kaynak Türleri:                                                      │
// │ → ContainerResource: Docker image tabanlı (PostgreSQL, Redis vs.)   │
// │ → ProjectResource: .NET projesi (.csproj dosyası)                   │
// └──────────────────────────────────────────────────────────────────────┘
//
// WithReference(resource) Ne Yapar?
// → Hedef servise, kaynak hakkında env var'ları enjekte eder:
//
//   Kaynak Türü   │ Enjekte Edilen Env Var
//   ──────────────┼─────────────────────────────────────────────────
//   PostgreSQL DB │ ConnectionStrings__catalog-db = "Host=...;Port=..."
//   MongoDB       │ ConnectionStrings__mongodb     = "mongodb://...@..."
//   Redis         │ ConnectionStrings__redis        = "hostname:6379"
//   RabbitMQ      │ ConnectionStrings__rabbitmq     = "amqp://user:pass@host"
//   Elasticsearch │ ConnectionStrings__elasticsearch = "http://host:9200"
//   Proje         │ services__catalog-api__http__0  = "http://host:port"
//
// → Servis bu değerleri IConfiguration ile okur (otomatik).
// → Hardcoded connection string gerekmez!
//
// WaitFor() Ne Yapar?
// → Servis, hedef kaynak "healthy" olana kadar bekler.
// → Race condition önlenir: DB hazır olmadan servis başlamaz.
// → docker-compose'daki "depends_on: condition: service_healthy" karşılığı.
//
// Service Discovery Nasıl Çalışır?
// → WithReference(catalogApi) → api-gateway şunu alır:
//   services__catalog-api__http__0 = "http://hostname:PORT"
// → Microsoft.Extensions.ServiceDiscovery bu env var'ı DNS gibi çözer.
// → appsettings.json'da "http://catalog-api" yazılabilir.
//
// Aspire Dashboard Nasıl Çalışır?
// → AppHost başlatıldığında Aspire Dashboard otomatik açılır.
// → Dashboard URL: http://localhost:18888
// → Her servisin log, trace, metric verileri burada toplanır.
// → Servis URL'leri, port'lar, env var'lar listesi görüntülenir.
// =============================================================================

var builder = DistributedApplication.CreateBuilder(args);

// =============================================================================
// 1. ALTYAPI KAYNAKLARI (Infrastructure Resources)
// =============================================================================
// EĞİTİCİ NOT:
// Altyapı kaynakları, .NET projeleri değil Docker container'larıdır.
// Aspire bu container'ları Docker Engine üzerinde otomatik olarak:
// → Başlatır (docker run)
// → Sağlık kontrolü yapar (healthcheck)
// → Gerekirse yeniden başlatır
// → Dashboard'da izler
// =============================================================================

// ─────────────────────────────────────────────────────────────────────────────
// PostgreSQL — İlişkisel Veritabanı (Write DB)
// ─────────────────────────────────────────────────────────────────────────────
// EĞİTİCİ NOT:
// AddPostgres("postgres"):
// → "postgres" → Bu kaynağın Aspire içindeki referans adı.
//   Başka kaynaklar WithReference() ile bu ismi kullanır.
//
// WithDataVolume("lms-postgres-data"):
// → Veri, konteyner yeniden başlasa bile Docker volume'da kalıcıdır.
//   Volume adı belirtilmezse Aspire rastgele bir isim atar.
//
// WithPgAdmin():
// → pgAdmin konteynerini de başlatır.
//   Aspire Dashboard'da pgAdmin URL'i görünür.
//   Görsel DB yönetimi için kullanılır (Development only).
//
// AddDatabase("catalog-db"):
// → PostgreSQL instance'ı üzerinde "catalog-db" adlı bir veritabanı kaynağı.
//   Bu, Aspire'ın connection string'i doğru veritabanına yönlendirmesini sağlar.
//   Servis bu isimle WithReference() üzerinden bağlanır.
// ─────────────────────────────────────────────────────────────────────────────
var postgres = builder.AddPostgres("postgres")
    .WithDataVolume("lms-postgres-data")
    .WithPgAdmin();

// Database per Service Pattern: Her mikroservisin kendi veritabanı
var catalogDb   = postgres.AddDatabase("catalog-db");
var identityDb  = postgres.AddDatabase("identity-db");
var borrowingDb = postgres.AddDatabase("borrowing-db");

// ─────────────────────────────────────────────────────────────────────────────
// MongoDB — NoSQL Veritabanı (Read DB / CQRS)
// ─────────────────────────────────────────────────────────────────────────────
// EĞİTİCİ NOT:
// CQRS (Command Query Responsibility Segregation) ile iki ayrı DB:
// → Yazma (Write/Command): PostgreSQL — ACID, transaction, tutarlılık
// → Okuma (Read/Query):    MongoDB    — hızlı okuma, denormalize veri
//
// Bu "Polyglot Persistence" prensibidir:
// → Her iş gereksinimi için en uygun veritabanı seçilir.
//   Tek veritabanı her ihtiyacı karşılamak zorunda değildir.
//
// WithMongoExpress():
// → MongoExpress container'ı başlatır.
//   Görsel MongoDB yönetimi (Development only).
// ─────────────────────────────────────────────────────────────────────────────
var mongodb = builder.AddMongoDB("mongodb")
    .WithDataVolume("lms-mongodb-data")
    .WithMongoExpress();

// ─────────────────────────────────────────────────────────────────────────────
// Redis — Dağıtık Önbellek (Distributed Cache)
// ─────────────────────────────────────────────────────────────────────────────
// EĞİTİCİ NOT:
// Redis kullanım senaryoları:
// 1. Distributed Cache (IDistributedCache): Sorgu sonuçlarını cache'ler.
//    MediatR CachingBehavior → Sorgu sonucu Redis'te cache'lenir.
//    Aynı sorgu tekrar gelirse DB'ye gitmeden cache'den döner.
//
// 2. IConnectionMultiplexer: Doğrudan Redis komutları için.
//    RedisCacheService bu interface'i kullanır.
//
// WithRedisInsight():
// → RedisInsight container'ı başlatır.
//   Görsel Redis yönetimi: key-value görüntüleme, memory analizi.
// ─────────────────────────────────────────────────────────────────────────────
var redis = builder.AddRedis("redis")
    .WithDataVolume("lms-redis-data")
    .WithRedisInsight();

// ─────────────────────────────────────────────────────────────────────────────
// RabbitMQ — Mesaj Kuyruğu (Event Bus)
// ─────────────────────────────────────────────────────────────────────────────
// EĞİTİCİ NOT:
// RabbitMQ, Event-Driven Architecture'ın merkezidir.
//
// Event akış örneği:
//   Catalog.API → [BookCreatedEvent] → RabbitMQ Exchange
//       ↓                                    ↓
//   [catalog.books queue]        [notification.book-created queue]
//       ↓                                    ↓
//   (Catalog kendi event'ini       Notification.API → E-posta gönder
//    handle etmez bu örnekte)
//
// WithManagementPlugin():
// → rabbitmq:management Docker image kullanır.
//   Management UI: http://localhost:15672
//   → Exchange, queue ve binding'leri görsel izleme
//   → Mesaj akışını gerçek zamanlı takip etme
//   → Dead Letter Queue (DLQ) yönetimi
//
// Aspire inject edilen connection string formatı:
//   amqp://guest:guest@hostname:5672
//   MassTransit bu formatı cfg.Host(new Uri(...)) ile kullanabilir.
// ─────────────────────────────────────────────────────────────────────────────
var rabbitmq = builder.AddRabbitMQ("rabbitmq")
    .WithDataVolume("lms-rabbitmq-data")
    .WithManagementPlugin();

// ─────────────────────────────────────────────────────────────────────────────
// Elasticsearch — Arama Motoru + Centralized Logging
// ─────────────────────────────────────────────────────────────────────────────
// EĞİTİCİ NOT:
// Elasticsearch çift amaçlı kullanılır:
// 1. Full-Text Search: /api/v1/books/search?q=...
//    → Kitap adı, yazar, açıklama gibi alanlarda hızlı arama
//    → PostgreSQL LIKE operatöründen çok daha hızlı
//
// 2. Centralized Logging: Serilog'un Elasticsearch sink'i
//    → Her servisin log'ları buraya yazılır
//    → Kibana ile görselleştirme ve arama yapılır
//    → Production'da ELK Stack: Elasticsearch + Logstash + Kibana
//
// Aspire.Hosting.Elasticsearch → Single-node development cluster başlatır.
// Production'da: Elastic Cloud veya çok node'lu cluster kullanılır.
// ─────────────────────────────────────────────────────────────────────────────
var elasticsearch = builder.AddElasticsearch("elasticsearch")
    .WithDataVolume("lms-elasticsearch-data");

// ─────────────────────────────────────────────────────────────────────────────
// Kibana — Elasticsearch Görselleştirme (AddContainer ile özel konteyner)
// ─────────────────────────────────────────────────────────────────────────────
// EĞİTİCİ NOT:
// Kibana için resmi bir Aspire hosting paketi yoktur.
// AddContainer() ile ham Docker image kullanılır.
// Bu yaklaşım Aspire'ın desteklemediği herhangi bir konteyner için geçerlidir.
//
// WithEnvironment("ELASTICSEARCH_HOSTS", "http://elasticsearch:9200"):
// → Kibana, Elasticsearch'in Docker iç ağ adresiyle bağlanır.
//   "elasticsearch" → Aspire'ın oluşturduğu container'ın hostname'i.
//
// WithHttpEndpoint(targetPort: 5601):
// → Aspire Dashboard'da Kibana URL'ini gösterir ve otomatik port forwardlar.
//   http://localhost:<random_host_port> → http://kibana_container:5601
//
// WaitFor(elasticsearch):
// → Kibana, Elasticsearch sağlıklı olana kadar başlamaz.
//   Elasticsearch olmadan Kibana açılmaz, bu race condition'ı önler.
// ─────────────────────────────────────────────────────────────────────────────
builder.AddContainer("kibana", "kibana", "8.12.0")
    .WithEnvironment("ELASTICSEARCH_HOSTS", "http://elasticsearch:9200")
    .WithHttpEndpoint(targetPort: 5601, name: "http")
    .WaitFor(elasticsearch);

// =============================================================================
// 2. MİKROSERVİSLER (Service Resources)
// =============================================================================
// EĞİTİCİ NOT:
// AddProject<T>() — .NET projesini Aspire kaynağı olarak ekler.
//
// T (örn. Projects.Catalog_API):
// → AppHost'un csproj'una ProjectReference eklince,
//   Aspire SDK bu tip adını otomatik üretir.
//   Tip adı: namespace nokta yerine _ kullanır.
//   Catalog.API → Projects.Catalog_API
//
// Resource ismi ("catalog-api"):
// → Aspire Dashboard'da görünen isim
// → Service Discovery'de kullanılan DNS ismi
//   "http://catalog-api" URL'ini çözmek için kullanılır
// → WithReference() ile bağlandığında env var adının bir parçası olur
// =============================================================================

// ─────────────────────────────────────────────────────────────────────────────
// Catalog API (Clean Architecture — Domain → Application → Infrastructure → API)
// ─────────────────────────────────────────────────────────────────────────────
// EĞİTİCİ NOT:
// WithReference() zinciri, Catalog API'nin bağımlılıklarını tanımlar.
//
// Her WithReference() çağrısı için inject edilen env var'lar:
//   catalogDb      → ConnectionStrings__catalog-db    = "Host=...;Database=catalog-db"
//   mongodb        → ConnectionStrings__mongodb        = "mongodb://...@hostname:27017"
//   redis          → ConnectionStrings__redis          = "hostname:6379"
//   rabbitmq       → ConnectionStrings__rabbitmq       = "amqp://user:pass@hostname"
//   elasticsearch  → ConnectionStrings__elasticsearch  = "http://hostname:9200"
//
// WaitFor() sırası önemlidir:
// → DB'ler hazır olmadan servis başlayıp "connection refused" hatası almasın.
// ─────────────────────────────────────────────────────────────────────────────
var catalogApi = builder.AddProject<CatalogApiProject>("catalog-api")
    .WithReference(catalogDb).WaitFor(catalogDb)
    .WithReference(mongodb).WaitFor(mongodb)
    .WithReference(redis).WaitFor(redis)
    .WithReference(rabbitmq).WaitFor(rabbitmq)
    .WithReference(elasticsearch).WaitFor(elasticsearch);

// ─────────────────────────────────────────────────────────────────────────────
// Identity API — JWT Authentication & Authorization
// ─────────────────────────────────────────────────────────────────────────────
var identityApi = builder.AddProject<IdentityApiProject>("identity-api")
    .WithReference(identityDb).WaitFor(identityDb)
    .WithReference(rabbitmq).WaitFor(rabbitmq);

// ─────────────────────────────────────────────────────────────────────────────
// Borrowing API — Ödünç Alma / İade Yönetimi
// ─────────────────────────────────────────────────────────────────────────────
// EĞİTİCİ NOT:
// WithReference(catalogApi) → Service Discovery için!
// → Borrowing, stok kontrolü için Catalog'a HTTP istek atar.
// → inject edilen env var: services__catalog-api__http__0 = "http://host:port"
// → Borrowing.API'de "http://catalog-api" URL'i bu env var ile çözülür.
//   Hardcoded port gerekmez!
// ─────────────────────────────────────────────────────────────────────────────
var borrowingApi = builder.AddProject<BorrowingApiProject>("borrowing-api")
    .WithReference(borrowingDb).WaitFor(borrowingDb)
    .WithReference(rabbitmq).WaitFor(rabbitmq)
    .WithReference(catalogApi);  // Service Discovery (port bilgisi inject)

// ─────────────────────────────────────────────────────────────────────────────
// Notification API — Consumer-Only Event Handler
// ─────────────────────────────────────────────────────────────────────────────
// EĞİTİCİ NOT:
// Notification servisi sadece RabbitMQ'dan event consume eder.
// HTTP endpoint'i çok azdır (sadece /health, /alive).
// Bu servis "fire and forget" mesajlarla çalışır:
// → Catalog/Identity/Borrowing event publish eder
// → Notification bu event'leri alıp e-posta/bildirim gönderir
// → Ana iş akışı Notification'ı beklemez (asenkron)
// ─────────────────────────────────────────────────────────────────────────────
var notificationApi = builder.AddProject<NotificationApiProject>("notification-api")
    .WithReference(rabbitmq).WaitFor(rabbitmq);

// ─────────────────────────────────────────────────────────────────────────────
// API Gateway — YARP Reverse Proxy
// ─────────────────────────────────────────────────────────────────────────────
// EĞİTİCİ NOT:
// API Gateway, tüm client isteklerini karşılayan tek giriş noktasıdır.
// YARP + Service Discovery entegrasyonu:
//
// appsettings.json'da:
//   "Address": "http://catalog-api"
//
// Service Discovery bu ismi çözer:
//   services__catalog-api__http__0 env var → "http://hostname:port"
//   YARP bu adresi kullanarak isteği yönlendirir.
//
// Tüm servis referansları YARP'ın yönlendirme tablosunu güncel tutar.
// Bir servisin adresi değişirse (restart, port değişimi) YARP otomatik güncellenir.
// ─────────────────────────────────────────────────────────────────────────────
builder.AddProject<ApiGatewayProject>("api-gateway")
    .WithReference(catalogApi)
    .WithReference(identityApi)
    .WithReference(borrowingApi)
    .WithReference(notificationApi);

builder.Build().Run();
