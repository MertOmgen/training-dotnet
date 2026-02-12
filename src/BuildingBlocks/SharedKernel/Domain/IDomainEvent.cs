// =============================================================================
// IDomainEvent — Domain Olayı Arayüzü
// =============================================================================
// 📚 EĞİTİCİ NOT (Tech-Tutor):
//
// NEDEN Domain Event?
// → Domain Event'ler, iş alanında (domain) gerçekleşen önemli olayları
//   temsil eder. ("Bir kitap oluşturuldu", "Bir kitap ödünç alındı" gibi)
//
// Domain Event vs Integration Event FARKI:
// → Domain Event: Aynı Bounded Context (servis) içinde kullanılır.
//   Genellikle aynı transaction içinde işlenir.
// → Integration Event: Farklı servisler arası iletişim için kullanılır.
//   RabbitMQ gibi message broker üzerinden yayınlanır.
//
// NASIL Çalışır?
// → Entity üzerinde RaiseDomainEvent() ile olay kayıt edilir.
// → SaveChanges sırasında MediatR ile dispatch edilir (in-process).
// → Gerekirse, domain event handler'ı bir integration event publish edebilir.
//
// MediatR INotification:
// → MediatR'ın INotification arayüzü, publish/subscribe (pub/sub) pattern'i
//   uygular. Bir event publish edildiğinde, tüm handler'lar çağrılır.
// =============================================================================

using MediatR;

namespace SharedKernel.Domain;

/// <summary>
/// Tüm domain event'lerin uyguladığı arayüz.
/// MediatR INotification ile dispatch desteği sağlar.
/// </summary>
public interface IDomainEvent : INotification
{
    /// <summary>Olayın oluşturulma zamanı</summary>
    DateTime OccurredOn { get; }
}

/// <summary>
/// Domain event'ler için base record.
/// Record kullanıyoruz çünkü event'ler immutable (değiştirilemez) olmalıdır.
/// </summary>
public abstract record DomainEventBase : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}
