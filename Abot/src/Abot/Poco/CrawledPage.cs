using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using AngleSharp.Dom.Html;
using AngleSharp.Parser.Html;
using Microsoft.Extensions.Logging;

namespace Abot.Poco
{
    public class CrawledPage : PageToCrawl
    {
        //https://msdn.microsoft.com/en-us/magazine/mt694089.aspx
        static ILogger _logger { get; } = new LoggerFactory().CreateLogger<CrawledPage>();

        HtmlParser _angleSharpHtmlParser;

        readonly Lazy<HtmlDocument> _htmlDocument;
        readonly Lazy<IHtmlDocument> _angleSharpHtmlDocument;

        public CrawledPage(Uri uri)
            : base(uri)
        {
            _htmlDocument = new Lazy<HtmlDocument>(InitializeHtmlAgilityPackDocument);
            _angleSharpHtmlDocument = new Lazy<IHtmlDocument>(InitializeAngleSharpHtmlParser);

            Content = new PageContent();
        }

        /// <summary>
        /// Lazy loaded Html Agility Pack (http://htmlagilitypack.codeplex.com/) document that can be used to retrieve/modify html elements on the crawled page.
        /// </summary>
        public HtmlDocument HtmlDocument => _htmlDocument.Value;

        /// <summary>
        /// Lazy loaded AngleSharp IHtmlDocument (https://github.com/AngleSharp/AngleSharp) that can be used to retrieve/modify html elements on the crawled page.
        /// </summary>
        public IHtmlDocument AngleSharpHtmlDocument => _angleSharpHtmlDocument.Value;

        /// <summary>
        /// Web request sent to the server
        /// </summary>
        public HttpWebRequest HttpWebRequest { get; set; }
        public HttpRequestMessage HttpRequestMessage { get; set; }

        /// <summary>
        /// Web response from the server. NOTE: The Close() method has been called before setting this property.
        /// </summary>
        public HttpWebResponseWrapper HttpWebResponse { get; set; }
        public HttpResponseMessage HttpResponseMessage { get; set; }

        /// <summary>
        /// The web exception that occurred during the crawl
        /// </summary>
        public WebException WebException { get; set; }

        public override string ToString()
        {
            if(HttpWebResponse == null)
                return Uri.AbsoluteUri;
            else
                return $"{Uri.AbsoluteUri}[{(int)HttpWebResponse.StatusCode}]";
        }

        /// <summary>
        /// Links parsed from page. This value is set by the WebCrawler.SchedulePageLinks() method only If the "ShouldCrawlPageLinks" rules return true or if the IsForcedLinkParsingEnabled config value is set to true.
        /// </summary>
        public IEnumerable<Uri> ParsedLinks { get; set; }

        /// <summary>
        /// The content of page request
        /// </summary>
        public PageContent Content { get; set; }

        /// <summary>
        /// A datetime of when the http request started
        /// </summary>
        public DateTime RequestStarted { get; set; }

        /// <summary>
        /// A datetime of when the http request completed
        /// </summary>
        public DateTime RequestCompleted { get; set; }

        /// <summary>
        /// A datetime of when the page content download started, this may be null if downloading the content was disallowed by the CrawlDecisionMaker or the inline delegate ShouldDownloadPageContent
        /// </summary>
        public DateTime? DownloadContentStarted { get; set; }

        /// <summary>
        /// A datetime of when the page content download completed, this may be null if downloading the content was disallowed by the CrawlDecisionMaker or the inline delegate ShouldDownloadPageContent
        /// </summary>
        public DateTime? DownloadContentCompleted { get; set; }

        /// <summary>
        /// The page that this pagee was redirected to
        /// </summary>
        public PageToCrawl RedirectedTo { get; set; }

        /// <summary>
        /// Time it took from RequestStarted to RequestCompleted in milliseconds
        /// </summary>
        public double Elapsed => (RequestCompleted - RequestStarted).TotalMilliseconds;

        private HtmlDocument InitializeHtmlAgilityPackDocument()
        {
            var hapDoc = new HtmlDocument();
            //TODO How do we handle the following line
            //hapDoc.OptionMaxNestedChildNodes = 5000;//did not make this an externally configurable property since it is really an internal issue to hap
            try
            {
                hapDoc.LoadHtml(Content.Text);
            }
            catch (Exception e)
            {
                hapDoc.LoadHtml("");
                _logger.LogError($"Error occurred while loading HtmlAgilityPack object for Url [{Uri}]", e);
            }
            return hapDoc;
        }

        private IHtmlDocument InitializeAngleSharpHtmlParser()
        {
            if(_angleSharpHtmlParser == null)
                _angleSharpHtmlParser = new HtmlParser();

            IHtmlDocument document;
            try
            {
                document = _angleSharpHtmlParser.Parse(Content.Text);
            }
            catch (Exception e)
            {
                document = _angleSharpHtmlParser.Parse("");

                _logger.LogError($"Error occurred while loading AngularSharp object for Url [{Uri}]", e);
            }

            return document;
        }
    }
}
