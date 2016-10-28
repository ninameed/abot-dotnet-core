
using Abot.Core;
using Abot.Poco;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;
namespace Abot.Tests.Unit.Core
{
    [TestFixture]
    public class AbotConfigurationSectionHandlerTest
    {
        AbotConfigurationSectionHandler _uut;

        public AbotConfigurationSectionHandlerTest()
        {
            var builder = new ConfigurationBuilder();
            builder.AddJsonFile("appsettings.json");
            var cr = builder.Build();

            _uut = new AbotConfigurationSectionHandler(cr);
        }


        [Test]
        public void GetSetion_FillsConfigValuesFromAppConfigFile()
        {
            Assert.IsNotNull(_uut.CrawlBehavior);
            Assert.AreEqual(44, _uut.CrawlBehavior.CrawlTimeoutSeconds);
            Assert.AreEqual("bbbb", _uut.CrawlBehavior.DownloadableContentTypes);
            Assert.AreEqual(true, _uut.CrawlBehavior.IsUriRecrawlingEnabled); 
            Assert.AreEqual(11, _uut.CrawlBehavior.MaxConcurrentThreads);
            Assert.AreEqual(33, _uut.CrawlBehavior.MaxPagesToCrawl);
            Assert.AreEqual(333, _uut.CrawlBehavior.MaxPagesToCrawlPerDomain);
            Assert.AreEqual(4444, _uut.CrawlBehavior.MaxPageSizeInBytes);
            Assert.AreEqual("aaaa", _uut.CrawlBehavior.UserAgentString);
            Assert.AreEqual(true, _uut.CrawlBehavior.IsExternalPageCrawlingEnabled);
            Assert.AreEqual(true, _uut.CrawlBehavior.IsExternalPageLinksCrawlingEnabled);
            Assert.AreEqual(21, _uut.CrawlBehavior.HttpServicePointConnectionLimit);
            Assert.AreEqual(22, _uut.CrawlBehavior.HttpRequestTimeoutInSeconds);
            Assert.AreEqual(23, _uut.CrawlBehavior.HttpRequestMaxAutoRedirects);
            Assert.AreEqual(true, _uut.CrawlBehavior.IsHttpRequestAutoRedirectsEnabled);
            Assert.AreEqual(true, _uut.CrawlBehavior.IsHttpRequestAutomaticDecompressionEnabled);
            Assert.AreEqual(true, _uut.CrawlBehavior.IsSendingCookiesEnabled);
            Assert.AreEqual(false, _uut.CrawlBehavior.IsSslCertificateValidationEnabled);
            Assert.AreEqual(true, _uut.CrawlBehavior.IsRespectUrlNamedAnchorOrHashbangEnabled);
            Assert.AreEqual(25, _uut.CrawlBehavior.MinAvailableMemoryRequiredInMb);
            Assert.AreEqual(26, _uut.CrawlBehavior.MaxMemoryUsageInMb);
            Assert.AreEqual(27, _uut.CrawlBehavior.MaxMemoryUsageCacheTimeInSeconds);
            Assert.AreEqual(28, _uut.CrawlBehavior.MaxCrawlDepth);
            Assert.AreEqual(29, _uut.CrawlBehavior.MaxLinksPerPage);
            Assert.AreEqual(true, _uut.CrawlBehavior.IsForcedLinkParsingEnabled);
            Assert.AreEqual(4, _uut.CrawlBehavior.MaxRetryCount);
            Assert.AreEqual(4444, _uut.CrawlBehavior.MinRetryDelayInMilliseconds);

            Assert.IsNotNull(_uut.Politeness);
            Assert.AreEqual(true, _uut.Politeness.IsRespectRobotsDotTextEnabled);
            Assert.AreEqual(true, _uut.Politeness.IsRespectMetaRobotsNoFollowEnabled);
            Assert.AreEqual(true, _uut.Politeness.IsRespectAnchorRelNoFollowEnabled);
            Assert.AreEqual(true, _uut.Politeness.IsRespectHttpXRobotsTagHeaderNoFollowEnabled);
            Assert.AreEqual(true, _uut.Politeness.IsIgnoreRobotsDotTextIfRootDisallowedEnabled);
            Assert.AreEqual("zzzz", _uut.Politeness.RobotsDotTextUserAgentString);
            Assert.AreEqual(55, _uut.Politeness.MinCrawlDelayPerDomainMilliSeconds);
            Assert.AreEqual(5, _uut.Politeness.MaxRobotsDotTextCrawlDelayInSeconds);

            Assert.IsNotNull(_uut.Extensions);
            Assert.AreEqual("value1", _uut.Extensions.GetValue("key1"));
            Assert.AreEqual("value2", _uut.Extensions.GetValue("key2"));
        }

        [Test]
        public void Convert_CovertsFromSectionObjectToDtoObject()
        {
            CrawlConfiguration result = _uut.Convert();

            Assert.IsNotNull(result);
            Assert.AreEqual(result.CrawlTimeoutSeconds, _uut.CrawlBehavior.CrawlTimeoutSeconds);
            Assert.AreEqual(result.DownloadableContentTypes, _uut.CrawlBehavior.DownloadableContentTypes);
            Assert.AreEqual(result.IsUriRecrawlingEnabled, _uut.CrawlBehavior.IsUriRecrawlingEnabled);
            Assert.AreEqual(result.MaxConcurrentThreads, _uut.CrawlBehavior.MaxConcurrentThreads);
            Assert.AreEqual(result.MaxPagesToCrawl, _uut.CrawlBehavior.MaxPagesToCrawl);
            Assert.AreEqual(result.MaxPagesToCrawlPerDomain, _uut.CrawlBehavior.MaxPagesToCrawlPerDomain);
            Assert.AreEqual(result.MaxPageSizeInBytes, _uut.CrawlBehavior.MaxPageSizeInBytes);
            Assert.AreEqual(result.UserAgentString, _uut.CrawlBehavior.UserAgentString);
            Assert.AreEqual(result.IsExternalPageCrawlingEnabled, _uut.CrawlBehavior.IsExternalPageCrawlingEnabled);
            Assert.AreEqual(result.IsExternalPageLinksCrawlingEnabled, _uut.CrawlBehavior.IsExternalPageLinksCrawlingEnabled);
            Assert.AreEqual(result.HttpServicePointConnectionLimit, _uut.CrawlBehavior.HttpServicePointConnectionLimit);
            Assert.AreEqual(result.HttpRequestTimeoutInSeconds, _uut.CrawlBehavior.HttpRequestTimeoutInSeconds);
            Assert.AreEqual(result.HttpRequestMaxAutoRedirects, _uut.CrawlBehavior.HttpRequestMaxAutoRedirects);
            Assert.AreEqual(true, _uut.CrawlBehavior.IsHttpRequestAutoRedirectsEnabled);
            Assert.AreEqual(true, _uut.CrawlBehavior.IsHttpRequestAutomaticDecompressionEnabled);
            Assert.AreEqual(true, _uut.CrawlBehavior.IsSendingCookiesEnabled);
            Assert.AreEqual(false, _uut.CrawlBehavior.IsSslCertificateValidationEnabled);
            Assert.AreEqual(true, _uut.CrawlBehavior.IsRespectUrlNamedAnchorOrHashbangEnabled);
            Assert.AreEqual(result.MinAvailableMemoryRequiredInMb, _uut.CrawlBehavior.MinAvailableMemoryRequiredInMb);
            Assert.AreEqual(result.MaxMemoryUsageInMb, _uut.CrawlBehavior.MaxMemoryUsageInMb);
            Assert.AreEqual(result.MaxMemoryUsageCacheTimeInSeconds, _uut.CrawlBehavior.MaxMemoryUsageCacheTimeInSeconds);
            Assert.AreEqual(result.MaxCrawlDepth, _uut.CrawlBehavior.MaxCrawlDepth);
            Assert.AreEqual(result.MaxLinksPerPage, _uut.CrawlBehavior.MaxLinksPerPage);
            Assert.AreEqual(result.IsForcedLinkParsingEnabled, _uut.CrawlBehavior.IsForcedLinkParsingEnabled);
            Assert.AreEqual(result.MaxRetryCount, _uut.CrawlBehavior.MaxRetryCount);
            Assert.AreEqual(result.MinRetryDelayInMilliseconds, _uut.CrawlBehavior.MinRetryDelayInMilliseconds);
            
            Assert.AreEqual(result.IsRespectRobotsDotTextEnabled, _uut.Politeness.IsRespectRobotsDotTextEnabled);
            Assert.AreEqual(result.IsRespectMetaRobotsNoFollowEnabled, _uut.Politeness.IsRespectMetaRobotsNoFollowEnabled);
            Assert.AreEqual(result.IsRespectHttpXRobotsTagHeaderNoFollowEnabled, _uut.Politeness.IsRespectHttpXRobotsTagHeaderNoFollowEnabled);
            Assert.AreEqual(result.IsRespectAnchorRelNoFollowEnabled, _uut.Politeness.IsRespectAnchorRelNoFollowEnabled);

            Assert.AreEqual(result.IsIgnoreRobotsDotTextIfRootDisallowedEnabled, _uut.Politeness.IsIgnoreRobotsDotTextIfRootDisallowedEnabled);
            Assert.AreEqual(result.RobotsDotTextUserAgentString, _uut.Politeness.RobotsDotTextUserAgentString);

            Assert.AreEqual(result.MinCrawlDelayPerDomainMilliSeconds, _uut.Politeness.MinCrawlDelayPerDomainMilliSeconds);
            Assert.AreEqual(result.MaxRobotsDotTextCrawlDelayInSeconds, _uut.Politeness.MaxRobotsDotTextCrawlDelayInSeconds);

            Assert.AreEqual(result.IsAlwaysLogin, _uut.Authorization.IsAlwaysLogin);
            Assert.AreEqual(result.LoginPassword, _uut.Authorization.LoginPassword);
            Assert.AreEqual(result.LoginUser, _uut.Authorization.LoginUser);

            Assert.IsNotNull(result.ConfigurationExtensions);
            Assert.AreEqual(result.ConfigurationExtensions["key1"], _uut.Extensions.GetValue("key1"));
            Assert.AreEqual(result.ConfigurationExtensions["key2"], _uut.Extensions.GetValue("key2"));
        }
    }
}
