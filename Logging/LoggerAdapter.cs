using Common;
using Microsoft.Extensions.Logging;
using WatchDog;

namespace Logging;

public class LoggerAdapter<T> : IAppLogger<T>
{
    private readonly ILogger<T> _logger;

    public LoggerAdapter(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<T>();
    }

    private static void SafeWatchLog(string message)
    {
        try
        {
            WatchLogger.Log(message);
        }
        catch
        {
            // WatchDog not initialized or unavailable; ignore safely.
        }
    }

    public void LogInformation(string message, params object[] args)
    {
        _logger.LogInformation(message, args);
        SafeWatchLog(message);
    }

    public void LogWarning(string message, params object[] args)
    {
        _logger.LogWarning(message, args);
        SafeWatchLog(message);
    }

    public void LogError(string message, params object[] args)
    {
        _logger.LogError(message, args);
        SafeWatchLog(message);
    }
}