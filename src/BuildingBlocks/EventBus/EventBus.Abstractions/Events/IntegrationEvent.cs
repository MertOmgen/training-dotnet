// =============================================================================
// IntegrationEvent — Servisler Arası Olay Sözleşmesi
// =============================================================================
// 📚 EĞİTİCİ NOT (Tech-Tutor):
//
// NEDEN IntegrationEvent?
// → Microservice mimarisinde servisler birbirinin veritabanına doğrudan
//   erişemez. Servisler arası iletişim "Integration Event" ile sağlanır.
//
// Domain Event vs Integration Event:
// ┌─────────────────┬──────────────────────────────────────────────────┐
// │ Domain Event     │ Aynı servis içinde (in-process), MediatR ile   │
// │ Integration Event│ Farklı servisler arası, Message Broker ile     │
// └─────────────────┴──────────────────────────────────────────────────┘
//
// NASIL Çalışır?
// → Catalog servisi bir kitap oluşturduğunda:
//   1. Domain Event (BookCreatedDomainEvent) → aynı servis içinde işlenir
//   2. Integration Event (BookCreatedIntegrationEvent) → RabbitMQ'ya publish
//   3. Borrowing servisi bu event'i consume eder ve kendi veritabanını günceller
//
// ÖNEMLİ: Event'ler immutable olmalıdır, bu yüzden record kullanıyoruz.
// =============================================================================

namespace EventBus.Abstractions.Events;

/// <summary>
/// Servisler arası iletişim için kullanılan temel integration event.
/// Her event benzersiz bir ID ve oluşturulma zamanına sahiptir.
/// </summary>
public record IntegrationEvent
{
    /// <summary>Her event'in benzersiz kimliği — idempotency kontrolü için kullanılır</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>Event'in oluşturulma zamanı (UTC)</summary>
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}
