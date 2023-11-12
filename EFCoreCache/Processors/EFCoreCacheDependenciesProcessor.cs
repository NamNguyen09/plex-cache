using System.Data.Common;
using EFCoreCache.CachePolicies;
using EFCoreCache.Interfaces;
using EFCoreCache.RedisCaches;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EFCoreCache.Processors;
public class EFCoreCacheDependenciesProcessor : IEFCoreCacheDependenciesProcessor
{
    private readonly IEFCoreCacheKeyPrefixProvider _cacheKeyPrefixProvider;
    private readonly IDataRedisCache _cacheServiceProvider;
    private readonly EFCoreCacheSettings _cacheSettings;
    private readonly ILogger<EFCoreCacheDependenciesProcessor> _dependenciesProcessorLogger;
    private readonly IEFCoreDebugLogger _logger;
    private readonly IEFCoreSqlCommandsProcessor _sqlCommandsProcessor;

    /// <summary>
    ///     Cache Dependencies Calculator
    /// </summary>
    public EFCoreCacheDependenciesProcessor(
        IEFCoreDebugLogger logger,
        ILogger<EFCoreCacheDependenciesProcessor> dependenciesProcessorLogger,
        IDataRedisCache cacheServiceProvider,
        IEFCoreSqlCommandsProcessor sqlCommandsProcessor,
        IOptions<EFCoreCacheSettings> cacheSettings,
        IEFCoreCacheKeyPrefixProvider cacheKeyPrefixProvider)
    {
        _logger = logger;
        _dependenciesProcessorLogger = dependenciesProcessorLogger;
        _cacheServiceProvider = cacheServiceProvider;
        _sqlCommandsProcessor = sqlCommandsProcessor;
        _cacheKeyPrefixProvider = cacheKeyPrefixProvider;

        if (cacheSettings == null)
        {
            throw new ArgumentNullException(nameof(cacheSettings));
        }

        _cacheSettings = cacheSettings.Value;
    }

    /// <summary>
    ///     Finds the related table names of the current query.
    /// </summary>
    public SortedSet<string> GetCacheDependencies(DbCommand command, DbContext context, EFCoreCachePolicy cachePolicy)
    {
        if (command == null)
        {
            throw new ArgumentNullException(nameof(command));
        }

        var tableNames = new SortedSet<string>(
                                               _sqlCommandsProcessor.GetAllTableNames(context).Select(x => x.TableName),
                                               StringComparer.OrdinalIgnoreCase);
        return GetCacheDependencies(cachePolicy, tableNames, command.CommandText);
    }

    /// <summary>
    ///     Finds the related table names of the current query.
    /// </summary>
    public SortedSet<string> GetCacheDependencies(EFCoreCachePolicy cachePolicy, SortedSet<string> tableNames,
                                                  string commandText)
    {
        if (cachePolicy == null)
        {
            throw new ArgumentNullException(nameof(cachePolicy));
        }

        var textsInsideSquareBrackets = _sqlCommandsProcessor.GetSqlCommandTableNames(commandText);
        var cacheDependencies = new SortedSet<string>(
                                                      tableNames.Intersect(textsInsideSquareBrackets,
                                                                           StringComparer.OrdinalIgnoreCase),
                                                      StringComparer.OrdinalIgnoreCase);
        if (cacheDependencies.Count != 0)
        {
            LogProcess(tableNames, textsInsideSquareBrackets, cacheDependencies);
            ////return PrefixCacheDependencies(cacheDependencies);
            return cacheDependencies;
        }

        cacheDependencies = cachePolicy.CacheItemsDependencies as SortedSet<string>;
        if (cacheDependencies is { Count: 0 })
        {
            if (_logger.IsLoggerEnabled)
            {
                _dependenciesProcessorLogger
                    .LogDebug("It's not possible to calculate the related table names of the current query[{CommandText}]. Please use EFCachePolicy.Configure(options => options.CacheDependencies(\"real_table_name_1\", \"real_table_name_2\")) to specify them explicitly.",
                              commandText);
            }

            cacheDependencies = new SortedSet<string>(StringComparer.OrdinalIgnoreCase)
                            {
                                EFCoreCachePolicy.UnknownsCacheDependency,
                            };
        }

        LogProcess(tableNames, textsInsideSquareBrackets, cacheDependencies);
        ////return PrefixCacheDependencies(cacheDependencies);
        return cacheDependencies ?? new SortedSet<string>();
    }

    /// <summary>
    ///     Invalidates all of the cache entries which are dependent on any of the specified root keys.
    /// </summary>
    public bool InvalidateCacheDependencies(string commandText)
    {
        if (!_sqlCommandsProcessor.IsCrudCommand(commandText))
        {
            if (_logger.IsLoggerEnabled)
            {
                _dependenciesProcessorLogger.LogDebug("Skipped invalidating a none-CRUD command[{CommandText}].",
                                                      commandText);
            }

            return false;
        }

        if (ShouldSkipCacheInvalidationCommands(commandText))
        {
            if (_logger.IsLoggerEnabled)
            {
                _dependenciesProcessorLogger
                    .LogDebug("Skipped invalidating the related cache entries of this query[{CommandText}] based on the provided predicate.",
                              commandText);
            }

            return false;
        }

        _cacheServiceProvider.InvalidateItem(commandText);

        return true;
    }

    private void LogProcess(SortedSet<string> tableNames, SortedSet<string> textsInsideSquareBrackets,
                            SortedSet<string>? cacheDependencies)
    {
        if (_logger.IsLoggerEnabled)
        {
            _dependenciesProcessorLogger
                .LogDebug("ContextTableNames: {Names}, PossibleQueryTableNames: {Texts} -> CacheDependencies: {Dependencies}.",
                          string.Join(", ", tableNames),
                          string.Join(", ", cacheDependencies ?? new SortedSet<string>(StringComparer.Ordinal)),
                          string.Join(", ", textsInsideSquareBrackets));
        }
    }

    private bool ShouldSkipCacheInvalidationCommands(string commandText) =>
        _cacheSettings.SkipCacheInvalidationCommands != null &&
        _cacheSettings.SkipCacheInvalidationCommands(commandText);

    private SortedSet<string> PrefixCacheDependencies(SortedSet<string>? cacheDependencies)
    {
        if (cacheDependencies is null)
        {
            return new SortedSet<string>(StringComparer.Ordinal);
        }

        var cacheKeyPrefix = _cacheKeyPrefixProvider.GetCacheKeyPrefix();
        return new SortedSet<string>(cacheDependencies.Select(x => $"{cacheKeyPrefix}{x}"),
                                     StringComparer.OrdinalIgnoreCase);
    }
}
