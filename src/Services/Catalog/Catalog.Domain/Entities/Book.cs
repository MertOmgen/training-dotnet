// =============================================================================
// Book — Kitap Aggregate Root
// =============================================================================
// 📚 EĞİTİCİ NOT (Tech-Tutor):
//
// NEDEN AggregateRoot?
// → Book, Catalog Bounded Context'inin ana Aggregate'idir.
//   Tüm kitap işlemleri (oluşturma, güncelleme, silme) bu sınıf
//   üzerinden yapılır. Dışarıdan doğrudan alt nesnelere erişilmez.
//
// AGGREGATE KURALLARI:
// 1. Dış dünya ile yalnızca AggregateRoot iletişim kurar
// 2. Bir Aggregate içindeki tüm değişiklikler tek bir transaction'da yapılır
// 3. Aggregate'ler arası referanslar ID ile yapılır (nesne referansı değil)
//
// DOMAIN EVENT'LER:
// → Book oluşturulduğunda BookCreatedDomainEvent raise edilir.
// → Bu event'in handler'ı, Integration Event publish edebilir (RabbitMQ'ya).
//
// ENCAPSULATION (Kapsülleme):
// → Property'ler private set ile korunur.
// → Değişiklikler sadece domain metotları ile yapılabilir.
//   Örnek: book.UpdateDetails(...) — doğrudan book.Title = "..." yapılamaz.
//   Bu, iş kurallarının her zaman uygulanmasını garanti eder.
// =============================================================================

using Catalog.Domain.Events;
using Catalog.Domain.ValueObjects;
using SharedKernel.Domain;

namespace Catalog.Domain.Entities;

/// <summary>
/// Kitap Aggregate Root — Catalog Bounded Context'inin ana entity'si.
/// Tüm kitap işlemleri bu sınıf üzerinden yönetilir.
/// </summary>
public class Book : AggregateRoot<Guid>
{
    /// <summary>Kitap başlığı</summary>
    public string Title { get; private set; } = null!;

    /// <summary>ISBN numarası — kitabın benzersiz uluslararası tanımlayıcısı</summary>
    public string Isbn { get; private set; } = null!;

    /// <summary>Kitap açıklaması</summary>
    public string? Description { get; private set; }

    /// <summary>Yazar bilgisi (Value Object)</summary>
    public Author Author { get; private set; } = null!;

    /// <summary>Yayınlanma yılı</summary>
    public int PublishedYear { get; private set; }

    /// <summary>Kategori</summary>
    public string Category { get; private set; } = null!;

    /// <summary>Toplam kopya sayısı</summary>
    public int TotalCopies { get; private set; }

    /// <summary>Mevcut (ödünç verilmemiş) kopya sayısı</summary>
    public int AvailableCopies { get; private set; }

    /// <summary>Kitap aktif mi? (soft delete için)</summary>
    public bool IsActive { get; private set; } = true;

    // EF Core için parametresiz constructor
    private Book() : base() { }

    // ─────────────────────────────────────────────────────────────
    // Private Constructor + Factory Method
    // → new Book() dışarıdan çağrılamaz. Tek yol: Book.Create()
    //   Bu pattern'e "Rich Domain Model" denir.
    //   Anemic Domain Model'de (anti-pattern) tüm property'ler public'tir
    //   ve iş mantığı servis katmanına taşınır.
    // ─────────────────────────────────────────────────────────────
    private Book(
        Guid id, string title, string isbn, string? description,
        Author author, int publishedYear, string category, int totalCopies)
        : base(id)
    {
        Title = title;
        Isbn = isbn;
        Description = description;
        Author = author;
        PublishedYear = publishedYear;
        Category = category;
        TotalCopies = totalCopies;
        AvailableCopies = totalCopies; // Yeni kitap → tüm kopyalar mevcut

        // ─────────────────────────────────────────────────────
        // Domain Event Raise
        // → Kitap oluşturulduğunda BookCreatedDomainEvent fırlatılır.
        // → Bu event henüz dispatch edilmez!
        //   SaveChanges sırasında MediatR ile dispatch edilir.
        // → Handler'da: Integration Event publish edilebilir (RabbitMQ'ya)
        // ─────────────────────────────────────────────────────
        RaiseDomainEvent(new BookCreatedDomainEvent(Id, Title, Isbn, Author.FullName));
    }

    /// <summary>
    /// Yeni kitap oluşturma factory metodu.
    /// Tüm validasyonlar burada yapılır.
    /// </summary>
    public static Result<Book> Create(
        string title, string isbn, string? description,
        Author author, int publishedYear, string category, int totalCopies)
    {
        // ─────────────────────────────────────────────────────
        // Domain Validasyonları
        // → İş kuralları Domain katmanında uygulanır.
        //   Application katmanında (FluentValidation) ise
        //   input validasyonları yapılır (boşluk, format vb.).
        //   İkisi farklı şeylerdir ve ikisi de gereklidir.
        // ─────────────────────────────────────────────────────
        if (string.IsNullOrWhiteSpace(title))
            return Result<Book>.Failure("Kitap başlığı boş olamaz.");

        if (string.IsNullOrWhiteSpace(isbn))
            return Result<Book>.Failure("ISBN boş olamaz.");

        if (publishedYear < 1000 || publishedYear > DateTime.UtcNow.Year + 1)
            return Result<Book>.Failure($"Geçersiz yayın yılı: {publishedYear}");

        if (totalCopies < 0)
            return Result<Book>.Failure("Kopya sayısı negatif olamaz.");

        if (string.IsNullOrWhiteSpace(category))
            return Result<Book>.Failure("Kategori boş olamaz.");

        var book = new Book(
            Guid.NewGuid(), title.Trim(), isbn.Trim(), description?.Trim(),
            author, publishedYear, category.Trim(), totalCopies);

        return Result<Book>.Success(book);
    }

    /// <summary>
    /// Kitap bilgilerini günceller.
    /// Domain metodu — iş kuralları korunur.
    /// </summary>
    public Result UpdateDetails(string title, string? description, string category)
    {
        if (string.IsNullOrWhiteSpace(title))
            return Result.Failure("Kitap başlığı boş olamaz.");

        Title = title.Trim();
        Description = description?.Trim();
        Category = category.Trim();
        UpdatedAt = DateTime.UtcNow;

        return Result.Success();
    }

    /// <summary>
    /// Kitap ödünç verildiğinde çağrılır.
    /// Mevcut kopya sayısını azaltır.
    /// </summary>
    public Result Borrow()
    {
        // ─────────────────────────────────────────────────────
        // İş Kuralı: Mevcut kopya yoksa ödünç verilemez!
        // Bu kural Domain katmanında korunur, başka hiçbir yerde değil.
        // ─────────────────────────────────────────────────────
        if (AvailableCopies <= 0)
            return Result.Failure("Bu kitabın mevcut kopyası bulunmamaktadır.");

        AvailableCopies--;
        UpdatedAt = DateTime.UtcNow;
        return Result.Success();
    }

    /// <summary>
    /// Kitap iade edildiğinde çağrılır.
    /// Mevcut kopya sayısını artırır.
    /// </summary>
    public Result Return()
    {
        if (AvailableCopies >= TotalCopies)
            return Result.Failure("Tüm kopyalar zaten mevcut.");

        AvailableCopies++;
        UpdatedAt = DateTime.UtcNow;
        return Result.Success();
    }

    /// <summary>Soft delete — kitabı pasif yapar</summary>
    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }
}
