// =============================================================================
// ElasticsearchService — Kitap Arama ve Veri Senkronizasyonu
// =============================================================================
// 📚 EĞİTİCİ NOT (Tech-Tutor):
//
// NEDEN Elasticsearch?
// → PostgreSQL LIKE sorgusu: "SELECT * FROM Books WHERE Title LIKE '%savaş%'"
//   - Yavaş (index kullanamaz), case-sensitive, fuzzy yok
// → Elasticsearch: Inverted index kullanır, milisaniye seviyesinde arama
//   - Full-text search, fuzzy matching, scoring, faceted search
//
// Senkronizasyon Stratejisi:
// → Write DB'ye (PostgreSQL) kitap eklenir/güncellenir
// → Domain Event → Integration Event → Elasticsearch sync
// → Elasticsearch güncellenmiştir ve aramaya hazırdır
//
// VERİ TUTARLILIĞI:
// → Bu "eventual consistency" modelidir. Kitap eklenip Elasticsearch
//   senkronize olana kadar kısa bir gecikme olabilir.
//   Bu, microservice mimarisinin doğal bir sonucudur.
//
// ALTERNATİF:
// → Debezium (CDC - Change Data Capture): Veritabanı log'larını dinler
//   ve otomatik senkronize eder. Daha güvenilir ama daha karmaşık.
// =============================================================================

using Catalog.Application.DTOs;
using Catalog.Application.Queries;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Catalog.Infrastructure.Search;

/// <summary>
/// Elasticsearch ile kitap arama ve veri senkronizasyonu servisi.
/// </summary>
public class ElasticsearchService : IRequestHandler<SearchBooksQuery, SearchBooksResult>
{
    private readonly ElasticsearchClient _client;
    private readonly ILogger<ElasticsearchService> _logger;
    private const string IndexName = "catalog-books";

    public ElasticsearchService(
        ElasticsearchClient client,
        ILogger<ElasticsearchService> logger)
    {
        _client = client;
        _logger = logger;
    }

    // =========================================================================
    // 1. Arama (SearchBooksQuery Handler)
    // =========================================================================

    /// <summary>
    /// Elasticsearch üzerinde full-text arama yapar.
    /// SearchBooksQuery'nin handler'ı olarak MediatR tarafından çağrılır.
    /// </summary>
    public async Task<SearchBooksResult> Handle(
        SearchBooksQuery request,
        CancellationToken cancellationToken)
    {
        var response = await _client.SearchAsync<BookDto>(s => s
            .Index(IndexName)
            // ─────────────────────────────────────────────────
            // Sayfalama: From = offset, Size = limit
            // ─────────────────────────────────────────────────
            .From((request.Page - 1) * request.PageSize)
            .Size(request.PageSize)
            .Query(q =>
            {
                // ─────────────────────────────────────────────
                // Bool Query: Birden fazla koşulu birleştirir
                // → Must: AND mantığı (tüm koşullar sağlanmalı)
                // → Should: OR mantığı (en az biri sağlanmalı)
                // → Filter: AND ama scoring'e katılmaz (performans)
                // ─────────────────────────────────────────────
                if (!string.IsNullOrWhiteSpace(request.SearchTerm))
                {
                    q.Bool(b =>
                    {
                        b.Must(must =>
                        {
                            must.MultiMatch(mm => mm
                                .Query(request.SearchTerm)
                                .Fields(new[] { "title^2", "authorFullName", "description", "category" })
                                .Fuzziness(new Fuzziness("AUTO"))
                            );
                        });

                        if (!string.IsNullOrWhiteSpace(request.Category))
                        {
                            b.Filter(filter =>
                            {
                                filter.Term(t => t
                                    .Field("category.keyword")
                                    .Value(request.Category)
                                );
                            });
                        }
                    });
                }
                else if (!string.IsNullOrWhiteSpace(request.Category))
                {
                    q.Term(t => t
                        .Field("category.keyword")
                        .Value(request.Category)
                    );
                }
                else
                {
                    q.MatchAll(new MatchAllQuery());
                }
            }),
            cancellationToken
        );

        if (!response.IsValidResponse)
        {
            _logger.LogError("Elasticsearch arama hatası: {Error}",
                response.ElasticsearchServerError?.Error?.Reason);
            return new SearchBooksResult([], 0, request.Page, request.PageSize);
        }

        var books = response.Documents.ToList().AsReadOnly();
        var totalCount = response.Total;

        return new SearchBooksResult(books, totalCount, request.Page, request.PageSize);
    }

    // =========================================================================
    // 2. Veri Senkronizasyonu (Write DB → Elasticsearch)
    // =========================================================================

    /// <summary>
    /// Bir kitabı Elasticsearch'e index'ler (ekler veya günceller).
    /// BookCreated/BookUpdated integration event handler'larından çağrılır.
    /// </summary>
    public async Task IndexBookAsync(BookDto book, CancellationToken cancellationToken = default)
    {
        // ─────────────────────────────────────────────────────
        // IndexAsync: Belgeyi index'e ekler veya günceller.
        // → DocumentId olarak Book.Id kullanılır.
        //   Aynı ID ile tekrar çağrılırsa güncellenir (upsert).
        // ─────────────────────────────────────────────────────
        var response = await _client.IndexAsync(
            book,
            idx => idx.Index(IndexName).Id(book.Id.ToString()),
            cancellationToken);

        if (response.IsValidResponse)
        {
            _logger.LogInformation(
                "[Elasticsearch] Kitap index'lendi: {BookId} - {Title}",
                book.Id, book.Title);
        }
        else
        {
            _logger.LogError(
                "[Elasticsearch] Index hatası: {BookId} - {Error}",
                book.Id, response.ElasticsearchServerError?.Error?.Reason);
        }
    }

    /// <summary>
    /// Elasticsearch index'ini oluşturur (uygulama başlangıcında çağrılır).
    /// </summary>
    public async Task EnsureIndexCreatedAsync(CancellationToken cancellationToken = default)
    {
        var existsResponse = await _client.Indices.ExistsAsync(IndexName, cancellationToken);

        if (!existsResponse.Exists)
        {
            _logger.LogInformation("[Elasticsearch] '{IndexName}' index'i oluşturuluyor...", IndexName);

            await _client.Indices.CreateAsync(IndexName, c => c
                .Mappings(m => m
                    .Properties<BookDto>(p => p
                        .Text(t => t.Title, td => td.Analyzer("standard"))
                        .Text(t => t.AuthorFullName, td => td.Analyzer("standard"))
                        .Text(t => t.Description, td => td.Analyzer("standard"))
                        .Keyword(t => t.Category)
                        .Keyword(t => t.Isbn)
                        .IntegerNumber(t => t.PublishedYear)
                        .Boolean(t => t.IsActive)
                    )
                ), cancellationToken);

            _logger.LogInformation("[Elasticsearch] '{IndexName}' index'i oluşturuldu.", IndexName);
        }
    }
}
