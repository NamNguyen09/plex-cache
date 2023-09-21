using System.Data.Common;
using EFCoreCache.CachePolicies;
using Microsoft.EntityFrameworkCore;

namespace EFCoreCache.Interfaces;
public interface IDbCommandInterceptorProcessor
{
    /// <summary>
    ///     Reads data from cache or cache it and then returns the result
    /// </summary>
    T ProcessExecutedCommands<T>(DbCommand command, DbContext? context, T result, EFCoreCachePolicy? cachePolicy);

    /// <summary>
    ///     Adds command's data to the cache
    /// </summary>
    T ProcessExecutingCommands<T>(DbCommand command, DbContext? context, T result, EFCoreCachePolicy? cachePolicy);

    /// <summary>
    ///     Is this command marked for caching?
    /// </summary>
    (bool ShouldSkipProcessing, EFCoreCachePolicy? CachePolicy) ShouldSkipProcessing(DbCommand? command, DbContext? context,
                                                                                                    CancellationToken cancellationToken = default);
}