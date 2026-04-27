# 📚 Kütüphane Yönetim Sistemi (LMS) - Microservices Architecture

Bu proje, **modern mikroservis mimarisi** prensipleriyle geliştirilmiş, ölçeklenebilir bir Kütüphane Yönetim Sistemi'dir. **.NET 8/9** ekosistemi üzerine inşa edilen bu eğitim projesi, endüstri standartlarında kullanılan teknolojileri ve tasarım kalıplarını (CQRS, DDD, Event-Driven Architecture) içermektedir. Projeye **.NET Aspire** entegre edilerek geliştirici deneyimi (Developer Experience), gözlemlenebilirlik (Observability) ve orkestrasyon yetenekleri önemli ölçüde iyileştirilmiştir.

---

## 🎯 Projenin Amacı ve Özeti

Bu projenin temel amacı, dağıtık sistemler dünyasında sıkça karşılaşılan problemleri ele almak ve en iyi uygulama yöntemlerini (best practices) sergilemek:
- **Yüksek Performans:** Okuma ve yazma işlemlerini ayırarak (CQRS) veritabanı yükünü dengelemek.
- **Ölçeklenebilirlik:** Her servisin bağımsız olarak ölçeklenebilmesini sağlamak.
- **Dayanıklılık (Resiliency):** Servisler arası asenkron iletişim (RabbitMQ) ile sistemin hataya dayanıklı olmasını sağlamak.
- **İzlenebilirlik:** Dağıtık loglama ve tracing (Elasticsearch, Kibana, OpenTelemetry, Aspire Dashboard) ile sistem sağlığını takip etmek.
- **Geliştirici Deneyimi:** .NET Aspire ile tüm altyapıyı tek komutla (`dotnet run`) ayağa kaldırmak, otomatik bağlantı dizesi enjeksiyonu ve servis keşfini sağlamak.

---

## 🏗️ Mimari ve Tasarım Desenleri

Proje, **Clean Architecture** prensiplerine sadık kalınarak tasarlanmıştır.

### Öne Çıkan Mimari Desenler:
1.  **Microservices Architecture:** Uygulama, sorumluluklarına göre (Bounded Contexts) küçük, bağımsız servislere ayrılmıştır.
2.  **CQRS (Command Query Responsibility Segregation):** 
    -   **Write Side (Commands):** Veri tutarlılığı (consistency) ön plandadır. PostgreSQL kullanılır.
    -   **Read Side (Queries):** Performans ön plandadır. MongoDB veya Elasticsearch kullanılır.
3.  **Domain-Driven Design (DDD):** İş kuralları, zengin domain modelleri (Aggregate Roots, Entities, Value Objects) içinde kapsüllenmiştir.
4.  **Event-Driven Architecture:** Servisler birbirleriyle doğrudan (senkron) haberleşmek yerine, **RabbitMQ** üzerinden olaylar (events) fırlatarak (asenkron) haberleşir. 
    -   *Örnek:* Bir kitap ödünç alındığında (`BookBorrowedEvent`), bildirim servisi bunu dinler ve kullanıcıya e-posta gönderir.
5.  **Outbox Pattern:** Event'lerin kaybolmaması ve veritabanı işlemlerinin transactional bütünlüğü için kullanılmıştır.

---

## 🛠️ Teknoloji Yığını (Tech Stack)

Projede kullanılan temel teknolojiler ve kullanım alanları:

| Teknoloji | Kullanım Alanı | Açıklama |
| :--- | :--- | :--- |
| **.NET 8/9** | Backend Framework | Servisler net8.0, AppHost net9.0 hedefler. |
| **.NET Aspire 9** | Orkestrasyon & Observability | AppHost + ServiceDefaults; geliştirme ortamı için docker-compose'un C# tabanlı, tip-güvenli alternatifi. |
| **PostgreSQL** | Veritabanı (Write) | İlişkisel veriler, kullanıcı bilgileri ve kitap kayıtları için ana veri kaynağı. pgAdmin, Aspire tarafından otomatik başlatılır. |
| **MongoDB** | Veritabanı (Read) | CQRS Read modelleri için optimize edilmiş NoSQL veritabanı. MongoExpress, Aspire tarafından otomatik başlatılır. |
| **Redis** | Distributed Cache | Sık erişilen verilerin (örn. katalog listeleri) önbelleğe alınması için. RedisInsight, Aspire tarafından otomatik başlatılır. |
| **RabbitMQ** | Message Broker | Servisler arası asenkron iletişim ve olay yönetimi. Management UI dahil. |
| **Elasticsearch** | Logging & Search | Merkezi loglama (Serilog) ve gelişmiş metin arama yetenekleri. |
| **Kibana** | Monitoring | Elasticsearch üzerindeki logları görselleştirmek için dashboard. |
| **Docker & Docker Compose** | Containerization | Production ortamı için (`docker-compose.prod.yml`). Geliştirmede Aspire tercih edilir. |
| **OpenTelemetry** | Tracing & Metrics | Dağıtık sistemde bir isteğin (request) izini sürmek; Aspire Dashboard'a OTLP üzerinden veri gönderir. |
| **MediatR** | In-Process Messaging | Servis içi Command/Query ayrımını yönetmek için. |

---

## 🔁 CI Entegrasyonu (GitHub Actions)

Projede artık GitHub Actions tabanlı bir **CI pipeline** bulunmaktadır. Pipeline dosyası `.github/workflows/ci.yml` altında yer alır ve kalite kapısı olarak her değişiklikte otomatik çalışır.

### Pipeline Özeti

- **Tetikleyiciler:** `master` branch'ine yapılan `push` ve `pull_request` olayları
- **Çalışma ortamı:** `ubuntu-latest`
- **.NET sürümü:** `8.x`
- **Temel adımlar:**
  - `dotnet restore Training-dotnet.slnx`
  - `dotnet build Training-dotnet.slnx --no-restore --configuration Release`
  - `dotnet test Training-dotnet.slnx --no-build --configuration Release`

### CI ile Doğrulanan Özellikler

- Solution içindeki servis ve ortak kütüphanelerin derlenebilir olması
- `tests/` altındaki xUnit tabanlı unit ve smoke testlerin çalıştırılması
- Test sonuçlarının **TRX artifact** olarak yüklenmesi
- Code coverage çıktılarının **Cobertura artifact** olarak yüklenmesi

### Üretilen Artifact'ler

- **`test-results`** → `*.trx`
- **`coverage-report`** → `coverage.cobertura.xml`

Bu yapı sayesinde `master` branch'ine merge edilmeden önce derleme ve test hataları erken aşamada yakalanır; aynı zamanda test çıktıları ile coverage raporları GitHub Actions üzerinden indirilebilir.

---

## 🧩 Servisler ve Modüller

Proje aşağıdaki ana servislerden oluşmaktadır:

### 1. Catalog Service
Kitapların yönetildiği servistir.
- Kitap ekleme, güncelleme, silme (Admin).
- Kitap arama, listeleme ve detay görüntüleme.
- **Teknolojiler:** PostgreSQL, MongoDB, Elasticsearch.

### 2. Borrowing Service (Ödünç Alma)
Ödünç alma süreçlerini yönetir.
- Bir kullanıcının kitap ödünç alması.
- İade işlemleri.
- Ceza/gecikme hesaplamaları (eğer varsa).
- **İletişim:** `BookBorrowed` eventi fırlatır.

### 3. Identity Service
Kimlik doğrulama ve yetkilendirme servisidir.
- Kullanıcı kaydı ve girişi.
- JWT (JSON Web Token) üretimi ve doğrulaması.
- Rol yönetimi (Admin, User).

### 4. Notification Service
Kullanıcıları bilgilendiren servistir.
- Arka planda çalışır (Background Worker).
- RabbitMQ kuyruklarını dinler (örn. `BookBorrowedEvent`, `UserCreatedEvent`).
- E-posta veya SMS simülasyonu yapar.

### 🏗️ Building Blocks (Core)
Tüm servislerin ortak kullandığı kütüphaneler:
- **SharedKernel:** Ortak Entity, ValueObject, Exception sınıfları.
- **EventBus:** RabbitMQ bağlantı ve mesajlaşma soyutlamaları.
- **Caching:** Redis bağlantı ve cache yönetimi soyutlamaları.

---

## ☁️ .NET Aspire Entegrasyonu

Proje, geliştirme ortamını yönetmek ve gözlemlenebilirliği artırmak için **.NET Aspire 9** entegrasyonunu içermektedir. Aspire, iki ayrı proje ile projeye dahil edilmiştir:

### 📦 AppHost (`src/AppHost`)

`AppHost`, tüm mikroservis altyapısının merkezi tanım noktasıdır. Normalde `docker-compose.yml` ile YAML formatında yapılan tanımların C# kodu ile yapılmasını sağlar.

**AppHost'un Yönettiği Kaynaklar:**

| Kaynak | Aspire Yönetimi | Yönetim Arayüzü |
| :--- | :--- | :--- |
| PostgreSQL | `AddPostgres()` + `AddDatabase()` | pgAdmin (otomatik başlatılır) |
| MongoDB | `AddMongoDB()` | MongoExpress (otomatik başlatılır) |
| Redis | `AddRedis()` | RedisInsight (otomatik başlatılır) |
| RabbitMQ | `AddRabbitMQ()` + `WithManagementPlugin()` | Management UI (otomatik) |
| Elasticsearch | `AddElasticsearch()` | — |
| Kibana | `AddContainer()` | `http://localhost:<dinamik-port>` |
| Catalog API | `AddProject<CatalogApiProject>()` | Aspire Dashboard |
| Identity API | `AddProject<IdentityApiProject>()` | Aspire Dashboard |
| Borrowing API | `AddProject<BorrowingApiProject>()` | Aspire Dashboard |
| Notification API | `AddProject<NotificationApiProject>()` | Aspire Dashboard |
| API Gateway | `AddProject<ApiGatewayProject>()` | Aspire Dashboard |

**Temel Aspire Özellikleri:**

- **Otomatik Bağlantı Dizesi Enjeksiyonu:** `WithReference()` ile bağlanan her kaynak, ilgili servise connection string'i environment variable olarak otomatik enjekte eder. Hardcoded bağlantı bilgisi gerekmez.
  - *Örnek:* `ConnectionStrings__catalog-db = "Host=...;Database=catalog-db;..."`
- **WaitFor() Bağımlılık Sıralaması:** Bir servis, bağımlı olduğu veritabanı "healthy" olana kadar başlamaz. Race condition önlenir.
- **Service Discovery:** Servisler birbirini IP/port ile değil, Aspire kaynak adıyla bulur (`http://catalog-api`). Aspire, gerçek adresi environment variable olarak enjekte eder.
- **Tip Güvenliği:** Proje referansları `IProjectMetadata` arayüzünü uygulayan sınıflarla (`CatalogApiProject`, `IdentityApiProject` vb.) tip-güvenli biçimde tanımlanır. Yanlış proje yolu derleme zamanında hata verir.

### 📦 ServiceDefaults (`src/ServiceDefaults`)

`ServiceDefaults`, tüm mikroservislerin paylaştığı ortak Aspire konfigürasyonunu içerir. Her servis `builder.AddServiceDefaults()` metodunu çağırarak aşağıdaki özellikleri otomatik olarak devralır:

| Özellik | Yöntem | Açıklama |
| :--- | :--- | :--- |
| **OpenTelemetry** | `ConfigureOpenTelemetry()` | Trace, Metric ve Log verilerini Aspire Dashboard'a OTLP üzerinden gönderir |
| **Health Checks** | `AddDefaultHealthChecks()` | `/health` (readiness) ve `/alive` (liveness) endpoint'lerini hazırlar |
| **Service Discovery** | `AddServiceDiscovery()` | `http://catalog-api` gibi isimleri gerçek adrese çözer |
| **HTTP Resilience** | `AddStandardResilienceHandler()` | Her `HttpClient`'a otomatik Retry + Circuit Breaker (Polly 8.x) ekler |

**OpenTelemetry Pillars:**
- **Traces:** Servisler arası istek akışını waterfall diyagramı olarak Aspire Dashboard'da gösterir.
- **Metrics:** RPS, P99 latency, error rate, .NET runtime metrikleri (GC, heap, thread pool).
- **Logs:** Yapısal log'lar OTLP üzerinden Aspire Dashboard'a aktarılır; trace ile ilişkilendirilir.

**HTTP Resilience Pipeline (Polly v8):**
```
┌───────────────────────────────────────────────────────┐
│ 1. Total Request Timeout  → 30 saniye (retry dahil)   │
│ 2. Retry                  → 3 deneme, exponential     │
│ 3. Circuit Breaker        → %50 hata oranında açılır  │
│ 4. Attempt Timeout        → Her deneme için 10 saniye │
└───────────────────────────────────────────────────────┘
```

### 🖥️ Aspire Dashboard

AppHost çalıştırıldığında **Aspire Dashboard** otomatik olarak açılır:

- **URL:** `http://localhost:18888`
- **Özellikler:**
  - Tüm servislerin gerçek zamanlı log/trace/metric verilerini tek ekranda gösterir.
  - Servis sağlık durumunu (`/health`, `/alive`) izler.
  - Distributed trace waterfall diyagramı sunar.
  - Servis URL'lerini, portlarını ve environment variable'larını listeler.
  - Her altyapı bileşeninin (pgAdmin, MongoExpress, RedisInsight vb.) URL'ini gösterir.

> **Not:** Aspire Dashboard yalnızca geliştirme ortamı içindir. Production'da Docker Compose (`docker-compose.prod.yml`) kullanılır.

---

## 🚀 Kurulum ve Çalıştırma

Projeyi yerel ortamınızda çalıştırmak için iki yöntem mevcuttur: **Aspire (Geliştirme)** ve **Docker Compose (Production)**.

---

### 🅰️ Yöntem 1: .NET Aspire ile Çalıştırma (Önerilen — Geliştirme)

Aspire, tüm veritabanlarını, mesaj kuyruklarını ve servisleri **tek bir komutla** başlatır. Docker Compose'a gerek kalmaz.

#### Gereksinimler
- **Docker Desktop** (Yüklü ve çalışır durumda olmalı)
- **.NET 9 SDK** (AppHost net9.0 hedefler)

#### Adım 1: Projeyi Klonlayın
```bash
git clone https://github.com/MertOmgen/training-dotnet.git
cd training-dotnet
```

#### Adım 2: AppHost'u Başlatın
```bash
dotnet run --project src/AppHost/AppHost.csproj
```

Bu komut tek seferde şunları başlatır:
- Tüm altyapı konteynerleri (PostgreSQL, MongoDB, Redis, RabbitMQ, Elasticsearch, Kibana)
- Tüm mikroservisler (Catalog, Identity, Borrowing, Notification API'leri)
- API Gateway (YARP)
- Aspire Dashboard

#### Adım 3: Aspire Dashboard üzerinden Erişim

Aspire Dashboard otomatik olarak açılır. Tüm URL'ler buradan dinamik olarak görülebilir:

- **Aspire Dashboard:** `http://localhost:18888`
- **pgAdmin (PostgreSQL Yönetim):** Dashboard'da `postgres-pgadmin` bağlantısından ulaşın
- **MongoExpress (MongoDB Yönetim):** Dashboard'da `mongodb-mongoexpress` bağlantısından ulaşın
- **RedisInsight (Redis Yönetim):** Dashboard'da `redis-redisinsight` bağlantısından ulaşın
- **RabbitMQ Management UI:** Dashboard'da `rabbitmq` bağlantısından ulaşın (Kullanıcı adı/Şifre: `guest`/`guest`)
- **Kibana (Logs):** Dashboard'da `kibana` bağlantısından ulaşın

> **Not:** Aspire dinamik port ataması yaptığından, servis URL'leri her çalıştırmada farklı olabilir. Güncel URL'ler için her zaman Aspire Dashboard'a bakın.

---

### 🐳 Yöntem 2: Docker Compose ile Çalıştırma (Production)

Production ortamı veya Aspire kullanmadan altyapı ayağa kaldırmak için:

#### Gereksinimler
- **Docker Desktop** (Yüklü ve çalışır durumda olmalı)
- **.NET 8 SDK** (Geliştirme yapacaksanız)

#### Adım 1: Projeyi Klonlayın
```bash
git clone https://github.com/MertOmgen/training-dotnet.git
cd training-dotnet
```

#### Adım 2: Altyapıyı Ayağa Kaldırın
```bash
docker-compose up -d
```
*Not: İlk çalıştırmada imajların indirilmesi biraz zaman alabilir.*

#### Adım 3: Servisleri Başlatın
```bash
dotnet run --project src/Services/Catalog/Catalog.API/Catalog.API.csproj
dotnet run --project src/Services/Identity/Identity.API/Identity.API.csproj
# Diğer servisler için de benzer şekilde...
```

#### Adım 4: Erişim
- **Catalog API (Swagger):** `http://localhost:5000/swagger`
- **RabbitMQ Management:** `http://localhost:15672` (Kullanıcı adı/Şifre: `guest`/`guest`)
- **Kibana (Logs):** `http://localhost:5601`

---

## ✅ Test ve CI ile Uyumlu Yerel Doğrulama

CI pipeline ile aynı doğrulamayı yerel ortamda çalıştırmak için:

```bash
dotnet restore Training-dotnet.slnx
dotnet build Training-dotnet.slnx --no-restore --configuration Release
dotnet test Training-dotnet.slnx --no-build --configuration Release
```

Bu komutlar, GitHub Actions workflow'unda çalışan build/test akışıyla birebir uyumludur.
