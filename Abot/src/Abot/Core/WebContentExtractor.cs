
using Abot.Poco;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Abot.Core
{
    public interface IWebContentExtractor : IDisposable
    {
        Task<PageContent> GetContentAsync(HttpResponseMessage httpResponseMessage);
    }

    public class WebContentExtractor : IWebContentExtractor
    {
        static ILogger _logger = new LoggerFactory().CreateLogger("AbotLogger");

        public virtual async Task<PageContent> GetContentAsync(HttpResponseMessage httpResponseMessage)
        {
            using (var memoryStream = await GetRawData(httpResponseMessage))
            {
                var charset = GetCharsetFromHeaders(httpResponseMessage);

                if (charset == null) {
                    memoryStream.Seek(0, SeekOrigin.Begin);

                    // Do not wrap in closing statement to prevent closing of this stream.
                    var srr = new StreamReader(memoryStream, Encoding.ASCII);
                    var body = srr.ReadToEnd();
                    charset = GetCharsetFromBody(body);
                }
                memoryStream.Seek(0, SeekOrigin.Begin);

                charset = CleanCharset(charset);
                var e = GetEncoding(charset);
                var content = "";
                using (var sr = new StreamReader(memoryStream, e))
                {
                    content = sr.ReadToEnd();
                }

                var pageContent = new PageContent();
                pageContent.Bytes = memoryStream.ToArray();
                pageContent.Charset = charset;
                pageContent.Encoding = e;
                pageContent.Text = content;

                return pageContent;
            }
        }

        protected virtual string GetCharsetFromHeaders(HttpResponseMessage httpResponseMessage)
        {
            return httpResponseMessage.Content.Headers.ContentType?.CharSet;
        }

        protected virtual string GetCharsetFromBody(string body)
        {
            String charset = null;
            
            if (body != null)
            {
                //find expression from : http://stackoverflow.com/questions/3458217/how-to-use-regular-expression-to-match-the-charset-string-in-html
                var match = Regex.Match(body, @"<meta(?!\s*(?:name|value)\s*=)(?:[^>]*?content\s*=[\s""']*)?([^>]*?)[\s""';]*charset\s*=[\s""']*([^\s""'/>]*)", RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    charset = string.IsNullOrWhiteSpace(match.Groups[2].Value) ? null : match.Groups[2].Value;
                }
            }

            return charset;
        }
        
        protected virtual Encoding GetEncoding(string charset)
        {
            var e = Encoding.UTF8;
            if (charset != null)
            {
                try
                {
                    e = Encoding.GetEncoding(charset);
                }
                catch{}
            }

            return e;
        }

        protected virtual string CleanCharset(string charset)
        {
            //TODO temporary hack, this needs to be a configurable value
            if (charset == "cp1251") //Russian, Bulgarian, Serbian cyrillic
                charset = "windows-1251";

            return charset;
        }

        private async Task<MemoryStream> GetRawData(HttpResponseMessage responseMessage)
        {
            var rawData = new MemoryStream();

            try
            {
                using (var rs = await responseMessage.Content.ReadAsStreamAsync())
                {
                    var buffer = new byte[1024];
                    var read = rs.Read(buffer, 0, buffer.Length);
                    while (read > 0)
                    {
                        rawData.Write(buffer, 0, read);
                        read = rs.Read(buffer, 0, buffer.Length);
                    }
                }
            }
            catch (Exception e)
            {
                _logger.LogWarning($"Error occurred while downloading content of url {responseMessage.RequestMessage.RequestUri}", e);
            }

            return rawData;
        }

        public virtual void Dispose()
        {
            // Nothing to do
        }
    }
}
