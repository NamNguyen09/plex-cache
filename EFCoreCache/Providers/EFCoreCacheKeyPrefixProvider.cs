using EFCoreCache.Interfaces;
using Microsoft.Extensions.Options;

namespace EFCoreCache.Providers;

public class EFCoreCacheKeyPrefixProvider : IEFCoreCacheKeyPrefixProvider
{
    private readonly EFCoreCacheSettings _cacheSettings;
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    ///     A custom cache key prefix provider for EF queries.
    /// </summary>
    public EFCoreCacheKeyPrefixProvider(IServiceProvider serviceProvider,
                                    IOptions<EFCoreCacheSettings> cacheSettings)
    {
        _serviceProvider = serviceProvider;
        if (cacheSettings == null)
        {
            throw new ArgumentNullException(nameof(cacheSettings));
        }

        _cacheSettings = cacheSettings.Value;
    }

    /// <summary>
    ///     returns the current provided cache key prefix
    /// </summary>
    public string GetCacheKeyPrefix() => _cacheSettings.CacheKeyPrefixSelector != null
                                             ? _cacheSettings.CacheKeyPrefixSelector(_serviceProvider)
                                             : _cacheSettings.CacheKeyPrefix;
    public string GetEntityCacheKeyPrefix() => _cacheSettings.EntityCachePrefix;
}
