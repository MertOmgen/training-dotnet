// =============================================================================
// BorrowingDbContext + Borrowing Entity'leri
// =============================================================================

using Microsoft.EntityFrameworkCore;

namespace Borrowing.API.Data;

/// <summary>Ödünç alma kaydı — her ödünç işlemi bir satır</summary>
public class BorrowingRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BookId { get; set; }
    public string UserId { get; set; } = null!;
    public string BookTitle { get; set; } = null!;
    public DateTime BorrowedAt { get; set; } = DateTime.UtcNow;
    public DateTime DueDate { get; set; }
    public DateTime? ReturnedAt { get; set; }
    public bool IsReturned => ReturnedAt.HasValue;
    public bool IsOverdue => !IsReturned && DueDate < DateTime.UtcNow;
}

public class BorrowingDbContext : DbContext
{
    public BorrowingDbContext(DbContextOptions<BorrowingDbContext> options)
        : base(options) { }

    public DbSet<BorrowingRecord> BorrowingRecords => Set<BorrowingRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BorrowingRecord>(entity =>
        {
            entity.ToTable("BorrowingRecords");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.BookId);
            entity.HasIndex(e => e.UserId);
            entity.Property(e => e.BookTitle).IsRequired().HasMaxLength(500);
        });
    }
}
