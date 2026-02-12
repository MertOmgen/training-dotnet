// =============================================================================
// BookRepository — IBookRepository Implementasyonu
// =============================================================================
// 📚 EĞİTİCİ NOT (Tech-Tutor):
//
// Bu sınıf Domain katmanında tanımlanan IBookRepository arayüzünü uygular.
// EF Core'un DbContext'i üzerinden PostgreSQL ile iletişim kurar.
//
// Repository Pattern Kuralları:
// → Sadece Aggregate Root için repository vardır (Book)
// → Repository, Aggregate'in tüm yaşam döngüsünü yönetir
// → LINQ sorguları burada soyutlanır — handler'lar LINQ bilmek zorunda değil
//
// Unit of Work:
// → SaveChangesAsync() metodu burada expose edilmiştir.
//   Ancak gerçek Unit of Work, CatalogDbContext tarafından yönetilir.
//   Repository'nin sorumluluğu: CRUD operasyonları soyutlamak.
// =============================================================================

using Catalog.Domain.Entities;
using Catalog.Domain.Repositories;
using Catalog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Infrastructure.Repositories;

/// <summary>
/// EF Core tabanlı IBookRepository implementasyonu.
/// PostgreSQL (Write DB) ile çalışır.
/// </summary>
public class BookRepository : IBookRepository
{
    private readonly CatalogDbContext _dbContext;

    public BookRepository(CatalogDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Book?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // ─────────────────────────────────────────────────────
        // FindAsync: Primary Key ile sorgu (en hızlı yol).
        // EF Core önce Change Tracker'da arar, yoksa veritabanına gider.
        // ─────────────────────────────────────────────────────
        return await _dbContext.Books.FindAsync([id], cancellationToken);
    }

    public async Task<Book?> GetByIsbnAsync(string isbn, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Books
            .FirstOrDefaultAsync(b => b.Isbn == isbn, cancellationToken);
    }

    public async Task<IReadOnlyList<Book>> GetAllAsync(
        int page = 1, int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        // ─────────────────────────────────────────────────────
        // Sayfalama (Pagination):
        // → Skip((page-1) * pageSize) → ilk N kaydı atla
        // → Take(pageSize) → sonraki N kaydı al
        //
        // AsNoTracking():
        // → Okuma sorgularında Change Tracker devre dışı bırakılır.
        //   Bu, bellek ve performans optimizasyonudur.
        //   Çünkü bu kayıtlar üzerinde güncelleme yapılmayacak.
        // ─────────────────────────────────────────────────────
        return await _dbContext.Books
            .AsNoTracking()
            .Where(b => b.IsActive)
            .OrderByDescending(b => b.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Book book, CancellationToken cancellationToken = default)
    {
        await _dbContext.Books.AddAsync(book, cancellationToken);
    }

    public void Update(Book book)
    {
        // ─────────────────────────────────────────────────────
        // EF Core Change Tracking:
        // → Entity zaten tracked ise Update çağrısına gerek yoktur.
        //   Entity üzerindeki property değişiklikleri SaveChanges'te
        //   otomatik algılanır.
        // → Bu metot, detached entity (yeniden yüklenen) senaryoları içindir.
        // ─────────────────────────────────────────────────────
        _dbContext.Books.Update(book);
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
