# .NET Aspire Integration Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** .NET Aspire ile tüm mikroservisleri ve altyapı kaynaklarını (PostgreSQL, MongoDB, Redis, RabbitMQ, Elasticsearch) orkestre etmek; mevcut docker-compose bağımlılığını ortadan kaldırmak; ServiceDefaults ile ortaklaşılan OpenTelemetry/health-check konfigürasyonunu tek yerden yönetmek ve Aspire Dashboard üzerinden birleşik gözlemlenebilirlik sağlamak.

**Architecture:** AppHost (orkestratör) projeleri + altyapı kaynaklarını tanımlar; ServiceDefaults projesi tüm servisler için ortak telemetri, health check ve resiliency konfigürasyonunu barındırır; her servis `AddServiceDefaults()` ile bu konfigürasyonu devralır; Aspire `WithReference()` mekanizması ile connection string'ler environment variable olarak enjekte edilir ve Service Discovery aktif edilir.

**Tech Stack:** .NET Aspire 8.x, `Aspire.Hosting.AppHost`, `Aspire.Hosting.PostgreSQL`, `Aspire.Hosting.MongoDB`, `Aspire.Hosting.Redis`, `Aspire.Hosting.RabbitMQ`, `Aspire.Hosting.Elasticsearch`, `Microsoft.Extensions.ServiceDiscovery`, `Microsoft.Extensions.Http.Resilience`, `Aspire.Npgsql.EntityFrameworkCore.PostgreSQL`, `Aspire.MongoDB.Driver`, `Aspire.StackExchange.Redis`

---

## Dosya Değişiklik Haritası

### Oluşturulacak Dosyalar
| Dosya | Sorumluluk |
|---|---|
| `src/AppHost/AppHost.csproj` | Aspire AppHost proje tanımı |
| `src/AppHost/Program.cs` | Tüm servis + altyapı kaynaklarının tanımlandığı orkestratör |
| `src/ServiceDefaults/ServiceDefaults.csproj` | Paylaşılan observability kütüphanesi |
| `src/ServiceDefaults/Extensions.cs` | `AddServiceDefaults()` extension method'u |

### Değiştirilecek Dosyalar
| Dosya | Ne Değişecek |
|---|---|
| `Training-dotnet.slnx` | Aspire/ klasörüne AppHost + ServiceDefaults eklenir |
| `docker-compose.yml` → `docker-compose.prod.yml` | Yeniden adlandırılır (silinmez) |
| `src/BuildingBlocks/EventBus/EventBus.RabbitMQ/RabbitMqServiceExtensions.cs` | Aspire connection string URI desteği eklenir |
| `src/Services/Catalog/Catalog.API/Catalog.API.csproj` | ServiceDefaults ref + Aspire component paketleri |
| `src/Services/Catalog/Catalog.API/Program.cs` | `AddServiceDefaults()`, Aspire-managed DB/Redis/Mongo |
| `src/Services/Catalog/Catalog.API/appsettings.json` | Hardcoded connection string'ler kaldırılır |
| `src/Services/Identity/Identity.API/Identity.API.csproj` | ServiceDefaults ref + Aspire.Npgsql |
| `src/Services/Identity/Identity.API/Program.cs` | `AddServiceDefaults()`, Aspire-managed DB |
| `src/Services/Identity/Identity.API/appsettings.json` | Hardcoded connection string'ler kaldırılır |
| `src/Services/Borrowing/Borrowing.API/Borrowing.API.csproj` | ServiceDefaults ref + Aspire.Npgsql |
| `src/Services/Borrowing/Borrowing.API/Program.cs` | `AddServiceDefaults()`, Aspire-managed DB + Service Discovery HttpClient |
| `src/Services/Borrowing/Borrowing.API/appsettings.json` | Hardcoded connection string'ler kaldırılır |
| `src/Services/Notification/Notification.API/Notification.API.csproj` | ServiceDefaults ref |
| `src/Services/Notification/Notification.API/Program.cs` | `AddServiceDefaults()`, Aspire RabbitMQ connection string |
| `Training-dotnet.csproj` (API Gateway) | ServiceDefaults ref |
| `Program.cs` (API Gateway) | `AddServiceDefaults()`, `MapDefaultEndpoints()` |
| `appsettings.json` (API Gateway) | YARP cluster URL'leri → Service Discovery isimleri |

---

## Task 1: ServiceDefaults Projesi

**Files:**
- Create: `src/ServiceDefaults/ServiceDefaults.csproj`
- Create: `src/ServiceDefaults/Extensions.cs`

- [ ] **Adım 1.1: ServiceDefaults.csproj oluştur**

```xml
<!--
  ServiceDefaults — Paylaşılan Observability Konfigürasyonu
  
  📚 EĞİTİCİ NOT (Tech-Tutor):
  
  .NET Aspire'da ServiceDefaults nedir?
  → "Convention over Configuration" prensibi: Her servis için
    aynı OpenTelemetry, health check ve resilience konfigürasyonunu
    tekrar tekrar yazmak yerine, bu ortak yapı tek bir projede tanımlanır
    ve her servis bu projeye referans vererek tüm konfigürasyonu devralır.
  
  Bu Proje Neleri İçerir?
  1. OpenTelemetry → Distributed Tracing + Metrics + Logging
  2. Health Checks → /health ve /alive endpoint'leri
  3. Service Discovery → Aspire yönetimli DNS-tabanlı servis bulma
  4. HttpClient Resilience → Retry + Circuit Breaker (varsayılan politikalar)
  
  Aspire Dashboard Neden Bu Prjoeye İhtiyaç Duyar?
  → Dashboard, OpenTelemetry verileri olmadan servisleri izleyemez.
    AddServiceDefaults() çağrısı:
    - OTLP (OpenTelemetry Protocol) exporter'ı aktif eder
    - Aspire Dashboard'un dinlediği endpoint'e telemetri gönderir
    - Tüm HTTP isteklerini/DB sorgularını otomatik trace eder
-->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsAspireSharedProject>true</IsAspireSharedProject>
  </PropertyGroup>

  <ItemGroup>
    <!-- Service Discovery: Servisler birbirini DNS ismiyle bulur -->
    <PackageReference Include="Microsoft.Extensions.ServiceDiscovery" Version="8.2.*" />
    
    <!-- HttpClient Resilience: Retry + Circuit Breaker varsayılan politikaları -->
    <PackageReference Include="Microsoft.Extensions.Http.Resilience" Version="8.10.*" />
    
    <!-- OpenTelemetry: OTLP exporter (Aspire Dashboard'a veri gönderir) -->
    <PackageReference Include="OpenTelemetry.Exporter.OpenTelemetryProtocol" Version="1.9.*" />
    <PackageReference Include="OpenTelemetry.Extensions.Hosting" Version="1.9.*" />
    <PackageReference Include="OpenTelemetry.Instrumentation.AspNetCore" Version="1.9.*" />
    <PackageReference Include="OpenTelemetry.Instrumentation.Http" Version="1.9.*" />
    <PackageReference Include="OpenTelemetry.Instrumentation.Runtime" Version="1.9.*" />
  </ItemGroup>
</Project>
```

Dosya yolu: `src/ServiceDefaults/ServiceDefaults.csproj`

- [ ] **Adım 1.2: Extensions.cs oluştur**

```csharp
// =============================================================================
// ServiceDefaults — Paylaşılan Aspire Konfigürasyonu
// =============================================================================
// 📚 EĞİTİCİ NOT (Tech-Tutor):
//
// Bu dosya tüm LMS mikroservislerinin ortak konfigürasyonunu içerir.
// Her servisin Program.cs'inde sadece şu satır çağrılır:
//   builder.AddServiceDefaults();
//
// NEDEN AddServiceDefaults()?
// → DRY (Don't Repeat Yourself): Her servis için aynı OpenTelemetry,
//   health check ve resilience konfigürasyonunu tekrar yazmak yerine
//   tek bir merkezi extension method kullanılır.
//
// AddServiceDefaults() Ne Yapar?
// ┌─────────────────────────────────────────────────────────────┐
// │ 1. ConfigureOpenTelemetry()                                 │
// │    → Aspire Dashboard için trace/metric/log gönderimi       │
// │ 2. AddDefaultHealthChecks()                                 │
// │    → /health (readiness) ve /alive (liveness) endpoint'leri │
// │ 3. AddServiceDiscovery()                                    │
// │    → HTTP istemcilerinde isim tabanlı çözümleme             │
// │ 4. ConfigureHttpClientDefaults()                            │
// │    → Retry + Circuit Breaker otomatik uygulanır             │
// └─────────────────────────────────────────────────────────────┘
//
// MapDefaultEndpoints() Ne Yapar?
// → /health → Kubernetes readiness probe için (servis hazır mı?)
// → /alive  → Kubernetes liveness probe için (servis canlı mı?)
// =============================================================================

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Microsoft.Extensions.Hosting;

// IHostApplicationBuilder: WebApplication.CreateBuilder() ve benzeri
// tüm host builder'ların ortak interface'i. Bu sayede hem web hem de
// worker service projeleri bu extension'ı kullanabilir.
public static class Extensions
{
    public static IHostApplicationBuilder AddServiceDefaults(
        this IHostApplicationBuilder builder)
    {
        // ─────────────────────────────────────────────────────────────────
        // 1. OpenTelemetry Konfigürasyonu
        // ─────────────────────────────────────────────────────────────────
        builder.ConfigureOpenTelemetry();

        // ─────────────────────────────────────────────────────────────────
        // 2. Health Check Kayıtları
        // ─────────────────────────────────────────────────────────────────
        builder.AddDefaultHealthChecks();

        // ─────────────────────────────────────────────────────────────────
        // 3. Service Discovery
        // ─────────────────────────────────────────────────────────────────
        // EĞİTİCİ NOT:
        // Service Discovery, servisler arasındaki HTTP iletişimini
        // DNS isimleri üzerinden çözer. Örneğin:
        //   new HttpClient { BaseAddress = new Uri("http://catalog-api") }
        // Aspire, bu ismi gerçek porta otomatik çevirir.
        // Production'da Kubernetes DNS veya Consul kullanılabilir.
        builder.Services.AddServiceDiscovery();

        // ─────────────────────────────────────────────────────────────────
        // 4. HttpClient Resilience Varsayılanları
        // ─────────────────────────────────────────────────────────────────
        // EĞİTİCİ NOT:
        // ConfigureHttpClientDefaults: Tüm HttpClient instance'larına
        // otomatik olarak:
        //   - AddServiceDiscovery(): URL çözümleme
        //   - AddStandardResilienceHandler(): Retry + Circuit Breaker
        // uygular. Her HttpClient için ayrı ayrı Polly eklemek gerekmez.
        //
        // Standard Resilience Handler ne içerir?
        // → Total Request Timeout: 30 saniye
        // → Retry: 3 deneme, exponential backoff
        // → Circuit Breaker: %50 hata oranında devre keser
        // → Attempt Timeout: Her deneme için 10 saniye
        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            http.AddServiceDiscovery();
            http.AddStandardResilienceHandler();
        });

        return builder;
    }

    // ─────────────────────────────────────────────────────────────────────
    // OpenTelemetry Konfigürasyonu
    // ─────────────────────────────────────────────────────────────────────
    // EĞİTİCİ NOT:
    //
    // OpenTelemetry (OTel) Üç Sütunu:
    // ┌──────────┬───────────────────────────────────────────────────────┐
    // │ Traces   │ İstek akışı izleme (servis A → B → C → DB)          │
    // │ Metrics  │ Sayısal ölçümler (RPS, latency histogramı, heap size) │
    // │ Logs     │ Yapısal log kayıtları (JSON formatında)               │
    // └──────────┴───────────────────────────────────────────────────────┘
    //
    // OTLP Exporter:
    // → OpenTelemetry Protocol — veriler gRPC/HTTP üzerinden gönderilir
    // → ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL env var otomatik okunur
    // → Aspire Dashboard bu adresi dinleyip verileri görselleştirir
    //
    // Instrumentation Library'ler:
    // → AspNetCore: HTTP istekleri için otomatik span oluşturur
    // → HttpClient: Dışa giden HTTP isteklerini izler
    // → Runtime: GC, thread pool, bellek metriklerini toplar
    // ─────────────────────────────────────────────────────────────────────
    public static IHostApplicationBuilder ConfigureOpenTelemetry(
        this IHostApplicationBuilder builder)
    {
        // Loglara trace/span ID'si ekle (log-trace korelasyonu)
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics =>
            {
                metrics
                    .AddAspNetCoreInstrumentation()  // HTTP endpoint metrikler
                    .AddHttpClientInstrumentation()  // Dışa giden HTTP metrikler
                    .AddRuntimeInstrumentation();    // .NET runtime metrikler
            })
            .WithTracing(tracing =>
            {
                tracing
                    .AddAspNetCoreInstrumentation() // HTTP istek span'ları
                    .AddHttpClientInstrumentation() // HttpClient span'ları
                    // EF Core instrumentation isteğe bağlı:
                    // .AddEntityFrameworkCoreInstrumentation()
                    ;
            });

        // OTLP Exporter: Aspire Dashboard'a veri gönder
        // ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL env var → dashboard adresi
        builder.AddOpenTelemetryExporters();

        return builder;
    }

    private static IHostApplicationBuilder AddOpenTelemetryExporters(
        this IHostApplicationBuilder builder)
    {
        // OTEL_EXPORTER_OTLP_ENDPOINT env var varsa OTLP exporter aktif et
        // Aspire AppHost bu env var'ı otomatik set eder
        var useOtlpExporter = !string.IsNullOrWhiteSpace(
            builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

        if (useOtlpExporter)
        {
            builder.Services.AddOpenTelemetry().UseOtlpExporter();
        }

        return builder;
    }

    // ─────────────────────────────────────────────────────────────────────
    // Health Check Varsayılanları
    // ─────────────────────────────────────────────────────────────────────
    // EĞİTİCİ NOT:
    //
    // İki tür health check vardır:
    // ┌────────────┬─────────────────────────────────────────────────────┐
    // │ /alive     │ Liveness: Uygulama canlı mı? Cevap vermiyorsa       │
    // │            │ Kubernetes pod'u yeniden başlatır.                   │
    // │ /health    │ Readiness: Uygulama istek almaya hazır mı?          │
    // │            │ DB bağlantısı yok → hazır değil → trafik yönlenmez │
    // └────────────┴─────────────────────────────────────────────────────┘
    //
    // Aspire Dashboard bu endpoint'leri düzenli olarak sorgular ve
    // servislerin sağlık durumunu gerçek zamanlı gösterir.
    // ─────────────────────────────────────────────────────────────────────
    public static IHostApplicationBuilder AddDefaultHealthChecks(
        this IHostApplicationBuilder builder)
    {
        builder.Services.AddHealthChecks()
            // Basit "servis canlı" kontrolü — her zaman Healthy döner
            .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);

        return builder;
    }

    // ─────────────────────────────────────────────────────────────────────
    // MapDefaultEndpoints — /health ve /alive route'larını ekler
    // ─────────────────────────────────────────────────────────────────────
    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        // EĞİTİCİ NOT:
        // Health check endpoint'leri yalnızca Development ortamında açık olur.
        // Production'da bu endpoint'lere yalnızca iç ağdan (cluster-internal)
        // erişilmeli, public internet'e açılmamalıdır.
        
        if (app.Environment.IsDevelopment())
        {
            // /health → Readiness probe (DB bağlantısı dahil tüm check'ler)
            app.MapHealthChecks("/health");

            // /alive → Liveness probe (sadece "self" tag'li check'ler)
            app.MapHealthChecks("/alive", new HealthCheckOptions
            {
                // Yalnızca "live" etiketli health check'leri çalıştır
                // DB health check'i buraya dahil etmiyoruz:
                // DB geçici down olduğunda pod yeniden başlatılmamalı!
                Predicate = r => r.Tags.Contains("live")
            });
        }

        return app;
    }
}
```

Dosya yolu: `src/ServiceDefaults/Extensions.cs`

- [ ] **Adım 1.3: Build kontrolü**
```powershell
cd "d:\_Merdo-Developing\_Merdo-Training\.NET8\Training-dotnet"
dotnet build src/ServiceDefaults/ServiceDefaults.csproj
```
Beklenen çıktı: `Build succeeded.`

---

## Task 2: AppHost Projesi

**Files:**
- Create: `src/AppHost/AppHost.csproj`
- Create: `src/AppHost/Program.cs`

- [ ] **Adım 2.1: AppHost.csproj oluştur**

```xml
<!--
  AppHost — .NET Aspire Orkestratörü
  
  📚 EĞİTİCİ NOT (Tech-Tutor):
  
  AppHost nedir?
  → .NET Aspire mimarisinin "kaptanı" — tek bir "dotnet run" komutuyla
    tüm servisleri, veritabanlarını ve altyapı bileşenlerini başlatır.
    docker-compose'a benzer ama kod ile tanımlanır, tip-güvenlidir.
  
  IsAspireHost = true ne sağlar?
  → SDK bu projeyi özel olarak işler:
    1. Referans verilen projeleri "proje kaynağı" olarak tanır
    2. Aspire Dashboard başlatma altyapısını enjekte eder
    3. OTEL endpoint'lerini otomatik konfigüre eder
    4. dotnet run → Dashboard + tüm servisler başlar
  
  Neden Ayrı Proje?
  → AppHost production deploy edilmez.
    Sadece geliştirme ortamında kullanılır.
    "dotnet aspire publish" ile Kubernetes manifesti üretilebilir.
  
  Aspire Dashboard (http://localhost:18888):
  → Tüm servislerin log/trace/metric verilerini tek ekranda gösterir
  → Servis sağlığını (health) gerçek zamanlı izler
  → Distributed trace görselleştirme (waterfall diagramı)
  → Environment variable / connection string'leri listeler
-->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <!-- Bu projeyi Aspire AppHost olarak işaretle -->
    <IsAspireHost>true</IsAspireHost>
    <UserSecretsId>aspire-apphost-lms</UserSecretsId>
  </PropertyGroup>

  <ItemGroup>
    <!--
      Aspire Hosting Paketleri:
      Her paketin amacı:
      - AppHost: Temel orkestrasyon altyapısı
      - PostgreSQL: AddPostgres() → pgAdmin ile birlikte konteyner yönetimi
      - MongoDB: AddMongoDB() → MongoExpress ile birlikte
      - Redis: AddRedis() → RedisInsight ile birlikte
      - RabbitMQ: AddRabbitMQ() → Management UI dahil
      - Elasticsearch: AddElasticsearch() → tek node development cluster
    -->
    <PackageReference Include="Aspire.Hosting.AppHost" Version="8.2.*" />
    <PackageReference Include="Aspire.Hosting.PostgreSQL" Version="8.2.*" />
    <PackageReference Include="Aspire.Hosting.MongoDB" Version="8.2.*" />
    <PackageReference Include="Aspire.Hosting.Redis" Version="8.2.*" />
    <PackageReference Include="Aspire.Hosting.RabbitMQ" Version="8.2.*" />
    <PackageReference Include="Aspire.Hosting.Elasticsearch" Version="8.2.*" />
  </ItemGroup>

  <ItemGroup>
    <!--
      Proje Referansları:
      AppHost, tüm servis projelerini "kaynak" olarak tanımlar.
      Bu sayede:
      - Projelerin derlenip başlatılmasını sağlar
      - Proje adını (Projects.Catalog_API) tip-güvenli kullanır
      - Environment variable'ları doğru projeye enjekte eder
    -->
    <ProjectReference Include="..\Services\Catalog\Catalog.API\Catalog.API.csproj" />
    <ProjectReference Include="..\Services\Identity\Identity.API\Identity.API.csproj" />
    <ProjectReference Include="..\Services\Borrowing\Borrowing.API\Borrowing.API.csproj" />
    <ProjectReference Include="..\Services\Notification\Notification.API\Notification.API.csproj" />
    <!-- API Gateway (root proje) -->
    <ProjectReference Include="..\..\Training-dotnet.csproj" />
  </ItemGroup>
</Project>
```

Dosya yolu: `src/AppHost/AppHost.csproj`

- [ ] **Adım 2.2: AppHost/Program.cs oluştur**

```csharp
// =============================================================================
// AppHost — .NET Aspire Orkestratörü
// =============================================================================
// 📚 EĞİTİCİ NOT (Tech-Tutor):
//
// Bu dosya, tüm LMS mikroservis altyapısının merkezi tanım noktasıdır.
// Normalde docker-compose.yml ile yapılan şeyi burada C# kodu ile yapıyoruz.
//
// NEDEN Kod ile Tanımlama (docker-compose yerine)?
// → Tip güvenliği: Yanlış resource ismi → derleme hatası (runtime'da değil)
// → Refactoring: Resource adı değişince tüm referanslar otomatik güncellenir
// → Koşullu logic: if/switch ile ortama göre farklı konfigürasyon mümkün
// → C# IDE desteği: IntelliSense, navigation, breakpoint
//
// Aspire Resource Modeli:
// ┌──────────────────────────────────────────────────────────────────────┐
// │ IResourceBuilder<T>: Her kaynak (DB, servis, konteyner) bu tip ile  │
// │ temsil edilir. WithReference(), WaitFor(), WithEnvironment() gibi   │
// │ fluent API metotları ile zenginleştirilir.                           │
// └──────────────────────────────────────────────────────────────────────┘
//
// WithReference(resource) Ne Yapar?
// → Hedef servise, kaynak hakkında ortam değişkenleri enjekte eder:
//   PostgreSQL için: ConnectionStrings__catalog-db = "Host=...;Database=..."
//   RabbitMQ için:   ConnectionStrings__rabbitmq   = "amqp://user:pass@host"
//   Redis için:      ConnectionStrings__redis       = "hostname:6379"
// → Servis bu değerleri IConfiguration ile okur.
// → Hardcoded connection string gerekmez, Aspire yönetir!
//
// Service Discovery Nasıl Çalışır?
// → WithReference(catalogApi) eklenen gateway şunu alır:
//   services__catalog-api__http__0 = "http://hostname:port"
// → Microsoft.Extensions.ServiceDiscovery bu env var'ı okuyarak
//   "http://catalog-api" URL'ini gerçek adrese çevirir.
// =============================================================================

var builder = DistributedApplication.CreateBuilder(args);

// =============================================================================
// 1. ALTYAPI KAYNAKLARI (Infrastructure Resources)
// =============================================================================

// ─────────────────────────────────────────────────────────────────────────────
// PostgreSQL
// ─────────────────────────────────────────────────────────────────────────────
// EĞİTİCİ NOT:
// AddPostgres() → PostgreSQL konteynerini başlatır.
// WithDataVolume() → Veri, konteyner yeniden başlasa bile kalıcıdır.
// WithPgAdmin() → http://localhost:XXXX/pgadmin → görsel DB yönetimi
//
// AddDatabase() → PostgreSQL instance'ına bağlı bir veritabanı kaynağı.
// Her veritabanı ayrı bir "bağlantı kaynağı" olarak temsil edilir.
// Servisler bu veritabanına WithReference() ile bağlanır.
// ─────────────────────────────────────────────────────────────────────────────
var postgres = builder.AddPostgres("postgres")
    .WithDataVolume("lms-postgres-data")
    .WithPgAdmin();  // Development için görsel yönetim UI

// Her servis için ayrı veritabanı (Database per Service Pattern)
var catalogDb   = postgres.AddDatabase("catalog-db");
var identityDb  = postgres.AddDatabase("identity-db");
var borrowingDb = postgres.AddDatabase("borrowing-db");

// ─────────────────────────────────────────────────────────────────────────────
// MongoDB (Catalog Read DB — CQRS)
// ─────────────────────────────────────────────────────────────────────────────
// EĞİTİCİ NOT:
// CQRS (Command Query Responsibility Segregation) paterninde
// okuma (query) ve yazma (command) tarafları ayrılır.
// Catalog servisi:
// → Yazma (Command): PostgreSQL — ACID uyumlu, transaction desteği
// → Okuma (Query):   MongoDB    — hızlı okuma, denormalize veri
//
// WithMongoExpress() → http://localhost:XXXX → MongoDB görsel yönetimi
// ─────────────────────────────────────────────────────────────────────────────
var mongodb = builder.AddMongoDB("mongodb")
    .WithDataVolume("lms-mongodb-data")
    .WithMongoExpress();  // Development için görsel yönetim UI

// ─────────────────────────────────────────────────────────────────────────────
// Redis (Distributed Cache)
// ─────────────────────────────────────────────────────────────────────────────
// EĞİTİCİ NOT:
// Redis, CachingBehavior (MediatR Pipeline) tarafından kullanılır.
// Sorgu sonuçları belirli süre cache'de tutulur.
// Aynı sorgu tekrar geldiğinde DB'ye gitme gerek kalmaz.
//
// WithRedisInsight() → http://localhost:XXXX → Redis görsel yönetim UI
// ─────────────────────────────────────────────────────────────────────────────
var redis = builder.AddRedis("redis")
    .WithDataVolume("lms-redis-data")
    .WithRedisInsight();  // Development için görsel yönetim UI

// ─────────────────────────────────────────────────────────────────────────────
// RabbitMQ (Event Bus — MassTransit)
// ─────────────────────────────────────────────────────────────────────────────
// EĞİTİCİ NOT:
// RabbitMQ, servisler arası asenkron event iletişimini sağlar.
// Örnek akış:
//   Catalog.API  →[BookCreatedEvent]→  RabbitMQ  →  Notification.API
//                                                  →  Borrowing.API
//
// WithManagementPlugin() → http://localhost:15672 → RabbitMQ Management UI
// Aspire bu port'u otomatik forward eder ve Dashboard'da gösterir.
//
// WithDataVolume() → Exchange/Queue tanımları konteyner yeniden başlayınca kaybolmaz.
// ─────────────────────────────────────────────────────────────────────────────
var rabbitmq = builder.AddRabbitMQ("rabbitmq")
    .WithDataVolume("lms-rabbitmq-data")
    .WithManagementPlugin();  // http://localhost:15672 → Management UI

// ─────────────────────────────────────────────────────────────────────────────
// Elasticsearch
// ─────────────────────────────────────────────────────────────────────────────
// EĞİTİCİ NOT:
// Elasticsearch iki amaçla kullanılır:
// 1. Full-Text Search: Kitap/yazar ismi aramaları için
// 2. Centralized Logging: Serilog'un Elasticsearch sink'i log'ları buraya yazar
//    Kibana bu log'ları görselleştirir.
//
// Aspire.Hosting.Elasticsearch paketi, tek node development cluster başlatır.
// ─────────────────────────────────────────────────────────────────────────────
var elasticsearch = builder.AddElasticsearch("elasticsearch")
    .WithDataVolume("lms-elasticsearch-data");

// ─────────────────────────────────────────────────────────────────────────────
// Kibana (Elasticsearch Görselleştirme — AddContainer ile özel konteyner)
// ─────────────────────────────────────────────────────────────────────────────
// EĞİTİCİ NOT:
// Kibana için resmi bir Aspire hosting paketi yoktur.
// AddContainer() ile ham Docker imajı kullanılır.
// Bu yaklaşım, Aspire'ın desteklemediği herhangi bir konteyner için geçerlidir.
//
// WithReference(elasticsearch) → Aspire, elasticsearch'in URL'ini
//   ELASTICSEARCH_URL env var olarak Kibana konteynerine enjekte eder.
// ─────────────────────────────────────────────────────────────────────────────
var kibana = builder.AddContainer("kibana", "kibana", "8.12.0")
    .WithEnvironment("ELASTICSEARCH_HOSTS", "http://elasticsearch:9200")
    .WithHttpEndpoint(targetPort: 5601, name: "http")
    .WaitFor(elasticsearch);

// =============================================================================
// 2. MİKROSERVİSLER (Service Resources)
// =============================================================================

// ─────────────────────────────────────────────────────────────────────────────
// Catalog API (Clean Architecture — 4 Katman)
// ─────────────────────────────────────────────────────────────────────────────
// EĞİTİCİ NOT:
// WithReference() çağrıları hangi environment variable'ları enjekte eder?
//
// WithReference(catalogDb)    → ConnectionStrings__catalog-db = "Host=...;Database=catalog-db"
// WithReference(mongodb)      → ConnectionStrings__mongodb     = "mongodb://...@hostname:27017"
// WithReference(redis)        → ConnectionStrings__redis        = "hostname:6379"
// WithReference(rabbitmq)     → ConnectionStrings__rabbitmq     = "amqp://user:pass@hostname"
// WithReference(elasticsearch)→ ConnectionStrings__elasticsearch = "http://hostname:9200"
//
// WaitFor() → Catalog API, bağımlı kaynaklar sağlıklı (healthy) olana kadar bekler.
// Bu, "race condition"ı önler: DB hazır değilken servis başlayıp hata almaz.
// ─────────────────────────────────────────────────────────────────────────────
var catalogApi = builder.AddProject<Projects.Catalog_API>("catalog-api")
    .WithReference(catalogDb).WaitFor(catalogDb)
    .WithReference(mongodb).WaitFor(mongodb)
    .WithReference(redis).WaitFor(redis)
    .WithReference(rabbitmq).WaitFor(rabbitmq)
    .WithReference(elasticsearch).WaitFor(elasticsearch);

// ─────────────────────────────────────────────────────────────────────────────
// Identity API (JWT Authentication)
// ─────────────────────────────────────────────────────────────────────────────
var identityApi = builder.AddProject<Projects.Identity_API>("identity-api")
    .WithReference(identityDb).WaitFor(identityDb)
    .WithReference(rabbitmq).WaitFor(rabbitmq);

// ─────────────────────────────────────────────────────────────────────────────
// Borrowing API (Ödünç Alma / İade)
// ─────────────────────────────────────────────────────────────────────────────
// EĞİTİCİ NOT:
// WithReference(catalogApi) → Service Discovery:
//   services__catalog-api__http__0 = "http://hostname:port"
// Borrowing servisi, Catalog servisine HTTP ile istek atmak için
// bu env var'ı kullanır (hardcoded URL yerine).
// ─────────────────────────────────────────────────────────────────────────────
var borrowingApi = builder.AddProject<Projects.Borrowing_API>("borrowing-api")
    .WithReference(borrowingDb).WaitFor(borrowingDb)
    .WithReference(rabbitmq).WaitFor(rabbitmq)
    .WithReference(catalogApi);  // Service Discovery için

// ─────────────────────────────────────────────────────────────────────────────
// Notification API (Consumer-Only)
// ─────────────────────────────────────────────────────────────────────────────
var notificationApi = builder.AddProject<Projects.Notification_API>("notification-api")
    .WithReference(rabbitmq).WaitFor(rabbitmq);

// ─────────────────────────────────────────────────────────────────────────────
// API Gateway (YARP Reverse Proxy)
// ─────────────────────────────────────────────────────────────────────────────
// EĞİTİCİ NOT:
// WithReference(catalogApi) → YARP Service Discovery:
//   Gateway, "http://catalog-api" URL'ini kullanarak Catalog servisine yönlendirir.
//   Aspire, bu ismi gerçek porta çevirir.
//   appsettings.json'daki "Address": "http://catalog-api" bu sayede çalışır.
// ─────────────────────────────────────────────────────────────────────────────
builder.AddProject<Projects.Training_dotnet>("api-gateway")
    .WithReference(catalogApi)
    .WithReference(identityApi)
    .WithReference(borrowingApi)
    .WithReference(notificationApi);

builder.Build().Run();
```

Dosya yolu: `src/AppHost/Program.cs`

- [ ] **Adım 2.3: AppHost build kontrolü**
```powershell
dotnet build src/AppHost/AppHost.csproj
```
Beklenen: `Build succeeded.`

---

## Task 3: RabbitMQ Connection String Desteği (BuildingBlocks)

**Files:**
- Modify: `src/BuildingBlocks/EventBus/EventBus.RabbitMQ/RabbitMqServiceExtensions.cs`

- [ ] **Adım 3.1: RabbitMqServiceExtensions.cs güncelle**

`cfg.Host(...)` çağrısını şu şekilde değiştir (Aspire `amqp://` URI desteği):

```csharp
// ─────────────────────────────────────────────────────────────────────
// Aspire Connection String desteği
// ─────────────────────────────────────────────────────────────────────
// EĞİTİCİ NOT:
// Aspire, RabbitMQ bağlantı bilgisini şu formatta enjekte eder:
//   ConnectionStrings:rabbitmq = "amqp://user:password@hostname:5672"
//
// Eğer bu connection string mevcutsa URI olarak kullan.
// Mevcutsa değilse, eski RabbitMQ:Host/Username/Password ayarlarına dön.
// Bu yaklaşım hem Aspire hem de standalone (docker-compose) ortamda çalışır.
// ─────────────────────────────────────────────────────────────────────
var rabbitConnectionString = configuration.GetConnectionString("rabbitmq");

if (!string.IsNullOrEmpty(rabbitConnectionString))
{
    // Aspire yönetimli ortam: connection string URI olarak gelir
    cfg.Host(new Uri(rabbitConnectionString));
}
else
{
    // Standalone (docker-compose / local) ortam: eski yöntem
    cfg.Host(
        configuration["RabbitMQ:Host"] ?? "localhost",
        configuration["RabbitMQ:VirtualHost"] ?? "/",
        h =>
        {
            h.Username(configuration["RabbitMQ:Username"] ?? "lms_user");
            h.Password(configuration["RabbitMQ:Password"] ?? "lms_password_2024");
        });
}
```

Eski `cfg.Host(...)` bloğunu yukarıdaki kodla değiştir.

- [ ] **Adım 3.2: EventBus.RabbitMQ build kontrolü**
```powershell
dotnet build src/BuildingBlocks/EventBus/EventBus.RabbitMQ/EventBus.RabbitMQ.csproj
```
Beklenen: `Build succeeded.`

---

## Task 4: Catalog.API — Aspire Entegrasyonu

**Files:**
- Modify: `src/Services/Catalog/Catalog.API/Catalog.API.csproj`
- Modify: `src/Services/Catalog/Catalog.API/Program.cs`
- Modify: `src/Services/Catalog/Catalog.API/appsettings.json`

- [ ] **Adım 4.1: Catalog.API.csproj — ServiceDefaults + Aspire komponent paketleri ekle**

`<ItemGroup>` içine eklenecek:
```xml
<!-- Aspire ServiceDefaults: AddServiceDefaults() extension method'u -->
<ProjectReference Include="..\..\..\ServiceDefaults\ServiceDefaults.csproj" />
```

Mevcut `StackExchange.Redis` ve `Microsoft.Extensions.Caching.StackExchangeRedis` referanslarını şunlarla DEĞIŞTIR:
```xml
<!-- Aspire Redis Integration: Bağlantı string'i Aspire yönetir -->
<PackageReference Include="Aspire.StackExchange.Redis" Version="8.2.*" />
<PackageReference Include="Aspire.StackExchange.Redis.StackExchangeRedisCache" Version="8.2.*" />
```

Mevcut OpenTelemetry paketlerini KALDIR (ServiceDefaults bunları zaten içerir):
- `OpenTelemetry.Exporter.Console`
- `OpenTelemetry.Extensions.Hosting`
- `OpenTelemetry.Instrumentation.AspNetCore`
- `OpenTelemetry.Instrumentation.Http`

Aspire PostgreSQL + MongoDB komponent paketleri EKLE:
```xml
<!-- Aspire EF Core + PostgreSQL: Connection string Aspire yönetir -->
<PackageReference Include="Aspire.Npgsql.EntityFrameworkCore.PostgreSQL" Version="8.2.*" />
<!-- Aspire MongoDB: Connection string Aspire yönetir -->
<PackageReference Include="Aspire.MongoDB.Driver" Version="8.2.*" />
```

- [ ] **Adım 4.2: Catalog.API/Program.cs güncellemeleri**

**a)** Serilog konfigürasyonunda Elasticsearch URL'ini env var'dan oku (builder oluşturulmadan önce Aspire config okunamamaz):

Mevcut kod:
```csharp
.WriteTo.Elasticsearch(new ElasticsearchSinkOptions(new Uri("http://localhost:9200"))
```

Yeni kod:
```csharp
// EĞİTİCİ NOT:
// Serilog konfigürasyonu builder oluşturulmadan önce yapılır.
// Bu noktada Aspire'ın enjekte ettiği IConfiguration henüz hazır değildir.
// Environment.GetEnvironmentVariable() ile Aspire'ın set ettiği
// ConnectionStrings__elasticsearch env var'ını okuyabiliriz.
var elasticsearchUrl = Environment.GetEnvironmentVariable("ConnectionStrings__elasticsearch")
    ?? "http://localhost:9200";

// ...
.WriteTo.Elasticsearch(new ElasticsearchSinkOptions(new Uri(elasticsearchUrl))
```

**b)** `builder.AddServiceDefaults()` çağrısı ekle — `builder.Host.UseSerilog()` satırından SONRA:
```csharp
// =============================================================================
// ASPIRE SERVICE DEFAULTS
// =============================================================================
// EĞİTİCİ NOT:
// AddServiceDefaults() tek satırda şunları sağlar:
// → OpenTelemetry (trace + metric + log) Aspire Dashboard'a gönderilir
// → /health ve /alive endpoint'leri hazırlanır
// → Service Discovery aktif edilir
// → HttpClient'lara otomatik Retry + Circuit Breaker eklenir
// =============================================================================
builder.AddServiceDefaults();
```

**c)** EF Core kaydını Aspire tarzına çevir:

Mevcut:
```csharp
builder.Services.AddDbContext<CatalogDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("WriteDb") ?? "..."));
```
Yeni:
```csharp
// =============================================================================
// EF Core + PostgreSQL (Aspire Managed Connection String)
// =============================================================================
// EĞİTİCİ NOT:
// AddNpgsqlDbContext<T>("catalog-db"):
// → "catalog-db" ismi AppHost'taki postgres.AddDatabase("catalog-db") ile eşleşir
// → Aspire, ConnectionStrings__catalog-db env var'ını otomatik enjekte eder
// → Connection string hardcode etmeye gerek kalmaz
// → Health check otomatik eklenir: DB'ye bağlanılamıyorsa /health Unhealthy döner
// → Retry politikası otomatik uygulanır (transient fault tolerans)
// =============================================================================
builder.AddNpgsqlDbContext<CatalogDbContext>("catalog-db");
```

**d)** MongoDB kaydını Aspire tarzına çevir:

Mevcut:
```csharp
builder.Services.AddSingleton<IMongoClient>(sp => {
    var connectionString = builder.Configuration.GetConnectionString("ReadDb") ?? "...";
    return new MongoClient(connectionString);
});
```
Yeni:
```csharp
// =============================================================================
// MongoDB (Aspire Managed Connection String)
// =============================================================================
// EĞİTİCİ NOT:
// AddMongoDBClient("mongodb"):
// → AppHost'taki builder.AddMongoDB("mongodb") ile eşleşir
// → ConnectionStrings__mongodb env var otomatik enjekte edilir
// → IMongoClient singleton olarak DI'ya kaydedilir
// → Health check otomatik eklenir
// =============================================================================
builder.AddMongoDBClient("mongodb");
// IMongoDatabase hâlâ manuel kaydedilir (database adı uygulama mantığı)
builder.Services.AddScoped<IMongoDatabase>(sp =>
    sp.GetRequiredService<IMongoClient>().GetDatabase("lms_catalog_read"));
```

**e)** Redis kaydını Aspire tarzına çevir:

Mevcut:
```csharp
builder.Services.AddStackExchangeRedisCache(options => { options.Configuration = "..."; });
builder.Services.AddSingleton<IConnectionMultiplexer>(...);
```
Yeni:
```csharp
// =============================================================================
// Redis (Aspire Managed Connection String)
// =============================================================================
// EĞİTİCİ NOT:
// AddRedisDistributedCache("redis"):
// → AppHost'taki builder.AddRedis("redis") ile eşleşir
// → ConnectionStrings__redis env var otomatik enjekte edilir
// → IDistributedCache ve IConnectionMultiplexer DI'ya kaydedilir
// → Health check otomatik eklenir
// =============================================================================
builder.AddRedisDistributedCache("redis");
builder.AddRedisClient("redis");  // IConnectionMultiplexer için
```

**f)** Elasticsearch kaydını Aspire connection string okuyacak şekilde güncelle:

```csharp
// =============================================================================
// Elasticsearch (Aspire Connection String)
// =============================================================================
// EĞİTİCİ NOT:
// Aspire.Hosting.Elasticsearch → ConnectionStrings__elasticsearch env var enjekte eder.
// Resmi Aspire Elastic komponent paketi opsiyonel; manuel kayıt da çalışır.
// =============================================================================
builder.Services.AddSingleton<ElasticsearchClient>(sp =>
{
    var elasticUrl = builder.Configuration.GetConnectionString("elasticsearch")
        ?? "http://localhost:9200";
    var settings = new ElasticsearchClientSettings(new Uri(elasticUrl));
    return new ElasticsearchClient(settings);
});
```

**g)** Uygulama middleware kısmına `MapDefaultEndpoints()` ekle:

```csharp
// Aspire health check endpoint'leri (/health, /alive)
app.MapDefaultEndpoints();
```

**h)** Mevcut OpenTelemetry blokunu kaldır (ServiceDefaults bunu yönetir):

Catalog.API/Program.cs'teki mevcut `builder.Services.AddOpenTelemetry(...)` bloğunu tamamen sil.

- [ ] **Adım 4.3: Catalog.API/appsettings.json güncelle**

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore.Database.Command": "Warning"
    }
  },
  "AllowedHosts": "*"
}
```

Kaldırılanlar:
- `ConnectionStrings` bloğu (Aspire env var olarak enjekte eder)
- `RabbitMQ` bloğu (Aspire connection string olarak enjekte eder)
- `Elasticsearch` bloğu (Aspire connection string olarak enjekte eder)

- [ ] **Adım 4.4: Catalog.API build kontrolü**
```powershell
dotnet build src/Services/Catalog/Catalog.API/Catalog.API.csproj
```
Beklenen: `Build succeeded.`

---

## Task 5: Identity.API — Aspire Entegrasyonu

**Files:**
- Modify: `src/Services/Identity/Identity.API/Identity.API.csproj`
- Modify: `src/Services/Identity/Identity.API/Program.cs`
- Modify: `src/Services/Identity/Identity.API/appsettings.json`

- [ ] **Adım 5.1: Identity.API.csproj güncelle**

Ekle:
```xml
<ProjectReference Include="..\..\..\ServiceDefaults\ServiceDefaults.csproj" />
<PackageReference Include="Aspire.Npgsql.EntityFrameworkCore.PostgreSQL" Version="8.2.*" />
```

- [ ] **Adım 5.2: Identity.API/Program.cs güncellemeleri**

`builder.Host.UseSerilog()` satırından sonra ekle:
```csharp
builder.AddServiceDefaults();
```

EF Core kaydını değiştir:
```csharp
// Eski: builder.Services.AddDbContext<IdentityDbContext>(options => options.UseNpgsql(...))
// Yeni:
// EĞİTİCİ NOT:
// "identity-db" → AppHost'taki postgres.AddDatabase("identity-db") ile eşleşir
builder.AddNpgsqlDbContext<IdentityDbContext>("identity-db");
```

`app.Run()` öncesine ekle:
```csharp
app.MapDefaultEndpoints();
```

- [ ] **Adım 5.3: Identity.API/appsettings.json güncelle**

`ConnectionStrings` ve `RabbitMQ` bloklarını kaldır. JWT ayarları korunur.

- [ ] **Adım 5.4: Identity.API build kontrolü**
```powershell
dotnet build src/Services/Identity/Identity.API/Identity.API.csproj
```

---

## Task 6: Borrowing.API — Aspire Entegrasyonu

**Files:**
- Modify: `src/Services/Borrowing/Borrowing.API/Borrowing.API.csproj`
- Modify: `src/Services/Borrowing/Borrowing.API/Program.cs`
- Modify: `src/Services/Borrowing/Borrowing.API/appsettings.json`

- [ ] **Adım 6.1: Borrowing.API.csproj güncelle**

```xml
<ProjectReference Include="..\..\..\ServiceDefaults\ServiceDefaults.csproj" />
<PackageReference Include="Aspire.Npgsql.EntityFrameworkCore.PostgreSQL" Version="8.2.*" />
```

- [ ] **Adım 6.2: Borrowing.API/Program.cs güncellemeleri**

`builder.Host.UseSerilog()` sonrasına:
```csharp
builder.AddServiceDefaults();
```

EF Core kaydını değiştir:
```csharp
// EĞİTİCİ NOT: "borrowing-db" → AppHost'taki postgres.AddDatabase("borrowing-db")
builder.AddNpgsqlDbContext<BorrowingDbContext>("borrowing-db");
```

HttpClient kaydını Service Discovery kullanacak şekilde güncelle:

```csharp
// =============================================================================
// Catalog Servisi HttpClient (Aspire Service Discovery)
// =============================================================================
// EĞİTİCİ NOT:
// Mevcut kod: client.BaseAddress = new Uri("http://localhost:5001")
//             → Hardcoded port, farklı ortamlarda çalışmaz
//
// Yeni kod: "http://catalog-api"
//           → Aspire Service Discovery bu ismi gerçek adrese çevirir
//           → AddServiceDefaults() ile kaydedilen ServiceDiscovery middleware bunu yakalar
//           → "catalog-api" ismi AppHost'taki AddProject("catalog-api") ile eşleşir
//
// services__catalog-api__http__0 env var → "http://hostname:port"
// HttpClient bu env var'ı otomatik okuyarak bağlantı kurar.
// =============================================================================
builder.Services.AddHttpClient("CatalogService", client =>
{
    client.BaseAddress = new Uri("http://catalog-api");
});
```

Polly bloğunu kaldır (ServiceDefaults `AddStandardResilienceHandler()` bunu zaten yapar).

`app.Run()` öncesine:
```csharp
app.MapDefaultEndpoints();
```

- [ ] **Adım 6.3: Borrowing.API/appsettings.json güncelle**

`ConnectionStrings`, `Services` ve `RabbitMQ` bloklarını kaldır.

- [ ] **Adım 6.4: Borrowing.API build kontrolü**
```powershell
dotnet build src/Services/Borrowing/Borrowing.API/Borrowing.API.csproj
```

---

## Task 7: Notification.API — Aspire Entegrasyonu

**Files:**
- Modify: `src/Services/Notification/Notification.API/Notification.API.csproj`
- Modify: `src/Services/Notification/Notification.API/Program.cs`
- Modify: `src/Services/Notification/Notification.API/appsettings.json`

- [ ] **Adım 7.1: Notification.API.csproj güncelle**

```xml
<ProjectReference Include="..\..\..\ServiceDefaults\ServiceDefaults.csproj" />
```

- [ ] **Adım 7.2: Notification.API/Program.cs güncellemeleri**

`builder.Host.UseSerilog()` sonrasına:
```csharp
builder.AddServiceDefaults();
```

MassTransit `busConfig.Host(...)` satırını kaldır; `RabbitMqServiceExtensions.cs`'deki güncellenmiş kod `ConnectionStrings:rabbitmq` okuyacak şekilde zaten değiştirildi (Task 3).

Notification.API'de MassTransit RabbitMQ konfigürasyonu doğrudan yapılmaktaysa:

```csharp
// EĞİTİCİ NOT:
// Aspire, ConnectionStrings__rabbitmq env var'ını enjekte eder.
// MassTransit bu URI'yi okuyarak bağlantı kurar.
var rabbitMqUri = builder.Configuration.GetConnectionString("rabbitmq");
cfg.UsingRabbitMq((context, busConfig) =>
{
    if (!string.IsNullOrEmpty(rabbitMqUri))
        busConfig.Host(new Uri(rabbitMqUri));
    else
        busConfig.Host(builder.Configuration["RabbitMQ:Host"] ?? "localhost", "/",
            h => { h.Username("lms_user"); h.Password("lms_password_2024"); });
    // ...
});
```

`app.Run()` öncesine:
```csharp
app.MapDefaultEndpoints();
```

- [ ] **Adım 7.3: Notification.API/appsettings.json güncelle**

`RabbitMQ` bloğunu kaldır. SMTP ayarları korunur.

- [ ] **Adım 7.4: Notification.API build kontrolü**
```powershell
dotnet build src/Services/Notification/Notification.API/Notification.API.csproj
```

---

## Task 8: API Gateway — Aspire Entegrasyonu

**Files:**
- Modify: `Training-dotnet.csproj`
- Modify: `Program.cs`
- Modify: `appsettings.json`

- [ ] **Adım 8.1: Training-dotnet.csproj güncelle**

```xml
<ProjectReference Include="src\ServiceDefaults\ServiceDefaults.csproj" />
```

- [ ] **Adım 8.2: Program.cs güncelle**

`builder.Host.UseSerilog()` sonrasına:
```csharp
builder.AddServiceDefaults();
```

Middleware sonuna (tüm `app.Use...` satırlarından sonra):
```csharp
app.MapDefaultEndpoints();
```

- [ ] **Adım 8.3: appsettings.json — YARP Service Discovery**

Her cluster'ın `Address` değerini güncelle:

```json
"Clusters": {
  "catalog-cluster": {
    "Destinations": {
      "default": {
        "Address": "http://catalog-api"
      }
    }
  },
  "identity-cluster": {
    "Destinations": {
      "default": {
        "Address": "http://identity-api"
      }
    }
  },
  "borrowing-cluster": {
    "Destinations": {
      "default": {
        "Address": "http://borrowing-api"
      }
    }
  },
  "notification-cluster": {
    "Destinations": {
      "default": {
        "Address": "http://notification-api"
      }
    }
  }
}
```

Not: YARP, `Microsoft.Extensions.ServiceDiscovery` ile entegre çalışır.  
`builder.Services.AddReverseProxy().LoadFromConfig(...).AddServiceDiscoveryDestinationResolver()` çağrısı gerekir.

Program.cs'te YARP kaydını güncelle:
```csharp
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddServiceDiscoveryDestinationResolver();  // Service Discovery entegrasyonu
```

- [ ] **Adım 8.4: API Gateway build kontrolü**
```powershell
dotnet build Training-dotnet.csproj
```

---

## Task 9: Solution File ve docker-compose Güncellemeleri

**Files:**
- Modify: `Training-dotnet.slnx`
- Rename: `docker-compose.yml` → `docker-compose.prod.yml`

- [ ] **Adım 9.1: Training-dotnet.slnx güncelle**

`<Solution>` içine ekle:
```xml
<!-- ═══════════════════════════════════════════════════════════════════ -->
<!-- .NET Aspire (Orkestrasyon ve ServiceDefaults) -->
<!-- ═══════════════════════════════════════════════════════════════════ -->
<Folder Name="/Aspire/">
  <Project Path="src/AppHost/AppHost.csproj" />
  <Project Path="src/ServiceDefaults/ServiceDefaults.csproj" />
</Folder>
```

- [ ] **Adım 9.2: docker-compose.yml'yi yeniden adlandır**
```powershell
Rename-Item "docker-compose.yml" "docker-compose.prod.yml"
```

- [ ] **Adım 9.3: README.md güncelle — Aspire çalıştırma talimatları ekle**

`README.md` içine "Çalıştırma" bölümüne ekle:

```markdown
## .NET Aspire ile Çalıştırma (Geliştirme Ortamı)

```bash
# Tüm servisleri ve altyapıyı tek komutla başlat:
dotnet run --project src/AppHost/AppHost.csproj

# Aspire Dashboard: http://localhost:18888
# pgAdmin: (Aspire Dashboard'daki linkten)
# RabbitMQ Management: http://localhost:15672
```

## Docker Compose ile Çalıştırma (Üretim Ortamı)

```bash
docker compose -f docker-compose.prod.yml up -d
```
```

---

## Task 10: Son Build ve Entegrasyon Testi

- [ ] **Adım 10.1: Full solution build**
```powershell
dotnet build Training-dotnet.slnx
```
Beklenen: `Build succeeded. 0 Error(s)`

- [ ] **Adım 10.2: AppHost başlatma testi**
```powershell
dotnet run --project src/AppHost/AppHost.csproj
```
Beklenen çıktı:
```
Building...
info: Aspire.Hosting[0] Distributed application starting.
info: Aspire.Hosting[0] Now listening on: http://localhost:18888
```

Ardından Aspire Dashboard'u aç: http://localhost:18888  
Kontrol edilecekler:
- Tüm servisler "Running" durumunda
- Tüm altyapı kaynakları "Running" durumunda
- Bir endpoint'e HTTP isteği at → trace Dashboard'da görünüyor mu?

- [ ] **Adım 10.3: Commit**
```powershell
git add .
git commit -m "feat: integrate .NET Aspire (AppHost + ServiceDefaults + Service Discovery)"
```
