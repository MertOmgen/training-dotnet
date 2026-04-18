# .NET Aspire Integration Design
**Date:** 2026-04-18  
**Status:** Approved  
**Project:** Training-dotnet (Library Management System — LMS)

---

## Bağlam (Context)

LMS projesi bir mikroservis mimarisidir:
- **API Gateway** (YARP Reverse Proxy) — root proje
- **Catalog.API** (Clean Architecture, 4 katman)
- **Identity.API** — JWT tabanlı kimlik doğrulama
- **Borrowing.API** — Ödünç alma / iade
- **Notification.API** — Event consumer, bildirim servisi

Altyapı bağımlılıkları:
- PostgreSQL (catalog, identity, borrowing DB'leri)
- MongoDB (Catalog Read DB — CQRS)
- Redis (Distributed Cache)
- RabbitMQ (MassTransit Event Bus)
- Elasticsearch + Kibana (Arama + Log)

Mevcut orkestrasyon: `docker-compose.yml`

---

## Hedefler

1. .NET Aspire ile tüm servisleri ve altyapıyı tek noktadan orkestre etmek
2. `docker-compose.yml` bağımlılığını kaldırmak
3. Aspire Dashboard üzerinden unified observability sağlamak
4. Service Discovery ile hardcoded URL'leri ortadan kaldırmak
5. Projeye Türkçe eğitici açıklamalar ekleyerek Aspire kavramlarını öğretmek

---

## Kararlar (ADRs)

| # | Karar | Gerekçe |
|---|-------|---------|
| 1 | Aspire **tam ortam** olarak kullanılacak | docker-compose tamamen kaldırılır (yedeklenir), Aspire hem servisleri hem altyapıyı yönetir |
| 2 | **Service Discovery** aktif edilecek | YARP ve servisler arası iletişimde hardcoded URL yerine isim kullanılır |
| 3 | Tüm projeler **net8.0** hedefler | Mevcut framework korunur; Aspire 8.x net8'i tam destekler |
| 4 | **Typed resource** entegrasyonu | `AddPostgres()`, `AddRedis()` vs. — tip güvenli connection string yönetimi |
| 5 | docker-compose `docker-compose.prod.yml` olarak yeniden adlandırılır | Üretim referansı korunur |

---

## Yeni Projeler

### 1. `src/AppHost/AppHost.csproj`
- SDK: `Microsoft.NET.Sdk` + `<IsAspireHost>true</IsAspireHost>`
- Framework: `net8.0`
- Rol: Tüm projeleri ve altyapıyı orkestre eder
- Aspire Dashboard bu projeyi çalıştırınca otomatik açılır

**Paketler:**
```xml
<PackageReference Include="Aspire.Hosting.AppHost" Version="8.*" />
<PackageReference Include="Aspire.Hosting.PostgreSQL" Version="8.*" />
<PackageReference Include="Aspire.Hosting.MongoDB" Version="8.*" />
<PackageReference Include="Aspire.Hosting.Redis" Version="8.*" />
<PackageReference Include="Aspire.Hosting.RabbitMQ" Version="8.*" />
<PackageReference Include="Aspire.Hosting.Elasticsearch" Version="8.*" />
```

**Project References:** Tüm servis .csproj dosyaları

### 2. `src/ServiceDefaults/ServiceDefaults.csproj`
- SDK: `Microsoft.NET.Sdk`
- Framework: `net8.0`
- Rol: Tüm servislerin ortak konfigürasyonu (OpenTelemetry, health checks, resilience, service discovery)

**Paketler:**
```xml
<PackageReference Include="Microsoft.Extensions.Http.Resilience" Version="8.*" />
<PackageReference Include="Microsoft.Extensions.ServiceDiscovery" Version="8.*" />
<PackageReference Include="OpenTelemetry.Exporter.OpenTelemetryProtocol" Version="1.*" />
<PackageReference Include="OpenTelemetry.Extensions.Hosting" Version="1.*" />
<PackageReference Include="OpenTelemetry.Instrumentation.AspNetCore" Version="1.*" />
<PackageReference Include="OpenTelemetry.Instrumentation.Http" Version="1.*" />
<PackageReference Include="OpenTelemetry.Instrumentation.Runtime" Version="1.*" />
```

---

## Kaynak Grafı (Resource Graph)

```
AppHost
├── postgres (PostgreSQL container — sürüm: 16-alpine)
│   ├── catalog-db         ──→  WithReference → Catalog.API
│   ├── identity-db        ──→  WithReference → Identity.API
│   └── borrowing-db       ──→  WithReference → Borrowing.API
│
├── mongodb                ──→  WithReference → Catalog.API (Read DB)
├── redis                  ──→  WithReference → Catalog.API (Cache)
│
├── rabbitmq (Management Plugin aktif)
│   ──→ WithReference → Catalog.API, Identity.API, Borrowing.API, Notification.API
│
├── elasticsearch          ──→  WithReference → Catalog.API
├── kibana (AddContainer — elasticsearch'e bağımlı)
│
├── catalog-api            ──→  WithReference → api-gateway, borrowing-api
├── identity-api           ──→  WithReference → api-gateway
├── borrowing-api          ──→  WithReference → api-gateway
├── notification-api
└── api-gateway (root proje)
```

---

## Mevcut Servislere Değişiklikler

### Her servis Program.cs
```csharp
// EKLENECEK (AddServiceDefaults çağrısı):
builder.AddServiceDefaults();
// ...
app.MapDefaultEndpoints(); // health + alive endpoint'leri
```

### Her servis .csproj
```xml
<!-- EKLENECEK: -->
<ProjectReference Include="..\..\ServiceDefaults\ServiceDefaults.csproj" />
```

**Aspire component paketleri:**
| Servis | Eklenecek Paket |
|--------|----------------|
| Catalog.API | `Aspire.Npgsql.EntityFrameworkCore.PostgreSQL`, `Aspire.StackExchange.Redis`, `Aspire.MongoDB.Driver` |
| Identity.API | `Aspire.Npgsql.EntityFrameworkCore.PostgreSQL` |
| Borrowing.API | `Aspire.Npgsql.EntityFrameworkCore.PostgreSQL` |
| Notification.API | (ServiceDefaults yeterli) |

### appsettings.json — Kaldırılacak Connection String'ler
Aspire bu değerleri environment variable olarak enjekte eder:
- `ConnectionStrings__catalog-db` → PostgreSQL catalog
- `ConnectionStrings__identity-db` → PostgreSQL identity
- `ConnectionStrings__borrowing-db` → PostgreSQL borrowing
- `ConnectionStrings__mongodb` → MongoDB
- `ConnectionStrings__redis` → Redis
- `ConnectionStrings__rabbitmq` → RabbitMQ

### MassTransit Konfigürasyonu (RabbitMQ)
```csharp
// ESKİ (hardcoded):
cfg.Host("localhost", "/", h => { h.Username("lms_user"); h.Password("..."); });

// YENİ (Aspire managed connection string):
var rabbitUri = builder.Configuration.GetConnectionString("rabbitmq")!;
cfg.Host(new Uri(rabbitUri));
```

### YARP appsettings.json — Service Discovery
```json
// ESKİ:
"Destinations": { "default": { "Address": "http://localhost:5001" } }

// YENİ (Service Discovery):
"Destinations": { "default": { "Address": "http://catalog-api" } }
```

---

## Eğitici Açıklamalar (Her Dosyada Eklenecek)

Her yeni ve değiştirilen dosyaya aşağıdaki konularda Türkçe açıklamalar eklenecek:

- **AppHost/Program.cs**: Aspire nedir, Resource Graph nedir, `WithReference` ne yapar
- **ServiceDefaults/Extensions.cs**: OpenTelemetry stack, health check türleri, resilience patterns
- **Her servisin Program.cs**: `AddServiceDefaults()` ne sağlar, `MapDefaultEndpoints()` neden gerekli
- **YARP appsettings.json**: Service Discovery nasıl çalışır, DNS-based resolution

---

## Solution Değişiklikleri (.slnx)

```xml
<Folder Name="/Aspire/">
  <Project Path="src/AppHost/AppHost.csproj" />
  <Project Path="src/ServiceDefaults/ServiceDefaults.csproj" />
</Folder>
```

---

## Riskler ve Azaltma

| Risk | Azaltma |
|------|---------|
| Mevcut OpenTelemetry konfigürasyonu ServiceDefaults ile çakışabilir | Her servisteki manuel OTel kaydı kaldırılır, ServiceDefaults üstlenir |
| RabbitMQ connection string formatı MassTransit ile uyumsuz olabilir | Aspire `amqp://` formatında verir; MassTransit bunu kabul eder — test edilecek |
| Elasticsearch için resmi Aspire paketi sınırlı olabilir | `Aspire.Hosting.Elasticsearch` yetersizse `AddContainer()` fallback'i kullanılır |
| docker-compose kaldırılması CI/CD pipeline'ını bozabilir | `docker-compose.yml` → `docker-compose.prod.yml` olarak yeniden adlandırılır, silinmez |
