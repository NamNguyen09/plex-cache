using cx.BinarySerializer.EFCache.Tables;
using EFCoreCache.CachePolicies;

namespace EFCoreCache.Interfaces;
public interface IEFCoreCachePolicyParser
{
    /// <summary>
    ///     Converts the `commandText` to an instance of `EFCachePolicy`
    /// </summary>
    EFCoreCachePolicy? GetEFCachePolicy(string commandText, IList<TableEntityInfo> allEntityTypes);

    /// <summary>
    ///     Does `commandText` contain EFCachePolicyTagPrefix?
    /// </summary>
    bool HasEFCachePolicy(string commandText);

    /// <summary>
    ///     Removes the EFCachePolicy line from the commandText
    /// </summary>
    string RemoveEFCachePolicyTag(string commandText);
}
