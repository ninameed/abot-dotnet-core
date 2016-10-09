using Abot.Poco;
using log4net;
using System;
using System.CodeDom;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using log4net.Core;

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
        static ILog _logger = LogManager.GetLogger("AbotLogger");

        protected CrawlConfiguration _config;
        protected IWebContentExtractor _extractor;
        protected CookieContainer _cookieContainer = new CookieContainer();

        public PageRequester(CrawlConfiguration config)
            : this(config, null)
        {

        }

        public PageRequester(CrawlConfiguration config, IWebContentExtractor contentExtractor)
        {
            if (config == null)
                throw new ArgumentNullException("config");

            _config = config;

            //TODO find the .net core equivalent
            //http://stackoverflow.com/questions/36398474/servicepointmanager-defaultconnectionlimit-in-net-core
            //if (_config.HttpServicePointConnectionLimit > 0)
            //    ServicePointManager.DefaultConnectionLimit = _config.HttpServicePointConnectionLimit;

            //if (!_config.IsSslCertificateValidationEnabled)
            //    ServicePointManager.ServerCertificateValidationCallback +=
            //        (sender, certificate, chain, sslPolicyErrors) => true;

            _extractor = contentExtractor ?? new WebContentExtractor();
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
                throw new ArgumentNullException("uri");

            CrawledPage crawledPage = new CrawledPage(uri);

            //https://blogs.msdn.microsoft.com/henrikn/2012/08/07/httpclient-httpclienthandler-and-webrequesthandler-explained/
            //TODO Reuse HttpClient http://stackoverflow.com/questions/15705092/do-httpclient-and-httpclienthandler-have-to-be-disposed
            //AND http://aspnetmonsters.com/2016/08/2016-08-27-httpclientwrong/

            HttpClientHandler httpClientHandler = null;
            HttpClient httpClient = null;
            HttpResponseMessage httpResponseMessage = null;
            Stream httpResponseStream = null;
            try
            {
                httpClientHandler = BuildHttpClientHandler();
                httpClient = BuildHttpClient(httpClientHandler);
                crawledPage.RequestStarted = DateTime.Now;

                httpResponseMessage = await httpClient.GetAsync(uri);
                //ProcessResponse(httpResponseMessage, httpClientHandler);
                //httpResponseStream = await httpClient.GetStreamAsync(uri);
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
                _logger.DebugFormat("Error occurred requesting url [{0}]", uri.AbsoluteUri);
                _logger.Debug(e);
            }
            catch (Exception e)
            {
                _logger.DebugFormat("Error occurred requesting url [{0}]", uri.AbsoluteUri);
                _logger.Debug(e);
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
                        CrawlDecision shouldDownloadContentDecision = shouldDownloadContent(crawledPage);
                        if (shouldDownloadContentDecision.Allow)
                        {
                            crawledPage.DownloadContentStarted = DateTime.Now;
                            crawledPage.Content = await _extractor.GetContent(httpResponseMessage);
                            crawledPage.DownloadContentCompleted = DateTime.Now;
                        }
                        else
                        {
                            _logger.DebugFormat("Links on page [{0}] not crawled, [{1}]", crawledPage.Uri.AbsoluteUri, shouldDownloadContentDecision.Reason);
                        }

                        //response.Close();//Should already be closed by _extractor but just being safe
                    }
                }
                catch (Exception e)
                {
                    _logger.DebugFormat("Error occurred finalizing requesting url [{0}]", uri.AbsoluteUri);
                    _logger.Debug(e);
                }
            }

            return crawledPage;
        }

        protected virtual HttpClientHandler BuildHttpClientHandler()
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

        protected virtual HttpClient BuildHttpClient(HttpClientHandler httpHandler)
        {
            var httpClient = new HttpClient(httpHandler);
            httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "*/*");
            httpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", _config.UserAgentString);
            
            if (_config.HttpRequestTimeoutInSeconds > 0)
                httpClient.Timeout = TimeSpan.FromSeconds(_config.HttpRequestTimeoutInSeconds);

            if (_config.IsAlwaysLogin)
            {
                string credentials = Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes(_config.LoginUser + ":" + _config.LoginPassword));
                httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Authorization",  "Basic " + credentials);
            }
            return httpClient;
        }

        //protected virtual void ProcessResponse(HttpResponseMessage responseMessage, HttpClientHandler httpHandler)
        //{
        //    //http://stackoverflow.com/questions/13318102/struggling-trying-to-get-cookie-out-of-response-with-httpclient-in-net-4-5
        //    //Should already be in the _cookieContainer, looks like if its not null it automatically adds them
        //    //if (responseMessage != null && _config.IsSendingCookiesEnabled)
        //    //{
        //    //    CookieCollection cookies = response.Cookies;
        //    //    _cookieContainer.Add(cookies);
        //    //}
        //}

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