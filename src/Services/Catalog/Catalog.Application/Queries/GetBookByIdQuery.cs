// =============================================================================
// GetBookByIdQuery — ID ile Kitap Sorgulama (CQRS - Query Tarafı)
// =============================================================================
// 📚 EĞİTİCİ NOT (Tech-Tutor):
//
// CQRS Query Tarafı:
// → Command'lar Write DB'ye (PostgreSQL) yazar.
// → Query'ler Read DB'den (MongoDB) okur.
//   Bu ayrım, okuma ve yazma işlemlerini bağımsız olarak ölçeklemeyi sağlar.
//
// ICacheableQuery:
// → Bu query, CachingBehavior tarafından otomatik cache'lenir.
//   Aynı ID ile ikinci istek geldiğinde MongoDB'ye gidilmez,
//   Redis cache'ten döner — bu büyük bir performans kazancıdır.
//
// CacheKey Tasarımı:
// → "catalog:books:{id}" formatı kullanılır.
//   Prefix ("catalog:books:") ile tüm kitap cache'leri 
//   tek seferde temizlenebilir (invalidation).
// =============================================================================

using Catalog.Application.DTOs;
using Caching.Redis;
using MediatR;

namespace Catalog.Application.Queries;

/// <summary>
/// ID ile kitap sorgulama. Read DB (MongoDB) kullanır.
/// ICacheableQuery ile otomatik Redis cache desteği.
/// </summary>
public record GetBookByIdQuery(Guid BookId) : IRequest<BookDto?>, ICacheableQuery
{
    /// <summary>Cache anahtarı — her kitap için benzersiz</summary>
    public string CacheKey => $"catalog:books:{BookId}";

    /// <summary>Cache süresi — 10 dakika</summary>
    public TimeSpan? CacheDuration => TimeSpan.FromMinutes(10);
}
