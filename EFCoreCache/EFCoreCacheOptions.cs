using EFCoreCache.CacheQueriesOptions;
using EFCoreCache.Enums;
using EFCoreCache.Interfaces;
using EFCoreCache.Providers;
using Microsoft.Extensions.Caching.Distributed;

namespace EFCoreCache;
public class EFCoreCacheOptions
{
    internal EFCoreCacheSettings Settings { get; } = new();

    /// <summary>
    ///     Puts the whole system in cache. In this case calling the `Cacheable()` methods won't be necessary.
    ///     If you specify the `Cacheable()` method, its setting will override this global setting.
    ///     If you want to exclude some queries from this global cache, apply the `NotCacheable()` method to them.
    /// </summary>
    /// <param name="expirationMode">Defines the expiration mode of the cache items globally.</param>
    /// <param name="timeout">The expiration timeout.</param>
    public EFCoreCacheOptions CacheAllQueries(CacheExpirationMode expirationMode, TimeSpan timeout)
    {
        Settings.CacheAllQueriesOptions = new CacheAllQueriesOptions
        {
            ExpirationMode = expirationMode,
            Timeout = timeout,
            IsActive = true,
        };
        return this;
    }

    /// <summary>
    ///     Puts the whole system in cache just for the specified `realTableNames`.
    ///     In this case calling the `Cacheable()` methods won't be necessary.
    ///     If you specify the `Cacheable()` method, its setting will override this global setting.
    ///     If you want to exclude some queries from this global cache, apply the `NotCacheable()` method to them.
    /// </summary>
    /// <param name="expirationMode">Defines the expiration mode of the cache items globally.</param>
    /// <param name="timeout">The expiration timeout.</param>
    /// <param name="tableNameComparison">How should we determine which tables should be cached?</param>
    /// <param name="realTableNames">
    ///     The real table names.
    ///     Queries containing these names will be cached.
    ///     Table names are not case sensitive.
    /// </param>
    public EFCoreCacheOptions CacheQueriesContainingTableNames(
        CacheExpirationMode expirationMode,
        TimeSpan timeout,
        TableNameComparison tableNameComparison = TableNameComparison.Contains,
        params string[] realTableNames)
    {
        Settings.CacheSpecificQueriesOptions = new CacheSpecificQueriesOptions(null)
        {
            ExpirationMode = expirationMode,
            Timeout = timeout,
            IsActive = true,
            TableNames = realTableNames,
            TableNameComparison = tableNameComparison,
        };
        return this;
    }

    /// <summary>
    ///     Puts the whole system in cache just for the specified `entityTypes`.
    ///     In this case calling the `Cacheable()` methods won't be necessary.
    ///     If you specify the `Cacheable()` method, its setting will override this global setting.
    ///     If you want to exclude some queries from this global cache, apply the `NotCacheable()` method to them.
    /// </summary>
    /// <param name="expirationMode">Defines the expiration mode of the cache items globally.</param>
    /// <param name="timeout">The expiration timeout.</param>
    /// <param name="tableTypeComparison">How should we determine which tables should be cached?</param>
    /// <param name="entityTypes">The real entity types. Queries containing these types will be cached.</param>
    public EFCoreCacheOptions CacheQueriesContainingTypes(
        CacheExpirationMode expirationMode,
        TimeSpan timeout,
        TableTypeComparison tableTypeComparison = TableTypeComparison.Contains,
        params Type[] entityTypes)
    {
        Settings.CacheSpecificQueriesOptions = new CacheSpecificQueriesOptions(entityTypes)
        {
            ExpirationMode = expirationMode,
            Timeout = timeout,
            IsActive = true,
            TableTypeComparison = tableTypeComparison,
        };
        return this;
    }

    /// <summary>
    ///     You can introduce a custom IEFHashProvider to be used as the HashProvider.
    ///     If you don't specify a custom hash provider, the default `XxHash64Unsafe` provider will be used.
    ///     `xxHash` is an extremely fast `non-cryptographic` Hash algorithm, working at speeds close to RAM limits.
    /// </summary>
    /// <typeparam name="T">Implements IEFHashProvider</typeparam>
    public EFCoreCacheOptions UseCustomHashProvider<T>() where T : IEFCoreHashProvider
    {
        Settings.HashProvider = typeof(T);
        return this;
    }


    /// <summary>
    ///     You can introduce a custom IEFCacheServiceProvider to be used as the CacheProvider.
    /// </summary>
    /// <typeparam name="T">Implements IEFCacheServiceProvider</typeparam>
    public EFCoreCacheOptions UseRedisCacheProvider<T>(string providerName,
                                                       string redisConnectionString) where T : IDistributedCache
    {
        Settings.CacheProvider = typeof(T);
        Settings.ProviderName = providerName;
        Settings.RedisConnectionString = redisConnectionString;
        return this;
    }

    /// <summary>
    ///     You can introduce a custom IEFCacheServiceProvider to be used as the CacheProvider.
    ///     If you specify the `Cacheable()` method options, its setting will override this global setting.
    /// </summary>
    /// <param name="expirationMode">Defines the expiration mode of the cache items globally.</param>
    /// <param name="timeout">The expiration timeout.</param>
    /// <typeparam name="T">Implements IEFCacheServiceProvider</typeparam>
    public EFCoreCacheOptions UseRedisCacheProvider<T>(CacheExpirationMode expirationMode,
                                                       TimeSpan timeout,
                                                       string providerName,
                                                       string redisConnectionString) where T : IDistributedCache
    {
        Settings.CacheProvider = typeof(T);
        Settings.ProviderName = providerName;
        Settings.RedisConnectionString = redisConnectionString;
        Settings.CachableQueriesOptions = new CachableQueriesOptions
        {
            ExpirationMode = expirationMode,
            Timeout = timeout,
            IsActive = true,
        };
        return this;
    }

    /// <summary>
    ///     Introduces the built-in `EFMemoryCacheServiceProvider` to be used as the CacheProvider.
    /// </summary>
    public EFCoreCacheOptions UseMemoryCacheProvider()
    {
        Settings.CacheProvider = typeof(EFCoreMemoryCacheServiceProvider);
        return this;
    }

    /// <summary>
    ///     Introduces the built-in `EFMemoryCacheServiceProvider` to be used as the CacheProvider.
    ///     If you specify the `Cacheable()` method options, its setting will override this global setting.
    /// </summary>
    /// <param name="expirationMode">Defines the expiration mode of the cache items globally.</param>
    /// <param name="timeout">The expiration timeout.</param>
    public EFCoreCacheOptions UseMemoryCacheProvider(CacheExpirationMode expirationMode,
                                                                TimeSpan timeout)
    {
        Settings.CacheProvider = typeof(EFCoreMemoryCacheServiceProvider);
        Settings.CachableQueriesOptions = new CachableQueriesOptions
        {
            ExpirationMode = expirationMode,
            Timeout = timeout,
            IsActive = true,
        };
        return this;
    }

    /// <summary>
    ///     Sets a dynamic prefix for the current cachedKey.
    /// </summary>
    /// <param name="prefix">
    ///     Selected cache key prefix.
    ///     This option will let you to choose a different cache key prefix for your current tenant.
    ///     <![CDATA[ Such as: serviceProvider => "EF_" + serviceProvider.GetRequiredService<IHttpContextAccesor>().HttpContext.Request.Headers["tenant-id"] ]]>
    /// </param>
    /// <returns>EFCoreSecondLevelCacheOptions.</returns>
    public EFCoreCacheOptions UseCacheKeyPrefix(Func<IServiceProvider, string>? prefix)
    {
        Settings.CacheKeyPrefixSelector = prefix;
        return this;
    }

    /// <summary>
    ///     Uses the cache key prefix.
    ///     Sets the prefix to all of the cachedKey's.
    ///     Its default value is `EF_`.
    /// </summary>
    /// <param name="prefix">The prefix.</param>
    /// <returns>EFCoreSecondLevelCacheOptions.</returns>
    public EFCoreCacheOptions UseCacheKeyPrefix(string prefix)
    {
        Settings.CacheKeyPrefix = prefix;
        return this;
    }
    public EFCoreCacheOptions UseEntityCacheKeyPrefix(string prefix)
    {
        Settings.EntityCachePrefix = prefix;
        return this;
    }
    public EFCoreCacheOptions UseExtraInvalidateSets(Dictionary<string, string> invalidateSets)
    {
        Settings.ExtraInvalidateSets = invalidateSets;
        return this;
    }
    /// <summary>
    ///     Should the debug level logging be disabled?
    ///     Set it to true for maximum performance.
    /// </summary>
    public EFCoreCacheOptions DisableLogging(bool value = false)
    {
        Settings.DisableLogging = value;
        return this;
    }

    /// <summary>
    ///     Possibility to allow caching with explicit transactions.
    ///     Its default value is false.
    /// </summary>
    public EFCoreCacheOptions AllowCachingWithExplicitTransactions(bool value = false)
    {
        Settings.AllowCachingWithExplicitTransactions = value;
        return this;
    }

    /// <summary>
    ///     Here you can decide based on the correct executing SQL command, should we cache its result or not?
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="predicate" /> is <c>null</c>.</exception>
    public EFCoreCacheOptions SkipCachingCommands(Predicate<string> predicate)
    {
        Settings.SkipCachingCommands = predicate ?? throw new ArgumentNullException(nameof(predicate));
        return this;
    }

    /// <summary>
    ///     Here you can decide based on the correct executing result, should we cache this result or not?
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="predicate" /> is <c>null</c>.</exception>
    public EFCoreCacheOptions SkipCachingResults(Predicate<(string CommandText, object Value)> predicate)
    {
        Settings.SkipCachingResults = predicate ?? throw new ArgumentNullException(nameof(predicate));
        return this;
    }

    /// <summary>
    ///     Here you can decide based on the correct executing SQL command, should we invalidate the cache or not?
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="predicate" /> is <c>null</c>.</exception>
    public EFCoreCacheOptions SkipCacheInvalidationCommands(Predicate<string> predicate)
    {
        Settings.SkipCacheInvalidationCommands = predicate ?? throw new ArgumentNullException(nameof(predicate));
        return this;
    }

    /// <summary>
    ///     Puts the whole system in cache except for the specified `realTableNames`.
    ///     In this case calling the `Cacheable()` methods won't be necessary.
    ///     If you specify the `Cacheable()` method, its setting will override this global setting.
    /// </summary>
    /// <param name="expirationMode">Defines the expiration mode of the cache items globally.</param>
    /// <param name="timeout">The expiration timeout.</param>
    /// <param name="realTableNames">
    ///     The real table names.
    ///     Queries containing these names will not be cached.
    ///     Table names are not case sensitive.
    /// </param>
    public EFCoreCacheOptions CacheAllQueriesExceptContainingTableNames(
        CacheExpirationMode expirationMode, TimeSpan timeout, params string[] realTableNames)
    {
        Settings.SkipCacheSpecificQueriesOptions = new SkipCacheSpecificQueriesOptions(null)
        {
            ExpirationMode = expirationMode,
            Timeout = timeout,
            IsActive = true,
            TableNames = realTableNames,
        };
        return this;
    }

    /// <summary>
    ///     Puts the whole system in cache except for the specified `entityTypes`.
    ///     In this case calling the `Cacheable()` methods won't be necessary.
    ///     If you specify the `Cacheable()` method, its setting will override this global setting.
    /// </summary>
    /// <param name="expirationMode">Defines the expiration mode of the cache items globally.</param>
    /// <param name="timeout">The expiration timeout.</param>
    /// <param name="entityTypes">The real entity types. Queries containing these types will not be cached.</param>
    public EFCoreCacheOptions CacheAllQueriesExceptContainingTypes(
        CacheExpirationMode expirationMode, TimeSpan timeout, params Type[] entityTypes)
    {
        Settings.SkipCacheSpecificQueriesOptions = new SkipCacheSpecificQueriesOptions(entityTypes)
        {
            ExpirationMode = expirationMode,
            Timeout = timeout,
            IsActive = true,
        };
        return this;
    }
    public EFCoreCacheOptions UseBinarySerializer()
    {
        Settings.BinarySerializer = new cx.BinarySerializer.BinarySerializer();
        return this;
    }
}
