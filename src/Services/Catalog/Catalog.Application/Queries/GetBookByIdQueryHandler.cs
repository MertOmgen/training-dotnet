// =============================================================================
// GetBookByIdQueryHandler — Kitap Sorgulama İşleyicisi
// =============================================================================
// 📚 EĞİTİCİ NOT (Tech-Tutor):
//
// Query Akışı (Read Path):
// ┌─────────┐   ┌──────────────┐   ┌────────────────────┐   ┌──────────┐
// │ Request │ → │CacheBehavior │ → │GetBookByIdHandler  │ → │ MongoDB  │
// └─────────┘   └──────────────┘   └────────────────────┘   └──────────┘
//                  ↓ (Cache HIT)
//              ┌──────────┐
//              │  Redis   │ → Doğrudan döner, MongoDB'ye gitmez
//              └──────────┘
//
// IReadDbContext:
// → Query handler'lar Write DB'ye (EF Core) erişmez!
//   Read DB (MongoDB) veya Elasticsearch ile çalışır.
//   Bu, CQRS'nin en önemli kuralıdır.
//
// NEDEN MongoDB Read DB?
// → Write DB'de normalize veri saklanır (3NF)
//   Read DB'de denormalize veri saklanır (sorgu optimizasyonu)
//   Örnek: Book + Author bilgileri tek bir dokümanda saklanır
// =============================================================================

using Catalog.Application.DTOs;
using MediatR;
using MongoDB.Driver;

namespace Catalog.Application.Queries;

/// <summary>
/// Read DB (MongoDB) üzerinden kitap sorgulama handler'ı.
/// CachingBehavior ile Redis cache desteği otomatik sağlanır.
/// </summary>
public class GetBookByIdQueryHandler : IRequestHandler<GetBookByIdQuery, BookDto?>
{
    private readonly IMongoCollection<BookDto> _booksCollection;

    public GetBookByIdQueryHandler(IMongoDatabase mongoDatabase)
    {
        // ─────────────────────────────────────────────────────
        // MongoDB Collection Erişimi
        // → Koleksiyon adı: "books"
        // → MongoDB'de koleksiyonlar otomatik oluşturulur (schema-less)
        // → BookDto doğrudan MongoDB'den okunur — ekstra mapping gerekmez
        // ─────────────────────────────────────────────────────
        _booksCollection = mongoDatabase.GetCollection<BookDto>("books");
    }

    public async Task<BookDto?> Handle(
        GetBookByIdQuery request,
        CancellationToken cancellationToken)
    {
        // ─────────────────────────────────────────────────────
        // MongoDB Sorgusu
        // → Builder pattern ile type-safe filtre oluşturulur
        // → SQL karşılığı: SELECT * FROM books WHERE Id = @id
        // → FirstOrDefaultAsync: Tek sonuç döner, yoksa null
        // ─────────────────────────────────────────────────────
        var filter = Builders<BookDto>.Filter.Eq(x => x.Id, request.BookId);

        return await _booksCollection
            .Find(filter)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
