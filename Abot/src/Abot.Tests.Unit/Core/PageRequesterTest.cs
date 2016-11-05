using System;
using Abot.Core;
using Abot.Poco;
using NUnit.Framework;
using System.Threading.Tasks;
using Abot.Tests.Unit.Helpers;

namespace Abot.Tests.Unit.Core
{
    [TestFixture]
    public class PageRequesterTest
    {
        PageRequester _unitUnderTest;
        UnitTestConfig unitTestConfig = new UnitTestConfig();
        Uri _validUri { get { return new Uri(unitTestConfig.SiteSimulatorBaseAddress); } }
        Uri _403ErrorUri { get { return new Uri(string.Concat(unitTestConfig.SiteSimulatorBaseAddress, "HttpResponse/Status403")); } }
        Uri _404ErrorUri { get { return new Uri(string.Concat(unitTestConfig.SiteSimulatorBaseAddress, "HttpResponse/Status404")); } }
        Uri _500ErrorUri { get { return new Uri(string.Concat(unitTestConfig.SiteSimulatorBaseAddress, "HttpResponse/Status500")); } }
        Uri _503ErrorUri { get { return new Uri(string.Concat(unitTestConfig.SiteSimulatorBaseAddress, "HttpResponse/Status503")); } }
        Uri _301To200Uri { get { return new Uri(string.Concat(unitTestConfig.SiteSimulatorBaseAddress, "HttpResponse/Redirect/?redirectHttpStatus=301&destinationHttpStatus=200")); } }
        Uri _301To404Uri { get { return new Uri(string.Concat(unitTestConfig.SiteSimulatorBaseAddress, "HttpResponse/Redirect/?redirectHttpStatus=301&destinationHttpStatus=404")); } }
        Uri _502ErrorUri { get { return new Uri("http://www.lakkjfkasdfjhqlkfj.com"); } }//non resolvable

        CrawlConfiguration _crawlConfig = new CrawlConfiguration { UserAgentString = "someuseragentstringhere" };

        [SetUp]
        public void SetUp()
        {
            _unitUnderTest = new PageRequester(_crawlConfig);
        }

        [Test]
        public void Constructor_NullUserAgent()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                new PageRequester(null);
            });
        }

        [Test]
        public async Task MakeRequest_200_ReturnsValidResponse()
        {
            CrawledPage result = await _unitUnderTest.MakeRequestAsync(_validUri);

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.HttpRequestMessage);
            Assert.IsNotNull(result.HttpWebResponse);
            Assert.IsNull(result.HttpRequestException);
            Assert.IsFalse(string.IsNullOrWhiteSpace(result.Content.Text));
            Assert.IsNotNull(result.HtmlDocument);
            Assert.AreEqual(200, (int)result.HttpWebResponse.StatusCode);
            Assert.IsTrue(result.Content.Bytes.Length > 900 && result.Content.Bytes.Length < 1400);

            DateTime fiveSecsAgo = DateTime.Now.AddSeconds(-5);
            Assert.IsTrue(fiveSecsAgo < result.RequestStarted);
            Assert.IsTrue(fiveSecsAgo < result.RequestCompleted);
            Assert.IsNotNull(result.DownloadContentStarted);
            Assert.IsNotNull(result.DownloadContentCompleted);
            Assert.IsTrue(fiveSecsAgo < result.DownloadContentStarted);
            Assert.IsTrue(fiveSecsAgo < result.DownloadContentCompleted); 
        }

        [Test]
        public async Task MakeRequest_403_ReturnsValidResponse()
        {
            CrawledPage result = await _unitUnderTest.MakeRequestAsync(_403ErrorUri);

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.HttpWebResponse);
            Assert.AreEqual(403, (int)result.HttpWebResponse.StatusCode);
        }

        [Test]
        public async Task MakeRequest_404_ReturnsValidResponse()
        {
            CrawledPage result = await _unitUnderTest.MakeRequestAsync(_404ErrorUri);

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.HttpRequestMessage);
            Assert.IsNotNull(result.HttpWebResponse);
            Assert.IsNotNull(result.HttpRequestException);
            Assert.IsTrue(string.IsNullOrWhiteSpace(result.Content.Text));
            Assert.IsNotNull(result.HtmlDocument);
            Assert.IsNotNull(result.AngleSharpHtmlDocument);
            Assert.AreEqual(404, (int)result.HttpWebResponse.StatusCode);
            Assert.IsTrue(result.Content.Bytes.Length == 0);
        }

        [Test]
        public async Task MakeRequest_500_ReturnsValidResponse()
        {
            CrawledPage result = await _unitUnderTest.MakeRequestAsync(_500ErrorUri);

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.HttpRequestMessage);
            Assert.IsNotNull(result.HttpWebResponse);
            Assert.IsNotNull(result.HttpRequestException);
            Assert.IsTrue(string.IsNullOrWhiteSpace(result.Content.Text));
            Assert.IsNotNull(result.HtmlDocument);
            Assert.AreEqual(500, (int)result.HttpWebResponse.StatusCode);
            Assert.IsTrue(result.Content.Bytes.Length == 0);
        }

        [Test]
        public async Task MakeRequest_503_ReturnsValidResponse()
        {
            CrawledPage result = await _unitUnderTest.MakeRequestAsync(_503ErrorUri);

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.HttpRequestMessage);
            Assert.IsNotNull(result.HttpWebResponse);
            Assert.IsNotNull(result.HttpRequestException);
            Assert.IsTrue(string.IsNullOrWhiteSpace(result.Content.Text));
            Assert.IsNotNull(result.HtmlDocument);
            Assert.IsNotNull(result.AngleSharpHtmlDocument);
            Assert.AreEqual(503, (int)result.HttpWebResponse.StatusCode);
            Assert.IsTrue(result.Content.Bytes.Length == 0);

        }

        [Test, Ignore("Cox intercepts 502 status and returns 200")]
        public async Task MakeHttpWebHeadRequest_NonResolvable_ReturnsNullResponse()
        {
            CrawledPage result = await _unitUnderTest.MakeRequestAsync(_502ErrorUri);

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.HttpRequestMessage);
            //Assert.IsNull(result.HttpWebResponse);
            //Assert.IsTrue(string.IsNullOrWhiteSpace(result.Content.Text));
            Assert.IsTrue(result.HttpRequestException.Message.StartsWith("The remote name could not be resolved: ") || result.HttpRequestException.Message.StartsWith("The remote server returned an error: (502) Bad Gateway."));
        }

        [Test]
        public async Task MakeRequest_AutoRedirect_301To200_ReturnsValidResponse()
        {
            CrawledPage result = await _unitUnderTest.MakeRequestAsync(_301To200Uri);

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.HttpRequestMessage);
            Assert.IsNotNull(result.HttpWebResponse);
            Assert.IsNull(result.HttpRequestException);
            Assert.IsFalse(string.IsNullOrWhiteSpace(result.Content.Text));
            Assert.IsNotNull(result.HtmlDocument);
            Assert.IsNotNull(result.AngleSharpHtmlDocument);
            Assert.AreEqual(200, (int)result.HttpWebResponse.StatusCode);
            Assert.IsTrue(result.Content.Bytes.Length > 0);

            DateTime fiveSecsAgo = DateTime.Now.AddSeconds(-5);
            Assert.IsTrue(fiveSecsAgo < result.RequestStarted);
            Assert.IsTrue(fiveSecsAgo < result.RequestCompleted);
            Assert.IsNotNull(result.DownloadContentStarted);
            Assert.IsNotNull(result.DownloadContentCompleted);
            Assert.IsTrue(fiveSecsAgo < result.DownloadContentStarted);
            Assert.IsTrue(fiveSecsAgo < result.DownloadContentCompleted); 
        }

        [Test]
        public async Task MakeRequest_AutoRedirect_301To404_ReturnsValidResponse()
        {
            CrawledPage result = await _unitUnderTest.MakeRequestAsync(_301To404Uri);

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.HttpRequestMessage);
            Assert.IsNotNull(result.HttpWebResponse);
            Assert.IsNotNull(result.HttpRequestException);
            Assert.IsTrue(string.IsNullOrWhiteSpace(result.Content.Text));
            Assert.IsNotNull(result.HtmlDocument);
            Assert.IsNotNull(result.AngleSharpHtmlDocument);
            Assert.AreEqual(404, (int)result.HttpWebResponse.StatusCode);
            Assert.IsTrue(result.Content.Bytes.Length == 0);
        }

        [Test]
        public void MakeRequest_NullUri()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                _unitUnderTest.MakeRequestAsync(null).GetAwaiter().GetResult();
            });
        }

        [Test]
        public async Task MakeRequest_CrawlDecisionReturnsFalse_CrawlsPageButDoesNotDowloadContent()
        {
            CrawledPage result = await _unitUnderTest.MakeRequestAsync(_validUri, (x) => new CrawlDecision { Allow = false });

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.HttpRequestMessage);
            Assert.IsNotNull(result.HttpWebResponse);
            Assert.IsNull(result.HttpRequestException);
            Assert.AreEqual("", result.Content.Text);
            Assert.IsNotNull(result.HtmlDocument);
            Assert.IsNotNull(result.AngleSharpHtmlDocument);
            Assert.AreEqual(200, (int)result.HttpWebResponse.StatusCode);
            Assert.IsNull(result.Content.Bytes);

            DateTime fiveSecsAgo = DateTime.Now.AddSeconds(-5);
            Assert.IsTrue(fiveSecsAgo < result.RequestStarted);
            Assert.IsTrue(fiveSecsAgo < result.RequestCompleted);
            Assert.IsNull(result.DownloadContentStarted);
            Assert.IsNull(result.DownloadContentCompleted);
        }
    }
}
