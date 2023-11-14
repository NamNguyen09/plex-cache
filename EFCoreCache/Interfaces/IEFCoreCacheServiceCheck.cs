namespace EFCoreCache.Interfaces
{
    public interface IEFCoreCacheServiceCheck
    {
        /// <summary>
        ///     Is the configured cache services online and available? Can we use it without any problem?
        /// </summary>
        bool IsCacheServiceAvailable();
    }
}
