
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
    /// <summary>
    /// HTTP connection event parameters
    /// </summary>
    public class HttpHeaderEventArgs
    {
        /// <summary>
        /// HTTP Method (GET, POST, etc)
        /// </summary>
        public string Method { get; private set; }
        /// <summary>
        /// Requested path (as-is, not decoded)
        /// </summary>
        public string Path { get; private set; }
        /// <summary>
        /// Protocol (usually HTTP/1.1)
        /// </summary>
        public string Protocol { get; private set; }

        /// <summary>
        /// Remote client
        /// </summary>
        public Socket Client { get; }
        /// <summary>
        /// List of headers
        /// </summary>
        public Dictionary<string, List<string>> Headers { get; }
        /// <summary>
        /// Raw headers
        /// </summary>
        private readonly List<string> RawLines;

        /// <summary>
        /// Creates a new instance
        /// </summary>
        /// <param name="Client">Open socket</param>
        public HttpHeaderEventArgs(Socket Client)
        {
            this.Client = Client;
            Headers = new Dictionary<string, List<string>>();
            RawLines = new List<string>();
        }

        /// <summary>
        /// Combines all headers into a string for forwarding
        /// </summary>
        /// <returns>Header string</returns>
        /// <remarks>
        /// This lacks the final CRLF+CRLF.
        /// This uses the raw headers as sent by the remote client.
        /// </remarks>
        public string CombineHeaders()
        {
            return string.Join("\r\n", RawLines);
        }

        /// <summary>
        /// Set the request line (first line of an HTTP request)
        /// </summary>
        /// <param name="RequestLine">Request line</param>
        /// <remarks>Cannot be done multiple times</remarks>
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

        /// <summary>
        /// Adds a header to the collection
        /// </summary>
        /// <param name="Name">Header name</param>
        /// <param name="Value">Header value</param>
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

        /// <summary>
        /// Replaces the value of the "Host" header
        /// </summary>
        /// <param name="NewHost">New host parameter</param>
        /// <remarks>Host should contain the port number if it's not 80 or 443</remarks>
        public void ReplaceHost(string NewHost)
        {
            for (var i = 0; i < RawLines.Count; i++)
            {
                if (RawLines[i].Trim().ToLower().StartsWith("host:"))
                {
                    RawLines[i] = $"Host: {NewHost}";
                }
            }
            (Headers["host"] = new List<string>()).Add(NewHost);
        }
    }

    /// <summary>
    /// Simple HTTP listener
    /// </summary>
    public class HttpListener : IDisposable
    {
        /// <summary>
        /// Delegate for connection handler
        /// </summary>
        /// <param name="Sender">Listener instance</param>
        /// <param name="Args">Connection arguments</param>
        public delegate void HttpHeaderCompleteHandler(object Sender, HttpHeaderEventArgs Args);

        /// <summary>
        /// Event for new connections
        /// </summary>
        public event HttpHeaderCompleteHandler HttpHeaderComplete = delegate { };

        /// <summary>
        /// Maximum permitted length in bytes of a single header line
        /// </summary>
        private const int MAX_HEADER_SIZE = 1024 * 8;
        /// <summary>
        /// Maximum number of permitted headers
        /// </summary>
        private const int MAX_HEADER_CONT = 50;

        /// <summary>
        /// Local listener address and port
        /// </summary>
        public IPEndPoint Listener { get; }
        /// <summary>
        /// Listener socket
        /// </summary>
        private Socket Server;

        /// <summary>
        /// Initializes a new listener on the given port and all interfaces
        /// </summary>
        /// <param name="Port">Port number</param>
        public HttpListener(int Port) : this(new IPEndPoint(IPAddress.Any, Port))
        {
            //NOOP
        }

        /// <summary>
        /// Initializes a new listener on the given endpoint
        /// </summary>
        /// <param name="Listener">Endpoint</param>
        public HttpListener(IPEndPoint Listener)
        {
            if (Listener is null)
            {
                throw new ArgumentNullException(nameof(Listener));
            }

            this.Listener = Listener;
        }

        /// <summary>
        /// Starts the listener
        /// </summary>
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

        /// <summary>
        /// Stops the listener
        /// </summary>
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

        /// <summary>
        /// Disposes this instance
        /// </summary>
        public void Dispose()
        {
            Stop();
        }

        /// <summary>
        /// Callback for new connections
        /// </summary>
        /// <param name="ar">Callback data</param>
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

        /// <summary>
        /// Reads all HTTP headers
        /// </summary>
        /// <param name="client">Connected client</param>
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

        /// <summary>
        /// Reads an unbuffered UTF-8 line from the socket
        /// </summary>
        /// <param name="client">Connected client</param>
        /// <param name="MaxLength">Maximum line length</param>
        /// <returns>Line, null if lenght exceeded or socket gone</returns>
        /// <remarks>
        /// This will only consume as many bytes as absolutely needed.
        /// This means it will not swallow bytes in the buffer when the socket is later used by other functions.
        /// </remarks>
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
                catch (Exception ex)
                {
                    Tools.LogEx(nameof(HttpListener) + " failed to read all headers from client", ex);
                    return null;
                }
            } while (Bytes.Count < 2 || Bytes[Bytes.Count - 2] != 13 || Bytes[Bytes.Count - 1] != 10);
            //Remove CRLF before converting to string
            Bytes.RemoveRange(Bytes.Count - 2, 2);
            return Encoding.UTF8.GetString(Bytes.ToArray());
        }
    }
}
