// =============================================================================
// BookCreatedDomainEvent — Kitap Oluşturuldu Domain Olayı
// =============================================================================
// 📚 EĞİTİCİ NOT (Tech-Tutor):
//
// Bu domain event, Book.Create() factory metodu çağrıldığında raise edilir.
// Handler'da yapılabilecek işlemler:
// 1. Integration Event publish (RabbitMQ → diğer servisler)
// 2. Elasticsearch'e kitap verisi senkronize etme
// 3. Outbox tablosuna mesaj yazma (Outbox Pattern)
//
// NEDEN Record?
// → Event'ler immutable (değiştirilemez) olmalıdır. C# record tipi
//   bu garantiyi sağlar (init-only properties + value equality).
// =============================================================================

using SharedKernel.Domain;

namespace Catalog.Domain.Events;

/// <summary>
/// Yeni kitap oluşturulduğunda raise edilen domain event.
/// </summary>
public sealed record BookCreatedDomainEvent(
    Guid BookId,
    string Title,
    string Isbn,
    string AuthorName) : DomainEventBase;
