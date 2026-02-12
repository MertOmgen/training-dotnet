// =============================================================================
// CachingBehavior — MediatR Pipeline ile Otomatik Cache
// =============================================================================
// 📚 EĞİTİCİ NOT (Tech-Tutor):
//
// NEDEN MediatR Pipeline Behavior?
// → CQRS'de her Query handler'ına manuel cache kodu yazmak yerine,
//   MediatR'ın pipeline özelliğini kullanarak "cross-cutting concern"
//   olarak cache uygularız.
//
// NASIL Çalışır? (Pipeline Akışı)
// ┌──────────┐    ┌──────────────────┐    ┌──────────────┐    ┌─────────┐
// │ Request  │ → │ ValidationBehavior│ → │CachingBehavior│ → │ Handler │
// └──────────┘    └──────────────────┘    └──────────────┘    └─────────┘
//
// CachingBehavior Akışı:
// 1. Gelen istek ICacheableQuery interface'ini uyguluyorsa → cache kontrolü yap
// 2. Cache'te varsa → hemen döndür (Handler'a gitme!)
// 3. Cache'te yoksa → Handler'ı çalıştır → sonucu cache'e yaz → döndür
//
// DECORATOR PATTERN:
// → Bu yapı aslında bir Decorator'dır. Handler'ın etrafına cache mantığı
//   "sarılır" (wrap edilir). Handler bundan habersizdir (SRP prensibi).
//
// ALTERNATİF:
// → Manuel cache: Her handler'da if (cache.Exists) kontrolü (DRY ihlali)
// → Scrutor library ile class-level decorator (daha karmaşık setup)
// =============================================================================

using MediatR;

namespace Caching.Redis;

/// <summary>
/// Bu interface'i implement eden Query'ler otomatik olarak cache'lenir.
/// Her cacheable query bir cache key ve opsiyonel TTL tanımlamalıdır.
/// </summary>
public interface ICacheableQuery
{
    /// <summary>
    /// Cache anahtarı. Benzersiz olmalı.
    /// Örnek: $"catalog:books:{BookId}"
    /// </summary>
    string CacheKey { get; }

    /// <summary>
    /// Cache süresi (TTL). null ise varsayılan süre kullanılır.
    /// </summary>
    TimeSpan? CacheDuration { get; }
}

/// <summary>
/// MediatR Pipeline Behavior — Query sonuçlarını otomatik cache'ler.
/// ICacheableQuery implement etmeyen istekler doğrudan Handler'a geçer.
/// </summary>
/// <typeparam name="TRequest">MediatR request tipi</typeparam>
/// <typeparam name="TResponse">MediatR response tipi</typeparam>
public class CachingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ICacheService _cacheService;

    public CachingBehavior(ICacheService cacheService)
    {
        _cacheService = cacheService;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // ─────────────────────────────────────────────────────────
        // Adım 1: İstek cacheable mi kontrol et
        // → ICacheableQuery implement etmiyorsa → direkt Handler'a geç
        // ─────────────────────────────────────────────────────────
        if (request is not ICacheableQuery cacheableQuery)
        {
            return await next(); // Pipeline'da bir sonraki behavior/handler'a geç
        }

        // ─────────────────────────────────────────────────────────
        // Adım 2: Cache'te var mı bak (Cache-Aside pattern)
        // ─────────────────────────────────────────────────────────
        var cachedResult = await _cacheService.GetAsync<TResponse>(
            cacheableQuery.CacheKey, cancellationToken);

        if (cachedResult is not null)
        {
            // Cache HIT → Handler'ı çalıştırmadan sonucu döndür
            // Bu, veritabanı sorgusunu tamamen atlar!
            return cachedResult;
        }

        // ─────────────────────────────────────────────────────────
        // Adım 3: Cache MISS → Handler'ı çalıştır
        // ─────────────────────────────────────────────────────────
        var response = await next();

        // ─────────────────────────────────────────────────────────
        // Adım 4: Sonucu cache'e yaz (gelecek istekler için)
        // ─────────────────────────────────────────────────────────
        await _cacheService.SetAsync(
            cacheableQuery.CacheKey,
            response,
            cacheableQuery.CacheDuration,
            cancellationToken);

        return response;
    }
}
