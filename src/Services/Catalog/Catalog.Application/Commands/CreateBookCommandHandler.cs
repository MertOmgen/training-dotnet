// =============================================================================
// CreateBookCommandHandler — Kitap Oluşturma İşleyicisi
// =============================================================================
// 📚 EĞİTİCİ NOT (Tech-Tutor):
//
// NASIL Çalışır? (Tam Akış)
// ┌──────────────────────────────────────────────────────────────────────────┐
// │ 1. API Endpoint → CreateBookCommand gönderir                            │
// │ 2. MediatR Pipeline:                                                    │
// │    a. ValidationBehavior → FluentValidation ile input kontrolü           │
// │    b. LoggingBehavior → İstek log'lanır                                 │
// │    c. CreateBookCommandHandler → İş mantığı çalışır                     │
// │ 3. Handler:                                                             │
// │    a. Author Value Object oluşturulur                                   │
// │    b. Book Aggregate oluşturulur (Domain Event raise edilir)             │
// │    c. Write DB'ye (PostgreSQL) kaydedilir                               │
// │    d. Domain Event dispatch edilir → Integration Event publish edilir     │
// │ 4. Result<BookDto> döner                                                 │
// └──────────────────────────────────────────────────────────────────────────┘
//
// IRequestHandler<TRequest, TResponse>:
// → MediatR'ın handler arayüzü. Her Command/Query için bir Handler yazılır.
//   MediatR, DI container üzerinden otomatik bulur ve çalıştırır.
// =============================================================================

using Catalog.Application.DTOs;
using Catalog.Domain.Entities;
using Catalog.Domain.Repositories;
using Catalog.Domain.ValueObjects;
using MediatR;
using SharedKernel.Domain;

namespace Catalog.Application.Commands;

/// <summary>
/// CreateBookCommand'ı işleyen handler.
/// Book Aggregate oluşturur ve Write DB'ye kaydeder.
/// </summary>
public class CreateBookCommandHandler : IRequestHandler<CreateBookCommand, Result<BookDto>>
{
    private readonly IBookRepository _bookRepository;

    public CreateBookCommandHandler(IBookRepository bookRepository)
    {
        _bookRepository = bookRepository;
    }

    public async Task<Result<BookDto>> Handle(
        CreateBookCommand request,
        CancellationToken cancellationToken)
    {
        // ─────────────────────────────────────────────────────
        // Adım 1: ISBN benzersizlik kontrolü
        // → Aynı ISBN ile birden fazla kitap oluşturulamaz.
        //   Bu bir iş kuralıdır (business rule).
        // ─────────────────────────────────────────────────────
        var existingBook = await _bookRepository.GetByIsbnAsync(
            request.Isbn, cancellationToken);

        if (existingBook is not null)
            return Result<BookDto>.Failure($"Bu ISBN ile zaten bir kitap mevcut: {request.Isbn}");

        // ─────────────────────────────────────────────────────
        // Adım 2: Author Value Object oluştur
        // → Author.Create() factory metodu validasyon yapar.
        //   Hata varsa erken dönüş yaparız (fail-fast).
        // ─────────────────────────────────────────────────────
        var authorResult = Author.Create(request.AuthorFirstName, request.AuthorLastName);

        if (authorResult.IsFailure)
            return Result<BookDto>.Failure(authorResult.Error!);

        // ─────────────────────────────────────────────────────
        // Adım 3: Book Aggregate oluştur
        // → Book.Create() iç domain validasyonlarını yapar.
        // → Constructor içinde BookCreatedDomainEvent raise eder.
        // ─────────────────────────────────────────────────────
        var bookResult = Book.Create(
            request.Title,
            request.Isbn,
            request.Description,
            authorResult.Value!,
            request.PublishedYear,
            request.Category,
            request.TotalCopies);

        if (bookResult.IsFailure)
            return Result<BookDto>.Failure(bookResult.Error!);

        var book = bookResult.Value!;

        // ─────────────────────────────────────────────────────
        // Adım 4: Write DB'ye kaydet (PostgreSQL)
        // → SaveChangesAsync sırasında:
        //   a. EF Core değişiklikleri persist eder
        //   b. Domain Event'ler dispatch edilir (MediatR ile)
        //   c. Outbox Pattern ile Integration Event kaydedilir
        // ─────────────────────────────────────────────────────
        await _bookRepository.AddAsync(book, cancellationToken);
        await _bookRepository.SaveChangesAsync(cancellationToken);

        // ─────────────────────────────────────────────────────
        // Adım 5: DTO'ya dönüştür ve döndür
        // → Domain entity doğrudan döndürülmez!
        //   AutoMapper kullanılabilir ama burada manual mapping
        //   ile tam kontrol sağlıyoruz.
        // ─────────────────────────────────────────────────────
        var dto = new BookDto(
            book.Id,
            book.Title,
            book.Isbn,
            book.Description,
            book.Author.FirstName,
            book.Author.LastName,
            book.Author.FullName,
            book.PublishedYear,
            book.Category,
            book.TotalCopies,
            book.AvailableCopies,
            book.IsActive,
            book.CreatedAt,
            book.UpdatedAt);

        return Result<BookDto>.Success(dto);
    }
}
