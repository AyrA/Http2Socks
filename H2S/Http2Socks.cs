using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.ServiceProcess;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace H2S
{
    public partial class Http2Socks : ServiceBase
    {
        /// <summary>
        /// HTTP listener
        /// </summary>
        private HttpListener Server;

        /// <summary>
        /// Control connection
        /// </summary>
        private ControlPort Control;

        /// <summary>
        /// If set to true, all HTTP connections is temporary stalled
        /// </summary>
        /// <remarks>
        /// This can be used by the control connection to update lists with multiple commands
        /// without any requests falling through.
        /// </remarks>
        private volatile bool Halt;

        /// <summary>
        /// Holds the password stored in the cookie file
        /// </summary>
        /// <remarks>Set to random value every time the service is started</remarks>
        private string CookiePassword;

        /// <summary>
        /// Holds a list of all blacklisted domains
        /// </summary>
        private List<BlacklistEntry> Blacklist;
        /// <summary>
        /// Holds a list of aliased domain names
        /// </summary>
        private List<AliasEntry> Aliases;

        /// <summary>
        /// Holds the configuration.
        /// Loaded once <see cref="OnStart(string[])"/> is called.
        /// </summary>
        private Settings C;

        /// <summary>
        /// NOOP
        /// </summary>
        public Http2Socks()
        {
            InitializeComponent();
            Blacklist = new List<BlacklistEntry>();
            Aliases = new List<AliasEntry>();
            Halt = false;
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
                    Tools.ExitEx("Service control error", new InvalidOperationException("Service start function called multiple times"));
                }

                C = new Settings(Tools.ConfigFile);
                if (C.Control.HashPassword())
                {
                    Tools.Log(nameof(Http2Socks), "INI password was hashed");
                    try
                    {
                        var Config = C.Save();
                        Config.FileName = Tools.ConfigFile;
                        Config.Write();
                    }
                    catch (Exception ex)
                    {
                        Tools.ExitEx("Unable to store hashed password", ex);
                        throw;
                    }
                }
                try
                {
                    C.Validate();
                }
                catch (Exception ex)
                {
                    Tools.ExitEx("Configuration failed to validate", ex);
                    throw;
                }

                if (C.Control.CookieFile != null)
                {
                    try
                    {
                        CookiePassword = C.Control.WriteCookieFile();
                    }
                    catch (Exception ex)
                    {
                        Tools.ExitEx("Cookie file configured but not writable", ex);
                        throw;
                    }
                }
                else
                {
                    CookiePassword = null;
                }

                LoadBlacklist(C.Dns.Blacklist);
                LoadAliases(C.Dns.Alias);
                Tools.Log(nameof(Http2Socks), $"{Blacklist.Count} domains are blacklisted");
                Tools.Log(nameof(Http2Socks), $"{Aliases.Count} domains are aliased");

                Server = new HttpListener(new IPEndPoint(C.Http.IP, C.Http.Port));
                Server.HttpHeaderComplete += Server_HttpHeaderComplete;
                try
                {
                    Server.Start();
                }
                catch (Exception ex)
                {
                    Tools.ExitEx("Unable to start HTTP listener", ex);
                    throw;
                }
                if (C.Control.Enabled)
                {
                    Control = new ControlPort(new IPEndPoint(C.Control.IP, C.Control.Port));
                    Control.Connection += Control_Connection;
                    try
                    {
                        Control.Start();
                    }
                    catch (Exception ex)
                    {
                        Tools.ExitEx("Unable to start control connection", ex);
                        throw;
                    }
                }
                Halt = false;
            }
        }

        /// <summary>
        /// Loads aliases from the given file
        /// </summary>
        /// <param name="ALFile">Alias list file</param>
        /// <returns>true, if loaded</returns>
        /// <remarks>If <paramref name="ALFile"/> is null or empty, the alias list is cleared</remarks>
        private bool LoadAliases(string ALFile)
        {
            if (string.IsNullOrWhiteSpace(ALFile))
            {
                Aliases.Clear();
                return true;
            }
            try
            {
                Aliases = new List<AliasEntry>(Tools.GetAliasEntries(ALFile));
                return true;
            }
            catch (Exception ex)
            {
                Tools.LogEx("Failed to reload blacklist", ex);
                return false;
            }
        }

        /// <summary>
        /// Loads the blacklist from the given file
        /// </summary>
        /// <param name="BLFile">Blacklist file</param>
        /// <returns>true, if loaded</returns>
        /// <remarks>If <paramref name="BLFile"/> is null or empty, the blacklist is cleared</remarks>
        private bool LoadBlacklist(string BLFile)
        {
            if (string.IsNullOrWhiteSpace(BLFile))
            {
                Blacklist.Clear();
                return true;
            }
            try
            {
                Blacklist = new List<BlacklistEntry>(Tools.GetBlacklistEntries(BLFile));
                return true;
            }
            catch (Exception ex)
            {
                Tools.LogEx("Failed to reload blacklist", ex);
                return false;
            }
        }

        /// <summary>
        /// Event handler for new control connections
        /// </summary>
        /// <param name="sender">Instance</param>
        /// <param name="c">Control connection</param>
        private void Control_Connection(object sender, ControlConnection c)
        {
            c.Auth += C_Auth;
            c.Command += C_Command;
        }

        /// <summary>
        /// Handler for control connection commands
        /// </summary>
        /// <param name="sender">Instance</param>
        /// <param name="Args">Arguments</param>
        private void C_Command(object sender, ControlConnection.CommandEventArgs Args)
        {
            Tools.Log(nameof(Http2Socks), $"Control command: {Args.Command}");
            switch (Args.Command)
            {
                case "INFO":
                    Args.Response = GetInfo(Args.IsAuthenticated);
                    Args.IsSuccess = true;
                    break;
                case "HALT":
                    if (Args.IsAuthenticated)
                    {
                        Args.Response = Halt ? "HTTP processing already stopped" : "HTTP processing halted";
                        if (!Halt)
                        {
                            Tools.Log(nameof(Http2Socks), "HTTP processing halted");
                        }
                        Halt = true;
                        Args.IsSuccess = true;
                    }
                    break;
                case "CONT":
                    if (Args.IsAuthenticated)
                    {
                        Args.Response = Halt ? "HTTP processing continued" : "HTTP processing was not stopped";
                        if (Halt)
                        {
                            Tools.Log(nameof(Http2Socks), "HTTP processing continued");
                        }
                        Halt = false;
                        Args.IsSuccess = true;
                    }
                    break;
                case "ALLIST":
                case "ALRELOAD":
                case "ALADD":
                case "ALREMOVE":
                case "ALSAVE":
                    HandleALCommands(Args);
                    break;
                case "BLLIST":
                case "BLRELOAD":
                case "BLADD":
                case "BLREMOVE":
                case "BLSAVE":
                    HandleBLCommands(Args);
                    break;
            }
        }

        /// <summary>
        /// Constructs a response for the INFO command
        /// </summary>
        /// <param name="isAuth">true, if command made while authenticated</param>
        /// <returns>Command response</returns>
        private string GetInfo(bool isAuth)
        {
            //Converts boolean into true=1 or false=0
            var B = new Func<bool, int>(delegate (bool x) { return x ? 1 : 0; });

            var SB = new StringBuilder();
            SB.AppendLine($"AUTH={B(isAuth)}");
            if (isAuth)
            {
                SB.AppendLine($"HALT={B(Halt)}");
                SB.AppendLine($"BL={Blacklist.Count}");
                SB.AppendLine($"AL={Aliases.Count}");
                SB.AppendLine($"BLFILE={B(!string.IsNullOrWhiteSpace(C.Dns.Blacklist))}");
                SB.AppendLine($"ALFILE={B(!string.IsNullOrWhiteSpace(C.Dns.Alias))}");
            }
            return SB.ToString().Trim();
        }

        /// <summary>
        /// handles alias commands
        /// </summary>
        /// <param name="Args">Arguments</param>
        private void HandleALCommands(ControlConnection.CommandEventArgs Args)
        {
            switch (Args.Command)
            {
                case "ALLIST":
                    if (Args.IsAuthenticated)
                    {
                        lock (Aliases)
                        {
                            Args.Response = Tools.SaveAliasEntries(Aliases).ToString();
                        }
                        Args.IsSuccess = true;
                    }
                    break;
                case "ALRELOAD":
                    if (Args.IsAuthenticated)
                    {
                        if (LoadAliases(C.Dns.Alias))
                        {
                            Args.Response = $"List reloaded. {Aliases.Count} entries";
                            Args.IsSuccess = true;
                        }
                        else
                        {
                            Args.Response = $"Failed to reload the file. It doesn't exists or is locked.";
                        }
                    }
                    break;
                case "ALADD":
                    if (Args.IsAuthenticated)
                    {
                        if (Args.Arguments.Length > 1)
                        {
                            AliasEntry A = null;
                            try
                            {
                                A = new AliasEntry
                                {
                                    Onion = Tools.NormalizeOnion(Args.Arguments[0]),
                                    Alias = Args.Arguments[1],
                                    Type = Args.Arguments.Length > 2 ? (AliasType)int.Parse(Args.Arguments[2]) : AliasType.Rewrite
                                };
                                A.Validate();
                            }
                            catch (Exception ex)
                            {
                                Args.Response = ex.Message;
                                A = null;
                            }
                            if (A != null)
                            {
                                Tools.Log(nameof(Http2Socks), $"Alias added for {A.Onion} --> {A.Alias}");
                                lock (Aliases)
                                {
                                    Aliases.RemoveAll(m => m.Onion == A.Onion || m.Alias == A.Alias);
                                    Aliases.Add(A);
                                }
                                Args.IsSuccess = true;
                            }
                        }
                        else
                        {
                            Args.Response = "This command requires at least two arguments";

                        }
                    }
                    break;
                case "ALREMOVE":
                    if (Args.IsAuthenticated)
                    {
                        if (Args.Arguments.Length >= 1)
                        {
                            var Onion = Tools.NormalizeOnion(Args.Arguments[0]);
                            if (Onion != null)
                            {
                                lock (Aliases)
                                {
                                    Aliases.RemoveAll(m => m.Onion == Onion);
                                }
                                Args.IsSuccess = true;
                            }
                            else
                            {
                                Args.Response = "Invalid onion domain";
                            }
                        }
                        else
                        {
                            Args.Response = "Invalid arguments";
                        }
                    }
                    break;
                case "ALSAVE":
                    if (Args.IsAuthenticated)
                    {
                        if (!string.IsNullOrWhiteSpace(C.Dns.Alias))
                        {
                            var A = Tools.SaveAliasEntries(Aliases);
                            A.FileName = C.Dns.Alias;
                            A.Write();
                            Args.IsSuccess = true;
                        }
                        else
                        {
                            Args.Response = "Alias list not configured";
                        }
                    }
                    break;
            }
        }

        /// <summary>
        /// Handles blacklist commands
        /// </summary>
        /// <param name="Args">Arguments</param>
        private void HandleBLCommands(ControlConnection.CommandEventArgs Args)
        {
            switch (Args.Command)
            {
                case "BLLIST":
                    if (Args.IsAuthenticated)
                    {
                        lock (Blacklist)
                        {
                            Args.Response = Tools.SaveBlacklistEntries(Blacklist).ToString();
                        }
                        Args.IsSuccess = true;
                    }
                    break;
                case "BLRELOAD":
                    if (Args.IsAuthenticated)
                    {
                        if (LoadBlacklist(C.Dns.Blacklist))
                        {
                            Args.Response = $"List reloaded. {Blacklist.Count} entries";
                            Args.IsSuccess = true;
                        }
                        else
                        {
                            Args.Response = $"Failed to reload the file. It doesn't exists or is locked.";
                        }
                    }
                    break;
                case "BLADD":
                    if (Args.IsAuthenticated)
                    {
                        if (Args.Arguments.Length > 0)
                        {
                            var A = Args.Arguments;
                            //Handle the fact that most arguments are optional
                            var Params = new string[5] {
                                A.Length > 0 ? A[0] : null,
                                A.Length > 1 ? A[1] : null,
                                A.Length > 2 ? A[2] : null,
                                A.Length > 3 ? A[3] : ((int)BlacklistType.Forbidden).ToString(),
                                A.Length > 4 ? A[4] : null
                            };
                            BlacklistEntry BL;
                            try
                            {
                                BL = new BlacklistEntry()
                                {
                                    Domain = Params[0],
                                    Name = Tools.UrlDecode(Params[1]),
                                    InternalNotes = Tools.UrlDecode(Params[2]),
                                    Type = (BlacklistType)int.Parse(Params[3]),
                                    URL = Params[4]
                                };
                                BL.Validate();
                            }
                            catch (Exception ex)
                            {
                                Args.Response = ex.Message;
                                BL = null;
                            }
                            if (BL != null)
                            {
                                Tools.Log(nameof(Http2Socks), $"Blacklisted {BL.Domain}");
                                lock (Blacklist)
                                {
                                    Blacklist.RemoveAll(m => m.Domain == BL.Domain);
                                    Blacklist.Add(BL);
                                }
                                Args.IsSuccess = true;
                            }
                        }
                        else
                        {
                            Args.Response = "Invalid arguments";
                        }
                    }
                    break;
                case "BLREMOVE":
                    if (Args.IsAuthenticated)
                    {
                        if (Args.Arguments.Length >= 1)
                        {
                            var Onion = Tools.NormalizeOnion(Args.Arguments[0]);
                            if (Onion != null)
                            {
                                Blacklist.RemoveAll(m => m.Domain == Onion);
                                Args.IsSuccess = true;
                            }
                            else
                            {
                                Args.Response = "Invalid onion domain";
                            }
                        }
                        else
                        {
                            Args.Response = "Invalid arguments";
                        }
                    }
                    break;
                case "BLSAVE":
                    if (Args.IsAuthenticated)
                    {
                        var BL = Tools.SaveBlacklistEntries(Blacklist);
                        BL.FileName = C.Dns.Blacklist;
                        if (BL.FileName != null)
                        {
                            BL.Write();
                            Args.IsSuccess = true;
                        }
                        else
                        {
                            Args.Response = "Blacklist not configured";
                        }
                    }
                    break;
            }
        }

        /// <summary>
        /// Handler for new authentication request
        /// </summary>
        /// <param name="sender">Instance</param>
        /// <param name="Args">Arguments</param>
        private void C_Auth(object sender, ControlConnection.AuthEventArgs Args)
        {
            if (C.Control.IsHashedPassword())
            {
                Args.Success = C.Control.CheckPassword(Args.AuthData);
            }
            if (!Args.Success && CookiePassword != null)
            {
                Args.Success = Args.AuthData == CookiePassword;
            }
            if (!Args.Success)
            {
                Tools.Log(nameof(Http2Socks), "Control connection authentication failure");
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
                if (Control != null)
                {
                    Control.Dispose();
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
            var Suffix = Regex.Escape(C.Dns.Suffix);
            var M = HostHeader[0].Match(@"^(.+)\." + Suffix + @"(:\d+)?$", RegexOptions.IgnoreCase);
            if (M == null)
            {
                Tools.Log(nameof(Http2Socks), $"Rejected host (format error): {HostHeader[0]}");
                HttpActions.BadRequest(Args.Client, $"Invalid 'Host' header format");
                Args.Client.Dispose();
                return;
            }

            while (Halt)
            {
                Thread.Sleep(100);
            }

            string Host = null;
            var AliasName = M[1].Match(@"([^.]+)\.onion$", RegexOptions.IgnoreCase);
            string Domain = AliasName?[1];
            var Alias = Domain == null ? null : Aliases.FirstOrDefault(m => m.Alias == Domain);
            //Handle aliases before trying to convert to onion
            if (Alias != null)
            {
                if (Alias.Type == AliasType.Redirect)
                {
                    //Construct redirect URL
                    var Redir = $"http://{Alias.Onion}.{C.Dns.Suffix}{Args.Path}";
                    Tools.Log(nameof(Http2Socks), $"Alias redirection: {Alias.Alias} --> {Alias.Onion}");
                    HttpActions.Redirect(Args.Client, Redir);
                    Args.Client.Dispose();
                    return;
                }
                Tools.Log(nameof(Http2Socks), $"Alias rewrite: {Alias.Alias} --> {Alias.Onion}");
                Host = Alias.Onion;
            }
            else
            {
                Host = Tools.NormalizeOnion(M[1]);
            }
            if (Host == null)
            {
                if (Tools.IsV2Onion(M[1]))
                {
                    Tools.Log(nameof(Http2Socks), $"Rejected host (outdated V2 onion): {M[1]}");
                    HttpActions.Gone(Args.Client, $"{HttpActions.HtmlEncode(M[1])} is a version 2 onion name which is no longer supported by Tor.");
                }
                else
                {
                    Tools.Log(nameof(Http2Socks), $"Rejected host (onion format error): {HostHeader[0]}");
                    HttpActions.BadRequest(Args.Client, "This service can only be used to access onion websites.");
                }
                Args.Client.Dispose();
                return;
            }
            //"Host" at this point is guaranteed to be a valid, normalized v3 onion domain

            //Handle blacklist
            if (Blacklist.Any(m => m.Domain == Host))
            {
                BlacklistRequest(Args.Client, Blacklist.First(m => m.Domain == Host));
                Args.Client.Dispose();
                return;
            }

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
                var IPE = new IPEndPoint(C.Tor.IP, C.Tor.Port);
                RemoteConnection = SocksClient.Open(IPE, Addr.Address.ToString(), Host, Port, C.Tor.Timeout);
                RemoteConnection.Send(Encoding.UTF8.GetBytes(Args.CombineHeaders() + "\r\n\r\n"));
            }
            catch (Exception ex)
            {
                HttpActions.ServiceUnavailable(Args.Client, $"<p>Cannot connect to the destination. Details: {HttpActions.HtmlEncode(ex.Message)}</p>");
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
                Tools.LogEx("Tools.Cat failed", ex);
            }
        }

        /// <summary>
        /// Constructs and sends the appropriate HTTP error messages to a client
        /// </summary>
        /// <param name="client">HTTP connection</param>
        /// <param name="e">Blacklist entry that matched the request</param>
        private void BlacklistRequest(Socket client, BlacklistEntry e)
        {
            if (client is null)
            {
                throw new ArgumentNullException(nameof(client));
            }

            if (e is null)
            {
                throw new ArgumentNullException(nameof(e));
            }

            Tools.Log(nameof(Http2Socks), $"Rejecting request to {e.Domain}");

            var DisplayName = string.IsNullOrEmpty(e.Name) ? e.Domain : $"\"{e.Name}\" ({e.Domain})";
            var Who = e.Type == BlacklistType.Forbidden ? "The owner of this service" : "A legal entity";
            var SB = new StringBuilder($"<p>{Who} has blocked access to {HttpActions.HtmlEncode(DisplayName)}.</p>");
            if (!string.IsNullOrEmpty(e.URL))
            {
                SB.Append(
                    "<p>Details about this decision can be found at: " +
                    $"<a href=\"{HttpActions.HtmlEncode(e.URL)}\">{HttpActions.HtmlEncode(e.URL)}</a></p>");
            }
            else
            {
                SB.Append("<p>The operator of this Http2Socks instance did not provide a reason for this decision</p>");
            }
            SB.Append("<p><hr /><br />You can always access onion services safely and anonymously with the Tor browser.</p>");
            switch (e.Type)
            {
                case BlacklistType.UFLR:
                    HttpActions.UFLR(client, SB.ToString(), e.URL);
                    break;
                default:
                    HttpActions.Forbidden(client, SB.ToString());
                    break;
            }
        }
    }
}
