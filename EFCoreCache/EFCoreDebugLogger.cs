using EFCoreCache.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EFCoreCache;

public class EFCoreDebugLogger : IEFCoreDebugLogger
{
    /// <summary>
    ///     Formats and writes a debug log message.
    /// </summary>
    public EFCoreDebugLogger(
        IOptions<EFCoreCacheSettings> cacheSettings,
        ILogger<EFCoreDebugLogger> logger)
    {
        if (cacheSettings == null)
        {
            throw new ArgumentNullException(nameof(cacheSettings));
        }

        var disableLogging = cacheSettings.Value.DisableLogging;
        if (logger is null)
        {
            throw new ArgumentNullException(nameof(logger));
        }

        IsLoggerEnabled = !disableLogging && logger.IsEnabled(LogLevel.Debug);
        if (IsLoggerEnabled)
        {
            logger.LogDebug("InstanceId: {Id}, Started @{Date} UTC.", Guid.NewGuid(), DateTime.UtcNow);
        }
    }

    /// <summary>
    ///     Determines whether the debug logger is enabled.
    /// </summary>
    public bool IsLoggerEnabled { get; }
}