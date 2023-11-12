namespace EFCoreCache.Interfaces;
public interface IEFCoreCacheKeyPrefixProvider
{
    string GetCacheKeyPrefix();
    string GetEntityCacheKeyPrefix();
}
