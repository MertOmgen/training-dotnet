// =============================================================================
// CreateBookCommand — Kitap Oluşturma Komutu (CQRS - Command Tarafı)
// =============================================================================
// 📚 EĞİTİCİ NOT (Tech-Tutor):
//
// CQRS (Command Query Responsibility Segregation) NEDİR?
// → Okuma (Query) ve Yazma (Command) işlemleri ayrı modeller ile
//   gerçekleştirilir.
//
// ┌───────────────────────────────────────────────────────────────────┐
// │                        CQRS Akışı                                │
// │                                                                   │
// │  Client → Command → Handler → Write DB (PostgreSQL)              │
// │  Client → Query   → Handler → Read DB (MongoDB/Elasticsearch)    │
// └───────────────────────────────────────────────────────────────────┘
//
// NEDEN IRequest<T>?
// → MediatR'ın IRequest<T> interface'i, bir isteğin bir yanıt döndürdüğünü 
//   belirtir. Command → Result<BookDto> döner.
//
// Command vs Query FARKI:
// → Command: Veri değiştirir, genellikle void veya Result döner
//   (burada BookDto dönüyoruz — client'a oluşturulan veriyi göstermek için)
// → Query: Veri okur, veri döner, asla state değiştirmez
// =============================================================================

using Catalog.Application.DTOs;
using MediatR;
using SharedKernel.Domain;

namespace Catalog.Application.Commands;

/// <summary>
/// Yeni kitap oluşturma komutu.
/// MediatR IRequest ile tanımlanır, Handler tarafından işlenir.
/// </summary>
public record CreateBookCommand(
    string Title,
    string Isbn,
    string? Description,
    string AuthorFirstName,
    string AuthorLastName,
    int PublishedYear,
    string Category,
    int TotalCopies) : IRequest<Result<BookDto>>;
