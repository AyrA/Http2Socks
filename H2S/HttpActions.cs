using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;

namespace H2S
{
    /// <summary>
    /// Provides generic HTTP answers in the form of small HTML documents
    /// </summary>
    public static class HttpActions
    {
        /// <summary>
        /// HTTP 307
        /// </summary>
        /// <param name="Client">Socket</param>
        /// <param name="NewURL">URL to redirect to</param>
        /// <returns>true, if successfully sent</returns>
        public static bool Redirect(Socket Client, string NewURL)
        {
            if (NewURL is null)
            {
                throw new ArgumentNullException(nameof(NewURL));
            }

            var Body=$"<p>This domain is aliased to <a href=\"{HtmlEncode(NewURL)}\">{HtmlEncode(NewURL)}</a></p>";

            Body = FormatBody(307, "Temporary Redirect", Body);
            var Headers = new List<string>(GetHeaders(Body))
            {
                $"Location: {NewURL}"
            };
            return SendHTTP(Client, 307, "Temporary Redirect", Headers, Body);
        }

        /// <summary>
        /// HTTP 502
        /// </summary>
        /// <param name="Client">Socket</param>
        /// <param name="Body">Message</param>
        /// <returns>true, if successfully sent</returns>
        public static bool GatewayTimeout(Socket Client, string Body)
        {
            if (string.IsNullOrEmpty(Body))
            {
                Body = "<p>The onion service is not responding in time. Please try again later</p>";
            }
            Body = FormatBody(504, "Gateway Timeout", Body);
            return SendHTTP(Client, 504, "Gateway Timeout", GetHeaders(Body), Body);
        }

        /// <summary>
        /// HTTP 503
        /// </summary>
        /// <param name="Client">Socket</param>
        /// <param name="Body">Message</param>
        /// <returns>true, if successfully sent</returns>
        public static bool ServiceUnavailable(Socket Client, string Body)
        {
            if (string.IsNullOrEmpty(Body))
            {
                Body = "<p>This service is temporarily unavailable. Try again later.</p>";
            }
            Body = FormatBody(503, "Service Unavailable", Body);
            return SendHTTP(Client, 503, "Service Unavailable", GetHeaders(Body), Body);
        }

        /// <summary>
        /// HTTP 410
        /// </summary>
        /// <param name="Client">Socket</param>
        /// <param name="Body">Message</param>
        /// <returns>true, if successfully sent</returns>
        public static bool Gone(Socket Client, string Body)
        {
            if (string.IsNullOrEmpty(Body))
            {
                Body = "<p>This resource is not available and will not be in the future.</p>";
            }
            Body = FormatBody(410, "Gone", Body);
            return SendHTTP(Client, 410, "Gone", GetHeaders(Body), Body);
        }

        /// <summary>
        /// HTTP 403
        /// </summary>
        /// <param name="Client">Socket</param>
        /// <param name="Body">Message</param>
        /// <returns>true, if successfully sent</returns>
        public static bool Forbidden(Socket Client, string Body)
        {
            if (string.IsNullOrEmpty(Body))
            {
                Body = "<p>Access to this resource is denied</p>";
            }
            Body = FormatBody(403, "Forbidden", Body);
            return SendHTTP(Client, 403, "Forbidden", GetHeaders(Body), Body);
        }

        /// <summary>
        /// HTTP 400
        /// </summary>
        /// <param name="Client">Socket</param>
        /// <param name="Body">Message</param>
        /// <returns>true, if successfully sent</returns>
        public static bool BadRequest(Socket Client, string Body)
        {
            if (string.IsNullOrEmpty(Body))
            {
                Body = "<p>Your application sent a malformed request</p>";
            }
            Body = FormatBody(400, "Bad Request", Body);
            return SendHTTP(Client, 400, "Bad Request", GetHeaders(Body), Body);
        }

        /// <summary>
        /// HTTP 451
        /// </summary>
        /// <param name="Client">Socket</param>
        /// <param name="Body">Message</param>
        /// <param name="URL">URL that holds block document with block reason</param>
        /// <returns>true, if successfully sent</returns>
        public static bool UFLR(Socket Client, string Body, string URL = null)
        {
            if (string.IsNullOrEmpty(Body))
            {
                Body = "<p>Access to this resource has been restricted for legal reasons</p>";
                if (!string.IsNullOrEmpty(URL))
                {
                    Body += $"More details can be found at: <a href=\"{URL}\">{URL}</a>";
                }
            }
            Body = FormatBody(451, "Unavailable For Legal Reasns", Body);

            var Headers = new List<string>(GetHeaders(Body));
            if (!string.IsNullOrEmpty(URL))
            {
                Headers.Add($"Link: <{URL}>; rel=\"blocked-by\"");
            }
            return SendHTTP(Client, 451, "Unavailable For Legal Reasns", Headers, Body);
        }

        /// <summary>
        /// Encodes the bare minimum HTML
        /// </summary>
        /// <param name="S">String</param>
        /// <returns>Escaped string</returns>
        public static string HtmlEncode(string S)
        {
            return S
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;");
        }

        /// <summary>
        /// Adds HTML framework around the supplied body value
        /// </summary>
        /// <param name="Code">HTTP code</param>
        /// <param name="CodeType">HTTP code description</param>
        /// <param name="Body">Body data</param>
        /// <returns></returns>
        private static string FormatBody(int Code, string CodeType, string Body)
        {
            return "<!DOCTYPE html>" +
"<html lang=\"en\">" +
"<head>" +
"<meta http-equiv=\"X-UA-Compatible\" content=\"IE=edge\" />" +
"<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\" />" +
$"<title>HTTP {Code}: {HtmlEncode(CodeType)}</title>" +
"</head>" +
"<body>" +
$"<h1>HTTP {Code}: {HtmlEncode(CodeType)}</h1>" +
Body +
"<p><i>This message was generated by <a href=\"https://github.com/AyrA/Http2Socks\">Http2Socks</a> and not the onion service you tried to access.</i></p>" +
"</body>" +
"</html>";
        }

        /// <summary>
        /// Gets headers for the given body content
        /// </summary>
        /// <param name="Body">Body text</param>
        /// <returns>Headers</returns>
        /// <remarks>Assumes that the body is plain UTF-8 text</remarks>
        private static string[] GetHeaders(string Body)
        {
            var L = string.IsNullOrEmpty(Body) ? 0 : Encoding.UTF8.GetByteCount(Body);
            return new string[]
            {
                //Type and encoding
                "Content-Type: text/html; charset=utf-8",
                //Content length in bytes
                $"Content-Length: {L}",
                //Disable caching of error messages
                "Cache-Control: no-store, max-age=0",
                //Tell client we're about to close the connection
                "Connection: close"
            };
        }

        /// <summary>
        /// Sends an HTTP answer and closes the socket
        /// </summary>
        /// <param name="Client">Socket</param>
        /// <param name="Code">HTTP status code</param>
        /// <param name="CodeDesc">HTTP status description</param>
        /// <param name="Headers">HTTP response headers</param>
        /// <param name="Body">Body content</param>
        /// <returns>true, if sucessfully sent</returns>
        public static bool SendHTTP(Socket Client, int Code, string CodeDesc, IEnumerable<string> Headers, string Body)
        {
            if (Client is null)
            {
                throw new ArgumentNullException(nameof(Client));
            }
            if (Code < 100 || Code > 999)
            {
                throw new ArgumentOutOfRangeException(nameof(Code));
            }
            using (Client)
            {
                var Lines = new StringBuilder();
                Lines.AppendLine($"HTTP/1.1 {Code} {CodeDesc}");
                if (Headers != null)
                {
                    foreach (var H in Headers)
                    {
                        Lines.AppendLine(H);
                    }
                }
                Lines.AppendLine();
                if (!string.IsNullOrEmpty(Body))
                {
                    Lines.Append(Body);
                }
                var Binary = Encoding.UTF8.GetBytes(Lines.ToString());
                try
                {
                    Client.Send(Binary);
                    Client.Disconnect(false);
                }
                catch
                {
                    return false;
                }
            }
            return true;
        }
    }
}
