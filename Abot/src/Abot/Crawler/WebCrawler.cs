using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Abot.Core;
using Abot.Poco;
using Abot.Util;
using Microsoft.Extensions.Logging;
using Timer = System.Threading.Timer;
using AutoMapper;
using Microsoft.Extensions.Configuration;

namespace Abot.Crawler
{
    public interface IWebCrawler : IDisposable
    {
        /// <summary>
        /// Synchronous event that is fired before a page is crawled.
        /// </summary>
        event EventHandler<PageCrawlStartingArgs> PageCrawlStarting;

        /// <summary>
        /// Synchronous event that is fired when an individual page has been crawled.
        /// </summary>
        event EventHandler<PageCrawlCompletedArgs> PageCrawlCompleted;

        /// <summary>
        /// Synchronous event that is fired when the ICrawlDecisionMaker.ShouldCrawl impl returned false. This means the page or its links were not crawled.
        /// </summary>
        event EventHandler<PageCrawlDisallowedArgs> PageCrawlDisallowed;

        /// <summary>
        /// Synchronous event that is fired when the ICrawlDecisionMaker.ShouldCrawlLinks impl returned false. This means the page's links were not crawled.
        /// </summary>
        event EventHandler<PageLinksCrawlDisallowedArgs> PageLinksCrawlDisallowed;

        /// <summary>
        /// Asynchronous event that is fired before a page is crawled.
        /// </summary>
        event EventHandler<PageCrawlStartingArgs> PageCrawlStartingAsync;

        /// <summary>
        /// Asynchronous event that is fired when an individual page has been crawled.
        /// </summary>
        event EventHandler<PageCrawlCompletedArgs> PageCrawlCompletedAsync;

        /// <summary>
        /// Asynchronous event that is fired when the ICrawlDecisionMaker.ShouldCrawl impl returned false. This means the page or its links were not crawled.
        /// </summary>
        event EventHandler<PageCrawlDisallowedArgs> PageCrawlDisallowedAsync;

        /// <summary>
        /// Asynchronous event that is fired when the ICrawlDecisionMaker.ShouldCrawlLinks impl returned false. This means the page's links were not crawled.
        /// </summary>
        event EventHandler<PageLinksCrawlDisallowedArgs> PageLinksCrawlDisallowedAsync;

        /// <summary>
        /// Synchronous method that registers a delegate to be called to determine whether a page should be crawled or not
        /// </summary>
        void ShouldCrawlPage(Func<PageToCrawl, CrawlContext, CrawlDecision> decisionMaker);

        /// <summary>
        /// Synchronous method that registers a delegate to be called to determine whether the page's content should be dowloaded
        /// </summary>
        /// <param name="shouldDownloadPageContent"></param>
        void ShouldDownloadPageContent(Func<CrawledPage, CrawlContext, CrawlDecision> decisionMaker);

        /// <summary>
        /// Synchronous method that registers a delegate to be called to determine whether a page's links should be crawled or not
        /// </summary>
        /// <param name="shouldCrawlPageLinksDelegate"></param>
        void ShouldCrawlPageLinks(Func<CrawledPage, CrawlContext, CrawlDecision> decisionMaker);

        /// <summary>
        /// Synchronous method that registers a delegate to be called to determine whether a cerain link on a page should be scheduled to be crawled
        /// </summary>
        void ShouldScheduleLink(Func<Uri, CrawledPage, CrawlContext, bool> decisionMaker);

        /// <summary>
        /// Synchronous method that registers a delegate to be called to determine whether a page should be recrawled
        /// </summary>
        void ShouldRecrawlPage(Func<CrawledPage, CrawlContext, CrawlDecision> decisionMaker);

        /// <summary>
        /// Synchronous method that registers a delegate to be called to determine whether the 1st uri param is considered an internal uri to the second uri param
        /// </summary>
        /// <param name="decisionMaker delegate"></param>
        void IsInternalUri(Func<Uri, Uri, bool> decisionMaker);

        /// <summary>
        /// Begins a crawl using the uri param
        /// </summary>
        Task<CrawlResult> CrawlAsync(Uri uri);

        /// <summary>
        /// Begins a crawl using the uri param, and can be cancelled using the CancellationToken
        /// </summary>
        Task<CrawlResult> CrawlAsync(Uri uri, CancellationTokenSource tokenSource);

        /// <summary>
        /// Dynamic object that can hold any value that needs to be available in the crawl context
        /// </summary>
        dynamic CrawlBag { get; set; }
    }

    
    public abstract class WebCrawler : IWebCrawler
    {
        static ILogger _logger = new LoggerFactory().CreateLogger<WebCrawler>();
        protected bool _crawlComplete = false;
        protected bool _crawlStopReported = false;
        protected bool _crawlCancellationReported = false;
        protected bool _maxPagesToCrawlLimitReachedOrScheduled = false;
        protected Timer _timeoutTimer;
        protected CrawlResult _crawlResult = null;
        protected CrawlContext _crawlContext;
        protected IThreadManager _threadManager;
        protected IScheduler _scheduler;
        protected IPageRequester _pageRequester;
        protected IHyperLinkParser _hyperLinkParser;
        protected ICrawlDecisionMaker _crawlDecisionMaker;
        protected IMemoryManager _memoryManager;
        protected Func<PageToCrawl, CrawlContext, CrawlDecision> _shouldCrawlPageDecisionMaker;
        protected Func<CrawledPage, CrawlContext, CrawlDecision> _shouldDownloadPageContentDecisionMaker;
        protected Func<CrawledPage, CrawlContext, CrawlDecision> _shouldCrawlPageLinksDecisionMaker;
        protected Func<CrawledPage, CrawlContext, CrawlDecision> _shouldRecrawlPageDecisionMaker;
        protected Func<Uri, CrawledPage, CrawlContext, bool> _shouldScheduleLinkDecisionMaker;
        protected Func<Uri, Uri, bool> _isInternalDecisionMaker = (uriInQuestion, rootUri) => uriInQuestion.Authority == rootUri.Authority;
        

        /// <summary>
        /// Dynamic object that can hold any value that needs to be available in the crawl context
        /// </summary>
        public dynamic CrawlBag { get; set; }

        #region Constructors

        /// <summary>
        /// Creates a crawler instance with the default settings and implementations.
        /// </summary>
        public WebCrawler()
            : this(null, null, null, null, null, null, null)
        {
        }

        /// <summary>
        /// Creates a crawler instance with custom settings or implementation. Passing in null for all params is the equivalent of the empty constructor.
        /// </summary>
        /// <param name="threadManager">Distributes http requests over multiple threads</param>
        /// <param name="scheduler">Decides what link should be crawled next</param>
        /// <param name="pageRequester">Makes the raw http requests</param>
        /// <param name="hyperLinkParser">Parses a crawled page for it's hyperlinks</param>
        /// <param name="crawlDecisionMaker">Decides whether or not to crawl a page or that page's links</param>
        /// <param name="crawlConfiguration">Configurable crawl values</param>
        /// <param name="memoryManager">Checks the memory usage of the host process</param>
        public WebCrawler(
            CrawlConfiguration crawlConfiguration,
            ICrawlDecisionMaker crawlDecisionMaker,
            IThreadManager threadManager,
            IScheduler scheduler,
            IPageRequester pageRequester,
            IHyperLinkParser hyperLinkParser,
            IMemoryManager memoryManager)
        {
            _crawlContext = new CrawlContext();
            _crawlContext.CrawlConfiguration = crawlConfiguration ?? GetCrawlConfigurationFromConfigFile();
            CrawlBag = _crawlContext.CrawlBag;

            _threadManager = threadManager ?? new TaskThreadManager(_crawlContext.CrawlConfiguration.MaxConcurrentThreads > 0 ? _crawlContext.CrawlConfiguration.MaxConcurrentThreads : Environment.ProcessorCount);
            _scheduler = scheduler ?? new Scheduler(_crawlContext.CrawlConfiguration.IsUriRecrawlingEnabled, null, null);
            _pageRequester = pageRequester ?? new PageRequester(_crawlContext.CrawlConfiguration);
            _crawlDecisionMaker = crawlDecisionMaker ?? new CrawlDecisionMaker();

            if (_crawlContext.CrawlConfiguration.MaxMemoryUsageInMb > 0
                || _crawlContext.CrawlConfiguration.MinAvailableMemoryRequiredInMb > 0)
            {
#if NET46
                _memoryManager = memoryManager ?? new MemoryManager(new CachedMemoryMonitor(new GcMemoryMonitor(), _crawlContext.CrawlConfiguration.MaxMemoryUsageCacheTimeInSeconds));
#else
                _memoryManager = memoryManager ?? new CoreMemoryManager(new CachedMemoryMonitor(new GcMemoryMonitor(), _crawlContext.CrawlConfiguration.MaxMemoryUsageCacheTimeInSeconds));
#endif
            }
            _hyperLinkParser = hyperLinkParser ?? new HapHyperLinkParser(_crawlContext.CrawlConfiguration, null);

            _crawlContext.Scheduler = _scheduler;
        }

#endregion Constructors

        /// <summary>
        /// Begins a synchronous crawl using the uri param, subscribe to events to process data as it becomes available
        /// </summary>
        public virtual async Task<CrawlResult> CrawlAsync(Uri uri)
        {
            return await CrawlAsync(uri, null);
        }

        /// <summary>
        /// Begins a synchronous crawl using the uri param, subscribe to events to process data as it becomes available
        /// </summary>
        public virtual async Task<CrawlResult> CrawlAsync(Uri uri, CancellationTokenSource cancellationTokenSource)
        {
            if (uri == null)
                throw new ArgumentNullException(nameof(uri));

            _crawlContext.RootUri = _crawlContext.OriginalRootUri = uri;

            if (cancellationTokenSource != null)
                _crawlContext.CancellationTokenSource = cancellationTokenSource;

            _crawlResult = new CrawlResult();
            _crawlResult.RootUri = _crawlContext.RootUri;
            _crawlResult.CrawlContext = _crawlContext;
            _crawlComplete = false;

            _logger.LogInformation($"About to crawl site [{uri.AbsoluteUri}]");
            PrintConfigValues(_crawlContext.CrawlConfiguration);

            if (_memoryManager != null)
            {
                _crawlContext.MemoryUsageBeforeCrawlInMb = _memoryManager.GetCurrentUsageInMb();
                _logger.LogInformation($"Starting memory usage for site [{uri.AbsoluteUri}] is [{_crawlContext.MemoryUsageBeforeCrawlInMb}mb]");
            }

            _crawlContext.CrawlStartDate = DateTime.Now;
            var timer = Stopwatch.StartNew();

            if (_crawlContext.CrawlConfiguration.CrawlTimeoutSeconds > 0)
            {
                _timeoutTimer = new Timer(HandleCrawlTimeout, null, 0, _crawlContext.CrawlConfiguration.CrawlTimeoutSeconds * 1000);
            }

            try
            {
                var rootPage = new PageToCrawl(uri) { ParentUri = uri, IsInternal = true, IsRoot = true };
                if (ShouldSchedulePageLink(rootPage))
                    _scheduler.Add(rootPage);

                VerifyRequiredAvailableMemory();
                await CrawlSite();
            }
            catch (Exception e)
            {
                _crawlResult.ErrorException = e;
                _logger.LogCritical($"An error occurred while crawling site [{uri}]", e);
            }
            finally
            {
                _threadManager?.Dispose();
            }

            _timeoutTimer?.Dispose();

            timer.Stop();

            if (_memoryManager != null)
            {
                _crawlContext.MemoryUsageAfterCrawlInMb = _memoryManager.GetCurrentUsageInMb();
                _logger.LogInformation($"Ending memory usage for site [{uri.AbsoluteUri}] is [{_crawlContext.MemoryUsageAfterCrawlInMb}mb]");
            }

            _crawlResult.Elapsed = timer.Elapsed;
            _logger.LogInformation($"Crawl complete for site [{_crawlResult.RootUri.AbsoluteUri}]: Crawled [{_crawlResult.CrawlContext.CrawledCount}] pages in [{_crawlResult.Elapsed}]");

            return _crawlResult;
        }

        private CrawlConfiguration GetCrawlConfigurationFromConfigFile()
        {
            var builder = new ConfigurationBuilder();
            builder.AddJsonFile("appsettings.json");
            var cr = builder.Build();

            AbotConfigurationSectionHandler configFromFile = new AbotConfigurationSectionHandler(cr);

            //TODO check sections exist
            //if (configFromFile == null)
            //    throw new InvalidOperationException("abot config section was NOT found");

            _logger.LogDebug("abot config section was found");
            return configFromFile.Convert();
        }

        #region Synchronous Events

        /// <summary>
        /// Synchronous event that is fired before a page is crawled.
        /// </summary>
        public event EventHandler<PageCrawlStartingArgs> PageCrawlStarting;

        /// <summary>
        /// Synchronous event that is fired when an individual page has been crawled.
        /// </summary>
        public event EventHandler<PageCrawlCompletedArgs> PageCrawlCompleted;

        /// <summary>
        /// Synchronous event that is fired when the ICrawlDecisionMaker.ShouldCrawl impl returned false. This means the page or its links were not crawled.
        /// </summary>
        public event EventHandler<PageCrawlDisallowedArgs> PageCrawlDisallowed;

        /// <summary>
        /// Synchronous event that is fired when the ICrawlDecisionMaker.ShouldCrawlLinks impl returned false. This means the page's links were not crawled.
        /// </summary>
        public event EventHandler<PageLinksCrawlDisallowedArgs> PageLinksCrawlDisallowed;

        protected virtual void FirePageCrawlStartingEvent(PageToCrawl pageToCrawl)
        {
            try
            {
                var threadSafeEvent = PageCrawlStarting;
                if (threadSafeEvent != null)
                    threadSafeEvent(this, new PageCrawlStartingArgs(_crawlContext, pageToCrawl));
            }
            catch (Exception e)
            {
                _logger.LogCritical($"An unhandled exception was thrown by a subscriber of the PageCrawlStarting event for url:{pageToCrawl.Uri.AbsoluteUri}", e);
            }
        }

        protected virtual void FirePageCrawlCompletedEvent(CrawledPage crawledPage)
        {
            try
            {
                var threadSafeEvent = PageCrawlCompleted;
                if (threadSafeEvent != null)
                    threadSafeEvent(this, new PageCrawlCompletedArgs(_crawlContext, crawledPage));
            }
            catch (Exception e)
            {
                _logger.LogCritical($"An unhandled exception was thrown by a subscriber of the PageCrawlCompleted event for url:{crawledPage.Uri.AbsoluteUri}", e);
            }
        }

        protected virtual void FirePageCrawlDisallowedEvent(PageToCrawl pageToCrawl, string reason)
        {
            try
            {
                var threadSafeEvent = PageCrawlDisallowed;
                if (threadSafeEvent != null)
                    threadSafeEvent(this, new PageCrawlDisallowedArgs(_crawlContext, pageToCrawl, reason));
            }
            catch (Exception e)
            {
                _logger.LogCritical($"An unhandled exception was thrown by a subscriber of the PageCrawlDisallowed event for url:{pageToCrawl.Uri.AbsoluteUri}", e);
            }
        }

        protected virtual void FirePageLinksCrawlDisallowedEvent(CrawledPage crawledPage, string reason)
        {
            try
            {
                var threadSafeEvent = PageLinksCrawlDisallowed;
                if (threadSafeEvent != null)
                    threadSafeEvent(this, new PageLinksCrawlDisallowedArgs(_crawlContext, crawledPage, reason));
            }
            catch (Exception e)
            {
                _logger.LogCritical($"An unhandled exception was thrown by a subscriber of the PageLinksCrawlDisallowed event for url:{crawledPage.Uri.AbsoluteUri}", e);
            }
        }

#endregion

        #region Asynchronous Events

        /// <summary>
        /// Asynchronous event that is fired before a page is crawled.
        /// </summary>
        public event EventHandler<PageCrawlStartingArgs> PageCrawlStartingAsync;

        /// <summary>
        /// Asynchronous event that is fired when an individual page has been crawled.
        /// </summary>
        public event EventHandler<PageCrawlCompletedArgs> PageCrawlCompletedAsync;

        /// <summary>
        /// Asynchronous event that is fired when the ICrawlDecisionMaker.ShouldCrawl impl returned false. This means the page or its links were not crawled.
        /// </summary>
        public event EventHandler<PageCrawlDisallowedArgs> PageCrawlDisallowedAsync;

        /// <summary>
        /// Asynchronous event that is fired when the ICrawlDecisionMaker.ShouldCrawlLinks impl returned false. This means the page's links were not crawled.
        /// </summary>
        public event EventHandler<PageLinksCrawlDisallowedArgs> PageLinksCrawlDisallowedAsync;

        protected virtual void FirePageCrawlStartingEventAsync(PageToCrawl pageToCrawl)
        {
            var threadSafeEvent = PageCrawlStartingAsync;
            if (threadSafeEvent != null)
            {
                //Fire each subscribers delegate async
                foreach (EventHandler<PageCrawlStartingArgs> del in threadSafeEvent.GetInvocationList())
                {
                    del.BeginInvoke(this, new PageCrawlStartingArgs(_crawlContext, pageToCrawl), null, null);
                }
            }
        }

        protected virtual void FirePageCrawlCompletedEventAsync(CrawledPage crawledPage)
        {
            var threadSafeEvent = PageCrawlCompletedAsync;
            
            if (threadSafeEvent == null)
                return;

            if (_scheduler.Count == 0)
            {
                //Must be fired synchronously to avoid main thread exiting before completion of event handler for first or last page crawled
                try
                {
                    threadSafeEvent(this, new PageCrawlCompletedArgs(_crawlContext, crawledPage));
                }
                catch (Exception e)
                {
                    _logger.LogCritical($"An unhandled exception was thrown by a subscriber of the PageCrawlCompleted event for url:{crawledPage.Uri.AbsoluteUri}", e);
                }
            }
            else
            {
                //Fire each subscribers delegate async
                foreach (EventHandler<PageCrawlCompletedArgs> del in threadSafeEvent.GetInvocationList())
                {
                    del.BeginInvoke(this, new PageCrawlCompletedArgs(_crawlContext, crawledPage), null, null);
                }
            }
        }

        protected virtual void FirePageCrawlDisallowedEventAsync(PageToCrawl pageToCrawl, string reason)
        {
            var threadSafeEvent = PageCrawlDisallowedAsync;
            if (threadSafeEvent != null)
            {
                //Fire each subscribers delegate async
                foreach (EventHandler<PageCrawlDisallowedArgs> del in threadSafeEvent.GetInvocationList())
                {
                    del.BeginInvoke(this, new PageCrawlDisallowedArgs(_crawlContext, pageToCrawl, reason), null, null);
                }
            }
        }

        protected virtual void FirePageLinksCrawlDisallowedEventAsync(CrawledPage crawledPage, string reason)
        {
            var threadSafeEvent = PageLinksCrawlDisallowedAsync;
            if (threadSafeEvent != null)
            {
                //Fire each subscribers delegate async
                foreach (EventHandler<PageLinksCrawlDisallowedArgs> del in threadSafeEvent.GetInvocationList())
                {
                    del.BeginInvoke(this, new PageLinksCrawlDisallowedArgs(_crawlContext, crawledPage, reason), null, null);
                }
            }
        }

#endregion


        /// <summary>
        /// Synchronous method that registers a delegate to be called to determine whether a page should be crawled or not
        /// </summary>
        public void ShouldCrawlPage(Func<PageToCrawl, CrawlContext, CrawlDecision> decisionMaker)
        {
            _shouldCrawlPageDecisionMaker = decisionMaker;
        }

        /// <summary>
        /// Synchronous method that registers a delegate to be called to determine whether the page's content should be dowloaded
        /// </summary>
        /// <param name="shouldDownloadPageContent"></param>
        public void ShouldDownloadPageContent(Func<CrawledPage, CrawlContext, CrawlDecision> decisionMaker)
        {
            _shouldDownloadPageContentDecisionMaker = decisionMaker;
        }

        /// <summary>
        /// Synchronous method that registers a delegate to be called to determine whether a page's links should be crawled or not
        /// </summary>
        /// <param name="shouldCrawlPageLinksDelegate"></param>
        public void ShouldCrawlPageLinks(Func<CrawledPage, CrawlContext, CrawlDecision> decisionMaker)
        {
            _shouldCrawlPageLinksDecisionMaker = decisionMaker;
        }

        /// <summary>
        /// Synchronous method that registers a delegate to be called to determine whether a cerain link on a page should be scheduled to be crawled
        /// </summary>
        public void ShouldScheduleLink(Func<Uri, CrawledPage, CrawlContext, bool> decisionMaker)
        {
            _shouldScheduleLinkDecisionMaker = decisionMaker;
        }

        /// <summary>
        /// Synchronous method that registers a delegate to be called to determine whether a page should be recrawled or not
        /// </summary>
        public void ShouldRecrawlPage(Func<CrawledPage, CrawlContext, CrawlDecision> decisionMaker)
        {
            _shouldRecrawlPageDecisionMaker = decisionMaker;
        }

        /// <summary>
        /// Synchronous method that registers a delegate to be called to determine whether the 1st uri param is considered an internal uri to the second uri param
        /// </summary>
        /// <param name="decisionMaker delegate"></param>     
        public void IsInternalUri(Func<Uri, Uri, bool> decisionMaker)
        {
            _isInternalDecisionMaker = decisionMaker;
        }

        //private CrawlConfiguration GetCrawlConfigurationFromConfigFile()
        //{
        //    AbotConfigurationSectionHandler configFromFile = AbotConfigurationSectionHandler.LoadFromXml();

        //    if (configFromFile == null)
        //        throw new InvalidOperationException("abot config section was NOT found");

        //    _logger.LogDebug($"abot config section was found");
        //    return configFromFile.Convert();
        //}

        protected virtual async Task CrawlSite()
        {
            while (!_crawlComplete)
            {
                RunPreWorkChecks();

                if (_scheduler.Count > 0)
                {
                    _threadManager.DoWork(() => ProcessPageAsync(_scheduler.GetNext()));
                }
                else if (!_threadManager.HasRunningThreads())
                {
                    _crawlComplete = true;
                }
                else
                {
                    _logger.LogDebug($"Waiting for links to be scheduled...");
                    await Task.Delay(2500);
                }
            }
        }

        protected virtual void VerifyRequiredAvailableMemory()
        {
            if (_crawlContext.CrawlConfiguration.MinAvailableMemoryRequiredInMb < 1)
                return;

            if (!_memoryManager.IsSpaceAvailable(_crawlContext.CrawlConfiguration.MinAvailableMemoryRequiredInMb))
            {
#if NET46
                throw new InsufficientMemoryException($"Process does not have the configured [{_crawlContext.CrawlConfiguration.MinAvailableMemoryRequiredInMb}mb] of available memory to crawl site [{_crawlContext.RootUri}]. This is configurable through the minAvailableMemoryRequiredInMb in app.conf or CrawlConfiguration.MinAvailableMemoryRequiredInMb.");
#else
                throw new OutOfMemoryException($"Process does not have the configured [{_crawlContext.CrawlConfiguration.MinAvailableMemoryRequiredInMb}mb] of available memory to crawl site [{_crawlContext.RootUri}]. This is configurable through the minAvailableMemoryRequiredInMb in app.conf or CrawlConfiguration.MinAvailableMemoryRequiredInMb.");
#endif
            }
        }

        protected virtual void RunPreWorkChecks()
        {
            CheckMemoryUsage();
            CheckForCancellationRequest();
            CheckForHardStopRequest();
            CheckForStopRequest();
        }

        protected virtual void CheckMemoryUsage()
        {
            if (_memoryManager == null
                || _crawlContext.IsCrawlHardStopRequested
                || _crawlContext.CrawlConfiguration.MaxMemoryUsageInMb < 1)
                return;

            var currentMemoryUsage = _memoryManager.GetCurrentUsageInMb();
                _logger.LogDebug($"Current memory usage for site [{_crawlContext.RootUri}] is [{currentMemoryUsage}mb]");

            if (currentMemoryUsage > _crawlContext.CrawlConfiguration.MaxMemoryUsageInMb)
            {
                _memoryManager.Dispose();
                _memoryManager = null;

                string message = $"Process is using [{currentMemoryUsage}mb] of memory which is above the max configured of [{_crawlContext.CrawlConfiguration.MaxMemoryUsageInMb}mb] for site [{_crawlContext.RootUri}]. This is configurable through the maxMemoryUsageInMb in app.conf or CrawlConfiguration.MaxMemoryUsageInMb.";
#if NET46
                _crawlResult.ErrorException = new InsufficientMemoryException(message);
#else
                _crawlResult.ErrorException = new OutOfMemoryException(message);
#endif
                _logger.LogCritical("Memory exception", _crawlResult.ErrorException);
                _crawlContext.IsCrawlHardStopRequested = true;
            }
        }

        protected virtual void CheckForCancellationRequest()
        {
            if (_crawlContext.CancellationTokenSource.IsCancellationRequested)
            {
                if (!_crawlCancellationReported)
                {
                    string message = $"Crawl cancellation requested for site [{_crawlContext.RootUri}]!";
                    _logger.LogCritical(message);
                    _crawlResult.ErrorException = new OperationCanceledException(message, _crawlContext.CancellationTokenSource.Token);
                    _crawlContext.IsCrawlHardStopRequested = true;
                    _crawlCancellationReported = true;
                }
            }
        }

        protected virtual void CheckForHardStopRequest()
        {
            if (_crawlContext.IsCrawlHardStopRequested)
            {
                if (!_crawlStopReported)
                {
                    _logger.LogInformation($"Hard crawl stop requested for site [{_crawlContext.RootUri}]!");
                    _crawlStopReported = true;
                }

                _scheduler.Clear();
                _threadManager.AbortAll();
                _scheduler.Clear();//to be sure nothing was scheduled since first call to clear()

                //Set all events to null so no more events are fired
                PageCrawlStarting = null;
                PageCrawlCompleted = null;
                PageCrawlDisallowed = null;
                PageLinksCrawlDisallowed = null;
                PageCrawlStartingAsync = null;
                PageCrawlCompletedAsync = null;
                PageCrawlDisallowedAsync = null;
                PageLinksCrawlDisallowedAsync = null;
            }
        }

        protected virtual void CheckForStopRequest()
        {
            if (_crawlContext.IsCrawlStopRequested)
            {
                if (!_crawlStopReported)
                {
                    _logger.LogInformation($"Crawl stop requested for site [{_crawlContext.RootUri}]!");
                    _crawlStopReported = true;
                }
                _scheduler.Clear();
            }
        }

        protected virtual void HandleCrawlTimeout(object state)
        {
            var elapsedTimer = state as Timer;
            if (elapsedTimer != null)
                elapsedTimer.Dispose();

            _logger.LogInformation($"Crawl timeout of [{_crawlContext.CrawlConfiguration.CrawlTimeoutSeconds}] seconds has been reached for [{_crawlContext.RootUri}]");
            _crawlContext.IsCrawlHardStopRequested = true;
        }

        //protected virtual async Task ProcessPage(PageToCrawl pageToCrawl)
        protected virtual async Task ProcessPageAsync(PageToCrawl pageToCrawl)
        {
            try
            {
                if (pageToCrawl == null)
                    return;

                ThrowIfCancellationRequested();

                AddPageToContext(pageToCrawl);

                //CrawledPage crawledPage = await CrawlThePage(pageToCrawl);
                CrawledPage crawledPage = await CrawlThePageAsync(pageToCrawl);

                // Validate the root uri in case of a redirection.
                if (crawledPage.IsRoot)
                    ValidateRootUriForRedirection(crawledPage);

                if (IsRedirect(crawledPage) && !_crawlContext.CrawlConfiguration.IsHttpRequestAutoRedirectsEnabled)
                    ProcessRedirect(crawledPage);
                
                if (PageSizeIsAboveMax(crawledPage))
                    return;

                ThrowIfCancellationRequested();

                var shouldCrawlPageLinks = ShouldCrawlPageLinks(crawledPage);
                if (shouldCrawlPageLinks || _crawlContext.CrawlConfiguration.IsForcedLinkParsingEnabled)
                    ParsePageLinks(crawledPage);

                ThrowIfCancellationRequested();

                if (shouldCrawlPageLinks)
                    SchedulePageLinks(crawledPage);

                ThrowIfCancellationRequested();

                FirePageCrawlCompletedEventAsync(crawledPage);
                FirePageCrawlCompletedEvent(crawledPage);

                if (ShouldRecrawlPage(crawledPage))
                {
                    crawledPage.IsRetry = true;
                    _scheduler.Add(crawledPage);
                }   
            }
            catch (OperationCanceledException oce)
            {
                _logger.LogDebug($"Thread cancelled while crawling/processing page [{pageToCrawl.Uri}]");
                throw;
            }
            catch (Exception e)
            {
                _crawlResult.ErrorException = e;
                _logger.LogCritical("Error occurred during processing of page [{pageToCrawl.Uri}]", e);

                _crawlContext.IsCrawlHardStopRequested = true;
            }
        }

        protected virtual void ProcessRedirect(CrawledPage crawledPage)
        {
            if (crawledPage.RedirectPosition >= 20)
                _logger.LogWarning($"Page [{crawledPage.Uri}] is part of a chain of 20 or more consecutive redirects, redirects for this chain will now be aborted.");
                
            try
            {
                var uri = ExtractRedirectUri(crawledPage);

                var page = new PageToCrawl(uri);
                page.ParentUri = crawledPage.ParentUri;
                page.CrawlDepth = crawledPage.CrawlDepth;
                page.IsInternal = IsInternalUri(uri);
                page.IsRoot = false;
                page.RedirectedFrom = crawledPage;
                page.RedirectPosition = crawledPage.RedirectPosition + 1;

                crawledPage.RedirectedTo = page;
                _logger.LogDebug($"Page [{crawledPage.Uri}] is requesting that it be redirect to [{crawledPage.RedirectedTo.Uri}]");

                if (ShouldSchedulePageLink(page))
                {
                    _logger.LogInformation($"Page [{crawledPage.Uri}] will be redirect to [{crawledPage.RedirectedTo.Uri}]");
                    _scheduler.Add(page);
                }
            }
            catch {}
        }

        protected virtual bool IsInternalUri(Uri uri)
        {
            return  _isInternalDecisionMaker(uri, _crawlContext.RootUri) ||
                _isInternalDecisionMaker(uri, _crawlContext.OriginalRootUri);
        }

        protected virtual bool IsRedirect(CrawledPage crawledPage)
        {
            var isRedirect = false;
            if (crawledPage.HttpWebResponse != null) {
                isRedirect = (_crawlContext.CrawlConfiguration.IsHttpRequestAutoRedirectsEnabled &&
                    crawledPage.HttpWebResponse.ResponseUri != null &&
                    crawledPage.HttpWebResponse.ResponseUri.AbsoluteUri != crawledPage.Uri.AbsoluteUri) ||
                    (!_crawlContext.CrawlConfiguration.IsHttpRequestAutoRedirectsEnabled &&
                    (int) crawledPage.HttpWebResponse.StatusCode >= 300 &&
                    (int) crawledPage.HttpWebResponse.StatusCode <= 399);
            }
            return isRedirect;
        }

        protected virtual void ThrowIfCancellationRequested()
        {
            if (_crawlContext.CancellationTokenSource != null && _crawlContext.CancellationTokenSource.IsCancellationRequested)
                _crawlContext.CancellationTokenSource.Token.ThrowIfCancellationRequested();
        }

        protected virtual bool PageSizeIsAboveMax(CrawledPage crawledPage)
        {
            var isAboveMax = false;
            if (_crawlContext.CrawlConfiguration.MaxPageSizeInBytes > 0 &&
                crawledPage.Content.Bytes != null && 
                crawledPage.Content.Bytes.Length > _crawlContext.CrawlConfiguration.MaxPageSizeInBytes)
            {
                isAboveMax = true;
                _logger.LogInformation($"Page [{crawledPage.Uri}] has a page size of [{crawledPage.Content.Bytes.Length}] bytes which is above the [{_crawlContext.CrawlConfiguration.MaxPageSizeInBytes}] byte max, no further processing will occur for this page");
            }
            return isAboveMax;
        }

        protected virtual bool ShouldCrawlPageLinks(CrawledPage crawledPage)
        {
            var shouldCrawlPageLinksDecision = _crawlDecisionMaker.ShouldCrawlPageLinks(crawledPage, _crawlContext);
            if (shouldCrawlPageLinksDecision.Allow)
                shouldCrawlPageLinksDecision = (_shouldCrawlPageLinksDecisionMaker != null) ? _shouldCrawlPageLinksDecisionMaker.Invoke(crawledPage, _crawlContext) : new CrawlDecision { Allow = true };

            if (!shouldCrawlPageLinksDecision.Allow)
            {
                _logger.LogDebug($"Links on page [{crawledPage.Uri.AbsoluteUri}] not crawled, [{shouldCrawlPageLinksDecision.Reason}]");
                FirePageLinksCrawlDisallowedEventAsync(crawledPage, shouldCrawlPageLinksDecision.Reason);
                FirePageLinksCrawlDisallowedEvent(crawledPage, shouldCrawlPageLinksDecision.Reason);
            }

            SignalCrawlStopIfNeeded(shouldCrawlPageLinksDecision);
            return shouldCrawlPageLinksDecision.Allow;
        }

        protected virtual bool ShouldCrawlPage(PageToCrawl pageToCrawl)
        {
            if (_maxPagesToCrawlLimitReachedOrScheduled)
                return false;

            var shouldCrawlPageDecision = _crawlDecisionMaker.ShouldCrawlPage(pageToCrawl, _crawlContext);
            if (!shouldCrawlPageDecision.Allow &&
                shouldCrawlPageDecision.Reason.Contains("MaxPagesToCrawl limit of"))
            {
                _maxPagesToCrawlLimitReachedOrScheduled = true;
                _logger.LogInformation("MaxPagesToCrawlLimit has been reached or scheduled. No more pages will be scheduled.");
                return false;
            }

            if (shouldCrawlPageDecision.Allow)
                shouldCrawlPageDecision = (_shouldCrawlPageDecisionMaker != null) ? _shouldCrawlPageDecisionMaker.Invoke(pageToCrawl, _crawlContext) : new CrawlDecision { Allow = true };

            if (!shouldCrawlPageDecision.Allow)
            {
                _logger.LogDebug($"Page [{pageToCrawl.Uri.AbsoluteUri}] not crawled, [{shouldCrawlPageDecision.Reason}]");
                FirePageCrawlDisallowedEventAsync(pageToCrawl, shouldCrawlPageDecision.Reason);
                FirePageCrawlDisallowedEvent(pageToCrawl, shouldCrawlPageDecision.Reason);
            }

            SignalCrawlStopIfNeeded(shouldCrawlPageDecision);
            return shouldCrawlPageDecision.Allow;
        }

        protected virtual bool ShouldRecrawlPage(CrawledPage crawledPage)
        {
            //TODO No unit tests cover these lines
            var shouldRecrawlPageDecision = _crawlDecisionMaker.ShouldRecrawlPage(crawledPage, _crawlContext);
            if (shouldRecrawlPageDecision.Allow)
                shouldRecrawlPageDecision = (_shouldRecrawlPageDecisionMaker != null) ? _shouldRecrawlPageDecisionMaker.Invoke(crawledPage, _crawlContext) : new CrawlDecision { Allow = true };

            if (!shouldRecrawlPageDecision.Allow)
            {
                _logger.LogDebug($"Page [{crawledPage.Uri.AbsoluteUri}] not recrawled, [{shouldRecrawlPageDecision.Reason}]");
            }
            else
            {
                // Look for the Retry-After header in the response.
                crawledPage.RetryAfter = null;
                if (crawledPage.HttpWebResponse != null &&
                    crawledPage.HttpWebResponse.Headers != null)
                {
                    var value = crawledPage.HttpWebResponse.GetResponseHeader("Retry-After");
                    if (!String.IsNullOrEmpty(value))
                    {
                        // Try to convert to DateTime first, then in double.
                        DateTime date;
                        double seconds;
                        if (crawledPage.LastRequest.HasValue && DateTime.TryParse(value, out date))
                        {
                            crawledPage.RetryAfter = (date - crawledPage.LastRequest.Value).TotalSeconds;
                        } 
                        else if (double.TryParse(value, out seconds))
                        {
                            crawledPage.RetryAfter = seconds;
                        }
                    }
                }
            }

            SignalCrawlStopIfNeeded(shouldRecrawlPageDecision);
            return shouldRecrawlPageDecision.Allow;
        }

        //protected virtual async Task<CrawledPage> CrawlThePage(PageToCrawl pageToCrawl)
        protected virtual async Task<CrawledPage> CrawlThePageAsync(PageToCrawl pageToCrawl)
        {
            _logger.LogDebug($"About to crawl page [{pageToCrawl.Uri.AbsoluteUri}]");
            FirePageCrawlStartingEventAsync(pageToCrawl);
            FirePageCrawlStartingEvent(pageToCrawl);

            if (pageToCrawl.IsRetry){ WaitMinimumRetryDelay(pageToCrawl); }
            
            pageToCrawl.LastRequest = DateTime.Now;

            var crawledPage = await _pageRequester.MakeRequestAsync(pageToCrawl.Uri, ShouldDownloadPageContent);
            //CrawledPage crawledPage = await _pageRequester.MakeRequestAsync(pageToCrawl.Uri, ShouldDownloadPageContent);

            dynamic combinedPageBag = this.CombinePageBags(pageToCrawl.PageBag, crawledPage.PageBag);
            Mapper.Initialize(cfg =>
                {
                    cfg.CreateMap<PageToCrawl, CrawledPage>();
                });
            Mapper.Map(pageToCrawl, crawledPage);
            crawledPage.PageBag = combinedPageBag;

            if (crawledPage.HttpWebResponse == null)
                _logger.LogInformation($"Page crawl complete, Status:[NA] Url:[{crawledPage.Uri.AbsoluteUri}] Elapsed:[{crawledPage.Elapsed}] Parent:[{crawledPage.ParentUri}] Retry:[{crawledPage.RetryCount}]");
            else
                _logger.LogInformation($"Page crawl complete, Status:[{Convert.ToInt32(crawledPage.HttpWebResponse.StatusCode)}] Url:[{crawledPage.Uri.AbsoluteUri}] Elapsed:[{crawledPage.Elapsed}] Parent:[{crawledPage.ParentUri}] Retry:[{crawledPage.RetryCount}]");

            return crawledPage;
        }

        protected virtual dynamic CombinePageBags(dynamic pageToCrawlBag, dynamic crawledPageBag )
        {
            IDictionary<string, object> combinedBag = new ExpandoObject();
            var pageToCrawlBagDict = pageToCrawlBag as IDictionary<string, object>;
            var crawledPageBagDict = crawledPageBag as IDictionary<string, object>;
            
            foreach (var entry in pageToCrawlBagDict) combinedBag[entry.Key] = entry.Value;
            foreach (var entry in crawledPageBagDict) combinedBag[entry.Key] = entry.Value;

            return combinedBag;
        }

        protected virtual void AddPageToContext(PageToCrawl pageToCrawl)
        {
            if (pageToCrawl.IsRetry)
            {
                pageToCrawl.RetryCount++;
                return;
            }

            var domainCount = 0;
            Interlocked.Increment(ref _crawlContext.CrawledCount);
            _crawlContext.CrawlCountByDomain.AddOrUpdate(pageToCrawl.Uri.Authority, 1, (key, oldValue) => oldValue + 1);
        }

        protected virtual void ParsePageLinks(CrawledPage crawledPage)
        {
            crawledPage.ParsedLinks = _hyperLinkParser.GetLinks(crawledPage);
        }

        protected virtual void SchedulePageLinks(CrawledPage crawledPage)
        {
            var linksToCrawl = 0;
            foreach (var uri in crawledPage.ParsedLinks)
            {
                // First validate that the link was not already visited or added to the list of pages to visit, so we don't
                // make the same validation and fire the same events twice.
                if (!_scheduler.IsUriKnown(uri) &&
                    (_shouldScheduleLinkDecisionMaker == null || _shouldScheduleLinkDecisionMaker.Invoke(uri, crawledPage, _crawlContext)))
                {
                    try //Added due to a bug in the Uri class related to this (http://stackoverflow.com/questions/2814951/system-uriformatexception-invalid-uri-the-hostname-could-not-be-parsed)
                    {
                        var page = new PageToCrawl(uri);
                        page.ParentUri = crawledPage.Uri;
                        page.CrawlDepth = crawledPage.CrawlDepth + 1;
                        page.IsInternal = IsInternalUri(uri);
                        page.IsRoot = false;

                        if (ShouldSchedulePageLink(page))
                        {
                            _scheduler.Add(page);
                            linksToCrawl++;
                        }

                        if (!ShouldScheduleMorePageLink(linksToCrawl))
                        {
                            _logger.LogInformation($"MaxLinksPerPage has been reached. No more links will be scheduled for current page [{crawledPage.Uri}].");
                            break;
                        }
                    }
                    catch { }
                }

                // Add this link to the list of known Urls so validations are not duplicated in the future.
                _scheduler.AddKnownUri(uri);
            }
        }

        protected virtual bool ShouldSchedulePageLink(PageToCrawl page)
        {
            if ((page.IsInternal || _crawlContext.CrawlConfiguration.IsExternalPageCrawlingEnabled) && (ShouldCrawlPage(page)))
                return true;

            return false;   
        }

        protected virtual bool ShouldScheduleMorePageLink(int linksAdded)
        {
            return _crawlContext.CrawlConfiguration.MaxLinksPerPage == 0 || _crawlContext.CrawlConfiguration.MaxLinksPerPage > linksAdded;
        }

        protected virtual CrawlDecision ShouldDownloadPageContent(CrawledPage crawledPage)
        {
            var decision = _crawlDecisionMaker.ShouldDownloadPageContent(crawledPage, _crawlContext);
            if (decision.Allow)
                decision = (_shouldDownloadPageContentDecisionMaker != null) ? _shouldDownloadPageContentDecisionMaker.Invoke(crawledPage, _crawlContext) : new CrawlDecision { Allow = true };

            SignalCrawlStopIfNeeded(decision);
            return decision;
        }

        protected virtual void PrintConfigValues(CrawlConfiguration config)
        {
            _logger.LogInformation("Configuration Values:");

            var indentString = new string(' ', 2);
            
            string abotVersion = typeof(WebCrawler).GetTypeInfo().Assembly.GetName().Version.ToString();
            _logger.LogInformation($"{indentString}Abot Version: {abotVersion}");
            foreach (PropertyInfo property in config.GetType().GetProperties())
            {
                if (property.Name != "ConfigurationExtensions")
                    _logger.LogInformation($"{indentString}{property.Name}: {property.GetValue(config, null)}");
            }

            foreach (var key in config.ConfigurationExtensions.Keys)
            {
                _logger.LogInformation($"{indentString}{key}: {config.ConfigurationExtensions[key]}");
            }
        }

        protected virtual void SignalCrawlStopIfNeeded(CrawlDecision decision)
        {
            if (decision.ShouldHardStopCrawl)
            {
                _logger.LogInformation($"Decision marked crawl [Hard Stop] for site [{_crawlContext.RootUri}], [{decision.Reason}]");
                _crawlContext.IsCrawlHardStopRequested = decision.ShouldHardStopCrawl;
            }
            else if (decision.ShouldStopCrawl)
            {
                _logger.LogInformation($"Decision marked crawl [Stop] for site [{_crawlContext.RootUri}], [{decision.Reason}]");
                _crawlContext.IsCrawlStopRequested = decision.ShouldStopCrawl;
            }
        }

        protected virtual async Task WaitMinimumRetryDelay(PageToCrawl pageToCrawl)
        {
            //TODO No unit tests cover these lines
            if (pageToCrawl.LastRequest == null)
            {
                _logger.LogWarning($"pageToCrawl.LastRequest value is null for Url:{pageToCrawl.Uri.AbsoluteUri}. Cannot retry without this value.");
                return;
            }

            var milliSinceLastRequest = (DateTime.Now - pageToCrawl.LastRequest.Value).TotalMilliseconds;
            double milliToWait;
            if (pageToCrawl.RetryAfter.HasValue)
            {
                // Use the time to wait provided by the server instead of the config, if any.
                milliToWait = pageToCrawl.RetryAfter.Value*1000 - milliSinceLastRequest;
            }
            else
            {
                if (!(milliSinceLastRequest < _crawlContext.CrawlConfiguration.MinRetryDelayInMilliseconds)) return;
                milliToWait = _crawlContext.CrawlConfiguration.MinRetryDelayInMilliseconds - milliSinceLastRequest;
            }

            _logger.LogInformation($"Waiting [{milliToWait}] milliseconds before retrying Url:[{pageToCrawl.Uri.AbsoluteUri}] LastRequest:[{pageToCrawl.LastRequest}] SoonestNextRequest:[{pageToCrawl.LastRequest.Value.AddMilliseconds(_crawlContext.CrawlConfiguration.MinRetryDelayInMilliseconds)}]");

            //TODO Cannot use RateLimiter since it currently cannot handle dynamic sleep times so using Thread.Sleep in the meantime
            if (milliToWait > 0)
                await Task.Delay(TimeSpan.FromSeconds(30));
        }

        /// <summary>
        /// Validate that the Root page was not redirected. If the root page is redirected, we assume that the root uri
        /// should be changed to the uri where it was redirected.
        /// </summary>
        protected virtual void ValidateRootUriForRedirection(CrawledPage crawledRootPage)
        {
            if (!crawledRootPage.IsRoot) {
                throw new ArgumentException("The crawled page must be the root page to be validated for redirection.");
            }

            if (IsRedirect(crawledRootPage)) {
                _crawlContext.RootUri = ExtractRedirectUri(crawledRootPage);
                _logger.LogInformation($"The root URI [{_crawlContext.OriginalRootUri}] was redirected to [{_crawlContext.RootUri}]. [{_crawlContext.RootUri}] is the new root.");
            }
        }

        /// <summary>
        /// Retrieve the URI where the specified crawled page was redirected.
        /// </summary>
        /// <remarks>
        /// If HTTP auto redirections is disabled, this value is stored in the 'Location' header of the response.
        /// If auto redirections is enabled, this value is stored in the response's ResponseUri property.
        /// </remarks>
        protected virtual Uri ExtractRedirectUri(CrawledPage crawledPage)
        {
            Uri locationUri;
            if (_crawlContext.CrawlConfiguration.IsHttpRequestAutoRedirectsEnabled) {
                // For auto redirects, look for the response uri.
                locationUri = crawledPage.HttpWebResponse.ResponseUri;
            } else {
                // For manual redirects, we need to look for the location header.
                var location = crawledPage.HttpWebResponse.GetResponseHeader("Location");
                
                // Check if the location is absolute. If not, create an absolute uri.
                if (!Uri.TryCreate(location, UriKind.Absolute, out locationUri))
                {
                    var baseUri = new Uri(crawledPage.Uri.GetComponents((UriComponents.Scheme | UriComponents.UserInfo | UriComponents.Host | UriComponents.Port), UriFormat.UriEscaped));
                    locationUri = new Uri(baseUri, location);
                }
            }
            return locationUri;
        }

        public virtual void Dispose()
        {
            if (_threadManager != null)
            {
                _threadManager.Dispose();
            }
            if (_scheduler != null)
            {
                _scheduler.Dispose();
            }
            if (_pageRequester != null)
            {
                _pageRequester.Dispose();
            }
            if (_memoryManager != null)
            {
                _memoryManager.Dispose();
            }
        }
    }
}