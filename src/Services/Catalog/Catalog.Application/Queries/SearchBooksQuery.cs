// =============================================================================
// SearchBooksQuery — Elasticsearch ile Kitap Arama
// =============================================================================
// 📚 EĞİTİCİ NOT (Tech-Tutor):
//
// NEDEN Elasticsearch?
// → MongoDB basit filtreleme için iyidir ama:
//   - Full-text search (kısmi eşleşme, fuzzy search) zayıftır
//   - Faceted search (kategoriye göre filtreleme) yavaştır
//   - Scoring (en ilgili sonuç önce) desteği yoktur
//
// Elasticsearch Bu Sorunları Çözer:
// → "Savaş ve Bar" yazıldığında "Savaş ve Barış"ı bulur (fuzzy)
// → "tolkien" yazıldığında hem "J.R.R. Tolkien" hem "Tolkien" bulunur
// → Sonuçlar relevance score'a göre sıralanır
// =============================================================================

using Catalog.Application.DTOs;
using MediatR;

namespace Catalog.Application.Queries;

/// <summary>
/// Elasticsearch üzerinden kitap arama sorgusu.
/// Full-text search, fuzzy matching ve sayfalama desteği.
/// </summary>
public record SearchBooksQuery(
    string? SearchTerm,
    string? Category,
    int Page = 1,
    int PageSize = 20) : IRequest<SearchBooksResult>;

/// <summary>Arama sonucu — toplam kayıt sayısı + sayfalanmış kitaplar</summary>
public record SearchBooksResult(
    IReadOnlyList<BookDto> Books,
    long TotalCount,
    int Page,
    int PageSize);
