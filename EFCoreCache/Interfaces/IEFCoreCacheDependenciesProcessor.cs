using System.Data.Common;
using EFCoreCache.CachePolicies;
using Microsoft.EntityFrameworkCore;

namespace EFCoreCache.Interfaces;
public interface IEFCoreCacheDependenciesProcessor
{
    /// <summary>
    ///     Finds the related table names of the current query.
    /// </summary>
    SortedSet<string> GetCacheDependencies(DbCommand command, DbContext context, EFCoreCachePolicy cachePolicy);

    /// <summary>
    ///     Finds the related table names of the current query.
    /// </summary>
    SortedSet<string> GetCacheDependencies(EFCoreCachePolicy cachePolicy, SortedSet<string> tableNames, string commandText);

    /// <summary>
    ///     Invalidates all of the cache entries which are dependent on any of the specified root keys.
    /// </summary>
    bool InvalidateCacheDependencies(string commandText, EFCoreCacheKey cacheKey);
}
