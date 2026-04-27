# CI/CD + Unit Test Entegrasyonu — Tasarım Belgesi

**Tarih:** 2026-04-27  
**Proje:** Training-dotnet (Library Management System)  
**Kapsam:** GitHub Actions CI pipeline + xUnit unit/smoke testleri  

---

## 1. Bağlam

Proje, .NET 8 + Clean Architecture + CQRS + .NET Aspire üzerine kurulu bir kütüphane yönetim sistemidir. GitHub'da barındırılmaktadır (`github.com/MertOmgen/training-dotnet`). Şu anda test altyapısı ve CI pipeline mevcut değildir.

**Mevcut servisler:**
- `Training-dotnet` — API Gateway (YARP)
- `Catalog` — Kitap yönetimi (Domain + Application + Infrastructure + API katmanları)
- `Borrowing.API` — Ödünç işlemleri
- `Identity.API` — Kimlik doğrulama
- `Notification.API` — Bildirim servisi

---

## 2. Kararlar

| Karar | Seçim | Gerekçe |
|-------|-------|---------|
| CI platformu | GitHub Actions | Repo zaten GitHub'da, sıfır altyapı |
| CD | Yok (şimdilik) | Eğitim projesi; deploy hedefi belirsiz |
| Test framework | xUnit + Moq + FluentAssertions | .NET de facto standart |
| Test yaklaşımı | Servis başına tek test projesi (Flat) | Servis başına bir .csproj; proje içi klasörler (Domain/, Application/) izin verilir |
| Test kapsamı | Catalog derin + diğer servisler smoke | Catalog en olgun katmanlı yapıya sahip |

---

## 3. Test Proje Yapısı

```
tests/
  Catalog.Tests/
    Catalog.Tests.csproj
    Domain/
      BookTests.cs
    Application/
      CreateBookCommandHandlerTests.cs
      CreateBookCommandValidatorTests.cs
      GetBookByIdQueryHandlerTests.cs
      ValidationBehaviorTests.cs
    API/
      CatalogEndpointsSmokeTests.cs
  Borrowing.Tests/
    Borrowing.Tests.csproj
    BorrowingApiSmokeTests.cs
  Identity.Tests/
    Identity.Tests.csproj
    IdentityApiSmokeTests.cs
  Notification.Tests/
    Notification.Tests.csproj
    NotificationApiSmokeTests.cs
```

### 3.1 Catalog.Tests — Derin Unit Testler

**Domain (BookTests.cs)**
- `Book.Create()` geçerli parametreler → `Result.Success`, entity property'leri doğru
- `Book.Create()` boş title → `Result.Failure`
- `Book.Create()` negatif `totalCopies` → `Result.Failure`
- `Book.Create()` geçersiz `publishedYear` (< 1000, > şimdiki yıl+1) → `Result.Failure`
- `Book.Create()` başarılı oluşturma → `BookCreatedDomainEvent` raise edilmiş

**Application — CreateBookCommandHandler**
- ISBN zaten mevcut → `_bookRepository.GetByIsbnAsync` çağrıldı, `Result.Failure` döndü
- ISBN benzersiz → `Book.Create()` çağrıldı, `_bookRepository.AddAsync` çağrıldı, `Result.Success`

**Application — CreateBookCommandValidator**
- Boş `Title` → validation error, mesaj doğru
- `Isbn` 10 veya 13 haneli değil → validation error
- Geçerli komut → validation error yok

**Application — GetBookByIdQueryHandler**
- MongoDB'de kitap yok → `null` döner
- MongoDB'de kitap var → `BookDto` döner, `Id` eşleşiyor

**Test kategorileri (`[Trait]`):**
- `[Trait("Category", "Unit")]` — Domain + Application testleri
- `[Trait("Category", "Smoke")]` — WebApplicationFactory testleri

### 3.2 Smoke Testler (Borrowing, Identity, Notification, Catalog API)

Her servis için `WebApplicationFactory<Program>` kullanılır. External bağımlılıklar (DB, Redis, RabbitMQ) mock veya stub'lanır (test ortamında `appsettings.Test.json` veya environment variable override).

Test senaryoları:
- `GET /health` → HTTP 200
- Uygulama startup'ta exception fırlatmıyor

**Aspire olmadan izole başlatma:**  
Her servisin `Program.cs` dosyası Aspire AppHost tarafından orkestrasyona bağımlıdır (connection string env var'ları Aspire inject eder). `WebApplicationFactory` bu env var'ları sağlamaz; bu nedenle her test projesinde `ConfigureTestServices` ile bağımlılıklar override edilir:

- **PostgreSQL / MongoDB / Redis** → `IBookRepository`, `IMongoDatabase` gibi interface'ler `Moq.Mock<T>` ile stub'lanır; gerçek DB bağlantısı denenmez
- **RabbitMQ** → `IEventBus` interface'i stub'lanır
- **Connection string'ler** → `appsettings.Test.json` içinde geçersiz/boş endpoint tanımlanır (bağlantı denenirse test hemen fail olsun diye değil, sadece servis kaydı hata vermesin diye)

```csharp
// Örnek — CustomWebApplicationFactory<Program>
protected override void ConfigureWebHost(IWebHostBuilder builder)
{
    builder.UseEnvironment("Test");
    builder.ConfigureTestServices(services =>
    {
        // Gerçek repository'yi mock ile değiştir
        services.RemoveAll<IBookRepository>();
        services.AddSingleton(Mock.Of<IBookRepository>());
        // vb.
    });
}
```

**`appsettings.Test.json` içeriği** (her API projesi altında):
```json
{
  "ConnectionStrings": {
    "postgres": "Host=localhost;Port=0;",
    "mongodb": "mongodb://localhost:0",
    "redis": "localhost:0"
  }
}
```
Bu dosya yüklenir ama `ConfigureTestServices` override'ları sayesinde gerçek bağlantı kurulmaz.

---

## 4. GitHub Actions CI Pipeline

**Dosya:** `.github/workflows/ci.yml`

**Tetikleyiciler:**
- `push` → `master`
- `pull_request` → `master`

**Job: `build-and-test` (ubuntu-latest)**

```
1. actions/checkout@v4
2. actions/setup-dotnet@v4 (dotnet-version: 8.x)
3. dotnet restore (solution veya tüm csproj)
4. dotnet build --no-restore --configuration Release
5. dotnet test --no-build --configuration Release
       --logger "trx;LogFileName=test-results.trx"
       --collect:"XPlat Code Coverage"
       (env: ASPNETCORE_ENVIRONMENT=Test)
6. actions/upload-artifact@v4 (test-results, coverage)
```

**Ortam değişkenleri (GitHub Actions `env:` bloğu):**
```yaml
env:
  ASPNETCORE_ENVIRONMENT: Test
  DOTNET_NOLOGO: true
  DOTNET_CLI_TELEMETRY_OPTOUT: true
```
`ASPNETCORE_ENVIRONMENT=Test` ayarlandığında `appsettings.Test.json` dosyaları otomatik yüklenir ve `WebApplicationFactory` test ortamını doğru şekilde başlatır.

**Önemli notlar:**
- `Training-dotnet.csproj` (API Gateway) `DefaultItemExcludes` ile `src/**` dışladığından, test projeleri ayrı çözüm dosyası (`Training-dotnet.slnx`) veya doğrudan glob pattern ile dahil edilir.
- Aspire `AppHost` projesi CI'da çalıştırılmaz; altyapı bağımlılığı gerektirir.
- Smoke testler `WebApplicationFactory` ile izole çalışır, external servis bağlantısı gerekmez.

---

## 5. Proje Referans ve Paket Bağımlılıkları

**Catalog.Tests.csproj bağımlılıkları:**
- `xunit`
- `xunit.runner.visualstudio`
- `Moq`
- `FluentAssertions`
- `Microsoft.AspNetCore.Mvc.Testing` (smoke testler için)
- `coverlet.collector` (code coverage)
- ProjectReference: `Catalog.Domain`, `Catalog.Application`, `Catalog.API`

**Diğer servis test projeleri:**
- `xunit`, `xunit.runner.visualstudio`, `FluentAssertions`
- `Microsoft.AspNetCore.Mvc.Testing`
- `coverlet.collector`
- İlgili API projesine ProjectReference

---

## 6. Solution Entegrasyonu

Test projeleri `Training-dotnet.slnx` solution dosyasına eklenecektir; böylece `dotnet test` tek komutla tüm suite'i çalıştırabilir.

---

## 7. Dışarıda Bırakılanlar

- Docker build/push (CD yok)
- Integration testleri (gerçek DB, Testcontainers) — gelecek adım
- Aspire AppHost CI entegrasyonu
- Branch protection rule (GitHub UI'da manuel yapılacak)
