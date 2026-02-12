// =============================================================================
// BookCreatedIntegrationEvent — Servisler Arası Kitap Oluşturulma Olayı
// =============================================================================
// 📚 EĞİTİCİ NOT (Tech-Tutor):
//
// Bu event, BookCreatedDomainEvent handler'ı tarafından publish edilir.
// RabbitMQ üzerinden diğer servislere yayınlanır.
//
// Akış:
// Book.Create() → BookCreatedDomainEvent (raise)
//   ↓ (MediatR dispatch)
// BookCreatedDomainEventHandler → BookCreatedIntegrationEvent (publish)
//   ↓ (RabbitMQ)
// Borrowing Service (consume) → Kendi veritabanını günceller
// Notification Service (consume) → Bildirim gönderir
// =============================================================================

using EventBus.Abstractions.Events;

namespace Catalog.Application.IntegrationEvents;

/// <summary>
/// Kitap oluşturulduğunda RabbitMQ'ya publish edilen integration event.
/// Borrowing ve Notification servisleri bu event'i consume eder.
/// </summary>
public record BookCreatedIntegrationEvent(
    Guid BookId,
    string Title,
    string Isbn,
    string AuthorName,
    string Category,
    int TotalCopies) : IntegrationEvent;
