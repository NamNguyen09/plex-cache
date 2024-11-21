using System.Collections;
using System.Data.Common;
using System.Globalization;
using System.Text;
using cx.BinarySerializer.EFCache.Tables;
using EFCoreCache.CachePolicies;
using EFCoreCache.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace EFCoreCache.Processors;
public class DbCommandInterceptorProcessor : IDbCommandInterceptorProcessor
{
    private readonly IEFCoreCacheDependenciesProcessor _cacheDependenciesProcessor;
    private readonly IEFCoreCachePolicyParser _cachePolicyParser;
    private readonly IEFCoreCacheServiceProvider _cacheService;
    private readonly IEFCoreCacheServiceCheck _cacheServiceCheck;
    private readonly EFCoreCacheSettings _cacheSettings;
    private readonly ILogger<DbCommandInterceptorProcessor> _interceptorProcessorLogger;
    private readonly IEFCoreDebugLogger _logger;
    private readonly IEFCoreSqlCommandsProcessor _sqlCommandsProcessor;

    /// <summary>
    ///     Helps processing SecondLevelCacheInterceptor
    /// </summary>
    public DbCommandInterceptorProcessor(IEFCoreCacheDependenciesProcessor cacheDependenciesProcessor,
                                        IEFCoreCachePolicyParser cachePolicyParser,
                                        IEFCoreCacheServiceProvider cacheService,
                                        IEFCoreCacheServiceCheck cacheServiceCheck,
                                        IOptions<EFCoreCacheSettings> cacheSettings,
                                        ILogger<DbCommandInterceptorProcessor> interceptorProcessorLogger,
                                        IEFCoreDebugLogger logger,
                                        IEFCoreSqlCommandsProcessor sqlCommandsProcessor)
    {
        if (cacheSettings == null)
        {
            throw new ArgumentNullException(nameof(cacheSettings));
        }

        _cacheSettings = cacheSettings.Value;

        _cacheDependenciesProcessor = cacheDependenciesProcessor;
        _cachePolicyParser = cachePolicyParser;
        _cacheService = cacheService;
        _cacheServiceCheck = cacheServiceCheck;
        _logger = logger;
        _interceptorProcessorLogger = interceptorProcessorLogger;
        _sqlCommandsProcessor = sqlCommandsProcessor;
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

        try
        {
            if (!_cacheServiceCheck.IsCacheServiceAvailable())
            {
                return result;
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

            var cacheDependencies = _cacheDependenciesProcessor.GetCacheDependencies(command, context, cachePolicy ?? new EFCoreCachePolicy());
            var dependencyEntitySets = new List<string>();
            foreach (var entitySet in cacheDependencies)
            {
                if (entitySet == null) continue;
                dependencyEntitySets.Add($"{entitySet}Entity");
            }

            string commandText = command.CommandText;
            if (_cacheDependenciesProcessor.InvalidateCacheDependencies(commandText, dependencyEntitySets))
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

            var distributedCacheOption = new DistributedCacheEntryOptions
            {
                AbsoluteExpiration = DateTimeOffset.Now.AddMinutes(cachePolicy.CacheTimeout.TotalMinutes),
                AbsoluteExpirationRelativeToNow = cachePolicy.CacheTimeout
            };

            string efCacheKey = GetEFCacheKey(command);
            if (result is int data)
            {
                if (ShouldSkipCachingResults(commandText, data)) return result;
                _cacheService.PutItem(efCacheKey, new EFCoreCachedData { NonQuery = data }, dependencyEntitySets, distributedCacheOption);

                if (!_logger.IsLoggerEnabled) return result;
                _interceptorProcessorLogger.LogDebug(CacheableEventId.QueryResultCached,
                                                     "[{Data}] added to the cache[{EfCacheKey}].", data, efCacheKey);
                return result;
            }

            if (result is DbDataReader dataReader)
            {
                EFCoreTableRows tableRows;
                using (var dbReaderLoader = new EFCoreDataReaderLoader(dataReader))
                {
                    tableRows = dbReaderLoader.LoadAndClose();
                }

                if (ShouldSkipCachingResults(commandText, tableRows)) return (T)(object)new EFCoreTableRowsDataReader(tableRows);

                _cacheService.PutItem(efCacheKey, new EFCoreCachedData { TableRows = tableRows, IsNull = tableRows == null }, dependencyEntitySets, distributedCacheOption);

                if (tableRows == null) tableRows = new EFCoreTableRows();
                if (!_logger.IsLoggerEnabled) return (T)(object)new EFCoreTableRowsDataReader(tableRows);

                _interceptorProcessorLogger.LogDebug(CacheableEventId.QueryResultCached,
                                                     "TableRows[{TableName}] added to the cache[{EfCacheKey}].",
                                                     tableRows.TableName, efCacheKey);

                return (T)(object)new EFCoreTableRowsDataReader(tableRows);
            }

            if (result is object)
            {
                if (ShouldSkipCachingResults(commandText, result)) return result;

                _cacheService.PutItem(efCacheKey, new EFCoreCachedData { Scalar = result, IsNull = result == null },
                                      dependencyEntitySets, distributedCacheOption);

                if (!_logger.IsLoggerEnabled) return result;

                _interceptorProcessorLogger.LogDebug(CacheableEventId.QueryResultCached,
                                                     "[{Result}] added to the cache[{EfCacheKey}].",
                                                     result, efCacheKey);
                return result;
            }

            return result;
        }
        catch (Exception ex)
        {
            if (!_cacheSettings.UseDbCallsIfCachingProviderIsDown)
            {
                throw;
            }

            if (_logger.IsLoggerEnabled)
            {
                _interceptorProcessorLogger.LogCritical(ex, "Interceptor Error");
            }

            return result;
        }
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

        try
        {
            if (!_cacheServiceCheck.IsCacheServiceAvailable())
            {
                return result;
            }

            if (cachePolicy == null)
            {
                if (!_logger.IsLoggerEnabled) return result;

                _interceptorProcessorLogger.LogDebug("Skipping a none-cachable command[{CommandText}].",
                                                     command.CommandText);
                return result;
            }

            string efCacheKey = GetEFCacheKey(command);
            object? cacheValue;
            if (!_cacheService.GetItem(efCacheKey, out cacheValue))
            {
                if (!_logger.IsLoggerEnabled) return result;
                _interceptorProcessorLogger.LogDebug("[{EfCacheKey}] was not present in the cache.", efCacheKey);
                return result;
            }
            if (cacheValue == null) return result;

            EFCoreCachedData? cacheResult = new EFCoreCachedData { IsNull = true };
            if (cacheValue != null
                && cacheValue.GetType() == typeof(JObject))
            {
                cacheResult = JsonConvert.DeserializeObject<EFCoreCachedData>(Convert.ToString(cacheValue) ?? "")
                                                               ?? new EFCoreCachedData { IsNull = true };
            }
            else if (cacheValue != null
                && cacheValue.GetType() == typeof(EFCoreCachedData))
            {
                cacheResult = (EFCoreCachedData)cacheValue;
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
                    return (T)Convert.ChangeType(InterceptionResult<DbDataReader>.SuppressWithResult(rows),
                                                 typeof(T), CultureInfo.InvariantCulture);
                }

                if (_logger.IsLoggerEnabled)
                {
                    _interceptorProcessorLogger
                        .LogDebug("Suppressed the result with the TableRows[{TableName}] from the cache[{EfCacheKey}].",
                                  nameof(result), efCacheKey);
                }

                using var dataRows = new EFCoreTableRowsDataReader(cacheResult.TableRows);
                return (T)Convert.ChangeType(InterceptionResult<DbDataReader>.SuppressWithResult(dataRows),
                                             typeof(T), CultureInfo.InvariantCulture);
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

                return (T)Convert.ChangeType(InterceptionResult<int>.SuppressWithResult(cachedResult),
                                             typeof(T), CultureInfo.InvariantCulture);
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

                return (T)Convert.ChangeType(InterceptionResult<object>.SuppressWithResult(cachedResult ?? new object()),
                                             typeof(T), CultureInfo.InvariantCulture);
            }

            if (_logger.IsLoggerEnabled)
            {
                _interceptorProcessorLogger.LogDebug("Skipped the result with {Type} type.", result?.GetType());
            }

            return result;
        }
        catch (Exception ex)
        {
            if (!_cacheSettings.UseDbCallsIfCachingProviderIsDown)
            {
                throw;
            }

            if (_logger.IsLoggerEnabled)
            {
                _interceptorProcessorLogger.LogCritical(ex, "Interceptor Error");
            }
            return result;
        }
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

        var commandText = command?.CommandText ?? "";
        var cachePolicy = GetCachePolicy(context, commandText);

        if (ShouldSkipQueriesInsideExplicitTransaction(command))
        {
            return (!_sqlCommandsProcessor.IsCrudCommand(commandText), cachePolicy);
        }

        if (_sqlCommandsProcessor.IsCrudCommand(commandText))
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
    private string GetEFCacheKey(DbCommand command, string saltKey = "")
    {
        var cacheKey = new StringBuilder();
        cacheKey.AppendLine(_cachePolicyParser.RemoveEFCachePolicyTag(command.CommandText));

        cacheKey.AppendLine("ConnectionString").Append('=').Append(command.Connection?.ConnectionString);

        foreach (DbParameter? parameter in command.Parameters)
        {
            if (parameter == null)
            {
                continue;
            }

            cacheKey.Append(parameter.ParameterName)
                    .Append('=').Append(GetParameterValue(parameter)).Append(',')
                    .Append("Size").Append('=').Append(parameter.Size).Append(',')
                    .Append("Precision").Append('=').Append(parameter.Precision).Append(',')
                    .Append("Scale").Append('=').Append(parameter.Scale).Append(',')
                    .Append("Direction").Append('=').Append(parameter.Direction).Append(',');
        }

        cacheKey.AppendLine("SaltKey").Append('=').Append(saltKey);
        return cacheKey.ToString().Trim();
    }
    private static string? GetParameterValue(DbParameter parameter)
    {
        return parameter.Value switch
        {
            DBNull => "null",
            null => "null",
            byte[] buffer => BytesToHex(buffer),
            Array array => EnumerableToString(array),
            IEnumerable enumerable => EnumerableToString(enumerable),
            _ => Convert.ToString(parameter.Value, CultureInfo.InvariantCulture),
        };
    }
    private static string EnumerableToString(IEnumerable array)
    {
        var sb = new StringBuilder();
        foreach (var item in array)
        {
            sb.Append(item);
        }

        return sb.ToString();
    }

    private static string BytesToHex(byte[] buffer)
    {
        var sb = new StringBuilder(buffer.Length * 2);
        foreach (var @byte in buffer)
        {
            sb.Append(@byte.ToString("X2", CultureInfo.InvariantCulture));
        }

        return sb.ToString();
    }
}