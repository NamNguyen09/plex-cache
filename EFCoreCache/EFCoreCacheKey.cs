namespace EFCoreCache
{
    public class EFCoreCacheKey
    {
        public EFCoreCacheKey(ISet<string> cacheDependencies) => CacheDependencies = cacheDependencies;
        public string KeyHash { set; get; } = default!;
        public Type? DbContext { get; set; }
        public ISet<string> CacheDependencies { get; }
        public override bool Equals(object? obj)
        {
            if (obj is not EFCoreCacheKey efCacheKey)
            {
                return false;
            }

            return string.Equals(KeyHash, efCacheKey.KeyHash, StringComparison.Ordinal) && DbContext == efCacheKey.DbContext;
        }
        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 17;
                return hash * 23 + KeyHash.GetHashCode(StringComparison.Ordinal) + (DbContext == null ? 0 : DbContext.Name.GetHashCode(StringComparison.Ordinal));
            }
        }
        public override string ToString() =>
            $"KeyHash: {KeyHash}, DbContext: {DbContext?.Name}, CacheDependencies: {string.Join(", ", CacheDependencies)}.";
    }
}
