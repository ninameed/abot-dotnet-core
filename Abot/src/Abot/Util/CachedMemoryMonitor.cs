using Microsoft.Extensions.Logging;
using System;
using System.Threading;

namespace Abot.Util
{
    
    public class CachedMemoryMonitor : IMemoryMonitor, IDisposable
    {
        static ILogger _logger = new LoggerFactory().CreateLogger("AbotLogger");
        IMemoryMonitor _memoryMonitor;
        Timer _usageRefreshTimer;
        int _cachedCurrentUsageInMb;

        public CachedMemoryMonitor(IMemoryMonitor memoryMonitor, int cacheExpirationInSeconds)
        {
            if (memoryMonitor == null)
                throw new ArgumentNullException(nameof(memoryMonitor));

            if (cacheExpirationInSeconds < 1)
                cacheExpirationInSeconds = 5;

            _memoryMonitor = memoryMonitor;

            UpdateCurrentUsageValue();

            _usageRefreshTimer = new Timer(UpdateCurrentUsageValue, null, cacheExpirationInSeconds * 1000, cacheExpirationInSeconds * 1000);
        }

        protected virtual void UpdateCurrentUsageValue(object state = null)
        {
            var oldUsage = _cachedCurrentUsageInMb;
            _cachedCurrentUsageInMb = _memoryMonitor.GetCurrentUsageInMb();
            _logger.LogDebug($"Updated cached memory usage value from [{oldUsage}mb] to [{_cachedCurrentUsageInMb}mb]");
        }

        public virtual int GetCurrentUsageInMb()
        {
            return _cachedCurrentUsageInMb;
        }

        public void Dispose()
        {
            _usageRefreshTimer.Dispose();
        }
    }
}
