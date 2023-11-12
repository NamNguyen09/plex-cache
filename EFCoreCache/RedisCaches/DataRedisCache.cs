using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using StackExchange.Redis;

namespace EFCoreCache.RedisCaches;
public class DataRedisCache : IDataRedisCache
{
    private readonly IConnectionMultiplexer _connectionMultiplexer;
    private readonly IDatabase _redisDatabase;
    private readonly EFCoreCacheSettings _cacheSettings;
    private readonly ILogger<DataRedisCache> _logger;

    public DataRedisCache(ILogger<DataRedisCache> logger,
        IOptions<EFCoreCacheSettings> cacheSettings)
    {
        if (cacheSettings == null
            || string.IsNullOrWhiteSpace(cacheSettings.Value.RedisConnectionString))
        {
            throw new ArgumentNullException(nameof(cacheSettings));
        }

        _connectionMultiplexer = ConnectionMultiplexer.Connect(cacheSettings.Value.RedisConnectionString);
        _redisDatabase = _connectionMultiplexer.GetDatabase();
        _cacheSettings = cacheSettings.Value;
        _logger = logger;
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
                var entry = JsonConvert.DeserializeObject<CacheEntry>(cachedData);
                value = entry ?? null;
                return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            var stackTrace = string.Format("{0}{1}{2}{3}", Environment.NewLine, ex.StackTrace, Environment.NewLine, Environment.StackTrace);
            _logger.LogError(string.Format("{0} - isHashedKey='{1}': {2} {3}", ex.GetType().FullName, hashed, ex.Message, stackTrace));
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
            var data = JsonConvert.SerializeObject(cacheEntry);
            _redisDatabase.StringSetAsync(hashedKey, data, expiration);
        }
        catch (Exception ex)
        {
            var stackTrace = string.Format("{0}{1}{2}{3}", Environment.NewLine, ex.StackTrace, Environment.NewLine, Environment.StackTrace);
            _logger.LogError(string.Format("{0} - isHashedKey='{1}': {2} {3}", ex.GetType().FullName, hashed, ex.Message, stackTrace));
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
            var stackTrace = string.Format("{0}{1}{2}{3}", Environment.NewLine, ex.StackTrace, Environment.NewLine, Environment.StackTrace);
            _logger.LogError(string.Format("{0} - isHashedKey='{1}': {2} {3}", ex.GetType().FullName, hashed, ex.Message, stackTrace));
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
            var stackTrace = string.Format("{0}{1}{2}{3}", Environment.NewLine, ex.StackTrace, Environment.NewLine, Environment.StackTrace);
            _logger.LogError(string.Format("{0} - isHashedKey='{1}': {2} {3}", ex.GetType().FullName, hashed, ex.Message, stackTrace));
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
            var stackTrace = string.Format("{0}{1}{2}{3}", Environment.NewLine, ex.StackTrace, Environment.NewLine, Environment.StackTrace);
            _logger.LogError(string.Format("{0}: {1} {2}", ex.GetType().FullName, ex.Message, stackTrace));
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
            var data = _redisDatabase.StringGet(hashedKey);
            CacheEntry? entry = JsonConvert.DeserializeObject<CacheEntry>(data);
            if (entry == null) return;
            _redisDatabase.KeyDeleteAsync(hashedKey);
            foreach (var set in entry.EntitySets)
            {
                _redisDatabase.SetRemoveAsync(AddCacheQualifier(set), hashedKey);
            }
        }
        catch (Exception ex)
        {
            var stackTrace = string.Format("{0}{1}{2}{3}", Environment.NewLine, ex.StackTrace, Environment.NewLine, Environment.StackTrace);
            _logger.LogError(string.Format("{0} - isHashedKey='{1}': {2} {3}", ex.GetType().FullName, hashed, ex.Message, stackTrace));
        }
    }
    public void ClearAllCache(string? pattern)
    {
        try
        {
            var endpoints = _connectionMultiplexer.GetEndPoints(true);
            foreach (var endpoint in endpoints)
            {
                if (endpoint == null) continue;
                var server = _connectionMultiplexer.GetServer(endpoint);
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
        catch (Exception exception)
        {
            _logger.LogError("Clear EFCache failed!", exception);
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
