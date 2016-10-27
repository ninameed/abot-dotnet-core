using Abot.Poco;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Abot.Core
{
    /// <summary>
    /// Handles parsing hyperlinks out of the raw html
    /// </summary>
    public interface IHyperLinkParser
    {
        /// <summary>
        /// Parses html to extract hyperlinks, converts each into an absolute url
        /// </summary>
        IEnumerable<Uri> GetLinks(CrawledPage crawledPage);
    }

    public abstract class HyperLinkParser : IHyperLinkParser
    {
        protected ILogger _logger = new LoggerFactory().CreateLogger("AbotLogger");
        protected CrawlConfiguration _config;
        protected Func<string, string> _cleanURLFunc;

        protected HyperLinkParser()
            :this(new CrawlConfiguration(), null)
        {

        }

        protected HyperLinkParser(CrawlConfiguration config, Func<string, string> cleanURLFunc)
        {
            _config = config;
            _cleanURLFunc = cleanURLFunc;
        }

        /// <summary>
        /// Parses html to extract hyperlinks, converts each into an absolute url
        /// </summary>
        public virtual IEnumerable<Uri> GetLinks(CrawledPage crawledPage)
        {
            CheckParams(crawledPage);

            var timer = Stopwatch.StartNew();

            var uris = GetUris(crawledPage, GetHrefValues(crawledPage));
            
            timer.Stop();
            _logger.LogDebug($"{ParserType} parsed links from [{crawledPage.Uri}] in [{timer.ElapsedMilliseconds}] milliseconds");

            return uris;
        }

        #region Abstract

        protected abstract string ParserType { get; }

        protected abstract IEnumerable<string> GetHrefValues(CrawledPage crawledPage);

        protected abstract string GetBaseHrefValue(CrawledPage crawledPage);

        protected abstract string GetMetaRobotsValue(CrawledPage crawledPage);

        #endregion

        protected virtual void CheckParams(CrawledPage crawledPage)
        {
            if (crawledPage == null)
                throw new ArgumentNullException(nameof(crawledPage));
        }

        protected virtual List<Uri> GetUris(CrawledPage crawledPage, IEnumerable<string> hrefValues)
        {
            var uris = new List<Uri>();
            if (hrefValues == null || hrefValues.Count() < 1)
                return uris;

            //Use the uri of the page that actually responded to the request instead of crawledPage.Uri (Issue 82).
            //Using HttpWebRequest.Address instead of HttpWebResonse.ResponseUri since this is the best practice and mentioned on http://msdn.microsoft.com/en-us/library/system.net.httpwebresponse.responseuri.aspx
            var uriToUse = crawledPage.HttpWebRequest.RequestUri ?? crawledPage.Uri;

            //If html base tag exists use it instead of page uri for relative links
            var baseHref = GetBaseHrefValue(crawledPage);
            if (!string.IsNullOrEmpty(baseHref))
            {
                if (baseHref.StartsWith("//"))
                    baseHref = crawledPage.Uri.Scheme + ":" + baseHref;

                try
                {
                    uriToUse = new Uri(baseHref);
                }
                catch { }
            }

            var href = "";
            foreach (var hrefValue in hrefValues)
            {
                try
                {
                    // Remove the url fragment part of the url if needed.
                    // This is the part after the # and is often not useful.
                    href = _config.IsRespectUrlNamedAnchorOrHashbangEnabled
                        ? hrefValue
                        : hrefValue.Split('#')[0];
                    var newUri = new Uri(uriToUse, href);

                    if (_cleanURLFunc != null)
                        newUri = new Uri(_cleanURLFunc(newUri.AbsoluteUri));

                    if (!uris.Exists(u => u.AbsoluteUri == newUri.AbsoluteUri))
                        uris.Add(newUri);
                }
                catch (Exception e)
                {
                    _logger.LogDebug($"Could not parse link [{hrefValue}] on page [{crawledPage.Uri}]", e);
                }
            }

            return uris;
        }

        protected virtual bool HasRobotsNoFollow(CrawledPage crawledPage)
        {
            //X-Robots-Tag http header
            if(_config.IsRespectHttpXRobotsTagHeaderNoFollowEnabled)
            {
                var xRobotsTagHeader = crawledPage.HttpWebResponse.GetResponseHeader("X-Robots-Tag");
                if (xRobotsTagHeader != null && 
                    (xRobotsTagHeader.ToLower().Contains("nofollow") ||
                     xRobotsTagHeader.ToLower().Contains("none")))
                {
                    _logger.LogInformation($"Http header X-Robots-Tag nofollow detected on uri [{crawledPage.Uri}], will not crawl links on this page.");
                    return true;
                }   
            }

            //Meta robots tag
            if (_config.IsRespectMetaRobotsNoFollowEnabled)
            {
                var robotsMeta = GetMetaRobotsValue(crawledPage);
                if (robotsMeta != null &&
                    (robotsMeta.ToLower().Contains("nofollow") ||
                     robotsMeta.ToLower().Contains("none")))
                {
                    _logger.LogInformation($"Meta Robots nofollow tag detected on uri [{crawledPage.Uri}], will not crawl links on this page.");
                    return true;
                }                
                
            }

            return false;
        }
    }
}