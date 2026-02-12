// =============================================================================
// RedisCacheService — StackExchange.Redis Implementasyonu
// =============================================================================
// 📚 EĞİTİCİ NOT (Tech-Tutor):
//
// NASIL Çalışır?
// → IDistributedCache (.NET'in built-in soyutlaması) üzerinden Redis'e erişir.
// → Veri JSON olarak serialize edilir ve byte[] olarak Redis'te saklanır.
// → TTL (Time To Live) ile cache otomatik süresi dolunca temizlenir.
//
// StackExchange.Redis vs Microsoft.Extensions.Caching.StackExchangeRedis:
// → StackExchange.Redis: Düşük seviye Redis client (tüm Redis komutları)
// → Microsoft.Extensions.Caching.StackExchangeRedis: IDistributedCache
//   implementasyonu (daha üst seviye, ama sınırlı)
// → Burada ikisini de kullanıyoruz: IDistributedCache temel get/set için,
//   StackExchange.Redis ise prefix bazlı silme (SCAN) için.
// =============================================================================

using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using StackExchange.Redis;

namespace Caching.Redis;

/// <summary>
/// Redis tabanlı ICacheService implementasyonu.
/// IDistributedCache + raw IConnectionMultiplexer kullanır.
/// </summary>
public class RedisCacheService : ICacheService
{
    private readonly IDistributedCache _distributedCache;
    private readonly IConnectionMultiplexer _connectionMultiplexer;

    // JSON serializasyon ayarları — tüm cache işlemlerinde tutarlı kullanılır
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false // Cache'te boyut optimizasyonu
    };

    public RedisCacheService(
        IDistributedCache distributedCache,
        IConnectionMultiplexer connectionMultiplexer)
    {
        _distributedCache = distributedCache;
        _connectionMultiplexer = connectionMultiplexer;
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        // ─────────────────────────────────────────────────────────
        // Cache-Aside Pattern:
        // 1. Önce cache'e bak
        // 2. Varsa → deserialize et ve döndür
        // 3. Yoksa → null döndür (caller veritabanından okuyacak)
        // ─────────────────────────────────────────────────────────
        var cachedData = await _distributedCache.GetStringAsync(key, cancellationToken);

        if (string.IsNullOrEmpty(cachedData))
            return default;

        return JsonSerializer.Deserialize<T>(cachedData, JsonOptions);
    }

    public async Task SetAsync<T>(
        string key, T value, TimeSpan? expiration = null,
        CancellationToken cancellationToken = default)
    {
        var serializedData = JsonSerializer.Serialize(value, JsonOptions);

        var options = new DistributedCacheEntryOptions();

        if (expiration.HasValue)
        {
            // ─────────────────────────────────────────────────────
            // AbsoluteExpirationRelativeToNow:
            // Cache'e yazıldığı andan itibaren belirtilen süre sonra
            // otomatik olarak silinir (TTL).
            //
            // SlidingExpiration alternatifi:
            // Her erişimde süre uzar. Sık erişilen veriler cache'te kalır.
            // Ancak memory leak riski taşıdığı için dikkatli kullanılmalıdır.
            // ─────────────────────────────────────────────────────
            options.AbsoluteExpirationRelativeToNow = expiration;
        }
        else
        {
            // Varsayılan TTL: 5 dakika
            options.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
        }

        await _distributedCache.SetStringAsync(key, serializedData, options, cancellationToken);
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        await _distributedCache.RemoveAsync(key, cancellationToken);
    }

    public async Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default)
    {
        // ─────────────────────────────────────────────────────────
        // SCAN Komutu ile Prefix Bazlı Silme
        // → Redis KEYS komutu tüm anahtarları tarar ve production'da
        //   performans sorunu yaratır (blocking operation).
        // → SCAN ise cursor-based tarama yapar ve non-blocking'dir.
        // → Burada StackExchange.Redis'in ServerAsync API'sini kullanıyoruz.
        //
        // KULLANIM:
        // → Bir kitap güncellendiğinde: RemoveByPrefixAsync("catalog:books:")
        //   Tüm kitap sorgularının cache'i temizlenir.
        // ─────────────────────────────────────────────────────────
        var server = _connectionMultiplexer.GetServer(
            _connectionMultiplexer.GetEndPoints().First());
        var database = _connectionMultiplexer.GetDatabase();

        await foreach (var key in server.KeysAsync(pattern: $"{prefix}*"))
        {
            await database.KeyDeleteAsync(key);
        }
    }
}
