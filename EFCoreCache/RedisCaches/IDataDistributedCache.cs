using Microsoft.Extensions.Caching.Distributed;

namespace EFCoreCache.RedisCaches;
public interface IDataDistributedCache : IDistributedCache
{
    bool GetItem(string key, out object? value);
    void PutItem(string key, object value, IEnumerable<string> dependentEntitySets, DistributedCacheEntryOptions options);
    void InvalidateSets(IEnumerable<string> entitySets);
    void InvalidateItem(string key);
    void ClearAllCache(string? parttern);
}
