// =============================================================================
// BorrowingApiSmokeTests — Borrowing.API Duman Testi
// =============================================================================
// 📚 EĞİTİCİ NOT (Tech-Tutor):
//
// Borrowing.API Bağımlılıkları:
// → BorrowingDbContext (PostgreSQL via Aspire) → InMemory ile değiştiriliyor
// → IEventBus (RabbitMQ/MassTransit) → Mock ile değiştiriliyor
// → MassTransitHostedService → Kaldırılıyor (RabbitMQ yokken bağlanmaya çalışmaması için)
//
// MassTransit'i Test'te Devre Dışı Bırakma:
// → MassTransitHostedService, IHostedService olarak kayıtlıdır.
// → Bu service'i kaldırarak bus'ın ayağa kalkması engellenir.
// → IEventBus arayüzü Mock ile sağlanır; böylece DI çözümlemesi sorunsuz çalışır.
//
// InMemory DbContext Kullanımı:
// → Gerçek PostgreSQL bağlantısı yerine bellek içi EF Core kullanılır.
// → Smoke test için yeterli: DbContext'in DI'dan resolve edilmesi test edilir.
// =============================================================================

using Borrowing.API.Data;
using EventBus.Abstractions;
using FluentAssertions;
using MassTransit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Moq;
using System.Net;
using Xunit;

namespace Borrowing.Tests;

// ─────────────────────────────────────────────────────────────────────────────
// WebApplicationFactory: Borrowing.API test sunucusu
// ─────────────────────────────────────────────────────────────────────────────
public sealed class BorrowingApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        // Aspire AddNpgsqlDbContext servis kaydı esnasında bağlantı dizesini okur.
        // İn-memory koleksiyon ile bu dize sağlanır; gerçek bağlantı yapılmaz.
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:borrowing-db"] = "Host=localhost;Port=5432;Database=borrowing_test;Username=test;Password=test",
                ["ConnectionStrings:rabbitmq"] = "amqp://guest:guest@localhost:5672",
            });
        });

        builder.ConfigureTestServices(services =>
        {
            // ─────────────────────────────────────────────────────────────
            // 1. BorrowingDbContext → InMemory ile değiştir
            // ─────────────────────────────────────────────────────────────
            // Aspire'ın AddNpgsqlDbContext'i EF Core context pooling kullanır
            // (IDbContextPool<T> Singleton). Bunu ve tüm BorrowingDbContext
            // bağımlı kayıtları kaldırıp sıfırdan InMemory kuruyoruz.
            var borrowingDbDescriptors = services
                .Where(d =>
                    d.ServiceType == typeof(DbContextOptions<BorrowingDbContext>) ||
                    d.ServiceType == typeof(BorrowingDbContext) ||
                    (d.ServiceType.IsGenericType &&
                     d.ServiceType.GenericTypeArguments.Contains(typeof(BorrowingDbContext))))
                .ToList();
            foreach (var d in borrowingDbDescriptors) services.Remove(d);
            services.AddDbContext<BorrowingDbContext>(opts =>
                opts.UseInMemoryDatabase("borrowing-smoke-test"));

            // ─────────────────────────────────────────────────────────────
            // 2. MassTransit HostedService → Kaldır
            // ─────────────────────────────────────────────────────────────
            // Test ortamında RabbitMQ yoktur.
            // MassTransitHostedService kaldırılmazsa bağlantı dener ve hata verebilir.
            var massTransitHosted = services
                .Where(d => d.ServiceType == typeof(IHostedService) &&
                            d.ImplementationType?.Name == "MassTransitHostedService")
                .ToList();
            foreach (var d in massTransitHosted) services.Remove(d);

            // ─────────────────────────────────────────────────────────────
            // 3. IEventBus → Mock ile sağla
            // ─────────────────────────────────────────────────────────────
            services.RemoveAll<IEventBus>();
            services.AddSingleton(_ => Mock.Of<IEventBus>());
        });
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Smoke Test Sınıfı
// ─────────────────────────────────────────────────────────────────────────────
[Trait("Category", "Smoke")]
public class BorrowingApiSmokeTests : IClassFixture<BorrowingApiFactory>
{
    private readonly HttpClient _client;

    public BorrowingApiSmokeTests(BorrowingApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    /// <summary>
    /// Borrowing.API'nin başarıyla ayağa kalktığını doğrular.
    /// </summary>
    [Fact]
    public async Task Get_Alive_Returns200()
    {
        var response = await _client.GetAsync("/alive");

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            because: "Liveness probe her zaman 200 dönmeli.");
    }
}
