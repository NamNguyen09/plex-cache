using cx.BinarySerializer;
using EFCoreCache.CacheQueriesOptions;

namespace EFCoreCache
{
    public class EFCoreCacheSettings
    {
        public string? RedisConnectionString { get; set; }
        public Type? CacheProvider { get; set; }

        /// <summary>
        ///     The selected hash provider
        /// </summary>
        public Type? HashProvider { get; set; }

        /// <summary>
        ///     Selected caching provider name
        /// </summary>
        public string? ProviderName { get; set; }

        /// <summary>
        ///     This option will let you to choose a different redis database for your current tenant.
        ///     <![CDATA[ Such as: (serviceProvider, cacheKey) => "redis-db-" + serviceProvider.GetRequiredService<IHttpContextAccesor>().HttpContext.Request.Headers["tenant-id"]; ]]>
        /// </summary>
        public Func<IServiceProvider, EFCoreCacheKey?, string>? CacheProviderName { set; get; }

        /// <summary>
        ///     Gets or sets the cache key prefix.
        ///     Its default value is `EF_`.
        /// </summary>
        /// <value>The cache key prefix.</value>
        public string CacheKeyPrefix { get; set; } = "_EFCache.Data_";
        public string EntityCachePrefix { get; set; } = "_EFCache.EntitySetKey_";

        /// <summary>
        ///     Gets or sets a dynamic cache key prefix.
        /// </summary>
        /// <value>The cache key prefix.</value>
        public Func<IServiceProvider, string>? CacheKeyPrefixSelector { get; set; }

        /// <summary>
        ///     CacheAllQueries Options
        /// </summary>
        public CacheAllQueriesOptions CacheAllQueriesOptions { get; set; } = new();

        /// <summary>
        ///     Cache Specific Queries Options
        /// </summary>
        public CacheSpecificQueriesOptions CacheSpecificQueriesOptions { get; set; } = new(null);

        /// <summary>
        ///     Cachable Queries Options
        /// </summary>
        public CachableQueriesOptions CachableQueriesOptions { get; set; } = new();

        /// <summary>
        ///     Skip Cache Specific Queries Options
        /// </summary>
        public SkipCacheSpecificQueriesOptions SkipCacheSpecificQueriesOptions { get; set; } = new(null);

        /// <summary>
        ///     Should the debug level logging be disabled?
        /// </summary>
        public bool DisableLogging { set; get; }

        /// <summary>
        ///     Possibility to allow caching with explicit transactions.
        ///     Its default value is false.
        /// </summary>
        public bool AllowCachingWithExplicitTransactions { set; get; }

        /// <summary>
        ///     Here you can decide based on the correct executing SQL command, should we cache its result or not?
        /// </summary>
        public Predicate<string>? SkipCachingCommands { set; get; }

        /// <summary>
        ///     Here you can decide based on the correct executing SQL command, should we invalidate the cache or not?
        /// </summary>
        public Predicate<string>? SkipCacheInvalidationCommands { set; get; }
        public Dictionary<string, string>? ExtraInvalidateSets { get; set; }

        /// <summary>
        ///     Here you can decide based on the correct executing result, should we cache this result or not?
        /// </summary>
        public Predicate<(string CommandText, object Value)>? SkipCachingResults { set; get; }
        public BinarySerializer? BinarySerializer { get; set; }
    }
}
