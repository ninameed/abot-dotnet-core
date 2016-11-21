using Abot.Core;
using Abot.Poco;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using Robots;

namespace Abot.Crawler
{
    using Abot.Util;
    using System.Threading;

    /// <summary>
    /// Polite web crawler
    /// </summary>
    public interface IPoliteWebCrawler : IWebCrawler
    {
        /// <summary>
        /// Event occur after robots txt is parsed asynchroniously
        /// </summary>
        event EventHandler<RobotsDotTextParseCompletedArgs> RobotsDotTextParseCompletedAsync;
        /// <summary>
        /// Event occur after robots txt is parsed synchroniously
        /// </summary>
        event EventHandler<RobotsDotTextParseCompletedArgs> RobotsDotTextParseCompleted;
    }
    /// <summary>
    /// Extends the WebCrawler class and added politeness features like crawl delays and respecting robots.txt files. 
    /// </summary>
    
    public class PoliteWebCrawler : WebCrawler, IPoliteWebCrawler
    {
        private static ILogger _logger = new LoggerFactory().CreateLogger("AbotLogger");
        protected IDomainRateLimiter _domainRateLimiter;
        protected IRobotsDotTextFinder _robotsDotTextFinder;
        protected IRobotsDotText _robotsDotText;

        public PoliteWebCrawler()
            : this(null, null, null, null, null, null, null, null, null)
        {
        }

        public PoliteWebCrawler(CrawlConfiguration crawlConfiguration)
            : this(crawlConfiguration, null, null, null, null, null, null, null, null)
        {
        }

        public PoliteWebCrawler(
            CrawlConfiguration crawlConfiguration,
            ICrawlDecisionMaker crawlDecisionMaker,
            IThreadManager threadManager,
            IScheduler scheduler,
            IPageRequester pageRequester,
            IHyperLinkParser hyperLinkParser,
            IMemoryManager memoryManager,
            IDomainRateLimiter domainRateLimiter,
            IRobotsDotTextFinder robotsDotTextFinder)
            : base(crawlConfiguration, crawlDecisionMaker, threadManager, scheduler, pageRequester, hyperLinkParser, memoryManager)
        {
            _domainRateLimiter = domainRateLimiter ?? new DomainRateLimiter(_crawlContext.CrawlConfiguration.MinCrawlDelayPerDomainMilliSeconds);
            _robotsDotTextFinder = robotsDotTextFinder ?? new RobotsDotTextFinder(new PageRequester(_crawlContext.CrawlConfiguration));
        }

        public override async Task<CrawlResult> CrawlAsync(Uri uri, CancellationTokenSource cancellationTokenSource)
        {
            var robotsDotTextCrawlDelayInSecs = 0;
            var robotsDotTextCrawlDelayInMillisecs = 0;

            //Load robots.txt
            if (_crawlContext.CrawlConfiguration.IsRespectRobotsDotTextEnabled)
            {
                _robotsDotText = await _robotsDotTextFinder.FindAsync(uri);

                if (_robotsDotText != null)
                {
                    FireRobotsDotTextParseCompletedAsync(_robotsDotText.Robots);
                    FireRobotsDotTextParseCompleted(_robotsDotText.Robots);

                    robotsDotTextCrawlDelayInSecs = _robotsDotText.GetCrawlDelay(_crawlContext.CrawlConfiguration.RobotsDotTextUserAgentString);
                    robotsDotTextCrawlDelayInMillisecs = robotsDotTextCrawlDelayInSecs * 1000;
                }
            }

            //Use whichever value is greater between the actual crawl delay value found, the max allowed crawl delay value or the minimum crawl delay required for every domain
            if (robotsDotTextCrawlDelayInSecs > 0 && robotsDotTextCrawlDelayInMillisecs > _crawlContext.CrawlConfiguration.MinCrawlDelayPerDomainMilliSeconds)
            {
                if (robotsDotTextCrawlDelayInSecs > _crawlContext.CrawlConfiguration.MaxRobotsDotTextCrawlDelayInSeconds)
                {
                    _logger.LogWarning($"[{uri}] robot.txt file directive [Crawl-delay: {_crawlContext.CrawlConfiguration.MaxRobotsDotTextCrawlDelayInSeconds}] is above the value set in the config value MaxRobotsDotTextCrawlDelay, will use MaxRobotsDotTextCrawlDelay value instead.");

                    robotsDotTextCrawlDelayInSecs = _crawlContext.CrawlConfiguration.MaxRobotsDotTextCrawlDelayInSeconds;
                    robotsDotTextCrawlDelayInMillisecs = robotsDotTextCrawlDelayInSecs * 1000;
                }

                _logger.LogWarning($"[{uri}] robot.txt file directive [Crawl-delay: {robotsDotTextCrawlDelayInSecs}] will be respected.");
                _domainRateLimiter.AddDomain(uri, robotsDotTextCrawlDelayInMillisecs);
            }

            PageCrawlStarting += (s, e) => _domainRateLimiter.RateLimit(e.PageToCrawl.Uri);

            return await base.CrawlAsync(uri, cancellationTokenSource);
        }

        protected override bool ShouldCrawlPage(PageToCrawl pageToCrawl)
        {
            var allowedByRobots = true;
            if (_robotsDotText != null)
                allowedByRobots = _robotsDotText.IsUrlAllowed(pageToCrawl.Uri.AbsoluteUri, _crawlContext.CrawlConfiguration.RobotsDotTextUserAgentString);


            //https://github.com/sjdirect/abot/issues/96 Handle scenario where the root is allowed but all the paths below are disallowed like "disallow: /*"
            var allPathsBelowRootAllowedByRobots = false;
            if (_robotsDotText != null && pageToCrawl.IsRoot && allowedByRobots)
            {
                var anyPathOffRoot = pageToCrawl.Uri.AbsoluteUri.EndsWith("/") ? pageToCrawl.Uri.AbsoluteUri + "aaaaa": pageToCrawl.Uri.AbsoluteUri + "/aaaaa";
                allPathsBelowRootAllowedByRobots = _robotsDotText.IsUrlAllowed(anyPathOffRoot, _crawlContext.CrawlConfiguration.RobotsDotTextUserAgentString);
            }

            if (_crawlContext.CrawlConfiguration.IsIgnoreRobotsDotTextIfRootDisallowedEnabled && pageToCrawl.IsRoot)    
            {
                if (!allowedByRobots)
                {
                    var message = $"Page [{pageToCrawl.Uri.AbsoluteUri}] [Disallowed by robots.txt file], however since IsIgnoreRobotsDotTextIfRootDisallowedEnabled is set to true the robots.txt file will be ignored for this site.";
                    _logger.LogDebug(message);
                    allowedByRobots = true;
                    _robotsDotText = null;
                }
                else if (!allPathsBelowRootAllowedByRobots)
                {
                    var message = $"All Pages below [{pageToCrawl.Uri.AbsoluteUri}] [Disallowed by robots.txt file], however since IsIgnoreRobotsDotTextIfRootDisallowedEnabled is set to true the robots.txt file will be ignored for this site.";
                    _logger.LogDebug(message);
                    allowedByRobots = true;
                    _robotsDotText = null;
                }

            }
            else if (!allowedByRobots)
            {
                var message = $"Page [{pageToCrawl.Uri.AbsoluteUri}] not crawled, [Disallowed by robots.txt file], set IsRespectRobotsDotText=false in config file if you would like to ignore robots.txt files.";
                _logger.LogDebug(message);

                FirePageCrawlDisallowedEventAsync(pageToCrawl, message);
                FirePageCrawlDisallowedEvent(pageToCrawl, message);

                return false;
            }

            return allowedByRobots && base.ShouldCrawlPage(pageToCrawl);
        }

        /// <summary>
        /// Event occur after robots txt is parsed asynchroniously
        /// </summary>
        public event EventHandler<RobotsDotTextParseCompletedArgs> RobotsDotTextParseCompletedAsync;

        /// <summary>
        /// Event occur after robots txt is parsed synchroniously
        /// </summary>
        public event EventHandler<RobotsDotTextParseCompletedArgs> RobotsDotTextParseCompleted;

        /// <summary>
        /// Fire robots txt parsed completed async
        /// </summary>
        /// <param name="robots"></param>
        protected virtual void FireRobotsDotTextParseCompletedAsync(IRobots robots)
        {
            var threadSafeEvent = RobotsDotTextParseCompletedAsync;
            if (threadSafeEvent == null) return;
            //Fire each subscribers delegate async
            foreach (var @delegate in threadSafeEvent.GetInvocationList())
            {
                var del = (EventHandler<RobotsDotTextParseCompletedArgs>) @delegate;
                Task.Run(() => { del(this, new RobotsDotTextParseCompletedArgs(_crawlContext, robots)); });
            }
        }

        /// <summary>
        /// Fire robots txt parsed completed
        /// </summary>
        /// <param name="robots"></param>
        protected virtual void FireRobotsDotTextParseCompleted(IRobots robots)
        {
            try
            {
                if (RobotsDotTextParseCompleted == null) return;
                RobotsDotTextParseCompleted.Invoke(this, new RobotsDotTextParseCompletedArgs(_crawlContext, robots));
            }
            catch (Exception e)
            {
                _logger.LogError("An unhandled exception was thrown by a subscriber of the PageLinksCrawlDisallowed event for robots.txt", e);
            }
        }
    }
}
