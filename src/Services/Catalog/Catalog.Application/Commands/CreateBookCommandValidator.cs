// =============================================================================
// CreateBookCommandValidator — FluentValidation ile Input Doğrulama
// =============================================================================
// 📚 EĞİTİCİ NOT (Tech-Tutor):
//
// Domain Validation vs Input Validation FARKI:
// ┌──────────────────────┬────────────────────────────────────────────┐
// │ Input Validation     │ FluentValidation — format, boşluk, uzunluk│
// │ (Application Layer)  │ Handler'a girmeden ÖNCE kontrol edilir     │
// ├──────────────────────┼────────────────────────────────────────────┤
// │ Domain Validation    │ Domain entity içinde — iş kuralları       │
// │ (Domain Layer)       │ Aggregate boundary içinde korunur          │
// └──────────────────────┴────────────────────────────────────────────┘
//
// NEDEN FluentValidation?
// → DataAnnotations ([Required], [MaxLength]) alternatifine göre:
//   - Daha okunabilir ve test edilebilir
//   - Koşullu validasyon (When/Unless) desteği
//   - Aynı property için birden fazla kural zincirleme
//   - DI desteği (veritabanı kontrolü gibi async validasyon)
//
// NASIL Çalışır?
// → ValidationBehavior (MediatR Pipeline) aracılığıyla Handler'dan ÖNCE
//   otomatik çalışır. ValidationException fırlatırsa Handler'a gidilmez.
// =============================================================================

using FluentValidation;

namespace Catalog.Application.Commands;

/// <summary>
/// CreateBookCommand için input validasyon kuralları.
/// MediatR Pipeline'da ValidationBehavior tarafından otomatik çağrılır.
/// </summary>
public class CreateBookCommandValidator : AbstractValidator<CreateBookCommand>
{
    public CreateBookCommandValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Kitap başlığı boş olamaz.")
            .MaximumLength(500).WithMessage("Kitap başlığı 500 karakteri aşamaz.");

        RuleFor(x => x.Isbn)
            .NotEmpty().WithMessage("ISBN boş olamaz.")
            .Matches(@"^(?:\d{10}|\d{13})$")
            .WithMessage("ISBN 10 veya 13 haneli bir sayı olmalıdır.");

        RuleFor(x => x.AuthorFirstName)
            .NotEmpty().WithMessage("Yazar adı boş olamaz.")
            .MaximumLength(100).WithMessage("Yazar adı 100 karakteri aşamaz.");

        RuleFor(x => x.AuthorLastName)
            .NotEmpty().WithMessage("Yazar soyadı boş olamaz.")
            .MaximumLength(100).WithMessage("Yazar soyadı 100 karakteri aşamaz.");

        RuleFor(x => x.PublishedYear)
            .InclusiveBetween(1000, DateTime.UtcNow.Year + 1)
            .WithMessage("Geçerli bir yayın yılı giriniz.");

        RuleFor(x => x.Category)
            .NotEmpty().WithMessage("Kategori boş olamaz.")
            .MaximumLength(200).WithMessage("Kategori 200 karakteri aşamaz.");

        RuleFor(x => x.TotalCopies)
            .GreaterThanOrEqualTo(0).WithMessage("Kopya sayısı negatif olamaz.");
    }
}
