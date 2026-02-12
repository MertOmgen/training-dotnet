// =============================================================================
// Entity Base Class — Tüm Domain Varlıklarının Atası
// =============================================================================
// 📚 EĞİTİCİ NOT (Tech-Tutor):
//
// NEDEN Entity Base Class?
// → DDD'de her varlık (entity) benzersiz bir kimliğe (identity) sahiptir.
//   İki entity, aynı özelliklere sahip olsa bile farklı kimliklerle
//   farklı nesneler olarak kabul edilir.
//   Örnek: İki kitabın adı aynı olabilir ama ISBN'leri farklıdır.
//
// NASIL Çalışır?
// → Generic <TId> ile kimlik tipi esnekliği sağlanır (Guid, int, string...).
// → Domain Event'ler bu base class'ta toplanır ve daha sonra dispatch edilir.
// → Equality karşılaştırması kimlik (Id) üzerinden yapılır.
//
// ALTERNATİF:
// → Bazı projeler Entity'yi record olarak tanımlar. Ancak record'lar
//   immutable olduğundan, mutable domain nesneleri için class tercih edilir.
// =============================================================================

namespace SharedKernel.Domain;

/// <summary>
/// Tüm domain entity'lerinin türediği base class.
/// Her entity benzersiz bir kimliğe ve domain event listesine sahiptir.
/// </summary>
/// <typeparam name="TId">Kimlik (Identity) tipi — genellikle Guid kullanılır</typeparam>
public abstract class Entity<TId> : IEquatable<Entity<TId>>
    where TId : notnull
{
    // -------------------------------------------------------------------------
    // Domain Events Listesi
    // -------------------------------------------------------------------------
    // EĞİTİCİ NOT:
    // Domain Event'ler, entity üzerinde gerçekleşen önemli olayları temsil eder.
    // Bu olaylar entity içinde biriktirilir (raise edilir) ve daha sonra
    // bir event dispatcher tarafından işlenir.
    // Bu yaklaşıma "Deferred Domain Events" denir.
    // -------------------------------------------------------------------------
    private readonly List<IDomainEvent> _domainEvents = [];

    /// <summary>Entity'nin benzersiz kimliği</summary>
    public TId Id { get; protected set; }

    /// <summary>Oluşturulma tarihi — audit trail için</summary>
    public DateTime CreatedAt { get; protected set; }

    /// <summary>Son güncelleme tarihi — audit trail için</summary>
    public DateTime? UpdatedAt { get; protected set; }

    protected Entity(TId id)
    {
        Id = id;
        CreatedAt = DateTime.UtcNow;
    }

    // EF Core için parametresiz constructor (reflection ile nesne oluşturur)
    protected Entity() { Id = default!; }

    // -------------------------------------------------------------------------
    // Domain Event Yönetimi
    // -------------------------------------------------------------------------

    /// <summary>Bekleyen domain event'lerin salt-okunur listesi</summary>
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    /// <summary>
    /// Yeni bir domain event ekler (raise eder).
    /// Event henüz dispatch edilmez — SaveChanges sırasında dispatch edilir.
    /// </summary>
    protected void RaiseDomainEvent(IDomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
    }

    /// <summary>
    /// Tüm domain event'leri temizler.
    /// Dispatch sonrası çağrılır.
    /// </summary>
    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }

    // -------------------------------------------------------------------------
    // Equality — Kimlik Tabanlı Karşılaştırma
    // -------------------------------------------------------------------------
    // EĞİTİCİ NOT:
    // Entity'ler kimlik (Id) ile karşılaştırılır, özellik değerleri ile değil.
    // Bu, DDD'nin temel kurallarından biridir.
    // -------------------------------------------------------------------------

    public bool Equals(Entity<TId>? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return EqualityComparer<TId>.Default.Equals(Id, other.Id);
    }

    public override bool Equals(object? obj) => Equals(obj as Entity<TId>);

    public override int GetHashCode() => EqualityComparer<TId>.Default.GetHashCode(Id);

    public static bool operator ==(Entity<TId>? left, Entity<TId>? right)
        => Equals(left, right);

    public static bool operator !=(Entity<TId>? left, Entity<TId>? right)
        => !Equals(left, right);
}
