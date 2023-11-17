using System.Security.Cryptography;
using System.Text;
using cx.BinarySerializer.EFCache;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using StackExchange.Redis;

namespace EFCoreCache.RedisCaches;
public class DataRedisCache : IDataRedisCache
{
    private readonly EFCoreCacheSettings _cacheSettings;
    private readonly ILogger<DataRedisCache> _logger;
    private static string? _configuration;

    public DataRedisCache(ILogger<DataRedisCache> logger,
        IOptions<EFCoreCacheSettings> cacheSettings)
    {
        if (cacheSettings == null
            || string.IsNullOrWhiteSpace(cacheSettings.Value.RedisConnectionString))
        {
            throw new ArgumentNullException(nameof(cacheSettings));
        }

        _cacheSettings = cacheSettings.Value;
        _logger = logger;
        SetConfiguration(cacheSettings.Value.RedisConnectionString);
    }
    static void SetConfiguration(string configuration)
    {
        _configuration = configuration;
    }
    private static Lazy<ConnectionMultiplexer> lazyConnection = new Lazy<ConnectionMultiplexer>(() =>
    {
        return ConnectionMultiplexer.Connect(_configuration);
    });

    public static ConnectionMultiplexer ConnectionMultiplexer
    {
        get
        {
            return lazyConnection.Value;
        }
    }
    private static IDatabase _redisDatabase
    {
        get
        {
            return ConnectionMultiplexer.GetDatabase();
        }
    }

    public byte[] Get(string key)
    {
        return _redisDatabase.StringGet(key);
    }
    public bool GetItem(string key, out object? value)
    {
        value = null;
        var (hashed, hashedKey) = GetHashKey(key);
        try
        {
            var cachedData = _redisDatabase.StringGet(hashedKey);
            if (!string.IsNullOrEmpty(cachedData))
            {
                CacheEntry? entry;
                if (_cacheSettings.BinarySerializer == null)
                {
                    entry = JsonConvert.DeserializeObject<CacheEntry>(cachedData);
                    value = entry == null ? null : entry.Value;
                    return true;
                }

                if (!_cacheSettings.BinarySerializer.TryDeserialize<CacheEntry>(cachedData, out entry))
                {
                    entry = null;
                }

                value = entry == null ? null : entry.Value;
                return true;
            }
            return false;
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
    public void PutItem(string key, object value, IEnumerable<string> dependentEntitySets, DistributedCacheEntryOptions options)
    {
        // ReSharper disable once PossibleMultipleEnumeration - the guard clause should not enumerate, its just checking the reference is not null
        var entitySets = dependentEntitySets.ToArray();

        var absoluteExpiration = options?.AbsoluteExpiration;
        var relativeExpiration = options?.SlidingExpiration;
        var expiration = GetExpiration(absoluteExpiration, relativeExpiration);

        var (hashed, hashedKey) = GetHashKey(key);

        try
        {
            foreach (var entitySet in entitySets)
            {
                _redisDatabase.SetAddAsync(AddCacheQualifier(entitySet), hashedKey);
                _redisDatabase.KeyExpireAsync(AddCacheQualifier(entitySet), expiration.Add(TimeSpan.FromMinutes(5)));
            }

            var cacheEntry = new CacheEntry(value, entitySets);
            if (_cacheSettings.BinarySerializer == null)
            {
                string? strData = JsonConvert.SerializeObject(cacheEntry);
                _redisDatabase.StringSetAsync(hashedKey, strData, expiration);
                return;
            }

            var byteData = _cacheSettings.BinarySerializer.Serialize(cacheEntry);
            _redisDatabase.StringSetAsync(hashedKey, byteData, expiration);
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
    public void Remove(string key)
    {
        var (hashed, hashedKey) = GetHashKey(key);
        try
        {
            _redisDatabase.KeyDelete(hashedKey);
        }
        catch (Exception ex)
        {
            if (_cacheSettings.DisableLogging) return;
            string? stackTrace = $"{Environment.NewLine}{ex.StackTrace}{Environment.NewLine}{Environment.StackTrace}";
            _logger.LogWarning($"{ex.GetType().FullName}: {stackTrace}");
            if (!hashed) return;
            _logger.LogDebug($"{ex.GetType().FullName} - isHashedKey='{hashed}' from cachekey '{key}': {ex.Message} {stackTrace}");
        }
    }
    public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
    {
        var absoluteExpiration = options?.AbsoluteExpiration;
        var relativeExpiration = options?.SlidingExpiration;
        var expiration = GetExpiration(absoluteExpiration, relativeExpiration);
        var (hashed, hashedKey) = GetHashKey(key);
        try
        {
            _redisDatabase.StringSet(hashedKey, value, expiration);
        }
        catch (Exception ex)
        {
            if (_cacheSettings.DisableLogging) return;
            string? stackTrace = $"{Environment.NewLine}{ex.StackTrace}{Environment.NewLine}{Environment.StackTrace}";
            _logger.LogWarning($"{ex.GetType().FullName}: {stackTrace}");
            if (!hashed) return;
            _logger.LogDebug($"{ex.GetType().FullName} - isHashedKey='{hashed}' from cachekey '{key}': {ex.Message} {stackTrace}");
        }
    }
    public Task<RedisValue> GetAsync(string key, CancellationToken token = default)
    {
        return _redisDatabase.StringGetAsync(GetPrefixedKey(key));
    }
    public Task RemoveAsync(string key, CancellationToken token = default)
    {
        return _redisDatabase.KeyDeleteAsync(GetPrefixedKey(key));
    }
    public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
    {
        var absoluteExpiration = options?.AbsoluteExpiration;
        var relativeExpiration = options?.SlidingExpiration;
        var expiration = GetExpiration(absoluteExpiration, relativeExpiration);

        return _redisDatabase.StringSetAsync(GetPrefixedKey(key), value, expiration);
    }
    private string GetPrefixedKey(string key)
    {
        return $"{_cacheSettings.CacheKeyPrefix}_{key}";
    }
    private TimeSpan GetExpiration(DateTimeOffset? absoluteExpiration, TimeSpan? relativeExpiration)
    {
        if (absoluteExpiration.HasValue && absoluteExpiration.Value != DateTimeOffset.MaxValue)
        {
            return absoluteExpiration.Value - DateTimeOffset.Now;
        }
        else if (relativeExpiration.HasValue && relativeExpiration.Value != TimeSpan.MaxValue)
        {
            return relativeExpiration.Value;
        }

        return new TimeSpan(1, 0, 0);
    }
    Task<byte[]?> IDistributedCache.GetAsync(string key, CancellationToken token)
    {
        byte[]? data = (byte[]?)Get(key);
        return Task.FromResult(data);
    }

    public Task RefreshAsync(string key, CancellationToken token = default)
    {
        Refresh(key);
        return Task.CompletedTask;
    }

    public void Refresh(string key)
    {
        // Update the cache entry's expiration using IDatabase.KeyExpire
        var expiration = _redisDatabase.KeyTimeToLive(key);
        if (!expiration.HasValue) return;
        _redisDatabase.KeyExpire(key, expiration);
    }
    public void InvalidateSets(IEnumerable<string> entitySets)
    {
        entitySets = AddExtraInvalidateSets(entitySets);
        var itemsToInvalidate = new HashSet<string>();

        try
        {
            foreach (var entitySet in entitySets)
            {
                var entitySetKey = AddCacheQualifier(entitySet);
                var keys = _redisDatabase.SetMembers(entitySetKey).Select(v => v.ToString());
                itemsToInvalidate.UnionWith(keys);
                if (keys.Any()) _redisDatabase.KeyDeleteAsync(entitySetKey);
            }
        }
        catch (Exception ex)
        {
            if (_cacheSettings.DisableLogging) return;
            string? stackTrace = $"{Environment.NewLine}{ex.StackTrace}{Environment.NewLine}{Environment.StackTrace}";
            _logger.LogError($"{ex.GetType().FullName}: {ex.Message} {stackTrace}");
            return;
        }

        foreach (var key in itemsToInvalidate)
        {
            InvalidateItem(key);
        }
    }

    /// <summary>
    /// We have split the domain into Assessment; Event; Organization and so on
    /// In the new domain, we have changed the name of entity like ResultEntity
    /// But in cxPlatform.Data it's different name like result_result
    /// That why we need this method to add extra InvalidateSets
    /// </summary>
    /// <param name="entitySets"></param>
    /// <returns></returns>
    private IEnumerable<string> AddExtraInvalidateSets(IEnumerable<string> entitySets)
    {
        var allInvalidateSets = new List<string>(entitySets);
        if (_cacheSettings.ExtraInvalidateSets == null) return allInvalidateSets;
        foreach (var item in entitySets)
        {
            if (!_cacheSettings.ExtraInvalidateSets.ContainsKey(item)) continue;
            var invalidateSet = _cacheSettings.ExtraInvalidateSets[item];
            if (allInvalidateSets.Contains(invalidateSet)) continue;
            allInvalidateSets.Add(invalidateSet);
        }
        return allInvalidateSets;
    }

    public void InvalidateItem(string key)
    {
        var (hashed, hashedKey) = GetHashKey(key);
        try
        {
            var cachedData = _redisDatabase.StringGet(hashedKey);
            CacheEntry? entry;
            if (_cacheSettings.BinarySerializer == null)
            {
                entry = JsonConvert.DeserializeObject<CacheEntry>(cachedData);
            }
            else if (!_cacheSettings.BinarySerializer.TryDeserialize<CacheEntry>(cachedData, out entry))
            {
                entry = null;
            }

            if (cachedData.HasValue) _redisDatabase.KeyDeleteAsync(hashedKey);
            if (entry == null) return;
            foreach (var set in entry.EntitySets)
            {
                _redisDatabase.SetRemoveAsync(AddCacheQualifier(set), hashedKey);
            }
        }
        catch (Exception ex)
        {
            if (_cacheSettings.DisableLogging) return;
            string? stackTrace = $"{Environment.NewLine}{ex.StackTrace}{Environment.NewLine}{Environment.StackTrace}";
            _logger.LogWarning($"{ex.GetType().FullName}: {stackTrace}");
            if (!hashed) return;
            _logger.LogDebug($"{ex.GetType().FullName} - isHashedKey='{hashed}' from cachekey '{key}': {ex.Message} {stackTrace}");
        }
    }
    public void ClearAllCache(string? pattern)
    {
        try
        {
            var endpoints = ConnectionMultiplexer.GetEndPoints(true);
            foreach (var endpoint in endpoints)
            {
                if (endpoint == null) continue;
                var server = ConnectionMultiplexer.GetServer(endpoint);
                if (!string.IsNullOrWhiteSpace(pattern))
                {
                    var keys = server.Keys(_redisDatabase.Database, pattern, 1000).ToArray();
                    if (keys.Any()) _redisDatabase.KeyDeleteAsync(keys);
                }
                else if (_redisDatabase.Database == 0)
                {
                    var keys = server.Keys(_redisDatabase.Database, _cacheSettings.CacheKeyPrefix + "*", 1000).ToArray();
                    if (keys.Any()) _redisDatabase.KeyDeleteAsync(keys);
                }
                else
                {
                    //Clear cache by DatabaseId
                    server.FlushDatabaseAsync(_redisDatabase.Database);
                }
            }
        }
        catch (Exception ex)
        {
            if (_cacheSettings.DisableLogging) return;
            _logger.LogError($"{ex.GetType().FullName} - Clear EFCache failed!", ex);
        }
    }
    public (bool, string) GetStatus()
    {
        string message = "Cache is ready";
        try
        {
            var database = ConnectionMultiplexer.GetDatabase();
            const string key = "CheckCacheStatus";
            database.StringSet(key, "CacheIsWorking");
            database.StringGet(key);
            database.KeyDelete(key);
            return (true, message);
        }
        catch (Exception exception)
        {
            message = exception.Message;
            return (false, message);
        }
    }
    private (bool, string) GetHashKey(string key)
    {
        string prefix = _cacheSettings.CacheKeyPrefix;
        bool hashed = false;
        // Uncomment the following to see the real queries to database
        ////return key;

        //Looking up large Keys in Redis can be expensive (comparing Large Strings), so if keys are large, hash them, otherwise if keys are short just use as-is
        if (key.Length <= 128) return (hashed, key.StartsWith(prefix) ? key : (prefix + key));
        using (var sha = SHA1.Create())
        {
            key = Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(key)));
            hashed = true;
            return (hashed, prefix + key);
        }
    }
    private RedisKey AddCacheQualifier(string entitySet)
    {
        return string.Concat(_cacheSettings.EntityCachePrefix, ".", entitySet);
    }
}
