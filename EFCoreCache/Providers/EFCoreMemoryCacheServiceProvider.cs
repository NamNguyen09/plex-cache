using EFCoreCache.Extensions;
using EFCoreCache.Interfaces;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EFCoreCache.Providers;
public class EFCoreMemoryCacheServiceProvider : IEFCoreCacheServiceProvider
{
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<EFCoreMemoryCacheServiceProvider> _logger;
    private readonly EFCoreCacheSettings _cacheSettings;

    /// <summary>
    ///     Using IMemoryCache as a cache service.
    /// </summary>
    public EFCoreMemoryCacheServiceProvider(
        IMemoryCache memoryCache,
        ILogger<EFCoreMemoryCacheServiceProvider> logger,
        IOptions<EFCoreCacheSettings> cacheSettings)
    {
        _memoryCache = memoryCache;
        _logger = logger;
        _cacheSettings = cacheSettings.Value;
    }

    public bool GetItem(string key, out object? value)
    {
        value = null;
        var (hashed, hashedKey) = key.ToHashKey();
        try
        {
            var cachedData = _memoryCache.Get<EFCoreCachedData>(hashedKey);
            if (cachedData == null) return false;

            value = cachedData;
            return true;
        }
        catch (Exception ex)
        {
            if (_cacheSettings.DisableLogging) return false;
            string? stackTrace = $"{Environment.NewLine}{ex.StackTrace}{Environment.NewLine}{Environment.StackTrace}";
            _logger.LogWarning($"{ex.GetType().FullName}: {stackTrace}");
            if (!hashed) return false;
            _logger.LogDebug($"{ex.GetType().FullName} - isHashedKey='{hashed}' from cachekey '{key}': {ex.Message} {stackTrace}");
        }

        return false;
    }

    public void PutItem(string key, object value,
        IEnumerable<string> dependentEntitySets,
        DistributedCacheEntryOptions options)
    {
        var entitySets = dependentEntitySets.ToArray();

        var absoluteExpiration = options?.AbsoluteExpiration;
        var relativeExpiration = options?.SlidingExpiration;

        var (hashed, hashedKey) = key.ToHashKey();

        try
        {
            MemoryCacheEntryOptions cacheOptions = new()
            {
                Size = 1,
                AbsoluteExpiration = absoluteExpiration,
                SlidingExpiration = relativeExpiration
            };

            foreach (var entitySet in entitySets)
            {
                string entitySetKey = AddCacheQualifier(entitySet);
                _memoryCache.GetOrCreate(entitySetKey, entry =>
                {
                    return new List<string>();
                })?.Add(hashedKey);
            }

            _memoryCache.Set(hashedKey, value, cacheOptions);
        }
        catch (Exception ex)
        {
            if (_cacheSettings.DisableLogging) return;
            string stackTrace = $"{Environment.NewLine}{ex.StackTrace}{Environment.NewLine}{Environment.StackTrace}";
            _logger.LogWarning($"{ex.GetType().FullName}: {stackTrace}");
            if (!hashed) return;
            _logger.LogDebug($"{ex.GetType().FullName} - isHashedKey='{hashed}' from cachekey '{key}': {ex.Message} {stackTrace}");
        }
    }

    public void InvalidateSets(IEnumerable<string> entitySets)
    {
        List<string> itemsToInvalidate = [];
        foreach (var entitySet in entitySets)
        {
            var entitySetKey = AddCacheQualifier(entitySet);
            if (_memoryCache.TryGetValue<List<string>>(entitySetKey, out List<string>? existingValue))
            {
                if (existingValue != null) itemsToInvalidate.AddRange(existingValue);
                _memoryCache.Remove(entitySetKey);
            }
        }

        itemsToInvalidate = [.. itemsToInvalidate.Distinct()];
        foreach (var cacheKey in itemsToInvalidate)
        {
            InvalidateItem(cacheKey);
        }
    }

    public void InvalidateItem(string key)
    {
        _memoryCache.Remove(key);
    }

    public void ClearAllCache(string? pattern)
    {
        ((MemoryCache)_memoryCache).Clear();
    }

    public (bool, string) GetStatus()
    {
        if (_memoryCache == null) return (false, "Memory cache is disabled");
        string message = "Memory cache is ready";
        var key = Guid.NewGuid().ToString();
        _memoryCache.Set(key, key, DateTimeOffset.Now.AddMinutes(1));
        _memoryCache.Remove(key);
        return (true, message);
    }
    string AddCacheQualifier(string entitySet)
    {
        return string.Concat(_cacheSettings.EntityCachePrefix, ".", entitySet);
    }
}
