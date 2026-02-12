// =============================================================================
// IdentityDbContext — Identity Veritabanı Context'i
// =============================================================================

using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Identity.API.Data;

/// <summary>
/// ASP.NET Core Identity tabloları + özel konfigürasyonlar için DbContext.
/// IdentityDbContext&lt;ApplicationUser&gt; kullanıcı tablosunu otomatik yönetir.
/// </summary>
public class IdentityDbContext : IdentityDbContext<ApplicationUser>
{
    public IdentityDbContext(DbContextOptions<IdentityDbContext> options)
        : base(options) { }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Tablo adlarını özelleştir (opsiyonel)
        builder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(e => e.FullName).IsRequired().HasMaxLength(200);
        });
    }
}
