// =============================================================================
// AggregateRoot — DDD Aggregate Kökü
// =============================================================================
// 📚 EĞİTİCİ NOT (Tech-Tutor):
//
// NEDEN AggregateRoot?
// → DDD'de Aggregate, birbiriyle ilişkili entity ve value object'lerin
//   bir bütün olarak yönetildiği kümelemedir.
// → AggregateRoot, bu kümenin "giriş noktası"dır. Dış dünya ile
//   yalnızca AggregateRoot üzerinden iletişim kurulur.
//
// ÖRNEK:
// → Book (AggregateRoot) + Author (ValueObject) bir Aggregate oluşturur.
// → Dışarıdan doğrudan Author'a erişilmez, Book üzerinden erişilir.
//
// KURAL:
// → Her Aggregate, kendi transaction sınırını tanımlar.
// → Bir Aggregate içindeki tüm değişiklikler tek bir transaction'da yapılır.
// → Aggregate'ler arası iletişim ise Domain Event'ler ile sağlanır.
//
// ALTERNATİF:
// → Bazı projeler AggregateRoot'u ayrı bir interface (IAggregateRoot) olarak
//   tanımlar. Ancak base class yaklaşımı, domain event yönetimini
//   merkezileştirdiği için daha pratiktir.
// =============================================================================

namespace SharedKernel.Domain;

/// <summary>
/// Aggregate köklerinin türediği base class.
/// Entity'den türer ve aggregate sınırını temsil eder.
/// </summary>
/// <typeparam name="TId">Kimlik tipi</typeparam>
public abstract class AggregateRoot<TId> : Entity<TId>
    where TId : notnull
{
    /// <summary>
    /// Optimistic Concurrency için versiyon numarası.
    /// EF Core bu değeri her güncelleme sırasında otomatik artırır
    /// ve eş zamanlı yazma çakışmalarını tespit eder.
    /// </summary>
    public int Version { get; protected set; }

    protected AggregateRoot(TId id) : base(id) { }

    // EF Core için parametresiz constructor
    protected AggregateRoot() : base() { }
}
