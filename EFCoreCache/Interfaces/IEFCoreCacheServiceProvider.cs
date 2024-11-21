using Microsoft.Extensions.Caching.Distributed;

namespace EFCoreCache.Interfaces;
public interface IEFCoreCacheServiceProvider
{
    bool GetItem(string key, out object? value);
    void PutItem(string key, object value, IEnumerable<string> dependentEntitySets, DistributedCacheEntryOptions options);
    void InvalidateSets(IEnumerable<string> entitySets);
    void InvalidateItem(string key);
    void ClearAllCache(string? pattern);
    (bool, string) GetStatus();
}
