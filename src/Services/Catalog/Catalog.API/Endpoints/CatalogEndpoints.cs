// =============================================================================
// CatalogEndpoints — Minimal API Endpoint Tanımları
// =============================================================================
// 📚 EĞİTİCİ NOT (Tech-Tutor):
//
// NEDEN Extension Method ile Endpoint Ayırma?
// → Program.cs'e tüm endpoint'leri yazmak dosyayı şişirir.
//   Extension method ile her servis kendi endpoint'lerini ayrı dosyada tanımlar.
//   Bu, Feature-based organization yaklaşımıdır.
//
// Minimal API vs Controller:
// → Controller: [ApiController], [HttpGet], ActionResult<T>
// → Minimal API: app.MapGet("/path", handler)
//   Her iki yaklaşım da aynı HTTP pipeline'ı kullanır.
//   Minimal API daha az ceremony (tören) gerektirir.
//
// MediatR Entegrasyonu:
// → Endpoint handler'ı, ISender üzerinden MediatR'a Command/Query gönderir.
//   Endpoint → MediatR → Pipeline Behaviors → Handler → Response
//   Endpoint'in kendisi iş mantığı içermez (Thin Controller/Endpoint prensibi).
// =============================================================================

using Catalog.Application.Commands;
using Catalog.Application.Queries;
using MediatR;

namespace Catalog.API.Endpoints;

public static class CatalogEndpoints
{
    /// <summary>
    /// Tüm Catalog endpoint'lerini WebApplication'a kaydeder.
    /// </summary>
    public static void MapCatalogEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/books")
            .WithTags("Books");

        // =====================================================================
        // POST /api/v1/books — Yeni Kitap Oluştur
        // =====================================================================
        // EĞİTİCİ NOT:
        // → ISender (MediatR) enjekte edilir, IMediator yerine.
        //   ISender sadece Send (Command/Query) yapabilir.
        //   IMediator hem Send hem Publish yapabilir.
        //   ISP (Interface Segregation) prensibi uyarınca ISender yeterlidir.
        // =====================================================================
        group.MapPost("/", async (CreateBookCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);

            if (result.IsFailure)
                return Results.BadRequest(new { Error = result.Error });

            // 201 Created + Location header
            return Results.Created($"/api/v1/books/{result.Value!.Id}", result.Value);
        })
        .WithName("CreateBook")
        .WithSummary("Yeni bir kitap oluşturur")
        .WithDescription("CQRS Command pattern ile kitap oluşturur. Validasyon, domain kuralları ve event publish işlemleri otomatik yürütülür.")
        .Produces(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest);

        // =====================================================================
        // GET /api/v1/books/{id} — ID ile Kitap Getir
        // =====================================================================
        // EĞİTİCİ NOT:
        // → Query, CachingBehavior üzerinden geçer.
        //   İlk istek: Redis MISS → MongoDB'den okur → Redis'e yazar
        //   İkinci istek: Redis HIT → doğrudan cache'ten döner
        // =====================================================================
        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetBookByIdQuery(id));

            return result is not null
                ? Results.Ok(result)
                : Results.NotFound(new { Error = $"Kitap bulunamadı: {id}" });
        })
        .WithName("GetBookById")
        .WithSummary("ID ile kitap getirir")
        .WithDescription("Redis cache + MongoDB Read DB üzerinden kitap sorgular. İlk istekte cache'e yazar, sonraki isteklerde cache'ten döner.")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        // =====================================================================
        // GET /api/v1/books/search — Kitap Ara (Elasticsearch)
        // =====================================================================
        // EĞİTİCİ NOT:
        // → Full-text search: Elasticsearch üzerinden çalışır
        // → Fuzzy matching: Yazım hataları tolere edilir
        // → Boosting: Başlık eşleşmesi daha yüksek puan alır
        // =====================================================================
        group.MapGet("/search", async (
            string? q, string? category, int page, int pageSize,
            ISender sender) =>
        {
            page = page <= 0 ? 1 : page;
            pageSize = pageSize <= 0 ? 20 : Math.Min(pageSize, 100);

            var result = await sender.Send(
                new SearchBooksQuery(q, category, page, pageSize));

            return Results.Ok(result);
        })
        .WithName("SearchBooks")
        .WithSummary("Kitap arar (Full-text search)")
        .WithDescription("Elasticsearch üzerinde full-text arama yapar. Fuzzy matching ve kategori filtresi destekler.")
        .Produces(StatusCodes.Status200OK);
    }
}
