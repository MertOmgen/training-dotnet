// =============================================================================
// Program.cs — Catalog API Giriş Noktası
// =============================================================================
// 📚 EĞİTİCİ NOT (Tech-Tutor):
//
// Bu dosya Catalog mikroservisinin başlangıç noktasıdır.
// .NET 8 Minimal API yaklaşımı kullanılmıştır.
//
// NEDEN Minimal API (Controller Yerine)?
// → Daha az boilerplate kod, daha hızlı başlangıç
// → Performans avantajı (request delegate compilation)
// → Microservice'ler için ideal: küçük, odaklı endpoint'ler
//
// NEDEN Controller Kullanılmadı?
// → Controller'lar büyük monolitik uygulamalar için daha uygundur.
//   Microservice'lerde genellikle az sayıda endpoint vardır.
//   Minimal API ile daha temiz ve okunabilir kod yazılır.
//
// DI (Dependency Injection) REGISTRASYON SIRASI:
// 1. Core Services (MediatR, FluentValidation)
// 2. Infrastructure (EF Core, MongoDB, Redis, Elasticsearch)
// 3. Event Bus (RabbitMQ/MassTransit)
// 4. Cross-Cutting (Serilog, OpenTelemetry)
// =============================================================================

using Catalog.API.Endpoints;
using Catalog.Application.Behaviors;
using Catalog.Application.Commands;
using Catalog.Application.Queries;
using Catalog.Domain.Repositories;
using Catalog.Infrastructure.Persistence;
using Catalog.Infrastructure.Repositories;
using Catalog.Infrastructure.Search;
using Caching.Redis;
using EventBus.RabbitMQ;
using Elastic.Clients.Elasticsearch;
using FluentValidation;
using MediatR;
using MongoDB.Driver;
using Serilog;
using Serilog.Sinks.Elasticsearch;
using StackExchange.Redis;

// =============================================================================
// 1. SERILOG YAPILANDIRMASI
// =============================================================================
// EĞİTİCİ NOT:
// Serilog, .NET'in varsayılan ILogger'ından çok daha güçlüdür.
// "Structured Logging" desteği ile log'lar aranabilir JSON formatında saklanır.
// Elasticsearch sink'i ile log'lar Kibana'da görselleştirilebilir.
// =============================================================================

// =============================================================================
// ASPIRE EĞİTİCİ NOT: Serilog + Elasticsearch URL Çözümü
// =============================================================================
// Aspire AppHost, her servisine ConnectionStrings__elasticsearch env var'ını
// inject eder. Ancak Serilog bootstrap, WebApplication.CreateBuilder()'dan
// ÖNCE gerçekleşir (henüz IConfiguration nesnesi hazır değil).
//
// Çözüm: Doğrudan Environment.GetEnvironmentVariable() ile okumak.
//   • Aspire ile: env var → "http://aspire-yönetimli-elasticsearch:9200"
//   • Standalone: env var yok → fallback "http://localhost:9200"
// =============================================================================
var elasticsearchUrl = Environment.GetEnvironmentVariable("ConnectionStrings__elasticsearch")
    ?? "http://localhost:9200";

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .Enrich.FromLogContext()
    .Enrich.WithProperty("ServiceName", "Catalog.API")
    .WriteTo.Console()
    .WriteTo.Elasticsearch(new ElasticsearchSinkOptions(new Uri(elasticsearchUrl))
    {
        IndexFormat = "lms-catalog-logs-{0:yyyy.MM.dd}",
        AutoRegisterTemplate = true,
        AutoRegisterTemplateVersion = AutoRegisterTemplateVersion.ESv7,
        NumberOfShards = 1,
        NumberOfReplicas = 0
    })
    .CreateLogger();

try
{
    Log.Information("Catalog.API başlatılıyor...");

    var builder = WebApplication.CreateBuilder(args);

    // Serilog'u ASP.NET Core'a entegre et
    builder.Host.UseSerilog();

    // =============================================================================
    // ASPIRE: AddServiceDefaults
    // =============================================================================
    // 📚 EĞİTİCİ NOT (Tech-Tutor):
    //
    // Bu tek satır çok şey yapar:
    // → OpenTelemetry (traces, metrics, logs) — OTLP Exporter ile Aspire Dashboard'a
    // → Health Checks (/health + /alive endpoint'leri)
    // → Service Discovery (DNS tabanlı — http://catalog-api gibi isimler çözülür)
    // → HTTP Resilience (retry, circuit breaker)
    //
    // ServiceDefaults projesindeki Extensions.cs'deki AddServiceDefaults() metodunu çağırır.
    // Bu metot, tüm servislerde ortak olan konfigürasyonu tek yerden yönetir.
    // =============================================================================
    builder.AddServiceDefaults();

    // =============================================================================
    // 2. MediatR + Pipeline Behaviors
    // =============================================================================
    // EĞİTİCİ NOT:
    // MediatR registrasyonunda assembly tarama yapılır.
    // Aynı assembly'deki tüm Handler, Validator ve Behavior'lar otomatik bulunur.
    //
    // Pipeline Behavior SIRASI ÖNEMLİDİR:
    // 1. ValidationBehavior → Geçersiz istek? → Handler'a gitme!
    // 2. LoggingBehavior → İsteği logla
    // 3. CachingBehavior → Cache'te var mı? → Handler'a gitme!
    // 4. Handler → İş mantığını çalıştır
    // =============================================================================

    builder.Services.AddMediatR(cfg =>
    {
        cfg.RegisterServicesFromAssembly(typeof(CreateBookCommand).Assembly);
        cfg.RegisterServicesFromAssembly(typeof(ElasticsearchService).Assembly);
    });

    // Pipeline Behaviors (sıra önemli!)
    builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
    builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
    builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(CachingBehavior<,>));

    // FluentValidation validator'larını otomatik kaydet
    builder.Services.AddValidatorsFromAssembly(typeof(CreateBookCommand).Assembly);

    // =============================================================================
    // 3. EF Core + PostgreSQL (Write DB) — Aspire Yönetimli
    // =============================================================================
    // 📚 EĞİTİCİ NOT (Tech-Tutor):
    //
    // Eski yaklaşım:
    //   builder.Services.AddDbContext<CatalogDbContext>(options =>
    //       options.UseNpgsql("Host=localhost;..."));
    //
    // Yeni Aspire yaklaşımı:
    //   builder.AddNpgsqlDbContext<CatalogDbContext>("catalog-db");
    //
    // "catalog-db" → AppHost'ta: postgres.AddDatabase("catalog-db")
    //
    // Aspire şunu inject eder:
    //   ConnectionStrings__catalog-db = "Host=aspire-postgres;Database=catalog-db;..."
    //
    // AddNpgsqlDbContext ne sağlar?
    // → Connection string'i otomatik okur
    // → Retry politikası ekler (geçici hatalarda otomatik yeniden bağlanır)
    // → Health check kaydeder (/health endpoint'inde görünür)
    // → DbContext'i Scoped olarak DI'ya kaydeder
    // =============================================================================
    builder.AddNpgsqlDbContext<CatalogDbContext>("catalog-db");

    // Repository DI kaydı
    builder.Services.AddScoped<IBookRepository, BookRepository>();

    // =============================================================================
    // 4. MongoDB (Read DB) — Aspire Yönetimli
    // =============================================================================
    // 📚 EĞİTİCİ NOT (Tech-Tutor):
    //
    // Eski yaklaşım:
    //   builder.Services.AddSingleton<IMongoClient>(sp => new MongoClient("..."));
    //
    // Yeni Aspire yaklaşımı:
    //   builder.AddMongoDBClient("mongodb");
    //
    // "mongodb" → AppHost'ta: builder.AddMongoDB("mongodb")
    //
    // AddMongoDBClient ne sağlar?
    // → ConnectionStrings__mongodb env var'ından connection string okur
    // → IMongoClient'ı Singleton olarak DI'ya kaydeder
    // → Health check ekler
    //
    // IMongoDatabase ise hâlâ manuel kayıt gerektirir (hangi DB adı seçileceği
    // uygulama mantığına özgüdür, Aspire tarafından bilinmez).
    // =============================================================================
    builder.AddMongoDBClient("mongodb");

    // IMongoDatabase: Catalog Read DB — manuel scoped kayıt
    builder.Services.AddScoped<IMongoDatabase>(sp =>
    {
        var client = sp.GetRequiredService<IMongoClient>();
        return client.GetDatabase("lms_catalog_read");
    });

    // =============================================================================
    // 5. Redis (Distributed Cache) — Aspire Yönetimli
    // =============================================================================
    // 📚 EĞİTİCİ NOT (Tech-Tutor):
    //
    // Eski yaklaşım:
    //   builder.Services.AddStackExchangeRedisCache(options => { options.Configuration = "..."; });
    //   builder.Services.AddSingleton<IConnectionMultiplexer>(sp => ConnectionMultiplexer.Connect("..."));
    //
    // Yeni Aspire yaklaşımı:
    //   builder.AddRedisDistributedCache("redis");  → IDistributedCache kaydı
    //   builder.AddRedisClient("redis");            → IConnectionMultiplexer kaydı
    //
    // "redis" → AppHost'ta: builder.AddRedis("redis")
    //
    // Her iki metot da:
    // → ConnectionStrings__redis env var'ından connection string okur
    // → Health check ekler
    // → Retry/circuit breaker politikası uygular
    // =============================================================================
    builder.AddRedisDistributedCache("redis");
    builder.AddRedisClient("redis");

    builder.Services.AddScoped<ICacheService, RedisCacheService>();

    // =============================================================================
    // 6. Elasticsearch (Search) — Aspire Connection String
    // =============================================================================
    // 📚 EĞİTİCİ NOT (Tech-Tutor):
    //
    // Aspire'ın Elasticsearch için resmi bir IDistributedApplicationBuilder extension'ı
    // mevcuttur (builder.AddElasticsearch) fakat bu, hosting paketi içindir (AppHost'ta).
    // Servis tarafında ise connection string'i manual okuyup ElasticsearchClient kurarız.
    //
    // ConnectionStrings__elasticsearch → AppHost'taki builder.AddElasticsearch("elasticsearch")
    // tarafından inject edilir.
    // =============================================================================
    builder.Services.AddSingleton<ElasticsearchClient>(sp =>
    {
        var esUrl = builder.Configuration.GetConnectionString("elasticsearch")
            ?? builder.Configuration["Elasticsearch:Url"]
            ?? "http://localhost:9200";
        var settings = new ElasticsearchClientSettings(new Uri(esUrl))
            .DefaultIndex("catalog-books");
        return new ElasticsearchClient(settings);
    });

    builder.Services.AddScoped<ElasticsearchService>();

    // =============================================================================
    // 7. RabbitMQ (Event Bus)
    // =============================================================================
    builder.Services.AddRabbitMqEventBus(builder.Configuration);

    // =============================================================================
    // 8. Swagger + CORS
    // =============================================================================
    // 📚 EĞİTİCİ NOT (Tech-Tutor):
    //
    // OpenTelemetry bloğu KALDIRILDI — AddServiceDefaults() bunu otomatik yapar.
    // ServiceDefaults/Extensions.cs'deki ConfigureOpenTelemetry() metodu:
    // → Tüm servisler için OTel konfigürasyonunu TEK YERDEN yönetir
    // → OTLP Exporter ile Aspire Dashboard'a gönderir
    // → AspNetCore + HTTP + Runtime Metrics enstrümanlarını ekler
    //
    // ┌───────────────────────────────────────────────────────┐
    // │  Aspire Dashboard (http://localhost:18888)            │
    // │  ├── Traces: Tüm servisler arası istek akışları       │
    // │  ├── Metrics: Request/s, latency, error rate          │
    // │  └── Logs: Structured log akışı                       │
    // └───────────────────────────────────────────────────────┘
    //
    // =============================================================================
    // 9. Swagger + CORS
    // =============================================================================
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new()
        {
            Title = "LMS Catalog API",
            Version = "v1",
            Description = "Library Management System — Kitap Kataloğu Servisi"
        });
    });

    // =========================================================================
    // APP MIDDLEWARE PIPELINE
    // =========================================================================
    var app = builder.Build();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    // Serilog HTTP request loglama
    app.UseSerilogRequestLogging();

    app.UseHttpsRedirection();

    // ─────────────────────────────────────────────────────
    // Minimal API Endpoint'lerini kaydet
    // → Extension method ile endpoint'ler ayrı dosyada tanımlanır.
    //   Bu, Program.cs'in temiz kalmasını sağlar.
    // ─────────────────────────────────────────────────────
    app.MapCatalogEndpoints();

    // =============================================================================
    // ASPIRE: Default Health Endpoint'leri (/health + /alive)
    // =============================================================================
    // 📚 EĞİTİCİ NOT (Tech-Tutor):
    //
    // MapDefaultEndpoints() şunları ekler (Development ortamında):
    //   GET /health → Readiness probe: tüm health check'leri çalıştırır
    //                 (PostgreSQL, MongoDB, Redis, RabbitMQ)
    //   GET /alive  → Liveness probe: sadece "live" tag'li check'leri çalıştırır
    //                 (servis hayatta mı? bağımlılıklar kontrol edilmez)
    //
    // Kubernetes Liveness vs Readiness Probe Farkı:
    //   Liveness  (/alive): "Servis kilitlendiyse yeniden başlat"
    //   Readiness (/health): "Trafik gönderilmeye hazır mı?"
    // =============================================================================
    app.MapDefaultEndpoints();

    // Elasticsearch index'ini oluştur (uygulama başlangıcında)
    // try-catch: Elasticsearch geçici olarak erişilemez durumdaysa
    // (test ortamı, soğuk başlatma) servis yine de ayağa kalkar.
    try
    {
        using var scope = app.Services.CreateScope();
        var esService = scope.ServiceProvider.GetRequiredService<ElasticsearchService>();
        await esService.EnsureIndexCreatedAsync();
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "Elasticsearch başlatılamadı; arama özellikleri devre dışı kalabilir.");
    }

    Log.Information("Catalog.API başarıyla başlatıldı.");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Catalog.API başlatılırken kritik hata oluştu!");
}
finally
{
    Log.CloseAndFlush();
}

// WebApplicationFactory icin gerekli — smoke testlerin Program sinifina erisimini saglar
public partial class Program { }
