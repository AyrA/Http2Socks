
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace H2S
{
    public class HttpHeaderEventArgs
    {
        public string Method { get; private set; }
        public string Path { get; private set; }
        public string Protocol { get; private set; }

        public Socket Client { get; }
        public Dictionary<string, List<string>> Headers { get; }

        private readonly List<string> RawLines;

        public HttpHeaderEventArgs(Socket Client)
        {
            this.Client = Client;
            Headers = new Dictionary<string, List<string>>();
            RawLines = new List<string>();
        }

        public string CombineHeaders()
        {
            return string.Join("\r\n", RawLines);
        }

        public void SetRequestLine(string RequestLine)
        {
            if (RequestLine is null)
            {
                throw new ArgumentNullException(nameof(RequestLine));
            }
            if (Method != null)
            {
                throw new InvalidOperationException("Request line has already been set");
            }
            var R = new Regex(@"^(\S+)\s+(\S+)\s+(.+)$");
            var M = R.Match(RequestLine);
            if (!M.Success)
            {
                throw new FormatException("Request line must be formatted as <type> <url> <protocol>");
            }
            Method = M.Groups[1].Value;
            Path = M.Groups[2].Value;
            Protocol = M.Groups[3].Value;
            RawLines.Add(RequestLine);
        }

        public void AddHeader(string Name, string Value)
        {
            if (string.IsNullOrEmpty(Name))
            {
                throw new ArgumentException($"'{nameof(Name)}' cannot be null or empty.", nameof(Name));
            }

            if (Value is null)
            {
                throw new ArgumentNullException(nameof(Value));
            }
            Name = Name.ToLower();
            if (!Headers.ContainsKey(Name))
            {
                Headers[Name] = new List<string>();
            }
            Headers[Name].Add(Value);
            RawLines.Add($"{Name}: {Value}");
        }

        public void ReplaceHost(string NewHost)
        {
            for(var i = 0; i < RawLines.Count; i++)
            {
                if (RawLines[i].Trim().ToLower().StartsWith("host:"))
                {
                    RawLines[i] = $"Host: {NewHost}";
                }
            }
            (Headers["host"] = new List<string>()).Add(NewHost);
        }
    }

    public class HttpListener : IDisposable
    {
        public delegate void HttpHeaderCompleteHandler(object Sender, HttpHeaderEventArgs Args);

        public event HttpHeaderCompleteHandler HttpHeaderComplete = delegate { };

        private const int MAX_HEADER_SIZE = 1024 * 8;

        public IPEndPoint Listener { get; }
        private Socket Server;

        public HttpListener(int Port) : this(new IPEndPoint(IPAddress.Any, Port))
        {
            //NOOP
        }

        public HttpListener(IPEndPoint Listener)
        {
            if (Listener is null)
            {
                throw new ArgumentNullException(nameof(Listener));
            }

            this.Listener = Listener;
        }

        public void Start()
        {
            lock (this)
            {
                if (Server == null)
                {
                    Server = new Socket(Listener.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                    Server.Bind(Listener);
                    Server.Listen(128);
                    Server.BeginAccept(ConIn, null);
                    Tools.Log(nameof(HttpListener), $"Listener on {Listener} started");
                }
                else
                {
                    throw new InvalidOperationException("Listener already started");
                }
            }
        }

        public void Stop()
        {
            lock (this)
            {
                if (Server != null)
                {
                    var Temp = Server;
                    Server = null;
                    Temp.Dispose();
                    Tools.Log(nameof(HttpListener), $"Listener on {Listener} stopped");
                }
            }
        }

        public void Dispose()
        {
            Stop();
        }

        private void ConIn(IAsyncResult ar)
        {
            Socket TempServer;
            Socket Client;
            lock (this)
            {
                TempServer = Server;
                if (Server == null)
                {
                    return;
                }
            }
            try
            {
                Client = TempServer.EndAccept(ar);
            }
            catch (Exception)
            {
                //Client probably already gone
                Client = null;
            }

            if (Client != null)
            {
                Tools.Log(nameof(HttpListener), $"Connection from {Client.RemoteEndPoint}");
                BeginReadHTTP(Client);
            }
            TempServer.BeginAccept(ConIn, null);
        }

        private void BeginReadHTTP(Socket client)
        {
            Thread T = new Thread(delegate ()
            {
                var Matcher = new Regex(@"^([^:]+):\s*(.*)$");
                var EventArgs = new HttpHeaderEventArgs(client);
                var EP = client.RemoteEndPoint as IPEndPoint;
                string Line = null;
                do
                {
                    try
                    {
                        Line = ReadUnbufferedLine(client, MAX_HEADER_SIZE);
                    }
                    catch
                    {
                        client.Dispose();
                        return;
                    }
                    if (EventArgs.Method == null)
                    {
                        EventArgs.SetRequestLine(Line);
                    }
                    else if (!string.IsNullOrEmpty(Line))
                    {
                        var M = Matcher.Match(Line);
                        if (M.Success)
                        {
                            EventArgs.AddHeader(M.Groups[1].Value, M.Groups[2].Value);
                        }
                        else
                        {
                            throw new FormatException($"Invalid line in HTTP headers: {Line}");
                        }
                    }
                } while (!string.IsNullOrEmpty(Line));
                if (Line == null)
                {
                    client.Dispose();
                    return;
                }
                HttpHeaderComplete(this, EventArgs);
            });
            T.Name = "HTTP client handler";
            T.IsBackground = true;
            T.Start();
        }

        private string ReadUnbufferedLine(Socket client, int MaxLength)
        {
            byte[] buffer = new byte[1];
            var Bytes = new List<byte>();
            do
            {
                try
                {
                    if (client.Receive(buffer) < 1)
                    {
                        throw new IOException("Unexpected end of socket stream");
                    }
                    Bytes.Add(buffer[0]);
                    if (Bytes.Count > MaxLength)
                    {
                        throw new IOException("HTTP Protocol error. Line too long.");
                    }
                }
                catch
                {
                    return null;
                }
            } while (Bytes.Count < 2 || Bytes[Bytes.Count - 2] != 13 || Bytes[Bytes.Count - 1] != 10);
            //Remove CRLF before converting to string
            Bytes.RemoveRange(Bytes.Count - 2, 2);
            return Encoding.UTF8.GetString(Bytes.ToArray());
        }
    }
}
