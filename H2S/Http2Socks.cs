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
        private HttpListener Server;

        public Http2Socks()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            lock (this)
            {
                if (Server != null)
                {
                    throw new InvalidOperationException("Service start function called multiple times");
                }
                Server = new HttpListener(new IPEndPoint(IPAddress.Loopback, 12243));
                Server.HttpHeaderComplete += Server_HttpHeaderComplete;
                Server.Start();
            }
        }

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
            var M = Regex.Match(HostHeader[0], @"^(\w+\.onion)\.local(:\d+)?$", RegexOptions.IgnoreCase);
            if (!M.Success)
            {
                Tools.Log(nameof(Http2Socks), $"Rejected host (disallowed or format error): {HostHeader[0]}");
                HttpActions.Forbidden(Args.Client, $"User not allowed to connect to {HostHeader[0]}");
                Args.Client.Dispose();
                return;
            }
            var Host = M.Groups[1].Value;
            var Port = 80;
            if (M.Groups.Count > 2 && M.Groups[2].Value.Length > 1)
            {
                if (!int.TryParse(M.Groups[2].Value.Substring(1), out Port))
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
                RemoteConnection = SocksClient.Open(new IPEndPoint(IPAddress.Loopback, 9050), Addr.Address.ToString(), Host, Port);
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
    }
}
