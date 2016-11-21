using Abot.Core;
using Abot.Poco;
using Abot.Tests.Unit.Helpers;
using Moq;
using NUnit.Framework;
using System;
using System.Threading.Tasks;

namespace Abot.Tests.Unit.Core
{
    [TestFixture]
    public class RobotsDotTextFinderTest
    {
        RobotsDotTextFinder _uut;
        Mock<IPageRequester> _fakePageRequester;
        CrawledPage _goodPageResult;
        CrawledPage _badPageResult;

        [OneTimeSetUp]
        public async Task TestFixtureSetup()
        {
            UnitTestConfig unitTestConfig = new UnitTestConfig();
            PageRequester pageRequster = new PageRequester(new CrawlConfiguration { UserAgentString = "aaa" });
            _goodPageResult = await pageRequster.MakeRequestAsync(new Uri(unitTestConfig.SiteSimulatorBaseAddress));
            _badPageResult = await pageRequster.MakeRequestAsync(new Uri(string.Concat(unitTestConfig.SiteSimulatorBaseAddress, "/HttpResponse/Status404")));
        }

        [SetUp]
        public void SetUp()
        {
            _fakePageRequester = new Mock<IPageRequester>();
            _uut = new RobotsDotTextFinder(_fakePageRequester.Object);
        }

        [Test]
        public void Constructor_PageRequesterIsNull()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                new RobotsDotTextFinder(null);
            });
        }

        [Test]
        public void Find_NullUrl()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                _uut.FindAsync(null).GetAwaiter().GetResult();
            });
        }

        [Test]
        public async Task Find_RobotsExists_UriIsDomain_ReturnsRobotsDotText()
        {
            Uri _rootUri = new Uri("http://a.com/");
            Uri expectedRobotsUri = new Uri("http://a.com/robots.txt");
            _fakePageRequester.Setup(f => f.MakeRequestAsync(It.Is<Uri>(u => u == expectedRobotsUri))).Returns(Task.FromResult(_goodPageResult));

            IRobotsDotText result = await _uut.FindAsync(_rootUri);

            _fakePageRequester.Verify(f => f.MakeRequestAsync(It.Is<Uri>(u => u == expectedRobotsUri)), Times.Exactly(1));
            Assert.IsNotNull(result);
        }

        [Test]
        public async Task Find_RobotsExists_UriIsSubDomain_ReturnsRobotsDotText()
        {
            Uri _rootUri = new Uri("http://aaa.a.com/");
            Uri expectedRobotsUri = new Uri("http://aaa.a.com/robots.txt");
            _fakePageRequester.Setup(f => f.MakeRequestAsync(It.Is<Uri>(u => u == expectedRobotsUri))).Returns(Task.FromResult(_goodPageResult));

            IRobotsDotText result = await _uut.FindAsync(_rootUri);

            _fakePageRequester.Verify(f => f.MakeRequestAsync(It.Is<Uri>(u => u == expectedRobotsUri)), Times.Exactly(1));
            Assert.IsNotNull(result);
        }

        [Test]
        public async Task Find_RobotsExists_UriIsNotRootDomain_ReturnsRobotsDotText()
        {
            Uri _rootUri = new Uri("http://a.com/a/b/b.html");
            Uri expectedRobotsUri = new Uri("http://a.com/robots.txt");
            _fakePageRequester.Setup(f => f.MakeRequestAsync(It.Is<Uri>(u => u == expectedRobotsUri))).Returns(Task.FromResult(_goodPageResult));

            IRobotsDotText result = await _uut.FindAsync(_rootUri);

            _fakePageRequester.Verify(f => f.MakeRequestAsync(It.Is<Uri>(u => u == expectedRobotsUri)), Times.Exactly(1));
            Assert.IsNotNull(result);
        }

        [Test]
        public async Task Find_RobotsExists_UriIsRootDomainNoSlash_ReturnsRobotsDotText()
        {
            Uri _rootUri = new Uri("http://a.com");
            Uri expectedRobotsUri = new Uri("http://a.com/robots.txt");
            _fakePageRequester.Setup(f => f.MakeRequestAsync(It.Is<Uri>(u => u == expectedRobotsUri))).Returns(Task.FromResult(_goodPageResult));

            IRobotsDotText result = await _uut.FindAsync(_rootUri);

            _fakePageRequester.Verify(f => f.MakeRequestAsync(It.Is<Uri>(u => u == expectedRobotsUri)), Times.Exactly(1));
            Assert.IsNotNull(result);
        }

        [Test]
        public async Task Find_RobotsExists_RootAndExpectedAreSame_ReturnsRobotsDotText()
        {
            Uri _rootUri = new Uri("http://a.com/robots.txt");
            Uri expectedRobotsUri = new Uri("http://a.com/robots.txt");
            _fakePageRequester.Setup(f => f.MakeRequestAsync(It.Is<Uri>(u => u == expectedRobotsUri))).Returns(Task.FromResult(_goodPageResult));

            IRobotsDotText result = await _uut.FindAsync(_rootUri);

            _fakePageRequester.Verify(f => f.MakeRequestAsync(It.Is<Uri>(u => u == expectedRobotsUri)), Times.Exactly(1));
            Assert.IsNotNull(result);
        }

        [Test]
        public async Task Find_RobotsDoesNotExists_ReturnsNull()
        {
            Uri _rootUri = new Uri("http://a.com/");
            Uri expectedRobotsUri = new Uri("http://a.com/robots.txt");
            _fakePageRequester.Setup(f => f.MakeRequestAsync(It.Is<Uri>(u => u == expectedRobotsUri))).Returns(Task.FromResult(_badPageResult));

            IRobotsDotText result = await _uut.FindAsync(_rootUri);

            _fakePageRequester.Verify(f => f.MakeRequestAsync(It.Is<Uri>(u => u == expectedRobotsUri)), Times.Exactly(1));
            Assert.IsNull(result);
        }
    }
}
