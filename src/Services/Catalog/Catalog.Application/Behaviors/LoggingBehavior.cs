// =============================================================================
// LoggingBehavior — İstek/Yanıt Loglama Pipeline Behavior
// =============================================================================
// 📚 EĞİTİCİ NOT (Tech-Tutor):
//
// NEDEN LoggingBehavior?
// → Her handler'a loglama kodu yazmak DRY ihlalidir.
//   Pipeline Behavior ile tüm istekler/yanıtlar merkezden loglanır.
//
// Structured Logging:
// → string.Format yerine template placeholder kullanırız: "{RequestName}"
//   Serilog bu template'i parse eder ve Elasticsearch'te aranabilir yapar.
//   Örnek: Elasticsearch'te "RequestName:CreateBookCommand" ile arayabilirsiniz.
// =============================================================================

using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Catalog.Application.Behaviors;

/// <summary>
/// Tüm MediatR isteklerini otomatik loglayan pipeline behavior.
/// İstek adı, süresi ve başarı/hata durumu loglanır.
/// </summary>
public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;

        // ─────────────────────────────────────────────────────
        // Stopwatch ile performans ölçümü
        // → IsHighResolution: Nanosaniye hassasiyetinde ölçüm
        //   Production'da yavaş sorguları tespit etmek için kritiktir.
        // ─────────────────────────────────────────────────────
        _logger.LogInformation(
            "[MediatR] İşleniyor: {RequestName} {@Request}",
            requestName, request);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var response = await next();
            stopwatch.Stop();

            _logger.LogInformation(
                "[MediatR] Tamamlandı: {RequestName} — Süre: {ElapsedMs}ms",
                requestName, stopwatch.ElapsedMilliseconds);

            // Yavaş sorgu uyarısı (500ms üzeri)
            if (stopwatch.ElapsedMilliseconds > 500)
            {
                _logger.LogWarning(
                    "[MediatR] YAVAŞ İSTEK: {RequestName} — Süre: {ElapsedMs}ms",
                    requestName, stopwatch.ElapsedMilliseconds);
            }

            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            _logger.LogError(ex,
                "[MediatR] HATA: {RequestName} — Süre: {ElapsedMs}ms — Hata: {ErrorMessage}",
                requestName, stopwatch.ElapsedMilliseconds, ex.Message);

            throw; // Exception'ı tekrar fırlat — global handler yakalasın
        }
    }
}
