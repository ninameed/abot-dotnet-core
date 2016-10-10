using Abot.Poco;
using Microsoft.Extensions.Logging;
using System;
using System.Net;
using System.Threading.Tasks;

namespace Abot.Core
{
    /// <summary>
    /// Finds and builds the robots.txt file abstraction
    /// </summary>
    public interface IRobotsDotTextFinder
    {
        /// <summary>
        /// Finds the robots.txt file using the rootUri. 
        /// If rootUri is http://yahoo.com, it will look for robots at http://yahoo.com/robots.txt.
        /// If rootUri is http://music.yahoo.com, it will look for robots at http://music.yahoo.com/robots.txt
        /// </summary>
        /// <param name="rootUri">The root domain</param>
        /// <returns>Object representing the robots.txt file or returns null</returns>
        Task<IRobotsDotText> Find(Uri rootUri);
    }

    public class RobotsDotTextFinder : IRobotsDotTextFinder
    {
        static ILogger _logger = new LoggerFactory().CreateLogger("AbotLogger");
        IPageRequester _pageRequester;

        public RobotsDotTextFinder(IPageRequester pageRequester)
        {
            if (pageRequester == null)
                throw new ArgumentNullException(nameof(pageRequester));

            _pageRequester = pageRequester;
        }

        public async Task<IRobotsDotText> Find(Uri rootUri)
        {
            if (rootUri == null)
                throw new ArgumentNullException(nameof(rootUri));

            var robotsUri = new Uri(rootUri, "/robots.txt");
            var page = await _pageRequester.MakeRequestAsync(robotsUri);
            if (page == null || page.WebException != null || page.HttpWebResponse == null || page.HttpWebResponse.StatusCode != HttpStatusCode.OK)
            {
                _logger.LogDebug($"Did not find robots.txt file at [{robotsUri}]");
                return null;
            }

            _logger.LogDebug($"Found robots.txt file at [{robotsUri}]");
            return new RobotsDotText(rootUri, page.Content.Text);
        }
    }
}
