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
    public class ControlPort : IDisposable
    {
        public const int VERSION = 1;

        public delegate void ConnectionHandler(object sender, ControlConnection connection);

        public event ConnectionHandler Connection = delegate { };

        public IPEndPoint Listener { get; }
        private Socket Server;

        public ControlPort(IPEndPoint EP)
        {
            if (EP is null)
            {
                throw new ArgumentNullException(nameof(EP));
            }

            Listener = EP;
        }

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

        public void Dispose()
        {
            Stop();
        }

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

    public class ControlConnection
    {
        public class CommandEventArgs
        {
            public string Response { get; set; }
            public string Command { get; }
            public string[] Arguments { get; }
            public bool IsAuthenticated { get; }
            public bool IsSuccess { get; set; }

            public CommandEventArgs(string Line, bool IsAuthenticated)
            {
                var Segments = Line.Split(' ');
                Command = Segments[0].ToUpper();
                Arguments = Segments.Skip(1).ToArray();
                this.IsAuthenticated = IsAuthenticated;
            }
        }

        public class AuthEventArgs
        {
            public bool Success { get; set; }
            public string AuthData { get; }

            public AuthEventArgs(string AuthData)
            {
                this.AuthData = AuthData;
            }
        }

        private static readonly object AuthenticationLock = new object();

        public delegate void CommandHandler(object sender, CommandEventArgs Args);
        public delegate void AuthHandler(object sender, AuthEventArgs Args);
        public delegate void ExitHandler(object sender);

        public event CommandHandler Command = delegate { };
        public event AuthHandler Auth = delegate { };
        public event ExitHandler Exit = delegate { };

        public Socket Client { get; }

        public bool IsAuthenticated { get; private set; }

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

        private void ControlLoop()
        {
            using (var NS = new NetworkStream(Client, true))
            {
                using (var SW = new StreamWriter(NS))
                {
                    SW.AutoFlush = true;
                    //Send greeting
                    WL(SW, "Http2Socks <https://github.com/AyrA/Http2Socks>");
                    WL(SW, "OK");
                    using (var SR = new StreamReader(NS))
                    {
                        while (true)
                        {
                            var ExitLoop = false;
                            var Line = SR.ReadLine();
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

        private static bool WL(StreamWriter SW, string Line)
        {
            try
            {
                SW.WriteLine(Line);
            }
            catch (Exception ex)
            {
                Tools.LogEx("Control connection unexpectedly gone", ex);
                return false;
            }
            return true;
        }
    }
}
