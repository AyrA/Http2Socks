using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;

namespace H2S
{
    public static class HttpActions
    {
        public static bool GatewayTimeout(Socket Client, string Body)
        {
            if (string.IsNullOrEmpty(Body))
            {
                Body = "Error 504: Gateway Timeout";
            }
            return SendHTTP(Client, 504, "Gateway Timeout", GetHeaders(Body), Body);
        }

        public static bool ServiceUnavailable(Socket Client, string Body)
        {
            if (string.IsNullOrEmpty(Body))
            {
                Body = "Error 503: Service Unavailable";
            }
            return SendHTTP(Client, 503, "Service Unavailable", GetHeaders(Body), Body);
        }

        public static bool Forbidden(Socket Client, string Body)
        {
            if (string.IsNullOrEmpty(Body))
            {
                Body = "Error 403: Forbidden";
            }
            return SendHTTP(Client, 403, "Forbidden", GetHeaders(Body), Body);
        }

        public static bool BadRequest(Socket Client, string Body)
        {
            if (string.IsNullOrEmpty(Body))
            {
                Body = "Error 400: Bad Request";
            }
            return SendHTTP(Client, 400, "Bad Request", GetHeaders(Body), Body);
        }

        private static string[] GetHeaders(string Body)
        {
            var L = string.IsNullOrEmpty(Body) ? 0 : Encoding.UTF8.GetByteCount(Body);
            return new string[]
            {
                "Content-Type: text/plain; charset=utf-8",
                $"Content-Length: {L}",
                "Connection: close"
            };
        }

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
