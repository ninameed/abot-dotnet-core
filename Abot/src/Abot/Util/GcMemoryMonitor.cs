using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;

namespace Abot.Util
{
    public interface IMemoryMonitor : IDisposable
    {
        int GetCurrentUsageInMb();
    }

    
    public class GcMemoryMonitor : IMemoryMonitor
    {
        static ILogger _logger = new LoggerFactory().CreateLogger("AbotLogger");

        public virtual int GetCurrentUsageInMb()
        {
            var timer = Stopwatch.StartNew();
            var currentUsageInMb = Convert.ToInt32(GC.GetTotalMemory(false) / (1024 * 1024));
            timer.Stop();

            _logger.LogDebug($"GC reporting [{currentUsageInMb}mb] currently thought to be allocated, took [{timer.ElapsedMilliseconds}] millisecs");

            return currentUsageInMb;       
        }

        public void Dispose()
        {
            //do nothing
        }
    }
}
