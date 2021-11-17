using System;
using System.Net;
using System.Net.Sockets;
using System.ServiceProcess;
using System.Text;
using System.Text.RegularExpressions;

namespace H2S
{
    public partial class Http2Socks : ServiceBase
    {
        /// <summary>
        /// HTTP listener
        /// </summary>
        private HttpListener Server;

        /// <summary>
        /// Holds the configuration.
        /// Loaded once <see cref="OnStart(string[])"/> is called.
        /// </summary>
        private Configuration C;

        /// <summary>
        /// NOOP
        /// </summary>
        public Http2Socks()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Starts the HTTP listener
        /// </summary>
        /// <param name="args">Arguments (ignored)</param>
        protected override void OnStart(string[] args)
        {
            lock (this)
            {
                if (Server != null)
                {
                    throw new InvalidOperationException("Service start function called multiple times");
                }
                C = new Configuration(Tools.ConfigFile);
                Tools.ValidateConfig(C);
                Server = new HttpListener(new IPEndPoint(IPAddress.Parse(C.Get("HTTP", "IP")), int.Parse(C.Get("HTTP", "Port"))));
                Server.HttpHeaderComplete += Server_HttpHeaderComplete;
                Server.Start();
            }
        }

        /// <summary>
        /// Stops the HTTP listener
        /// </summary>
        protected override void OnStop()
        {
            lock (this)
            {
                if (Server != null)
                {
                    Server.Stop();
                    Server.Dispose();
                    Server = null;
                }
            }
        }

        /// <summary>
        /// Pauses the HTTP listener
        /// </summary>
        protected override void OnPause()
        {
            lock (this)
            {
                if (Server != null)
                {
                    Server.Stop();
                }
                else
                {
                    throw new InvalidOperationException("Cannot pause because service is not started");
                }
            }
        }

        /// <summary>
        /// Resumes the HTTP listener
        /// </summary>
        protected override void OnContinue()
        {
            lock (this)
            {
                if (Server != null)
                {
                    Server.Start();
                }
                else
                {
                    throw new InvalidOperationException("Cannot continue because service is not paused");
                }
            }
        }

        /// <summary>
        /// Callback for new connections
        /// </summary>
        /// <param name="Sender">HTTP listener</param>
        /// <param name="Args">Connection arguments</param>
        private void Server_HttpHeaderComplete(object Sender, HttpHeaderEventArgs Args)
        {
            var Addr = Args.Client.RemoteEndPoint as IPEndPoint;
            var HostHeader = Args.Headers["host"];
            if (HostHeader == null || HostHeader.Count != 1)
            {
                Tools.Log(nameof(Http2Socks), $"Rejected host (missing header)");
                HttpActions.BadRequest(Args.Client, "Request should contain exactly one \"Host\" header");
                Args.Client.Dispose();
                return;
            }
            var Suffix = Regex.Escape(C.Get("DNS", "Suffix"));
            var M = HostHeader[0].Match(@"^(\w+\.onion)\." + Suffix + @"(:\d+)?$", RegexOptions.IgnoreCase);
            if (M == null)
            {
                Tools.Log(nameof(Http2Socks), $"Rejected host (disallowed or format error): {HostHeader[0]}");
                HttpActions.Forbidden(Args.Client, $"User not allowed to connect to {HostHeader[0]}");
                Args.Client.Dispose();
                return;
            }
            var Host = M[1];
            var Port = 80;
            if (M.Length > 2 && M[2].Length > 1)
            {
                if (!int.TryParse(M[2].Substring(1), out Port))
                {
                    Tools.Log(nameof(Http2Socks), $"Rejected host (invalid port): {HostHeader[0]}");
                    HttpActions.BadRequest(Args.Client, "Invalid \"Host\" header format");
                    Args.Client.Dispose();
                    return;
                }
            }
            Tools.Log(nameof(Http2Socks), $"Accepted host: {Host}");
            if (Port == 80 || Port == 443)
            {
                Args.ReplaceHost(Host);
            }
            else
            {
                Args.ReplaceHost($"{Host}:{Port}");
            }
            Socket RemoteConnection;
            try
            {
                var IPE = new IPEndPoint(IPAddress.Parse(C.Get("TOR", "IP")), int.Parse(C.Get("TOR", "Port")));
                RemoteConnection = SocksClient.Open(IPE, Addr.Address.ToString(), Host, Port, int.Parse(C.Get("TOR", "Timeout")));
                RemoteConnection.Send(Encoding.UTF8.GetBytes(Args.CombineHeaders() + "\r\n\r\n"));
            }
            catch (Exception ex)
            {
                HttpActions.ServiceUnavailable(Args.Client, $"Cannot connect to the destination. Details: {ex.Message}");
                Args.Client.Dispose();
                Tools.LogEx("SOCKS failed", ex);
                return;
            }
            try
            {
                Tools.Cat(Args.Client, RemoteConnection);
            }
            catch (Exception ex)
            {
                Tools.LogEx("CAT failed", ex);
            }
        }
    }
}
