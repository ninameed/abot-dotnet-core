using Abot.Core;
using Abot.Crawler;
using Abot.Poco;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Abot.Tests.Integration
{
    [TestFixture]
    public class CrawlSiteSimulator : CrawlTestBase
    {
        public CrawlSiteSimulator()
            :base(new Uri("http://localhost:1111/"), 25)
        {
            
        }

        [Test]
        public async Task Crawl_VerifyCrawlResultIsAsExpected()
        {
            await new PageRequester(new CrawlConfiguration{ UserAgentString = "aaa" }).MakeRequestAsync(new Uri("http://localhost:1111/PageGenerator/ClearCounters"));
            await base.CrawlAndAssertAsync(new PoliteWebCrawler());
        }

        [Test]
        public async Task Crawl_MaxPagesTo5_OnlyCrawls5Pages()
        {
            await new PageRequester(new CrawlConfiguration{ UserAgentString = "aaa" }).MakeRequestAsync(new Uri("http://localhost:1111/PageGenerator/ClearCounters"));
            
            CrawlConfiguration configuration = new CrawlConfiguration();
            configuration.MaxPagesToCrawl = 5;

            int pagesCrawledCount = 0;

            PoliteWebCrawler crawler = new PoliteWebCrawler(configuration, null, null, null, null, null, null, null, null);
            crawler.PageCrawlCompleted += (a, b) =>
            {
                pagesCrawledCount++;
            };

            await crawler.CrawlAsync(new Uri("http://localhost:1111/"));

            Assert.AreEqual(5, pagesCrawledCount);
        }

        [Test]
        public async Task Crawl_MaxPagesTo25_OnlyCrawls25Pages()
        {
            await new PageRequester(new CrawlConfiguration { UserAgentString = "aaa" }).MakeRequestAsync(new Uri("http://localhost:1111/PageGenerator/ClearCounters"));

            CrawlConfiguration configuration = new CrawlConfiguration();
            configuration.MaxPagesToCrawl = 25;
            configuration.IsExternalPageCrawlingEnabled = true;
            configuration.IsExternalPageLinksCrawlingEnabled = true;

            int pagesCrawledCount = 0;

            PoliteWebCrawler crawler = new PoliteWebCrawler(configuration, null, null, null, null, null, null, null, null);
            crawler.PageCrawlCompleted += (a, b) =>
            {
                pagesCrawledCount++;
            };

            var res = await crawler.CrawlAsync(new Uri("http://localhost:1111/"));
            
            Assert.AreEqual(25, pagesCrawledCount);
        }

        [Test]
        public async Task Crawl_MaxPagesTo5_WithCrawlDelay_OnlyCrawls5Pages()
        {
            await new PageRequester(new CrawlConfiguration{ UserAgentString = "aaa" }).MakeRequestAsync(new Uri("http://localhost:1111/PageGenerator/ClearCounters"));

            CrawlConfiguration configuration = new CrawlConfiguration();
            configuration.MinCrawlDelayPerDomainMilliSeconds = 1000; //adding delay since it increases the chance of issues with abot crawling more than MaxPagesToCrawl.
            configuration.MaxPagesToCrawl = 5;

            int pagesCrawledCount = 0;

            PoliteWebCrawler crawler = new PoliteWebCrawler(configuration, null, null, null, null, null, null, null, null);
            crawler.PageCrawlCompleted += (a, b) => pagesCrawledCount++;

            await crawler.CrawlAsync(new Uri("http://localhost:1111/"));

            Assert.AreEqual(5, pagesCrawledCount);
        }

        [Test]
        public async Task Crawl_CrawlTimeoutIs1Sec_TimesOut()
        {
            CrawlConfiguration configuration = new CrawlConfiguration();
            configuration.CrawlTimeoutSeconds = 2;

            int pagesCrawledCount = 0;

            PoliteWebCrawler crawler = new PoliteWebCrawler(configuration, null, null, null, null, null, null, null, null);
            crawler.PageCrawlCompleted += (a, b) => pagesCrawledCount++;

            CrawlResult result = await crawler.CrawlAsync(new Uri("http://localhost:1111/"));

            Assert.IsFalse(result.ErrorOccurred);
            Assert.IsTrue(result.Elapsed.TotalSeconds < 8, "Took more than 8 seconds");
            Assert.IsTrue(pagesCrawledCount < 2, "Crawled more than 2 pages");
        }

        [Test]
        public async Task Crawl_CancellationTokenCancelled_StopsCrawl()
        {
            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
            System.Timers.Timer timer = new System.Timers.Timer(800);
            timer.Elapsed += (o, e) =>
            {
                cancellationTokenSource.Cancel();
                timer.Stop();
                timer.Dispose();
            };
            timer.Start();

            PoliteWebCrawler crawler = new PoliteWebCrawler();
            CrawlResult result = await crawler.CrawlAsync(new Uri("http://localhost:1111/"), cancellationTokenSource);

            Assert.IsTrue(result.ErrorOccurred);
            Assert.IsTrue(result.ErrorException is OperationCanceledException);
        }

        [Test]
        public async Task Crawl_IsRateLimited()
        {
            await new PageRequester(new CrawlConfiguration { UserAgentString = "aaa" }).MakeRequestAsync(new Uri("http://localhost:1111/PageGenerator/ClearCounters"));

            CrawlConfiguration configuration = new CrawlConfiguration();
            configuration.MaxPagesToCrawl = 3;
            configuration.MinCrawlDelayPerDomainMilliSeconds = 1000; // 1 second * 2 pages = 2 (or more) seconds
            
            int pagesCrawledCount = 0;

            var crawler = new PoliteWebCrawler(configuration);
            crawler.PageCrawlCompleted += (a, b) => pagesCrawledCount++;

            var uriToCrawl = new Uri("http://localhost:1111/");
            var start = DateTime.Now;
            await crawler.CrawlAsync(uriToCrawl);
            var elapsed = DateTime.Now - start;

            Assert.GreaterOrEqual(elapsed.TotalMilliseconds, 2000);
            Assert.AreEqual(3, pagesCrawledCount);
        }

        [Test]
        public async Task Crawl_RetryEnabled_VerifyCrawlResultIsAsExpected()
        {
            await new PageRequester(new CrawlConfiguration { UserAgentString = "aaa" }).MakeRequestAsync(new Uri("http://localhost:1111/PageGenerator/ClearCounters"));

            var builder = new ConfigurationBuilder();
            builder.AddJsonFile("appsettings.json");
            var cr = builder.Build();

            CrawlConfiguration configuration = new AbotConfigurationSectionHandler(cr).Convert();

            configuration.MaxRetryCount = 3;
            configuration.MinRetryDelayInMilliseconds = 2000;

            await base.CrawlAndAssertAsync(new PoliteWebCrawler(configuration));
        }

        protected override List<PageResult> GetExpectedCrawlResult()
        {
            List<PageResult> expectedCrawlResult = new List<PageResult>
            {
                new PageResult { Url = "http://localhost:1111/", HttpStatusCode = 200},
                new PageResult { Url = "http://localhost:1111/PageGenerator/Generate?Status200Count=5&Status403Count=1&Status404Count=2&Status500Count=3&Status503Count=4&Page=1", HttpStatusCode = 200},
                new PageResult { Url = "http://localhost:1111/PageGenerator/Generate?Status200Count=5&Status403Count=1&Status404Count=2&Status500Count=3&Status503Count=4&Page=3", HttpStatusCode = 200},
                new PageResult { Url = "http://localhost:1111/PageGenerator/Generate?Status200Count=5&Status403Count=1&Status404Count=2&Status500Count=3&Status503Count=4&Page=2", HttpStatusCode = 200},
                new PageResult { Url = "http://localhost:1111/PageGenerator/Generate?Status200Count=5&Status403Count=1&Status404Count=2&Status500Count=3&Status503Count=4&Page=4", HttpStatusCode = 200},
                new PageResult { Url = "http://localhost:1111/HttpResponse/Status200?page=1", HttpStatusCode = 200},
                new PageResult { Url = "http://localhost:1111/HttpResponse/Status200?page=3", HttpStatusCode = 200},
                new PageResult { Url = "http://localhost:1111/HttpResponse/Status200?page=2", HttpStatusCode = 200},
                new PageResult { Url = "http://localhost:1111/HttpResponse/Status200?page=4", HttpStatusCode = 200},
                new PageResult { Url = "http://localhost:1111/HttpResponse/Status200?page=5", HttpStatusCode = 200},
                new PageResult { Url = "http://localhost:1111/HttpResponse/Status403?page=1", HttpStatusCode = 403},
                new PageResult { Url = "http://localhost:1111/HttpResponse/Status404?page=2", HttpStatusCode = 404},
                new PageResult { Url = "http://localhost:1111/HttpResponse/Status404?page=1", HttpStatusCode = 404},
                new PageResult { Url = "http://localhost:1111/HttpResponse/Status500?page=1", HttpStatusCode = 500},
                new PageResult { Url = "http://localhost:1111/HttpResponse/Status500?page=3", HttpStatusCode = 500},
                new PageResult { Url = "http://localhost:1111/HttpResponse/Status500?page=2", HttpStatusCode = 500},
                new PageResult { Url = "http://localhost:1111/HttpResponse/Status503?page=2", HttpStatusCode = 503},
                new PageResult { Url = "http://localhost:1111/HttpResponse/Status503?page=1", HttpStatusCode = 503},
                new PageResult { Url = "http://localhost:1111/HttpResponse/Status200?page=12", HttpStatusCode = 200},
                new PageResult { Url = "http://localhost:1111/HttpResponse/Status200?page=11", HttpStatusCode = 200},
                new PageResult { Url = "http://localhost:1111/HttpResponse/Status503?page=3", HttpStatusCode = 503},
                new PageResult { Url = "http://localhost:1111/HttpResponse/Status503?page=4", HttpStatusCode = 503},
                new PageResult { Url = "http://localhost:1111/HttpResponse/Status200?page=13", HttpStatusCode = 200},
                new PageResult { Url = "http://localhost:1111/HttpResponse/Status200?page=14", HttpStatusCode = 200},
                new PageResult { Url = "http://localhost:1111/HttpResponse/Status200?page=15", HttpStatusCode = 200},
                new PageResult { Url = "http://localhost:1111/HttpResponse/Status403?page=3", HttpStatusCode = 403},
                new PageResult { Url = "http://localhost:1111/HttpResponse/Status404?page=5", HttpStatusCode = 404},
                new PageResult { Url = "http://localhost:1111/HttpResponse/Status404?page=6", HttpStatusCode = 404},
                new PageResult { Url = "http://localhost:1111/HttpResponse/Status500?page=7", HttpStatusCode = 500},
                new PageResult { Url = "http://localhost:1111/HttpResponse/Status500?page=8", HttpStatusCode = 500},
                new PageResult { Url = "http://localhost:1111/HttpResponse/Status200?page=6", HttpStatusCode = 200},
                new PageResult { Url = "http://localhost:1111/HttpResponse/Status503?page=9", HttpStatusCode = 503},
                new PageResult { Url = "http://localhost:1111/HttpResponse/Status500?page=9", HttpStatusCode = 500},
                new PageResult { Url = "http://localhost:1111/HttpResponse/Status200?page=7", HttpStatusCode = 200},
                new PageResult { Url = "http://localhost:1111/HttpResponse/Status503?page=12", HttpStatusCode = 503},
                new PageResult { Url = "http://localhost:1111/HttpResponse/Status503?page=10", HttpStatusCode = 503},
                new PageResult { Url = "http://localhost:1111/HttpResponse/Status503?page=11", HttpStatusCode = 503},
                new PageResult { Url = "http://localhost:1111/HttpResponse/Status200?page=8", HttpStatusCode = 200},
                new PageResult { Url = "http://localhost:1111/HttpResponse/Status200?page=9", HttpStatusCode = 200},
                new PageResult { Url = "http://localhost:1111/HttpResponse/Status200?page=10", HttpStatusCode = 200},
                new PageResult { Url = "http://localhost:1111/HttpResponse/Status403?page=2", HttpStatusCode = 403},
                new PageResult { Url = "http://localhost:1111/HttpResponse/Status404?page=3", HttpStatusCode = 404},
                new PageResult { Url = "http://localhost:1111/HttpResponse/Status404?page=4", HttpStatusCode = 404},
                new PageResult { Url = "http://localhost:1111/HttpResponse/Status500?page=4", HttpStatusCode = 500},
                new PageResult { Url = "http://localhost:1111/HttpResponse/Status500?page=5", HttpStatusCode = 500},
                new PageResult { Url = "http://localhost:1111/HttpResponse/Status200?page=16", HttpStatusCode = 200},
                new PageResult { Url = "http://localhost:1111/HttpResponse/Status500?page=6", HttpStatusCode = 500},
                new PageResult { Url = "http://localhost:1111/HttpResponse/Status503?page=6", HttpStatusCode = 503},
                new PageResult { Url = "http://localhost:1111/HttpResponse/Status503?page=5", HttpStatusCode = 503},
                new PageResult { Url = "http://localhost:1111/HttpResponse/Status200?page=17", HttpStatusCode = 200},
                new PageResult { Url = "http://localhost:1111/HttpResponse/Status200?page=18", HttpStatusCode = 200},
                new PageResult { Url = "http://localhost:1111/HttpResponse/Status200?page=19", HttpStatusCode = 200},
                new PageResult { Url = "http://yahoo.com/", HttpStatusCode = 200},
                new PageResult { Url = "http://localhost:1111/HttpResponse/Status503?page=8", HttpStatusCode = 503},
                new PageResult { Url = "http://localhost:1111/HttpResponse/Status200?page=20", HttpStatusCode = 200},
                new PageResult { Url = "http://localhost:1111/HttpResponse/Status503?page=7", HttpStatusCode = 503},
                new PageResult { Url = "http://localhost:1111/HttpResponse/Status403?page=4", HttpStatusCode = 403},
                new PageResult { Url = "http://localhost:1111/HttpResponse/Status404?page=7", HttpStatusCode = 404},
                new PageResult { Url = "http://localhost:1111/HttpResponse/Status500?page=11", HttpStatusCode = 500},
                new PageResult { Url = "http://localhost:1111/HttpResponse/Status500?page=10", HttpStatusCode = 500},
                new PageResult { Url = "http://localhost:1111/HttpResponse/Status404?page=8", HttpStatusCode = 404},
                new PageResult { Url = "http://localhost:1111/HttpResponse/Status503?page=13", HttpStatusCode = 503},
                new PageResult { Url = "http://localhost:1111/HttpResponse/Status500?page=12", HttpStatusCode = 500},
                new PageResult { Url = "http://localhost:1111/HttpResponse/Status503?page=14", HttpStatusCode = 503},
                new PageResult { Url = "http://localhost:1111/HttpResponse/Status503?page=15", HttpStatusCode = 503},
                new PageResult { Url = "http://localhost:1111/HttpResponse/Status503?page=16", HttpStatusCode = 503},
                new PageResult { Url = "http://zoogle2323.com/", HttpStatusCode = 200}
            };

            return expectedCrawlResult;
        }
    }
}
