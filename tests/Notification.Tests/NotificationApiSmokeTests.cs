// =============================================================================
// NotificationApiSmokeTests — Notification.API Duman Testi
// =============================================================================
// 📚 EĞİTİCİ NOT (Tech-Tutor):
//
// Notification.API Bağımlılıkları:
// → MassTransit (RabbitMQ transport, 4 consumer) → Hosted service kaldırılıyor
// → MailKit (e-posta gönderimi) → Test ortamında çalışmaz ama DI lazy kayıtlı
// → Veritabanı YOK (consumer-only servis)
//
// Consumer-Only Servis:
// → Notification.API HTTP endpoint expose etmez (sadece /alive, /health, /swagger).
// → Görevi: RabbitMQ event'lerini consume edip e-posta göndermek.
// → Bu nedenle smoke test yalnızca startup'ı doğrular.
//
// MassTransit Hosted Service Kaldırma:
// → MassTransitHostedService'i kaldırarak RabbitMQ bağlantı denemesi engellenir.
// → Bu, test izolasyonu için standart bir yaklaşımdır.
// =============================================================================

using FluentAssertions;
using MassTransit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using System.Net;
using Xunit;

namespace Notification.Tests;

// ─────────────────────────────────────────────────────────────────────────────
// WebApplicationFactory: Notification.API test sunucusu
// ─────────────────────────────────────────────────────────────────────────────
public sealed class NotificationApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        // RabbitMQ bağlantı dizesi sağla (MassTransit servis kaydı için)
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:rabbitmq"] = "amqp://guest:guest@localhost:5672",
            });
        });

        builder.ConfigureTestServices(services =>
        {
            // MassTransit HostedService → Kaldır
            // Test ortamında RabbitMQ mevcut değil; hosted service olmadan
            // uygulama başarıyla ayağa kalkar.
            var massTransitHosted = services
                .Where(d => d.ServiceType == typeof(IHostedService) &&
                            d.ImplementationType?.Name == "MassTransitHostedService")
                .ToList();
            foreach (var d in massTransitHosted) services.Remove(d);
        });
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Smoke Test Sınıfı
// ─────────────────────────────────────────────────────────────────────────────
[Trait("Category", "Smoke")]
public class NotificationApiSmokeTests : IClassFixture<NotificationApiFactory>
{
    private readonly HttpClient _client;

    public NotificationApiSmokeTests(NotificationApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    /// <summary>
    /// Notification.API'nin başarıyla ayağa kalktığını doğrular.
    /// Consumer-only servis olsa da /alive endpoint'i cevap vermelidir.
    /// </summary>
    [Fact]
    public async Task Get_Alive_Returns200()
    {
        var response = await _client.GetAsync("/alive");

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            because: "Liveness probe her zaman 200 dönmeli.");
    }
}
