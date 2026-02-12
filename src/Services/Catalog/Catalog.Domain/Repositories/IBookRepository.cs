// =============================================================================
// IBookRepository — Kitap Repository Arayüzü
// =============================================================================
// 📚 EĞİTİCİ NOT (Tech-Tutor):
//
// NEDEN Repository Pattern?
// → Domain katmanı veritabanı teknolojisinden habersiz olmalıdır.
//   Bu arayüz Domain'de tanımlanır, implementasyonu Infrastructure'dadır.
//   Bu, Dependency Inversion Principle (SOLID-D) uygulamasıdır.
//
// Repository vs DbContext Doğrudan Kullanımı:
// → DbContext doğrudan kullanmak mümkündür ancak:
//   - Domain katmanı EF Core'a bağımlı olur (Clean Architecture ihlali)
//   - Unit test'lerde mock'lama zorlaşır
//   - Aggregate sınırları bulanıklaşır
//
// KURAL: Her Aggregate Root için bir Repository
// → Book AggregateRoot → IBookRepository
// → Author bir Value Object olduğu için ayrı repository'si yoktur
// =============================================================================

using Catalog.Domain.Entities;

namespace Catalog.Domain.Repositories;

/// <summary>
/// Kitap Aggregate Root için repository arayüzü.
/// Write DB (PostgreSQL) ile etkileşim kurar.
/// </summary>
public interface IBookRepository
{
    /// <summary>ID ile kitap getir</summary>
    Task<Book?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>ISBN ile kitap getir</summary>
    Task<Book?> GetByIsbnAsync(string isbn, CancellationToken cancellationToken = default);

    /// <summary>Tüm aktif kitapları getir (sayfalama ile)</summary>
    Task<IReadOnlyList<Book>> GetAllAsync(
        int page = 1, int pageSize = 20,
        CancellationToken cancellationToken = default);

    /// <summary>Yeni kitap ekle</summary>
    Task AddAsync(Book book, CancellationToken cancellationToken = default);

    /// <summary>Kitap güncelle</summary>
    void Update(Book book);

    /// <summary>
    /// Unit of Work pattern — tüm değişiklikleri tek seferde kaydet.
    /// EĞİTİCİ NOT: SaveChanges burada çünkü transaction sınırı
    /// Aggregate sınırı ile aynı olmalıdır.
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
