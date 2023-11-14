using EFCoreCache.CachePolicies;
using EFCoreCache.Hashes;
using EFCoreCache.Interfaces;
using EFCoreCache.Processors;
using EFCoreCache.Providers;
using EFCoreCache.RedisCaches;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace EFCoreCache.Extensions
{
    public static class EFCoreServiceCollectionExtensions
    {
        public static IServiceCollection AddEFCoreSecondLevelCache(
                       this IServiceCollection services,
                       Action<EFCoreCacheOptions> options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            services.AddMemoryCache();
            services.TryAddSingleton<IEFCoreDebugLogger, EFCoreDebugLogger>();
            services.TryAddSingleton<IEFCoreCacheKeyPrefixProvider, EFCoreCacheKeyPrefixProvider>();
            services.TryAddSingleton<IEFCoreCacheServiceCheck, EFCoreCacheServiceCheck>();
            services.TryAddSingleton<IEFCoreCachePolicyParser, EFCoreCachePolicyParser>();
            services.TryAddSingleton<IEFCoreSqlCommandsProcessor, EFCoreSqlCommandsProcessor>();
            services.TryAddSingleton<IEFCoreCacheDependenciesProcessor, EFCoreCacheDependenciesProcessor>();
            services.TryAddSingleton<ILockProvider, LockProvider>();
            services.TryAddSingleton<IMemoryCacheChangeTokenProvider, EFMemoryCacheChangeTokenProvider>();
            services.TryAddSingleton<IDbCommandInterceptorProcessor, DbCommandInterceptorProcessor>();
            services.TryAddSingleton<EFCoreCacheInterceptor>();

            ConfigOptions(services, options);
            return services;
        }
        public static IServiceCollection AddRedisCache(
                      this IServiceCollection services,
                      Action<EFCoreCacheOptions> options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            ConfigOptions(services, options);
            return services;
        }

        private static void ConfigOptions(IServiceCollection services, Action<EFCoreCacheOptions> options)
        {
            var cacheOptions = new EFCoreCacheOptions();
            options.Invoke(cacheOptions);

            AddHashProvider(services, cacheOptions);
            AddCacheServiceProvider(services, cacheOptions);
            AddOptions(services, cacheOptions);
        }

        private static void AddHashProvider(IServiceCollection services, EFCoreCacheOptions cacheOptions)
        {
            if (cacheOptions.Settings.HashProvider == null)
            {
                services.TryAddSingleton<IEFCoreHashProvider, XxHash64Unsafe>();
            }
            else
            {
                services.TryAddSingleton(typeof(IEFCoreHashProvider), cacheOptions.Settings.HashProvider);
            }
        }

        private static void AddOptions(IServiceCollection services, EFCoreCacheOptions cacheOptions)
        {
            services.TryAddSingleton(Options.Create(cacheOptions.Settings));
        }

        private static void AddCacheServiceProvider(IServiceCollection services, EFCoreCacheOptions cacheOptions)
        {
            if (cacheOptions.Settings.CacheProvider == null)
            {
                services.TryAddSingleton<IEFCoreCacheServiceProvider, EFCoreMemoryCacheServiceProvider>();
            }
            else
            {
                services.TryAddSingleton(typeof(IDataRedisCache), cacheOptions.Settings.CacheProvider);
            }
        }
    }
}
