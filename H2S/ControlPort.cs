using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace H2S
{
    /// <summary>
    /// Listens for and accepts control connections
    /// </summary>
    public class ControlPort : IDisposable
    {
        /// <summary>
        /// Current API version
        /// </summary>
        public const int VERSION = 1;

        /// <summary>
        /// Handler for a new connection
        /// </summary>
        /// <param name="sender">Instance</param>
        /// <param name="connection">Connection</param>
        public delegate void ConnectionHandler(object sender, ControlConnection connection);

        /// <summary>
        /// Event for new connections
        /// </summary>
        public event ConnectionHandler Connection = delegate { };

        /// <summary>
        /// Local listener endpoint
        /// </summary>
        public IPEndPoint Listener { get; }
        /// <summary>
        /// Listener socket
        /// </summary>
        private Socket Server;

        /// <summary>
        /// Creates a new instance
        /// </summary>
        /// <param name="EP">Local listener endpoint</param>
        public ControlPort(IPEndPoint EP)
        {
            if (EP is null)
            {
                throw new ArgumentNullException(nameof(EP));
            }

            Listener = EP;
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
                    try
                    {
                        Server.Bind(Listener);
                        Server.Listen(128);
                        Server.BeginAccept(ConIn, null);
                    }
                    catch (Exception ex)
                    {
                        Tools.LogEx("Cannot create control connection listener", ex);
                        Server.Dispose();
                        Server = null;
                        throw;
                    }
                    Tools.Log(nameof(ControlPort), $"Listening on {Listener}");
                }
                else
                {
                    throw new InvalidOperationException("Server already running");
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
                    Tools.Log(nameof(ControlPort), "Listener stopped");
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
        /// Handler for new connections
        /// </summary>
        /// <param name="ar">Async values</param>
        private void ConIn(IAsyncResult ar)
        {
            Socket Client = null;
            lock (this)
            {
                if (Server == null)
                {
                    return;
                }
                try
                {
                    Client = Server.EndAccept(ar);
                }
                catch (Exception ex)
                {
                    Tools.LogEx("Unable to accept client connection", ex);
                }
            }
            if (Client != null)
            {
                Tools.Log(nameof(ControlPort), $"Connection from {Client.RemoteEndPoint}");
                ControlConnection C = new ControlConnection(Client);
                Connection(this, C);
            }
            Server.BeginAccept(ConIn, null);
        }
    }

    /// <summary>
    /// Represents a control port connection
    /// </summary>
    public class ControlConnection
    {
        /// <summary>
        /// Command event arguments
        /// </summary>
        public class CommandEventArgs
        {
            /// <summary>
            /// Response for this command
            /// </summary>
            /// <remarks>Do not add the final "OK" or "ERR"</remarks>
            public string Response { get; set; }
            /// <summary>
            /// Command that the client sent
            /// </summary>
            /// <remarks>This has already been converted to uppercase</remarks>
            public string Command { get; }
            /// <summary>
            /// Arguments sent for the command
            /// </summary>
            public string[] Arguments { get; }
            /// <summary>
            /// true if this command comes from an authenticated connection
            /// </summary>
            public bool IsAuthenticated { get; }
            /// <summary>
            /// Sets whether the response should indicate success or error
            /// </summary>
            public bool IsSuccess { get; set; }

            /// <summary>
            /// Creates a new instance
            /// </summary>
            /// <param name="Line">Unmodified line sent by the client</param>
            /// <param name="IsAuthenticated">true if currently authenticated</param>
            public CommandEventArgs(string Line, bool IsAuthenticated)
            {
                var Segments = Line.Split(' ');
                Command = Segments[0].ToUpper();
                Arguments = Segments.Skip(1).ToArray();
                this.IsAuthenticated = IsAuthenticated;
            }
        }

        /// <summary>
        /// Authentication event arguments
        /// </summary>
        public class AuthEventArgs
        {
            /// <summary>
            /// Sets if authentication is successfully completed or not
            /// </summary>
            public bool Success { get; set; }
            /// <summary>
            /// Gets the authentication data
            /// </summary>
            /// <remarks>This is all the text following the AUTH command as-is</remarks>
            public string AuthData { get; }

            /// <summary>
            /// Creates a new instance
            /// </summary>
            /// <param name="AuthData">Authentication data</param>
            public AuthEventArgs(string AuthData)
            {
                this.AuthData = AuthData;
            }
        }

        /// <summary>
        /// Global lock that prevents flooding with authentication requests
        /// </summary>
        private static readonly object AuthenticationLock = new object();

        /// <summary>
        /// Command handler
        /// </summary>
        /// <param name="sender">Instance</param>
        /// <param name="Args">Command arguments</param>
        public delegate void CommandHandler(object sender, CommandEventArgs Args);
        /// <summary>
        /// Authentication handler
        /// </summary>
        /// <param name="sender">Instance</param>
        /// <param name="Args">Authentication handler</param>
        public delegate void AuthHandler(object sender, AuthEventArgs Args);
        /// <summary>
        /// Connection close handler
        /// </summary>
        /// <param name="sender">Instance</param>
        public delegate void ExitHandler(object sender);

        /// <summary>
        /// Event for new commands
        /// </summary>
        /// <remarks>This is not triggered for commands handled internally</remarks>
        public event CommandHandler Command = delegate { };
        /// <summary>
        /// Event for authentication requests
        /// </summary>
        /// <remarks>Authentication requests are already delayed. Do not delay manually</remarks>
        public event AuthHandler Auth = delegate { };
        /// <summary>
        /// Event for when a connection exits
        /// </summary>
        /// <remarks>Also triggered if the connection ends without the EXIT command</remarks>
        public event ExitHandler Exit = delegate { };

        /// <summary>
        /// Client connection
        /// </summary>
        public Socket Client { get; }

        /// <summary>
        /// Gets if this connection is authenticated
        /// </summary>
        public bool IsAuthenticated { get; private set; }

        /// <summary>
        /// Creates a new instance
        /// </summary>
        /// <param name="Client">Client connection</param>
        public ControlConnection(Socket Client)
        {
            if (Client is null)
            {
                throw new ArgumentNullException(nameof(Client));
            }

            this.Client = Client;
            new Thread(ControlLoop)
            {
                IsBackground = true,
                Name = "Control connection"
            }.Start();
        }

        /// <summary>
        /// Handler for commands
        /// </summary>
        private void ControlLoop()
        {
            using (var NS = new NetworkStream(Client, true))
            {
                using (var SW = new StreamWriter(NS))
                {
                    SW.AutoFlush = true;
                    //Send greeting
                    if(
                        !WL(SW, "Http2Socks <https://github.com/AyrA/Http2Socks>")||
                        !WL(SW, "OK"))
                    {
                        Exit(this);
                        return;
                    }
                    using (var SR = new StreamReader(NS))
                    {
                        while (true)
                        {
                            var ExitLoop = false;
                            var Line = LR(SR);
                            if (Line == null)
                            {
                                break;
                            }
                            var Cmd = new CommandEventArgs(Line, IsAuthenticated);
                            switch (Cmd.Command)
                            {
                                case "VERSION":
                                    Cmd.IsSuccess = true;
                                    Cmd.Response = ControlPort.VERSION.ToString();
                                    break;
                                case "NOOP":
                                    Cmd.IsSuccess = true;
                                    break;
                                case "EXIT":
                                    ExitLoop = true;
                                    Cmd.IsSuccess = true;
                                    break;
                                case "AUTH":
                                    if (!IsAuthenticated)
                                    {
                                        var AuthArgs = new AuthEventArgs(string.Join(" ", Cmd.Arguments));
                                        lock (AuthenticationLock)
                                        {
                                            Thread.Sleep(500);
                                        }
                                        Auth(this, AuthArgs);
                                        IsAuthenticated |= AuthArgs.Success;
                                        if (AuthArgs.Success)
                                        {
                                            Cmd.Response = "User authenticated";
                                            Cmd.IsSuccess = true;
                                        }
                                        else
                                        {
                                            Cmd.Response = "Authentication failed";
                                        }
                                    }
                                    else
                                    {
                                        Cmd.Response = "User already authenticated";
                                    }
                                    break;
                                default:
                                    Command(this, Cmd);
                                    break;
                            }
                            if (Cmd.Response != null)
                            {
                                WL(SW, Cmd.Response);
                            }
                            WL(SW, Cmd.IsSuccess ? "OK" : "ERR");
                            if (ExitLoop)
                            {
                                Tools.Log(nameof(ControlPort), "Client disconnected");
                                Exit(this);
                                return;
                            }
                        }
                    }
                }
            }
            Exit(this);
        }

        /// <summary>
        /// Safe writing to an unsafe data stream
        /// </summary>
        /// <param name="SW">Stream writer instance</param>
        /// <param name="Line">Line to write</param>
        /// <returns>true if written sucessfully, false if the write crashed</returns>
        private static bool WL(StreamWriter SW, string Line)
        {
            try
            {
                SW.WriteLine(Line);
            }
            catch (Exception ex)
            {
                Tools.LogEx($"Control connection unexpectedly gone when trying to write \"{Line}\"", ex);
                return false;
            }
            return true;
        }

        /// <summary>
        /// Safe reading from an unsafe data stream
        /// </summary>
        /// <param name="SR">Stream reader</param>
        /// <returns>Line read from stream. null on failure</returns>
        private static string LR(StreamReader SR)
        {
            try
            {
                return SR.ReadLine();
            }
            catch (Exception ex)
            {
                Tools.LogEx("Control connection unexpectedly gone", ex);
                return null;
            }
        }
    }
}
