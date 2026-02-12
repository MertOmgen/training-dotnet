// =============================================================================
// Author — Yazar Value Object
// =============================================================================
// 📚 EĞİTİCİ NOT (Tech-Tutor):
//
// NEDEN Value Object?
// → Author burada bir Entity değil, Value Object olarak modellenmiştir.
//   Çünkü bu Bounded Context'te (Catalog) yazar bağımsız bir yaşam
//   döngüsüne sahip değildir — her zaman bir Book ile birlikte var olur.
//
// TASARIM KARARI:
// → Farklı bir Bounded Context'te (örneğin "Author Management Service")
//   Author bir Entity veya AggregateRoot olabilirdi.
//   Bu, DDD'nin "aynı kavram, farklı context'lerde farklı model" ilkesidir.
//
// IMMUTABILITY:
// → Value Object'ler değiştirilemez. Yeni bir değer gerektiğinde
//   yeni bir nesne oluşturulur (functional programming prensibi).
// → private set kullanılır ve Create factory metodu ile validasyon zorlanır.
// =============================================================================

using SharedKernel.Domain;

namespace Catalog.Domain.ValueObjects;

/// <summary>
/// Kitap yazarını temsil eden Value Object.
/// Ad ve Soyadı eşit olan iki Author eşit kabul edilir (kimlik yok).
/// </summary>
public class Author : ValueObject
{
    /// <summary>Yazar adı</summary>
    public string FirstName { get; private set; }

    /// <summary>Yazar soyadı</summary>
    public string LastName { get; private set; }

    /// <summary>Tam ad (hesaplanan özellik)</summary>
    public string FullName => $"{FirstName} {LastName}";

    // ─────────────────────────────────────────────────────────────
    // Private Constructor + Factory Method (Create)
    // → Constructor private olduğu için dışarıdan new Author() yapılamaz.
    //   Tek yol Create() metodu üzerinden geçmektir.
    //   Bu sayede validasyon her zaman uygulanır.
    // ─────────────────────────────────────────────────────────────
    private Author(string firstName, string lastName)
    {
        FirstName = firstName;
        LastName = lastName;
    }

    /// <summary>
    /// Validasyonlu Author oluşturma factory metodu.
    /// Hata varsa Result.Failure döner, başarılıysa Result.Success döner.
    /// </summary>
    public static Result<Author> Create(string firstName, string lastName)
    {
        if (string.IsNullOrWhiteSpace(firstName))
            return Result<Author>.Failure("Yazar adı boş olamaz.");

        if (string.IsNullOrWhiteSpace(lastName))
            return Result<Author>.Failure("Yazar soyadı boş olamaz.");

        if (firstName.Length > 100)
            return Result<Author>.Failure("Yazar adı 100 karakteri aşamaz.");

        if (lastName.Length > 100)
            return Result<Author>.Failure("Yazar soyadı 100 karakteri aşamaz.");

        return Result<Author>.Success(new Author(firstName.Trim(), lastName.Trim()));
    }

    /// <summary>
    /// ValueObject eşitlik karşılaştırması için bileşenler.
    /// FirstName + LastName eşitse → iki Author eşittir.
    /// </summary>
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return FirstName.ToLowerInvariant();
        yield return LastName.ToLowerInvariant();
    }
}
