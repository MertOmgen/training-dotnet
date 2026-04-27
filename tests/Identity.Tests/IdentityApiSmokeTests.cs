// =============================================================================
// IdentityApiSmokeTests — Identity.API Duman Testi
// =============================================================================
// 📚 EĞİTİCİ NOT (Tech-Tutor):
//
// Identity.API Bağımlılıkları:
// → IdentityDbContext (PostgreSQL via Aspire) → InMemory ile değiştiriliyor
// → ASP.NET Core Identity (UserManager, SignInManager) → InMemory DB ile çalışır
// → IEventBus (RabbitMQ/MassTransit) → Mock ile değiştiriliyor
// → MassTransitHostedService → Kaldırılıyor
//
// ASP.NET Core Identity + InMemory:
// → AddIdentity<ApplicationUser, IdentityRole>().AddEntityFrameworkStores<IdentityDbContext>()
//   kaydı, DbContext'in InMemory ile değiştirilmesiyle sorunsuz çalışır.
// → UserStore ve RoleStore, DbContext'e bağlı olduğundan InMemory destekler.
// =============================================================================

using EventBus.Abstractions;
using FluentAssertions;
using Identity.API.Data;
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

namespace Identity.Tests;

// ─────────────────────────────────────────────────────────────────────────────
// WebApplicationFactory: Identity.API test sunucusu
// ─────────────────────────────────────────────────────────────────────────────
public sealed class IdentityApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        // Aspire AddNpgsqlDbContext servis kaydı esnasında bağlantı dizesini okur.
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:identity-db"] = "Host=localhost;Port=5432;Database=identity_test;Username=test;Password=test",
                ["ConnectionStrings:rabbitmq"] = "amqp://guest:guest@localhost:5672",
            });
        });

        builder.ConfigureTestServices(services =>
        {
            // ─────────────────────────────────────────────────────────────
            // 1. IdentityDbContext → InMemory ile değiştir
            // ─────────────────────────────────────────────────────────────
            // Aspire'ın AddNpgsqlDbContext'i EF Core context pooling kullanır.
            // Tüm IdentityDbContext bağımlı kayıtları kaldırıp InMemory kuruyoruz.
            var identityDbDescriptors = services
                .Where(d =>
                    d.ServiceType == typeof(DbContextOptions<IdentityDbContext>) ||
                    d.ServiceType == typeof(IdentityDbContext) ||
                    (d.ServiceType.IsGenericType &&
                     d.ServiceType.GenericTypeArguments.Contains(typeof(IdentityDbContext))))
                .ToList();
            foreach (var d in identityDbDescriptors) services.Remove(d);
            services.AddDbContext<IdentityDbContext>(opts =>
                opts.UseInMemoryDatabase("identity-smoke-test"));

            // ─────────────────────────────────────────────────────────────
            // 2. MassTransit HostedService → Kaldır
            // ─────────────────────────────────────────────────────────────
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
public class IdentityApiSmokeTests : IClassFixture<IdentityApiFactory>
{
    private readonly HttpClient _client;

    public IdentityApiSmokeTests(IdentityApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    /// <summary>
    /// Identity.API'nin başarıyla ayağa kalktığını doğrular.
    /// </summary>
    [Fact]
    public async Task Get_Alive_Returns200()
    {
        var response = await _client.GetAsync("/alive");

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            because: "Liveness probe her zaman 200 dönmeli.");
    }
}
