// =============================================================================
// MassTransitEventBus — RabbitMQ Üzerinden Event Yayınlama
// =============================================================================
// 📚 EĞİTİCİ NOT (Tech-Tutor):
//
// NASIL Çalışır?
// → MassTransit'in IPublishEndpoint arayüzü kullanılır.
// → PublishAsync çağrıldığında mesaj RabbitMQ'ya gönderilir.
// → MassTransit otomatik olarak:
//   - Exchange oluşturur (fan-out veya topic bazlı)
//   - Mesajı serialize eder (System.Text.Json)
//   - Retry policy uygular (konfigüre edilebilir)
//   - Dead Letter Queue'ya yönlendirir (başarısız mesajlar için)
//
// MassTransit CONVENTION:
// → Event tipi adı → Exchange adı olur
//   Örnek: BookCreatedIntegrationEvent → "EventBus.Abstractions.Events:BookCreatedIntegrationEvent"
// → Consumer sınıf adı → Queue adı olur
//
// DI Kaydı:
// → Program.cs'te services.AddMassTransit(x => { ... }) ile konfigüre edilir.
// =============================================================================

using EventBus.Abstractions;
using EventBus.Abstractions.Events;
using MassTransit;

namespace EventBus.RabbitMQ;

/// <summary>
/// MassTransit tabanlı IEventBus implementasyonu.
/// RabbitMQ'ya integration event publish eder.
/// </summary>
public class MassTransitEventBus : IEventBus
{
    // -------------------------------------------------------------------------
    // IPublishEndpoint: MassTransit'in publish soyutlaması.
    // IBus'tan farklı olarak, sadece publish sorumluluğu taşır (ISP prensibi).
    // -------------------------------------------------------------------------
    private readonly IPublishEndpoint _publishEndpoint;

    public MassTransitEventBus(IPublishEndpoint publishEndpoint)
    {
        _publishEndpoint = publishEndpoint;
    }

    /// <inheritdoc/>
    public async Task PublishAsync<T>(T @event, CancellationToken cancellationToken = default)
        where T : IntegrationEvent
    {
        // ─────────────────────────────────────────────────────────────────
        // MassTransit, burada şunları yapar:
        // 1. Event nesnesini JSON olarak serialize eder
        // 2. RabbitMQ exchange'ine publish eder (fan-out)
        // 3. Tüm subscribe olan consumer'lar mesajı alır
        // 4. Hata durumunda retry policy'sine göre tekrar dener
        // ─────────────────────────────────────────────────────────────────
        await _publishEndpoint.Publish(@event, cancellationToken);
    }
}
