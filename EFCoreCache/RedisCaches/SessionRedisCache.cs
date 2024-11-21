using Microsoft.Extensions.Caching.Distributed;
using StackExchange.Redis;

namespace EFCoreCache.RedisCaches;
public class SessionRedisCache : IDistributedCache
{
    private readonly IDatabase _redisDatabase;
    private readonly string _prefix;
    private readonly DistributedCacheEntryOptions _options;

    public SessionRedisCache(IConnectionMultiplexer connectionMultiplexer, string cacheKeyPrefix, DistributedCacheEntryOptions options)
    {
        _redisDatabase = connectionMultiplexer.GetDatabase();
        _prefix = cacheKeyPrefix;
        _options = options;
    }

    public byte[]? Get(string key)
    {
        return _redisDatabase.StringGet(GetPrefixedKey(key));
    }
    public void Remove(string key)
    {
        _redisDatabase.KeyDelete(GetPrefixedKey(key));
    }
    public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
    {
        options = _options == null ? options : _options;
        var absoluteExpiration = options?.AbsoluteExpiration;
        var relativeExpiration = options?.SlidingExpiration;
        var expiration = GetExpiration(absoluteExpiration, relativeExpiration);

        _redisDatabase.StringSet(GetPrefixedKey(key), value, expiration);
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
        options = _options == null ? options : _options;
        var absoluteExpiration = options?.AbsoluteExpiration;
        var relativeExpiration = options?.SlidingExpiration;
        var expiration = GetExpiration(absoluteExpiration, relativeExpiration);

        return _redisDatabase.StringSetAsync(GetPrefixedKey(key), value, expiration);
    }
    private string GetPrefixedKey(string key)
    {
        return $"{_prefix}-{key}";
    }
    private TimeSpan? GetExpiration(DateTimeOffset? absoluteExpiration, TimeSpan? relativeExpiration)
    {
        if (absoluteExpiration.HasValue && absoluteExpiration.Value != DateTimeOffset.MaxValue)
        {
            return absoluteExpiration.Value - DateTimeOffset.Now;
        }
        else if (relativeExpiration.HasValue && relativeExpiration.Value != TimeSpan.MaxValue)
        {
            return relativeExpiration.Value;
        }

        return null;
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
}
