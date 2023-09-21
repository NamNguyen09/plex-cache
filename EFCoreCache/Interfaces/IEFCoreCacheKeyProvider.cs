using System.Data.Common;
using EFCoreCache.CachePolicies;
using Microsoft.EntityFrameworkCore;

namespace EFCoreCache.Interfaces;

public interface IEFCoreCacheKeyProvider
{
    /// <summary>
    ///     Gets an EF query and returns its hashed key to store in the cache.
    /// </summary>
    /// <param name="command">The EF query.</param>
    /// <param name="context">DbContext is a combination of the Unit Of Work and Repository patterns.</param>
    /// <param name="cachePolicy">determines the Expiration time of the cache.</param>
    /// <returns>Information of the computed key of the input LINQ query.</returns>
    EFCoreCacheKey GetEFCacheKey(DbCommand command, DbContext context, EFCoreCachePolicy cachePolicy);
}
