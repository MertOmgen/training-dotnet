// =============================================================================
// CatalogDbContext — EF Core Write DB (PostgreSQL)
// =============================================================================
// 📚 EĞİTİCİ NOT (Tech-Tutor):
//
// NEDEN EF Core?
// → .NET dünyasının standart ORM'idir (Object-Relational Mapper).
//   Domain entity'lerini doğrudan PostgreSQL tablolarına eşler.
//   SQL yazmadan CRUD işlemleri yapabilirsiniz.
//
// DbContext Sorumlulukları:
// 1. Entity-tablo eşlemesi (OnModelCreating)
// 2. Change Tracking (hangi entity değişti?)
// 3. Unit of Work pattern (SaveChanges = tek transaction)
// 4. Domain Event dispatch (SaveChanges override)
//
// NEDEN SaveChanges Override?
// → Domain Event'ler, entity'de biriktirilir (deferred dispatch).
//   SaveChanges çağrıldığında:
//   1. Değişiklikler veritabanına yazılır
//   2. Domain Event'ler MediatR ile dispatch edilir
//   Bu sıralama, "eventual consistency" sağlar.
//
// ÖNEMLİ: Bu DbContext SADECE Write işlemleri için kullanılır!
// Read işlemleri MongoDB veya Elasticsearch üzerinden yapılır (CQRS).
// =============================================================================

using Catalog.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Domain;

namespace Catalog.Infrastructure.Persistence;

/// <summary>
/// EF Core DbContext — PostgreSQL Write DB.
/// Domain Event dispatch ve Outbox Pattern desteği içerir.
/// </summary>
public class CatalogDbContext : DbContext
{
    private readonly IMediator _mediator;

    public CatalogDbContext(
        DbContextOptions<CatalogDbContext> options,
        IMediator mediator) : base(options)
    {
        _mediator = mediator;
    }

    /// <summary>Kitaplar tablosu</summary>
    public DbSet<Book> Books => Set<Book>();

    /// <summary>Outbox mesajları tablosu (Outbox Pattern)</summary>
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // ─────────────────────────────────────────────────────
        // Fluent API ile Entity Konfigürasyonu
        // → Data Annotations ([Table], [Column]) yerine Fluent API tercih edilir.
        //   Çünkü: Domain entity'ye altyapı attribute'ları eklemek
        //   Clean Architecture ihlalidir.
        // ─────────────────────────────────────────────────────
        modelBuilder.Entity<Book>(entity =>
        {
            entity.ToTable("Books");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Title)
                .IsRequired()
                .HasMaxLength(500);

            entity.Property(e => e.Isbn)
                .IsRequired()
                .HasMaxLength(13);

            // ISBN benzersizlik index'i
            entity.HasIndex(e => e.Isbn)
                .IsUnique();

            entity.Property(e => e.Description)
                .HasMaxLength(2000);

            entity.Property(e => e.Category)
                .IsRequired()
                .HasMaxLength(200);

            // ─────────────────────────────────────────────────
            // Value Object → Owned Entity (EF Core)
            // → Author bir Value Object olduğu için ayrı tablosu yoktur.
            //   "OwnsOne" ile Book tablosuna gömülür (embedded).
            //   Sonuç: Books tablosunda Author_FirstName, Author_LastName
            //   kolonları oluşur.
            // ─────────────────────────────────────────────────
            entity.OwnsOne(e => e.Author, author =>
            {
                author.Property(a => a.FirstName)
                    .HasColumnName("AuthorFirstName")
                    .IsRequired()
                    .HasMaxLength(100);

                author.Property(a => a.LastName)
                    .HasColumnName("AuthorLastName")
                    .IsRequired()
                    .HasMaxLength(100);
            });

            // ─────────────────────────────────────────────────
            // Concurrency Token (Optimistic Concurrency)
            // → Version property'si her güncellemede otomatik artar.
            //   İki kullanıcı aynı kitabı aynı anda güncellerse,
            //   ikincisi DbUpdateConcurrencyException alır.
            // ─────────────────────────────────────────────────
            entity.Property(e => e.Version)
                .IsConcurrencyToken();

            // Domain Event'ler veritabanına kaydedilmez
            entity.Ignore(e => e.DomainEvents);
        });

        // ─────────────────────────────────────────────────────
        // Outbox Message Konfigürasyonu
        // → Outbox Pattern: Integration Event'ler önce bu tabloya yazılır,
        //   sonra background job ile RabbitMQ'ya publish edilir.
        // ─────────────────────────────────────────────────────
        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.ToTable("OutboxMessages");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Type).IsRequired().HasMaxLength(500);
            entity.Property(e => e.Content).IsRequired();

            entity.HasIndex(e => e.ProcessedAt)
                .HasFilter("\"ProcessedAt\" IS NULL"); // Sadece işlenmemiş mesajlar
        });
    }

    /// <summary>
    /// SaveChanges override — Domain Event dispatch + Audit trail.
    /// </summary>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // ─────────────────────────────────────────────────────
        // Adım 1: Domain Event'leri topla
        // → ChangeTracker üzerinden domain event'li entity'leri bul.
        //   Entity'ler üzerindeki event'leri bir listeye al.
        // ─────────────────────────────────────────────────────
        var domainEvents = ChangeTracker.Entries<Entity<Guid>>()
            .SelectMany(entry => entry.Entity.DomainEvents)
            .ToList();

        // ─────────────────────────────────────────────────────
        // Adım 2: Entity'lerden event'leri temizle
        // → Aynı event birden fazla dispatch edilmesin.
        // ─────────────────────────────────────────────────────
        foreach (var entry in ChangeTracker.Entries<Entity<Guid>>())
        {
            entry.Entity.ClearDomainEvents();
        }

        // ─────────────────────────────────────────────────────
        // Adım 3: Veritabanına kaydet
        // ─────────────────────────────────────────────────────
        var result = await base.SaveChangesAsync(cancellationToken);

        // ─────────────────────────────────────────────────────
        // Adım 4: Domain Event'leri dispatch et
        // → SaveChanges başarılı olduktan SONRA dispatch edilir.
        //   Bu sıralama önemlidir: Veritabanı başarısız olursa
        //   event'ler dispatch edilmez.
        //
        // DİKKAT: Bu yaklaşımda event handler'daki hata
        // veritabanı transaction'ını geri almaz!
        // Tam tutarlılık için Outbox Pattern kullanılmalıdır.
        // ─────────────────────────────────────────────────────
        foreach (var domainEvent in domainEvents)
        {
            await _mediator.Publish(domainEvent, cancellationToken);
        }

        return result;
    }
}

// =============================================================================
// OutboxMessage — Outbox Pattern Veri Modeli
// =============================================================================
/// <summary>
/// İşlenmesi gereken integration event mesajlarını saklayan tablo.
/// Background job tarafından okunup RabbitMQ'ya publish edilir.
/// </summary>
public class OutboxMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Type { get; set; } = null!;
    public string Content { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAt { get; set; }
    public string? Error { get; set; }
}
