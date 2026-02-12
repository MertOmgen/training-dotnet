// =============================================================================
// ICacheService — Cache Soyutlaması
// =============================================================================
// 📚 EĞİTİCİ NOT (Tech-Tutor):
//
// NEDEN Soyutlama (Interface)?
// → Servisler ICacheService'e bağımlıdır, Redis'e değil.
//   Unit test'lerde mock'layabilir, yarın farklı cache provider
//   kullanabiliriz (örneğin: Azure Redis Cache, Memcached).
//
// Cache Stratejileri:
// → Cache-Aside: Uygulama cache'i kontrol eder (en yaygın, burada uygulanan)
// → Write-Through: Yazma işlemi cache'i de günceller
// → Write-Behind: Cache'e yazılır, veritabanı asenkron güncellenir
// =============================================================================

namespace Caching.Redis;

/// <summary>
/// Cache işlemleri için soyut arayüz.
/// Redis implementasyonu bu arayüzü uygular.
/// </summary>
public interface ICacheService
{
    /// <summary>
    /// Cache'ten değer okur. Yoksa default döner.
    /// </summary>
    /// <typeparam name="T">Dönüş tipi</typeparam>
    /// <param name="key">Cache anahtarı</param>
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cache'e değer yazar.
    /// </summary>
    /// <param name="key">Cache anahtarı</param>
    /// <param name="value">Saklanacak değer</param>
    /// <param name="expiration">Opsiyonel süre dolum süresi (TTL)</param>
    Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cache'ten belirli bir anahtarı siler.
    /// Veri güncellendiğinde cache invalidation için kullanılır.
    /// </summary>
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Belirli bir pattern'e uyan tüm anahtarları siler.
    /// Örnek: "catalog:books:*" → tüm kitap cache'lerini temizler.
    /// </summary>
    Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default);
}
