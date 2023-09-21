using EFCoreCache.Enums;

namespace EFCoreCache.CacheQueriesOptions
{
    public class CacheAllQueriesOptions
    {
        public CacheExpirationMode ExpirationMode { set; get; }
        public TimeSpan Timeout { set; get; }

        /// <summary>
        ///     Enables or disables the `CacheAllQueries` feature.
        /// </summary>
        public bool IsActive { set; get; }
    }
}
