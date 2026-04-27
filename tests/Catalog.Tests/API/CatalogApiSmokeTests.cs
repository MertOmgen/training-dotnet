// =============================================================================
// CatalogApiSmokeTests — Catalog.API Duman Testi
// =============================================================================
// 📚 EĞİTİCİ NOT (Tech-Tutor):
//
// DUMAN TESTİ (Smoke Test) NEDİR?
// → "Uygulamayı yak, duman çıkıyor mu bak" metaforundan gelir.
// → Amacı: Servis exception fırlatmadan ayağa kalkıyor mu?
// → Derin iş mantığı testi YAPMAZ, sadece bootstrap'i doğrular.
//
// WebApplicationFactory<TProgram> NASIL ÇALIŞIR?
// → Microsoft.AspNetCore.Mvc.Testing paketi
// → Gerçek HTTP katmanı olmadan, in-process test sunucusu oluşturur.
// → Program.cs'teki tüm DI kayıtları ve middleware pipeline çalışır.
// → ConfigureWebHost ile test ortamı özelleştirilebilir.
//
// NEDEN "Development" ORTAMI?
// → ServiceDefaults/Extensions.cs'deki MapDefaultEndpoints() yalnızca
//   Development ortamında /alive ve /health endpoint'lerini map'ler.
// → Test için bu endpoint'lere ihtiyacımız var.
//
// NEDEN ConfigureTestServices?
// → IBookRepository ve IMongoDatabase, MongoDB'ye bağlı gerçek servisler.
// → Test ortamında MongoDB yoktur.
// → Mock ile gerçek bağımlılıkların yerine "sahte" ama davranışsız
//   nesneler koyarız. Smoke test için yeterli.
//
// NEDEN /alive (not /health)?
// → /alive = Liveness probe: Sadece "live" tag'li health check'ler çalışır.
//   Bu check'ler DB/Redis bağlantısına bakmaz.
// → /health = Readiness probe: Tüm bağımlılıkları (DB, Redis, MongoDB) kontrol eder.
//   Test ortamında gerçek bağlantı olmadığından Unhealthy döner.
// =============================================================================

using Catalog.Domain.Repositories;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MongoDB.Driver;
using Moq;
using System.Net;
using Xunit;

namespace Catalog.Tests.API;

// ─────────────────────────────────────────────────────────────────────────────
// WebApplicationFactory: Test sunucusunu yapılandırır
// ─────────────────────────────────────────────────────────────────────────────
public sealed class CatalogApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Development ortamı: MapDefaultEndpoints() /alive ve /health'i map'ler
        builder.UseEnvironment("Development");

        // Aspire extension'ları (AddNpgsqlDbContext, AddMongoDBClient, AddRedisClient)
        // servis kayıt zamanında bağlantı dizesini okur.
        // ConfigureAppConfiguration bu dizileri sağlar (gerçek bağlantı gerekmez).
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:catalog-db"] = "Host=localhost;Port=5432;Database=catalog_test;Username=test;Password=test",
                ["ConnectionStrings:mongodb"] = "mongodb://localhost:27017",
                ["ConnectionStrings:redis"] = "localhost:6379",
                ["ConnectionStrings:elasticsearch"] = "http://localhost:9200",
                ["ConnectionStrings:rabbitmq"] = "amqp://guest:guest@localhost:5672",
            });
        });

        // Gerçek bağımlılıkları mock'larla değiştir
        // → IBookRepository: PostgreSQL'e bağlı gerçek repository yerine no-op mock
        // → IMongoDatabase: MongoDB'ye bağlı gerçek DB yerine no-op mock
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IBookRepository>();
            services.AddScoped(_ => Mock.Of<IBookRepository>());

            services.RemoveAll<IMongoDatabase>();
            services.AddScoped(_ => Mock.Of<IMongoDatabase>());
        });
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Smoke Test Sınıfı
// ─────────────────────────────────────────────────────────────────────────────
[Trait("Category", "Smoke")]
public class CatalogApiSmokeTests : IClassFixture<CatalogApiFactory>
{
    private readonly HttpClient _client;

    public CatalogApiSmokeTests(CatalogApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    /// <summary>
    /// Catalog.API'nin başarıyla ayağa kalktığını ve /alive endpoint'inin
    /// 200 döndürdüğünü doğrular.
    /// </summary>
    [Fact]
    public async Task Get_Alive_Returns200()
    {
        // Act
        var response = await _client.GetAsync("/alive");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            because: "Liveness probe her zaman 200 dönmeli — bağımlılık durumundan bağımsız.");
    }
}
