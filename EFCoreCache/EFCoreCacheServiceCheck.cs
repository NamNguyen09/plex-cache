using EFCoreCache.Interfaces;
using EFCoreCache.RedisCaches;
using Microsoft.Extensions.Options;

namespace EFCoreCache
{
    public class EFCoreCacheServiceCheck : IEFCoreCacheServiceCheck
    {
        private readonly EFCoreCacheSettings _cacheSettings;
        private readonly IDataRedisCache _cacheServiceProvider;

        private bool? _isCacheServerAvailable;
        private DateTime? _lastCheckTime;

        /// <summary>
        ///     Is the configured cache provider online?
        /// </summary>
        public EFCoreCacheServiceCheck(IOptions<EFCoreCacheSettings> cacheSettings,
                                   IDataRedisCache cacheServiceProvider)
        {
            if (cacheSettings == null)
            {
                throw new ArgumentNullException(nameof(cacheSettings));
            }

            _cacheSettings = cacheSettings.Value;
            _cacheServiceProvider = cacheServiceProvider;
        }

        /// <summary>
        ///     Is the configured cache services online and available? Can we use it without any problem?
        /// </summary>
        public bool IsCacheServiceAvailable()
        {
            if (!_cacheSettings.UseDbCallsIfCachingProviderIsDown)
            {
                return true;
            }

            var now = DateTime.UtcNow;

            if (_lastCheckTime.HasValue &&
                _isCacheServerAvailable.HasValue &&
                now - _lastCheckTime.Value < _cacheSettings.NextCacheServerAvailabilityCheck)
            {
                return _isCacheServerAvailable.Value;
            }

            try
            {
                var (isCacheAlive, message) = _cacheServiceProvider.GetStatus();
                _lastCheckTime = now;
                if (isCacheAlive)
                {
                    _isCacheServerAvailable = true;
                }
                else
                {
                    _isCacheServerAvailable = false;
                }
            }
            catch
            {
                _isCacheServerAvailable = false;
                if (_cacheSettings.UseDbCallsIfCachingProviderIsDown)
                {
                    throw;
                }
            }

            return _isCacheServerAvailable.Value;
        }
    }
}
