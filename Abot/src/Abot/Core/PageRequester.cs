using Abot.Poco;
using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Abot.Core
{
    public interface IPageRequester : IDisposable
    {
        /// <summary>
        /// Make an http web request to the url and download its content
        /// </summary>
        Task<CrawledPage> MakeRequestAsync(Uri uri);

        /// <summary>
        /// Make an http web request to the url and download its content based on the param func decision
        /// </summary>
        Task<CrawledPage> MakeRequestAsync(Uri uri, Func<CrawledPage, CrawlDecision> shouldDownloadContent);
    }

    public class PageRequester : IPageRequester
    {
        static ILogger _logger = new LoggerFactory().CreateLogger<PageRequester>();

        protected CrawlConfiguration _config;
        protected IWebContentExtractor _extractor;
        protected CookieContainer _cookieContainer = new CookieContainer();
        protected HttpClientHandler _httpClientHandler;
        protected HttpClient _httpClient;

        public PageRequester(CrawlConfiguration config)
            : this(config, null)
        {

        }

        public PageRequester(CrawlConfiguration config, IWebContentExtractor contentExtractor)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            _config = config;

            //TODO find the .net core equivalent
            //http://stackoverflow.com/questions/36398474/servicepointmanager-defaultconnectionlimit-in-net-core
            //if (_config.HttpServicePointConnectionLimit > 0)
            //    ServicePointManager.DefaultConnectionLimit = _config.HttpServicePointConnectionLimit;

            //if (!_config.IsSslCertificateValidationEnabled)
            //    ServicePointManager.ServerCertificateValidationCallback +=
            //        (sender, certificate, chain, sslPolicyErrors) => true;

            _extractor = contentExtractor ?? new WebContentExtractor();

            _httpClientHandler = BuildHttpClientHandler();
            _httpClient = BuildHttpClient(_httpClientHandler);
        }

        /// <summary>
        /// Make an http web request to the url and download its content
        /// </summary>
        public virtual async Task<CrawledPage> MakeRequestAsync(Uri uri)
        {
            return await MakeRequestAsync(uri, (x) => new CrawlDecision { Allow = true });
        }

        /// <summary>
        /// Make an http web request to the url and download its content based on the param func decision
        /// </summary>
        public virtual async Task<CrawledPage> MakeRequestAsync(Uri uri, Func<CrawledPage, CrawlDecision> shouldDownloadContent)
        {
            if (uri == null)
                throw new ArgumentNullException(nameof(uri));

            var crawledPage = new CrawledPage(uri);
            HttpResponseMessage httpResponseMessage = null;
            try
            {
                crawledPage.RequestStarted = DateTime.Now;
                httpResponseMessage = await _httpClient.GetAsync(uri);
            }
            //catch (HttpRequestException hre)
            //{
            //http://stackoverflow.com/questions/22382373/httprequestexception-vs-webexception
            //}
            //catch (IOException ioe)
            //{

            //}
            catch (WebException e)
            {
                crawledPage.WebException = e;
                _logger.LogDebug($"Error occurred requesting url [{uri.AbsoluteUri}]", e);
            }
            catch (Exception e)
            {
                _logger.LogDebug($"Error occurred requesting url [{uri.AbsoluteUri}]", e);
            }
            finally
            {
                try
                {
                    crawledPage.HttpResponseMessage = httpResponseMessage;
                    crawledPage.RequestCompleted = DateTime.Now;
                    if (httpResponseMessage != null)
                    {
                        crawledPage.HttpWebResponse = new HttpWebResponseWrapper(httpResponseMessage);
                        var shouldDownloadContentDecision = shouldDownloadContent(crawledPage);
                        if (shouldDownloadContentDecision.Allow)
                        {
                            crawledPage.DownloadContentStarted = DateTime.Now;
                            crawledPage.Content = await _extractor.GetContent(httpResponseMessage);
                            crawledPage.DownloadContentCompleted = DateTime.Now;
                        }
                        else
                        {
                            _logger.LogDebug($"Links on page [{crawledPage.Uri.AbsoluteUri}] not crawled, [{shouldDownloadContentDecision.Reason}]");
                        }

                        //response.Close();//Should already be closed by _extractor but just being safe
                    }
                }
                catch (Exception e)
                {
                    _logger.LogDebug($"Error occurred finalizing requesting url [{uri.AbsoluteUri}]", e);
                }
            }

            return crawledPage;
        }

        //Do not mark as virtual, could cause issues in inheritance constructoring calling
        private HttpClientHandler BuildHttpClientHandler()
        {
            var handler = new HttpClientHandler();

            handler.AllowAutoRedirect = _config.IsHttpRequestAutoRedirectsEnabled;
            if (_config.HttpRequestMaxAutoRedirects > 0)
                handler.MaxAutomaticRedirections = _config.HttpRequestMaxAutoRedirects;

            if (_config.IsHttpRequestAutomaticDecompressionEnabled)
                handler.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;

            //http://stackoverflow.com/questions/13318102/struggling-trying-to-get-cookie-out-of-response-with-httpclient-in-net-4-5
            if (_config.IsSendingCookiesEnabled)
                handler.CookieContainer = _cookieContainer;

            //Supposedly this does not work... https://github.com/sjdirect/abot/issues/122
            //if (_config.IsAlwaysLogin)
            //{
            //    handler.Credentials = new NetworkCredential(_config.LoginUser, _config.LoginPassword);
            //    handler.UseDefaultCredentials = false;
            //}

            return handler;
        }

        //Do not mark as virtual, could cause issues in inheritance constructoring calling
        private HttpClient BuildHttpClient(HttpClientHandler httpHandler)
        {
            var httpClient = new HttpClient(httpHandler);
            httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "*/*");
            httpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", _config.UserAgentString);
            
            if (_config.HttpRequestTimeoutInSeconds > 0)
                httpClient.Timeout = TimeSpan.FromSeconds(_config.HttpRequestTimeoutInSeconds);

            if (_config.IsAlwaysLogin)
            {
                var credentials = Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes(_config.LoginUser + ":" + _config.LoginPassword));
                httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Authorization",  "Basic " + credentials);
            }
            return httpClient;
        }

        public void Dispose()
        {
            if (_extractor != null)
            {
                _extractor.Dispose();
            }
            _cookieContainer = null;
            _config = null;
        }
    }
}