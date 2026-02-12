// =============================================================================
// ApplicationUser — Özelleştirilmiş Kullanıcı Entity'si
// =============================================================================
// 📚 EĞİTİCİ NOT (Tech-Tutor):
//
// ASP.NET Core Identity varsayılan olarak IdentityUser sınıfını kullanır.
// Özel alanlar (FullName, MembershipDate gibi) eklemek için
// IdentityUser'dan türetme yapılır.
//
// NEDEN IdentityUser Base Class?
// → Id, UserName, Email, PasswordHash, PhoneNumber vb. alanlar hazırdır.
// → İlişkili tablolar (Claims, Roles, Logins) otomatik yönetilir.
// =============================================================================

using Microsoft.AspNetCore.Identity;

namespace Identity.API.Data;

/// <summary>
/// LMS uygulamasının kullanıcı modeli.
/// IdentityUser'dan türer, özel alanlar ekler.
/// </summary>
public class ApplicationUser : IdentityUser
{
    /// <summary>Kullanıcının tam adı</summary>
    public string FullName { get; set; } = null!;

    /// <summary>Üyelik başlangıç tarihi</summary>
    public DateTime MembershipDate { get; set; } = DateTime.UtcNow;

    /// <summary>Hesap aktif mi?</summary>
    public bool IsActive { get; set; } = true;
}
