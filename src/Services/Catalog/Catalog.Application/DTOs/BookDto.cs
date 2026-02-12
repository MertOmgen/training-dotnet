// =============================================================================
// BookDto — Kitap Veri Transfer Nesnesi
// =============================================================================
// 📚 EĞİTİCİ NOT (Tech-Tutor):
//
// NEDEN DTO (Data Transfer Object)?
// → Domain entity'leri doğrudan API response'una verilmemelidir:
//   1. Güvenlik: Domain entity private/internal property'ler içerebilir
//   2. Performans: Client'ın ihtiyaç duymadığı veriler gönderilir
//   3. Bağımlılık: API contract'ı domain modeline bağımlı olur
//      Domain değişince API da kırılır — bu çok tehlikelidir!
//
// DTO vs Record:
// → C# record tipi, DTO'lar için idealdir: immutable, auto-equality,
//   with-expressions ile kopyalama desteği.
// =============================================================================

namespace Catalog.Application.DTOs;

/// <summary>
/// Kitap bilgilerini client'a taşıyan DTO.
/// Domain entity'si dışarıya açılmaz, sadece bu DTO kullanılır.
/// </summary>
public record BookDto(
    Guid Id,
    string Title,
    string Isbn,
    string? Description,
    string AuthorFirstName,
    string AuthorLastName,
    string AuthorFullName,
    int PublishedYear,
    string Category,
    int TotalCopies,
    int AvailableCopies,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
