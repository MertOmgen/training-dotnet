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
using Microsoft.EntityFrameworkCore;
using MongoDB.Driver;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
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

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .Enrich.FromLogContext()
    .Enrich.WithProperty("ServiceName", "Catalog.API")
    .WriteTo.Console()
    .WriteTo.Elasticsearch(new ElasticsearchSinkOptions(new Uri("http://localhost:9200"))
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
    // 3. EF Core + PostgreSQL (Write DB)
    // =============================================================================
    builder.Services.AddDbContext<CatalogDbContext>(options =>
        options.UseNpgsql(
            builder.Configuration.GetConnectionString("WriteDb")
            ?? "Host=localhost;Database=lms_catalog_db;Username=lms_user;Password=lms_password_2024"));

    // Repository DI kaydı
    builder.Services.AddScoped<IBookRepository, BookRepository>();

    // =============================================================================
    // 4. MongoDB (Read DB)
    // =============================================================================
    // EĞİTİCİ NOT:
    // CQRS'de Read DB ayrıdır. Query handler'lar MongoDB'den okur.
    // MongoDB client'ı singleton olarak kaydedilir (connection pooling).
    // =============================================================================
    builder.Services.AddSingleton<IMongoClient>(sp =>
    {
        var connectionString = builder.Configuration.GetConnectionString("ReadDb")
            ?? "mongodb://lms_user:lms_password_2024@localhost:27017";
        return new MongoClient(connectionString);
    });

    builder.Services.AddScoped<IMongoDatabase>(sp =>
    {
        var client = sp.GetRequiredService<IMongoClient>();
        return client.GetDatabase("lms_catalog_read");
    });

    // =============================================================================
    // 5. Redis (Distributed Cache)
    // =============================================================================
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = builder.Configuration.GetConnectionString("Redis")
            ?? "localhost:6379";
        options.InstanceName = "lms_catalog_";
    });

    builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
        ConnectionMultiplexer.Connect(
            builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379"));

    builder.Services.AddScoped<ICacheService, RedisCacheService>();

    // =============================================================================
    // 6. Elasticsearch (Search)
    // =============================================================================
    builder.Services.AddSingleton<ElasticsearchClient>(sp =>
    {
        var settings = new ElasticsearchClientSettings(
            new Uri(builder.Configuration["Elasticsearch:Url"] ?? "http://localhost:9200"))
            .DefaultIndex("catalog-books");
        return new ElasticsearchClient(settings);
    });

    builder.Services.AddScoped<ElasticsearchService>();

    // =============================================================================
    // 7. RabbitMQ (Event Bus)
    // =============================================================================
    builder.Services.AddRabbitMqEventBus(builder.Configuration);

    // =============================================================================
    // 8. OpenTelemetry (Distributed Tracing)
    // =============================================================================
    // EĞİTİCİ NOT:
    // NEDEN Distributed Tracing?
    // → Microservice mimarisinde tek bir HTTP isteği birden fazla servisten geçer.
    //   OpenTelemetry, bu isteğin tüm servisler arasındaki yolculuğunu takip eder.
    //   Her istek bir "Trace ID" ile işaretlenir ve Zipkin/Jaeger'da görselleştirilir.
    //
    // ┌──────────┐    ┌──────────┐    ┌──────────────┐    ┌──────────────┐
    // │ Client   │ → │ Gateway  │ → │ Catalog API  │ → │ PostgreSQL   │
    // │          │    │ (YARP)   │    │ TraceId: X   │    │ TraceId: X   │
    // └──────────┘    └──────────┘    └──────────────┘    └──────────────┘
    //                                       ↓
    //                                ┌──────────────┐
    //                                │ RabbitMQ     │
    //                                │ TraceId: X   │
    //                                └──────────────┘
    // =============================================================================
    builder.Services.AddOpenTelemetry()
        .ConfigureResource(resource => resource.AddService("Catalog.API"))
        .WithTracing(tracing => tracing
            .AddAspNetCoreInstrumentation() // Dışarıdan gelen HTTP isteklerini otomatik yakala
            .AddHttpClientInstrumentation() // Başka servislere giden HTTP isteklerini otomatik yakala
            .AddConsoleExporter());
            //.AddOtlpExporter();              Verileri Jaeger, Zipkin veya Elasticsearch'e gönder
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

    // Elasticsearch index'ini oluştur (uygulama başlangıcında)
    using (var scope = app.Services.CreateScope())
    {
        var esService = scope.ServiceProvider.GetRequiredService<ElasticsearchService>();
        await esService.EnsureIndexCreatedAsync();
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
