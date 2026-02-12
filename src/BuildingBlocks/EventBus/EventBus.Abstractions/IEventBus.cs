// =============================================================================
// IEventBus — Olay Veriyolu Arayüzü
// =============================================================================
// 📚 EĞİTİCİ NOT (Tech-Tutor):
//
// NEDEN IEventBus Arayüzü?
// → SOLID - Interface Segregation & Dependency Inversion
// → Servisler IEventBus'a bağımlıdır, RabbitMQ'ya değil.
//   Yarın RabbitMQ yerine Azure Service Bus kullanmak istersek,
//   sadece implementasyonu değiştiririz, servislere dokunmayız.
//
// NASIL Kullanılır?
// → Command Handler içinde:
//   await _eventBus.PublishAsync(new BookCreatedIntegrationEvent(book.Id));
// =============================================================================

using EventBus.Abstractions.Events;

namespace EventBus.Abstractions;

/// <summary>
/// Event Bus soyutlaması. 
/// Somut implementasyon (RabbitMQ, Kafka, vb.) bu arayüzü uygular.
/// </summary>
public interface IEventBus
{
    /// <summary>
    /// Bir integration event'i message broker'a publish eder.
    /// Tüm subscribe olan consumer'lar bu event'i alacaktır.
    /// </summary>
    Task PublishAsync<T>(T @event, CancellationToken cancellationToken = default)
        where T : IntegrationEvent;
}
