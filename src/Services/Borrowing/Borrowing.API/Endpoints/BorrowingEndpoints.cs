// =============================================================================
// BorrowingEndpoints — Ödünç Alma/İade Endpoint'leri
// =============================================================================
// 📚 EĞİTİCİ NOT (Tech-Tutor):
//
// Servisler Arası İletişim Modeli:
// → Borrowing servisi, Catalog servisine HTTP ile kitap var mı kontrol eder.
//   Bu senkron iletişimdir ve Polly ile dayanıklı hale getirilmiştir.
// → Ödünç alma/iade sonrası Integration Event publish eder → asenkron
//   Catalog servisi bu event'i consume edip stok günceller.
//
// SAGA Pattern (Basitleştirilmiş):
// Ödünç alma işlemi birden fazla servis arasında koordinasyon gerektirir:
// 1. Borrowing: Kayıt oluştur
// 2. Catalog: Stok azalt
// 3. Notification: Bildirim gönder
// Herhangi bir adım başarısız olursa compensating action gerekir.
// Burada basitleştirilmiş event-driven choreography kullanıyoruz.
// =============================================================================

using Borrowing.API.Data;
using EventBus.Abstractions;
using EventBus.Abstractions.Events;
using Microsoft.EntityFrameworkCore;

namespace Borrowing.API.Endpoints;

public static class BorrowingEndpoints
{
    public static void MapBorrowingEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/borrowing")
            .WithTags("Borrowing");

        // =====================================================================
        // POST /borrow — Kitap Ödünç Al
        // =====================================================================
        group.MapPost("/borrow", async (
            BorrowRequest request,
            BorrowingDbContext db,
            IEventBus eventBus) =>
        {
            // Aktif ödünç var mı kontrol et
            var existingBorrow = await db.BorrowingRecords
                .AnyAsync(b => b.BookId == request.BookId
                    && b.UserId == request.UserId
                    && b.ReturnedAt == null);

            if (existingBorrow)
                return Results.BadRequest(new { Error = "Bu kitap zaten ödünç alınmış." });

            var record = new BorrowingRecord
            {
                BookId = request.BookId,
                UserId = request.UserId,
                BookTitle = request.BookTitle,
                DueDate = DateTime.UtcNow.AddDays(14) // 2 hafta ödünç süresi
            };

            db.BorrowingRecords.Add(record);
            await db.SaveChangesAsync();

            // ─────────────────────────────────────────────────
            // BookBorrowedIntegrationEvent publish
            // → Catalog servisi: Stok azaltır
            // → Notification servisi: Bildirim gönderir
            // ─────────────────────────────────────────────────
            await eventBus.PublishAsync(new BookBorrowedIntegrationEvent(
                record.Id, record.BookId, record.UserId, record.BookTitle));

            return Results.Created($"/api/v1/borrowing/{record.Id}", record);
        })
        .WithName("BorrowBook")
        .WithSummary("Kitap ödünç alır");

        // =====================================================================
        // POST /return — Kitap İade Et
        // =====================================================================
        group.MapPost("/return/{borrowingId:guid}", async (
            Guid borrowingId,
            BorrowingDbContext db,
            IEventBus eventBus) =>
        {
            var record = await db.BorrowingRecords.FindAsync(borrowingId);

            if (record is null)
                return Results.NotFound(new { Error = "Ödünç kaydı bulunamadı." });

            if (record.IsReturned)
                return Results.BadRequest(new { Error = "Bu kitap zaten iade edilmiş." });

            record.ReturnedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            await eventBus.PublishAsync(new BookReturnedIntegrationEvent(
                record.Id, record.BookId, record.UserId));

            return Results.Ok(record);
        })
        .WithName("ReturnBook")
        .WithSummary("Kitap iade eder");

        // =====================================================================
        // GET /user/{userId} — Kullanıcının Ödünç Kitapları
        // =====================================================================
        group.MapGet("/user/{userId}", async (string userId, BorrowingDbContext db) =>
        {
            var records = await db.BorrowingRecords
                .AsNoTracking()
                .Where(b => b.UserId == userId)
                .OrderByDescending(b => b.BorrowedAt)
                .ToListAsync();

            return Results.Ok(records);
        })
        .WithName("GetUserBorrowings")
        .WithSummary("Kullanıcının ödünç aldığı kitapları listeler");
    }
}

// Integration Event'ler
public record BorrowRequest(Guid BookId, string UserId, string BookTitle);

public record BookBorrowedIntegrationEvent(
    Guid BorrowingId, Guid BookId, string UserId, string BookTitle) : IntegrationEvent;

public record BookReturnedIntegrationEvent(
    Guid BorrowingId, Guid BookId, string UserId) : IntegrationEvent;
