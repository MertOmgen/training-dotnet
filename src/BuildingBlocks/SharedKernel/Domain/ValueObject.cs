// =============================================================================
// ValueObject — DDD Değer Nesnesi
// =============================================================================
// 📚 EĞİTİCİ NOT (Tech-Tutor):
//
// NEDEN ValueObject?
// → Entity'lerden farklı olarak, Value Object'lerin kimliği yoktur.
//   İki Value Object, tüm özellikleri eşitse eşit kabul edilir.
//
// ÖRNEK:
// → "Adres" bir Value Object'tir. İki adres aynı sokak/şehir/posta koduna
//   sahipse eşittir — ayrı bir ID'ye ihtiyaç yoktur.
// → "Para" (Money) bir Value Object'tir: 100 TL = 100 TL, farklı ID yok.
// → "Author" da bu projede bir Value Object olarak modellenmiştir:
//   Ad ve Soyadı aynı olan iki yazar aynı kabul edilir.
//
// NASIL Çalışır?
// → GetEqualityComponents() metodu, eşitlik karşılaştırması için
//   kullanılacak tüm özellikleri döndürür.
// → Alt sınıflar bu metodu override ederek kendi özelliklerini tanımlar.
//
// ÖNEMLİ:
// → Value Object'ler immutable (değiştirilemez) olmalıdır.
//   Yeni bir değer gerektiğinde yeni bir nesne oluşturulur.
// =============================================================================

namespace SharedKernel.Domain;

/// <summary>
/// Tüm Value Object'lerin türediği base class.
/// Eşitlik, kimlik (Id) yerine tüm özellikler üzerinden hesaplanır.
/// </summary>
public abstract class ValueObject : IEquatable<ValueObject>
{
    /// <summary>
    /// Eşitlik karşılaştırmasında kullanılacak bileşenleri döndürür.
    /// Her alt sınıf kendi özelliklerini burada listeler.
    /// </summary>
    protected abstract IEnumerable<object?> GetEqualityComponents();

    public bool Equals(ValueObject? other)
    {
        if (other is null) return false;
        if (GetType() != other.GetType()) return false;

        return GetEqualityComponents()
            .SequenceEqual(other.GetEqualityComponents());
    }

    public override bool Equals(object? obj) => Equals(obj as ValueObject);

    public override int GetHashCode()
    {
        return GetEqualityComponents()
            .Aggregate(0, (hash, component) =>
                HashCode.Combine(hash, component?.GetHashCode() ?? 0));
    }

    public static bool operator ==(ValueObject? left, ValueObject? right)
        => Equals(left, right);

    public static bool operator !=(ValueObject? left, ValueObject? right)
        => !Equals(left, right);
}
