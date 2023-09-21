using System.Data.Common;
using System.Globalization;
using EFCoreCache.CachePolicies;
using EFCoreCache.Interfaces;
using EFCoreCache.Tables;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EFCoreCache.Processors;
public class DbCommandInterceptorProcessor : IDbCommandInterceptorProcessor
{
    private readonly IEFCoreCacheDependenciesProcessor _cacheDependenciesProcessor;
    private readonly IEFCoreCacheKeyProvider _cacheKeyProvider;
    private readonly IEFCoreCachePolicyParser _cachePolicyParser;
    private readonly IEFCoreCacheServiceProvider _cacheService;
    private readonly EFCoreCacheSettings _cacheSettings;
    private readonly ILogger<DbCommandInterceptorProcessor> _interceptorProcessorLogger;
    private readonly IEFCoreDebugLogger _logger;
    private readonly IEFCoreSqlCommandsProcessor _sqlCommandsProcessor;

    /// <summary>
    ///     Helps processing SecondLevelCacheInterceptor
    /// </summary>
    public DbCommandInterceptorProcessor(
        IEFCoreDebugLogger logger,
        ILogger<DbCommandInterceptorProcessor> interceptorProcessorLogger,
        IEFCoreCacheServiceProvider cacheService,
        IEFCoreCacheDependenciesProcessor cacheDependenciesProcessor,
        IEFCoreCacheKeyProvider cacheKeyProvider,
        IEFCoreCachePolicyParser cachePolicyParser,
        IEFCoreSqlCommandsProcessor sqlCommandsProcessor,
        IOptions<EFCoreCacheSettings> cacheSettings)
    {
        _cacheService = cacheService;
        _cacheDependenciesProcessor = cacheDependenciesProcessor;
        _cacheKeyProvider = cacheKeyProvider;
        _cachePolicyParser = cachePolicyParser;
        _logger = logger;
        _interceptorProcessorLogger = interceptorProcessorLogger;
        _sqlCommandsProcessor = sqlCommandsProcessor;

        if (cacheSettings == null)
        {
            throw new ArgumentNullException(nameof(cacheSettings));
        }

        _cacheSettings = cacheSettings.Value;
    }

    /// <summary>
    ///     Reads data from cache or cache it and then returns the result
    /// </summary>
    public T ProcessExecutedCommands<T>(DbCommand command, DbContext? context, T result, EFCoreCachePolicy? cachePolicy)
    {
        if (command == null)
        {
            throw new ArgumentNullException(nameof(command));
        }

        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        if (result is EFCoreTableRowsDataReader rowsReader)
        {
            if (_logger.IsLoggerEnabled)
            {
                _interceptorProcessorLogger.LogDebug(CacheableEventId.CacheHit,
                                                     "Returning the cached TableRows[{TableName}].",
                                                     rowsReader.TableName);
            }

            return result;
        }

        var commandText = command.CommandText;
        var efCacheKey = _cacheKeyProvider.GetEFCacheKey(command, context, cachePolicy ?? new EFCoreCachePolicy());
        if (_cacheDependenciesProcessor.InvalidateCacheDependencies(commandText, efCacheKey))
        {
            return result;
        }

        if (cachePolicy == null)
        {
            if (_logger.IsLoggerEnabled)
            {
                _interceptorProcessorLogger.LogDebug("Skipping a none-cachable command[{CommandText}].", commandText);
            }

            return result;
        }

        if (result is int data)
        {
            if (!ShouldSkipCachingResults(commandText, data))
            {
                _cacheService.InsertValue(efCacheKey, new EFCoreCachedData { NonQuery = data }, cachePolicy);

                if (_logger.IsLoggerEnabled)
                {
                    _interceptorProcessorLogger.LogDebug(CacheableEventId.QueryResultCached,
                                                         "[{Data}] added to the cache[{EfCacheKey}].", data,
                                                         efCacheKey);
                }
            }

            return result;
        }

        if (result is DbDataReader dataReader)
        {
            EFCoreTableRows tableRows;
            using (var dbReaderLoader = new EFCoreDataReaderLoader(dataReader))
            {
                tableRows = dbReaderLoader.LoadAndClose();
            }

            if (!ShouldSkipCachingResults(commandText, tableRows))
            {
                _cacheService.InsertValue(efCacheKey, new EFCoreCachedData { TableRows = tableRows }, cachePolicy);

                if (_logger.IsLoggerEnabled)
                {
                    _interceptorProcessorLogger.LogDebug(CacheableEventId.QueryResultCached,
                                                         "TableRows[{TableName}] added to the cache[{EfCacheKey}].",
                                                         tableRows.TableName, efCacheKey);
                }
            }

            return (T)(object)new EFCoreTableRowsDataReader(tableRows);
        }

        if (result is object)
        {
            if (!ShouldSkipCachingResults(commandText, result))
            {
                _cacheService.InsertValue(efCacheKey, new EFCoreCachedData { Scalar = result }, cachePolicy);

                if (_logger.IsLoggerEnabled)
                {
                    _interceptorProcessorLogger.LogDebug(CacheableEventId.QueryResultCached,
                                                         "[{Result}] added to the cache[{EfCacheKey}].",
                                                         result, efCacheKey);
                }
            }

            return result;
        }

        return result;
    }

    /// <summary>
    ///     Reads command's data from the cache, if any.
    /// </summary>
    public T ProcessExecutingCommands<T>(DbCommand command, DbContext? context, T result, EFCoreCachePolicy? cachePolicy)
    {
        if (command == null)
        {
            throw new ArgumentNullException(nameof(command));
        }

        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        var commandText = command.CommandText;
        if (cachePolicy == null)
        {
            if (_logger.IsLoggerEnabled)
            {
                _interceptorProcessorLogger.LogDebug("Skipping a none-cachable command[{CommandText}].",
                                                     commandText);
            }

            return result;
        }

        var efCacheKey = _cacheKeyProvider.GetEFCacheKey(command, context, cachePolicy);
        if (!(_cacheService.GetValue(efCacheKey, cachePolicy) is EFCoreCachedData cacheResult))
        {
            if (_logger.IsLoggerEnabled)
            {
                _interceptorProcessorLogger.LogDebug("[{EfCacheKey}] was not present in the cache.", efCacheKey);
            }

            return result;
        }

        if (result is InterceptionResult<DbDataReader>)
        {
            if (cacheResult.IsNull || cacheResult.TableRows == null)
            {
                if (_logger.IsLoggerEnabled)
                {
                    _interceptorProcessorLogger.LogDebug("Suppressed the result with an empty TableRows.");
                }

                using var rows = new EFCoreTableRowsDataReader(new EFCoreTableRows());
                return (T)Convert.ChangeType(
                                             InterceptionResult<DbDataReader>.SuppressWithResult(rows),
                                             typeof(T),
                                             CultureInfo.InvariantCulture);
            }

            if (_logger.IsLoggerEnabled)
            {
                _interceptorProcessorLogger
                    .LogDebug("Suppressed the result with the TableRows[{TableName}] from the cache[{EfCacheKey}].",
                              cacheResult.TableRows.TableName, efCacheKey);
            }

            using var dataRows = new EFCoreTableRowsDataReader(cacheResult.TableRows);
            return (T)Convert.ChangeType(
                                         InterceptionResult<DbDataReader>.SuppressWithResult(dataRows),
                                         typeof(T),
                                         CultureInfo.InvariantCulture);
        }

        if (result is InterceptionResult<int>)
        {
            var cachedResult = cacheResult.IsNull ? default : cacheResult.NonQuery;

            if (_logger.IsLoggerEnabled)
            {
                _interceptorProcessorLogger
                    .LogDebug("Suppressed the result with {CachedResult} from the cache[{EfCacheKey}].",
                              cachedResult, efCacheKey);
            }

            return (T)Convert.ChangeType(
                                         InterceptionResult<int>.SuppressWithResult(cachedResult),
                                         typeof(T),
                                         CultureInfo.InvariantCulture);
        }

        if (result is InterceptionResult<object>)
        {
            var cachedResult = cacheResult.IsNull ? default : cacheResult.Scalar;

            if (_logger.IsLoggerEnabled)
            {
                _interceptorProcessorLogger
                    .LogDebug("Suppressed the result with {CachedResult} from the cache[{EfCacheKey}].",
                              cachedResult, efCacheKey);
            }

            return (T)Convert.ChangeType(
                                         InterceptionResult<object>.SuppressWithResult(cachedResult ?? new object()),
                                         typeof(T),
                                         CultureInfo.InvariantCulture);
        }

        if (_logger.IsLoggerEnabled)
        {
            _interceptorProcessorLogger.LogDebug("Skipped the result with {Type} type.",
                                                 result?.GetType());
        }

        return result;
    }

    /// <summary>
    ///     Is this command marked for caching?
    /// </summary>
    public (bool ShouldSkipProcessing, EFCoreCachePolicy? CachePolicy) ShouldSkipProcessing(
        DbCommand? command, DbContext? context, CancellationToken cancellationToken = default)
    {
        if (context is null)
        {
            return (true, null);
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return (true, null);
        }

        var commandCommandText = command?.CommandText ?? "";
        var cachePolicy = GetCachePolicy(context, commandCommandText);

        if (ShouldSkipQueriesInsideExplicitTransaction(command))
        {
            return (!_sqlCommandsProcessor.IsCrudCommand(commandCommandText), cachePolicy);
        }

        if (_sqlCommandsProcessor.IsCrudCommand(commandCommandText))
        {
            return (false, cachePolicy);
        }

        return cachePolicy is null ? (true, null) : (false, cachePolicy);
    }

    private bool ShouldSkipQueriesInsideExplicitTransaction(DbCommand? command) =>
        !_cacheSettings.AllowCachingWithExplicitTransactions && command?.Transaction is not null;

    private EFCoreCachePolicy? GetCachePolicy(DbContext context, string commandText)
    {
        var allEntityTypes = _sqlCommandsProcessor.GetAllTableNames(context);
        return _cachePolicyParser.GetEFCachePolicy(commandText, allEntityTypes);
    }

    private bool ShouldSkipCachingResults(string commandText, object value)
    {
        var result = _cacheSettings.SkipCachingResults != null &&
                     _cacheSettings.SkipCachingResults((commandText, value));
        if (result && _logger.IsLoggerEnabled)
        {
            _interceptorProcessorLogger.LogDebug("Skipped caching of this result based on the provided predicate.");
        }

        return result;
    }
}