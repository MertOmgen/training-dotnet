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
// │    → OTEL_EXPORTER_OTLP_ENDPOINT env var'ı otomatik okunur │
// │ 2. AddDefaultHealthChecks()                                 │
// │    → /health (readiness) ve /alive (liveness) hazırlığı     │
// │ 3. AddServiceDiscovery()                                    │
// │    → HTTP istemcilerinde isim tabanlı URL çözümleme         │
// │ 4. ConfigureHttpClientDefaults()                            │
// │    → Her HttpClient'a Retry + Circuit Breaker eklenir       │
// └─────────────────────────────────────────────────────────────┘
//
// MapDefaultEndpoints() Ne Yapar?
// → /health → Readiness probe: Servis isteklere hazır mı?
//             (DB bağlantısı vs. kontrol edilir)
// → /alive  → Liveness probe: Servis process'i canlı mı?
//             (Sadece "live" tag'li check'ler çalışır)
//
// Kubernetes Probe Farkı:
// → Liveness fail → Pod yeniden başlatılır
// → Readiness fail → Pod traffic almaktan çıkarılır (yeniden başlatılmaz)
// Bu yüzden /alive'da DB kontrolü olmamalı!
// =============================================================================

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

// IHostApplicationBuilder namespace'i: WebApplication.CreateBuilder() ve benzeri
// tüm host builder'ların paylaştığı interface. Bu sayede hem web hem de
// Worker Service projeleri bu extension'ı kullanabilir.
namespace Microsoft.Extensions.Hosting;

public static class Extensions
{
    // =========================================================================
    // AddServiceDefaults — Ana Entry Point
    // =========================================================================
    // EĞİTİCİ NOT:
    // Bu extension method, IHostApplicationBuilder üzerinde tanımlanır.
    // .NET 8+ ile gelen bu interface sayesinde:
    //   - ASP.NET Core projeleri (WebApplication.CreateBuilder)
    //   - Worker Service projeleri (Host.CreateApplicationBuilder)
    //   hepsi bu method'u kullanabilir.
    // =========================================================================
    public static IHostApplicationBuilder AddServiceDefaults(
        this IHostApplicationBuilder builder)
    {
        // ─────────────────────────────────────────────────────────────────
        // 1. OpenTelemetry: Distributed Tracing + Metrics + Logging
        // ─────────────────────────────────────────────────────────────────
        builder.ConfigureOpenTelemetry();

        // ─────────────────────────────────────────────────────────────────
        // 2. Health Checks: /health ve /alive endpoint hazırlığı
        // ─────────────────────────────────────────────────────────────────
        builder.AddDefaultHealthChecks();

        // ─────────────────────────────────────────────────────────────────
        // 3. Service Discovery: DNS-tabanlı servis buluculuk
        // ─────────────────────────────────────────────────────────────────
        // EĞİTİCİ NOT:
        // AddServiceDiscovery() kayıt yapısını şöyle açıklayalım:
        //
        // Geleneksel (Hardcoded) Yaklaşım:
        //   new HttpClient { BaseAddress = new Uri("http://localhost:5001") }
        //   → Port değişirse her yerde güncellenmeli!
        //
        // Service Discovery Yaklaşımı:
        //   new HttpClient { BaseAddress = new Uri("http://catalog-api") }
        //   → "catalog-api" ismi, Aspire AppHost'taki resource adıyla eşleşir
        //   → Aspire, gerçek host:port bilgisini env var olarak enjekte eder
        //   → IServiceEndpointResolver bu env var'ları DNS gibi çözer
        //
        // Env var formatı (Aspire tarafından enjekte edilir):
        //   services__catalog-api__http__0 = "http://hostname:5001"
        builder.Services.AddServiceDiscovery();

        // ─────────────────────────────────────────────────────────────────
        // 4. HttpClient Resilience + Service Discovery Varsayılanları
        // ─────────────────────────────────────────────────────────────────
        // EĞİTİCİ NOT:
        // ConfigureHttpClientDefaults: Tüm IHttpClientFactory ile üretilen
        // HttpClient instance'larına otomatik olarak şunları uygular:
        //
        // AddServiceDiscovery():
        //   → "http://catalog-api" URL'ini gerçek adrese çevirir
        //   → Her istekten önce endpoint resolver çalışır
        //
        // AddStandardResilienceHandler():
        //   → Polly v8 tabanlı, 4 katmanlı resilience pipeline:
        //
        //   ┌─────────────────────────────────────────────────────────┐
        //   │ 1. Total Request Timeout  → 30 saniye (tüm retry dahil) │
        //   │ 2. Retry                  → 3 deneme, exponential jitter │
        //   │ 3. Circuit Breaker        → %50 hata oranında açılır     │
        //   │ 4. Attempt Timeout        → Her deneme için 10 saniye    │
        //   └─────────────────────────────────────────────────────────┘
        //
        // NEDEN Otomatik Resilience?
        // → Microservice mimarisinde her servis diğerine HTTP ile bağlıdır.
        //   Birinin geçici kesintisi domino etkisi yaratmamalı.
        //   Bu varsayılan politikalar çoğu senaryoyu kapsar.
        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            // URL çözümleme (Service Discovery)
            http.AddServiceDiscovery();
            // Retry + Circuit Breaker (varsayılan Polly politikaları)
            http.AddStandardResilienceHandler();
        });

        return builder;
    }

    // =========================================================================
    // ConfigureOpenTelemetry — Dağıtık İzleme Konfigürasyonu
    // =========================================================================
    // 📚 EĞİTİCİ NOT:
    //
    // OpenTelemetry (OTel) — Vendor-Neutral Observability Standardı
    //
    // OTel'in 3 Sütunu (Three Pillars of Observability):
    // ┌──────────┬───────────────────────────────────────────────────────┐
    // │ Traces   │ İstek akışı görselleştirme                            │
    // │          │ → "Kullanıcı bir kitap aradığında hangi servisler     │
    // │          │    kaç ms sürdü?" sorusunu yanıtlar                   │
    // │          │ Waterfall diyagramı olarak Dashboard'da görünür        │
    // ├──────────┼───────────────────────────────────────────────────────┤
    // │ Metrics  │ Sayısal ölçümler (zaman serisi)                       │
    // │          │ → RPS, P99 latency, error rate, heap size             │
    // │          │ Dashboard'da grafik/gauge olarak görünür              │
    // ├──────────┼───────────────────────────────────────────────────────┤
    // │ Logs     │ Yapısal olay kayıtları (structured logs)              │
    // │          │ → Serilog'un ürettiği log'lar OTel üzerinden          │
    // │          │   Aspire Dashboard'a aktarılır                        │
    // └──────────┴───────────────────────────────────────────────────────┘
    //
    // OTLP (OpenTelemetry Protocol):
    // → OTel verisini taşıyan protokol (gRPC veya HTTP/JSON)
    // → Aspire Dashboard bir OTLP sunucusu çalıştırır
    // → OTEL_EXPORTER_OTLP_ENDPOINT env var → Aspire AppHost tarafından
    //   otomatik set edilir; servislerin adresi hardcode etmesi gerekmez
    //
    // Instrumentation Library'ler (otomatik telemetri):
    // → AspNetCore: Her HTTP isteği için otomatik span oluşturur
    // → HttpClient: Dışa giden istekleri otomatik izler
    // → Runtime: GC pause, thread starvation, memory gibi JVM-benzeri metrikler
    // =========================================================================
    public static IHostApplicationBuilder ConfigureOpenTelemetry(
        this IHostApplicationBuilder builder)
    {
        // Loglara trace/span ID'si ekle (log-trace korelasyonu)
        // → Bir log satırından ilgili distributed trace'e gidebilirsiniz
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics =>
            {
                metrics
                    .AddAspNetCoreInstrumentation()  // HTTP endpoint metrikleri
                    .AddHttpClientInstrumentation()  // Dışa giden HTTP metrikleri
                    .AddRuntimeInstrumentation();    // .NET runtime metrikleri
            })
            .WithTracing(tracing =>
            {
                tracing
                    .AddAspNetCoreInstrumentation()  // HTTP istek span'ları
                    .AddHttpClientInstrumentation(); // HttpClient span'ları
            });

        // OTLP Exporter: Aspire Dashboard'a veri gönder
        builder.AddOpenTelemetryExporters();

        return builder;
    }

    private static IHostApplicationBuilder AddOpenTelemetryExporters(
        this IHostApplicationBuilder builder)
    {
        // EĞİTİCİ NOT:
        // OTEL_EXPORTER_OTLP_ENDPOINT env var'ı Aspire AppHost tarafından
        // her servise otomatik olarak inject edilir.
        // Bu env var varsa → OTLP exporter'ı aktif et.
        // Bu env var yoksa → Exporter kapalı (standalone çalışma için)
        //
        // Bu yaklaşım sayesinde servisler hem Aspire ile hem de standalone
        // (docker-compose.prod.yml ile) çalışabilir.
        var useOtlpExporter = !string.IsNullOrWhiteSpace(
            builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

        if (useOtlpExporter)
        {
            // UseOtlpExporter: Hem trace hem metric hem log'u OTLP üzerinden gönder
            builder.Services.AddOpenTelemetry().UseOtlpExporter();
        }

        return builder;
    }

    // =========================================================================
    // AddDefaultHealthChecks — Health Check Kayıtları
    // =========================================================================
    // 📚 EĞİTİCİ NOT:
    //
    // Health Check Türleri:
    // ┌─────────────────────────────────────────────────────────────────┐
    // │ Liveness  (/alive) → "Bu process canlı mı?"                    │
    // │  → SADECE "live" tag'li check'ler çalışır                      │
    // │  → Fail → Kubernetes pod'u yeniden başlatır                    │
    // │  → DB down iken pod restart istemiyoruz → DB buraya eklenmez   │
    // ├─────────────────────────────────────────────────────────────────┤
    // │ Readiness (/health) → "Bu servis trafik almaya hazır mı?"      │
    // │  → Tüm check'ler (DB, Redis vs.) çalışır                       │
    // │  → Fail → K8s bu pod'a trafik yönlendirmez (restart yok)      │
    // └─────────────────────────────────────────────────────────────────┘
    //
    // Aspire Dashboard bu endpoint'leri düzenli sorgulayarak
    // servislerin sağlığını gerçek zamanlı gösterir.
    //
    // Aspire komponent paketleri (Aspire.Npgsql.*, Aspire.StackExchange.Redis.*)
    // kendi health check'lerini otomatik ekler:
    // → AddNpgsqlDbContext() → PostgreSQL ping check'i ekler
    // → AddRedisDistributedCache() → Redis ping check'i ekler
    // =========================================================================
    public static IHostApplicationBuilder AddDefaultHealthChecks(
        this IHostApplicationBuilder builder)
    {
        builder.Services.AddHealthChecks()
            // "self" check: Servis process'i canlı mı? Her zaman Healthy.
            // "live" tag'i: /alive endpoint'inde çalışacak
            .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);

        return builder;
    }

    // =========================================================================
    // MapDefaultEndpoints — /health ve /alive route'larını ekler
    // =========================================================================
    // EĞİTİCİ NOT:
    // Bu method WebApplication üzerinde çalışır (middleware pipeline).
    // app.MapHealthChecks() → ASP.NET Core minimal API routing'e health check ekler.
    //
    // Neden sadece Development'ta açık?
    // → Health endpoint'leri iç ağdan (cluster-internal) erişilmeli.
    //   Production'da NetworkPolicy veya Ingress kuralları ile korunur.
    //   Development'ta güvenlik endişesi yoktur, tüm endpoint'ler açık.
    // =========================================================================
    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            // /health → Readiness: Tüm health check'ler (DB, Redis, vs.) çalışır
            app.MapHealthChecks("/health");

            // /alive → Liveness: Yalnızca "live" tag'li check'ler çalışır
            // → DB health check buraya dahil değil!
            //   DB geçici down olduğunda pod'u yeniden başlatmak istemiyoruz.
            app.MapHealthChecks("/alive", new HealthCheckOptions
            {
                Predicate = r => r.Tags.Contains("live")
            });
        }

        return app;
    }
}
