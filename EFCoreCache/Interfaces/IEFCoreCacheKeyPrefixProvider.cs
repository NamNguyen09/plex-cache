namespace EFCoreCache.Interfaces;
public interface IEFCoreCacheKeyPrefixProvider
{
    /// <summary>
    ///     returns the current provided cache key prefix
    /// </summary>
    string GetCacheKeyPrefix();
}
