using Abot.Poco;
using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;

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
        protected HttpMessageHandler _httpClientHandler;
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
            HttpRequestMessage request = null;
            HttpResponseMessage httpResponseMessage = null;
            try
            {
                request = BuildRequestObject(_httpClient, uri);
                crawledPage.RequestStarted = DateTime.Now;
                httpResponseMessage = await _httpClient.SendAsync(request);
            }
            catch (Exception e)
            {
                _logger.LogDebug($"Error occurred requesting url [{uri.AbsoluteUri}]", e);
            }
            finally
            {
                try
                {
                    crawledPage.HttpRequestMessage = request;
                    crawledPage.RequestCompleted = DateTime.Now;
                    if (httpResponseMessage != null)
                    {
                        crawledPage.HttpWebResponse = new HttpWebResponseWrapper(httpResponseMessage, _cookieContainer);
                        var shouldDownloadContentDecision = shouldDownloadContent(crawledPage);
                        if (shouldDownloadContentDecision.Allow)
                        {
                            crawledPage.DownloadContentStarted = DateTime.Now;
                            crawledPage.Content = await _extractor.GetContentAsync(httpResponseMessage);
                            crawledPage.DownloadContentCompleted = DateTime.Now;
                        }
                        else
                        {
                            _logger.LogDebug($"Links on page [{crawledPage.Uri.AbsoluteUri}] not crawled, [{shouldDownloadContentDecision.Reason}]");
                        }

                        httpResponseMessage.EnsureSuccessStatusCode();
                        httpResponseMessage.Dispose();//Should already be closed by _extractor but just being safe
                    }
                }
                catch (HttpRequestException e)
                {
                    crawledPage.HttpRequestException = e;
                    _logger.LogDebug($"Error occurred finalizing requesting url [{uri.AbsoluteUri}]", e);
                }
                catch (Exception e)
                {
                    _logger.LogDebug($"Error occurred finalizing requesting url [{uri.AbsoluteUri}]", e);
                }
            }

            return crawledPage;
        }

        private HttpRequestMessage BuildRequestObject(HttpClient c, Uri uri)
        {
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.UserAgent.ParseAdd(_config.UserAgentString);
            request.Headers.Accept.ParseAdd("*/*");

            if (_config.IsHttpRequestAutomaticDecompressionEnabled)
                request.Headers.AcceptEncoding.ParseAdd("gzip, deflate");

            if (_config.HttpRequestTimeoutInSeconds > 0)
                c.Timeout = TimeSpan.FromSeconds(_config.HttpRequestTimeoutInSeconds);

            if (_config.IsAlwaysLogin)
            {
                string credentials = Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes(_config.LoginUser + ":" + _config.LoginPassword));
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
            }

            return request;
        }

        //Do not mark as virtual, could cause issues in inheritance constructoring calling
        private HttpMessageHandler BuildHttpClientHandler()
        {
#if NETSTANDARD1_6
            var handler = new HttpClientHandler();
#elif NET46
            var handler = new WebRequestHandler();
#endif

            handler.AllowAutoRedirect = _config.IsHttpRequestAutoRedirectsEnabled;
            if (_config.HttpRequestMaxAutoRedirects > 0)
            {
                handler.MaxAutomaticRedirections = _config.HttpRequestMaxAutoRedirects;
            }
            if (_config.IsHttpRequestAutomaticDecompressionEnabled)
            {
                handler.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            }
            //http://stackoverflow.com/questions/13318102/struggling-trying-to-get-cookie-out-of-response-with-httpclient-in-net-4-5
            if (_config.IsSendingCookiesEnabled)
            {
                handler.CookieContainer = _cookieContainer;
            }

            //Supposedly this does not work... https://github.com/sjdirect/abot/issues/122
            //if (_config.IsAlwaysLogin)
            //{
            //    handler.Credentials = new NetworkCredential(_config.LoginUser, _config.LoginPassword);
            //    handler.UseDefaultCredentials = false;
            //}

#if NETSTANDARD1_6

            if (!_config.IsSslCertificateValidationEnabled)
            {
                handler.ServerCertificateCustomValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true;
            }

            //TODO find the .net core equivalent
            //http://stackoverflow.com/questions/36398474/servicepointmanager-defaultconnectionlimit-in-net-core
            //if (_config.HttpServicePointConnectionLimit > 0)
            //    ServicePointManager.DefaultConnectionLimit = _config.HttpServicePointConnectionLimit;

#elif NET46

            if (!_config.IsSslCertificateValidationEnabled)
            {
                handler.ServerCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true;
            }

            if (_config.HttpServicePointConnectionLimit > 0)
            {
                ServicePointManager.DefaultConnectionLimit = _config.HttpServicePointConnectionLimit;
            }
#endif
            return handler;
        }

        //Do not mark as virtual, could cause issues in inheritance constructoring calling
        private HttpClient BuildHttpClient(HttpMessageHandler httpHandler)
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