// =============================================================================
// ValidationBehavior — MediatR Pipeline ile Otomatik Validasyon
// =============================================================================
// 📚 EĞİTİCİ NOT (Tech-Tutor):
//
// NEDEN Pipeline Behavior?
// → Her Command handler'ının başına validasyon kodu yazmak yerine,
//   cross-cutting concern olarak pipeline'a ekliyoruz.
//   Bu, AOP (Aspect-Oriented Programming) yaklaşımının MediatR ile
//   uygulanmasıdır.
//
// Pipeline Sırası:
// Request → ValidationBehavior → LoggingBehavior → CachingBehavior → Handler
//
// DI Kaydı sırasında behavior sırası belirlenebilir:
// services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
// services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
// =============================================================================

using FluentValidation;
using MediatR;

namespace Catalog.Application.Behaviors;

/// <summary>
/// FluentValidation validator'larını otomatik çalıştıran MediatR pipeline behavior.
/// Validasyon başarısız olursa Handler'a gidilmez, ValidationException fırlatılır.
/// </summary>
public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    // ─────────────────────────────────────────────────────────
    // IEnumerable<IValidator<TRequest>>:
    // → DI container, bu request tipi için kayıtlı TÜM validator'ları enjekte eder.
    //   Bir Command için birden fazla validator olabilir.
    //   Hiç validator yoksa → boş liste gelir → validasyon atlanır.
    // ─────────────────────────────────────────────────────────
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // Validator yoksa doğrudan Handler'a geç
        if (!_validators.Any())
            return await next();

        // ─────────────────────────────────────────────────────
        // Tüm validator'ları paralel çalıştır
        // → WhenAll ile tüm validasyonlar eş zamanlı tamamlanır.
        //   Bu, birden fazla validator olduğunda performans kazandırır.
        // ─────────────────────────────────────────────────────
        var context = new ValidationContext<TRequest>(request);

        var validationResults = await Task.WhenAll(
            _validators.Select(v => v.ValidateAsync(context, cancellationToken)));

        // Tüm hataları topla
        var failures = validationResults
            .SelectMany(result => result.Errors)
            .Where(error => error is not null)
            .ToList();

        // Hata varsa → ValidationException fırlatılır
        // Handler'a asla ulaşılmaz (fail-fast prensibi)
        if (failures.Count > 0)
            throw new ValidationException(failures);

        // Validasyon başarılı → Pipeline'da bir sonraki adıma geç
        return await next();
    }
}
