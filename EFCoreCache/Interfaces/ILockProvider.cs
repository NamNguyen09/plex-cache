namespace EFCoreCache.Interfaces;
public interface ILockProvider
{
    /// <summary>
    ///     Tries to enter the sync lock
    /// </summary>
    IDisposable Lock(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Tries to enter the async lock
    /// </summary>
    Task<IDisposable> LockAsync(CancellationToken cancellationToken = default);
}