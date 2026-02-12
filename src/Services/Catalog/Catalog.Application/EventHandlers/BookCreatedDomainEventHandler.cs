// =============================================================================
// BookCreatedDomainEventHandler — Domain Event'i Integration Event'e Çevirme
// =============================================================================
// 📚 EĞİTİCİ NOT (Tech-Tutor):
//
// NEDEN İki Aşamalı Event?
// → Domain Event (in-process) → Integration Event (cross-process)
//   Bu ayrım şunları sağlar:
//   1. Domain katmanı message broker'dan habersiz kalır (Clean Architecture)
//   2. Integration Event publish başarısız olursa domain işlemi etkilenmez
//      (Outbox Pattern ile garanti edilir)
//
// Outbox Pattern:
// → Integration Event direkt RabbitMQ'ya publish edilmez!
//   Önce Outbox tablosuna yazılır (aynı transaction içinde).
//   Ayrı bir background job Outbox'taki mesajları RabbitMQ'ya gönderir.
//   Bu, "at-least-once delivery" garantisi sağlar.
//
// ┌────────────────────────────────────────────────────────────────────┐
// │ OUTBOX PATTERN AKIŞI:                                              │
// │                                                                    │
// │ 1. Book + OutboxMessage → aynı transaction → PostgreSQL            │
// │ 2. Background Job → Outbox tablosunu tarar                        │
// │ 3. İşlenmemiş mesajları RabbitMQ'ya publish eder                  │
// │ 4. Başarılı olanları "processed" olarak işaretler                 │
// │                                                                    │
// │ Bu sayede: DB kaydı başarılı + RabbitMQ publish başarısız          │
// │ durumunda bile mesaj kaybolmaz!                                    │
// └────────────────────────────────────────────────────────────────────┘
// =============================================================================

using Catalog.Application.IntegrationEvents;
using Catalog.Domain.Events;
using EventBus.Abstractions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Catalog.Application.EventHandlers;

/// <summary>
/// BookCreatedDomainEvent handler'ı.
/// Domain event'i integration event'e çevirir ve publish eder.
/// </summary>
public class BookCreatedDomainEventHandler : INotificationHandler<BookCreatedDomainEvent>
{
    private readonly IEventBus _eventBus;
    private readonly ILogger<BookCreatedDomainEventHandler> _logger;

    public BookCreatedDomainEventHandler(
        IEventBus eventBus,
        ILogger<BookCreatedDomainEventHandler> logger)
    {
        _eventBus = eventBus;
        _logger = logger;
    }

    public async Task Handle(
        BookCreatedDomainEvent notification,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "[DomainEvent] BookCreatedDomainEvent işleniyor. BookId: {BookId}, Title: {Title}",
            notification.BookId, notification.Title);

        // ─────────────────────────────────────────────────────
        // Domain Event → Integration Event dönüşümü
        // → Domain Event'in tüm bilgileri Integration Event'e kopyalanmaz!
        //   Sadece diğer servislerin ihtiyaç duyduğu bilgiler gönderilir.
        //   Bu, servisler arası mini "contract" (sözleşme) tanımlar.
        // ─────────────────────────────────────────────────────
        var integrationEvent = new BookCreatedIntegrationEvent(
            notification.BookId,
            notification.Title,
            notification.Isbn,
            notification.AuthorName,
            Category: "Genel", // Domain event'te kategori yok → varsayılan
            TotalCopies: 0);   // Domain event'te kopya sayısı yok

        // ─────────────────────────────────────────────────────
        // Integration Event → RabbitMQ'ya publish
        // → Production'da burada Outbox Pattern kullanılmalıdır:
        //   _outboxRepository.Add(new OutboxMessage(integrationEvent));
        //   Background job ile asenkron publish edilir.
        // ─────────────────────────────────────────────────────
        await _eventBus.PublishAsync(integrationEvent, cancellationToken);

        _logger.LogInformation(
            "[IntegrationEvent] BookCreatedIntegrationEvent publish edildi. BookId: {BookId}",
            notification.BookId);
    }
}
