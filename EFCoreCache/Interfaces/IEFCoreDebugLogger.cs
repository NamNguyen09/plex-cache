namespace EFCoreCache.Interfaces;

public interface IEFCoreDebugLogger
{
    /// <summary>
    ///     Determines whether the debug logger is enabled.
    /// </summary>
    bool IsLoggerEnabled { get; }
}
