using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace H2S
{
    /// <summary>
    /// Generic Functions
    /// </summary>
    public static class Tools
    {
        /// <summary>
        /// The default suggested timeout for connections
        /// </summary>
        /// <remarks>
        /// The system default is 21 seconds because of how TCP was designed half a century ago.
        /// This is horribly outdated.
        /// A server that takes more than 5 seconds to even complete the handshake alone will not be going to send any useful content after.
        /// </remarks>
        public const int DEFAULT_TIMEOUT = 5000;

        public static readonly string AppDirectory;
        public static readonly string ConfigFile;

        static Tools()
        {
            AppDirectory = Path.GetDirectoryName(Path.GetFullPath(Environment.GetCommandLineArgs()[0]));
            ConfigFile = Path.Combine(AppDirectory, "config.ini");
        }

        /// <summary>
        /// Connects the socket to the supplied remote destination for at most "<paramref name="Timeout"/>" milliseconds.
        /// </summary>
        /// <param name="S">Socket reference</param>
        /// <param name="EP">Remote endpoint</param>
        /// <param name="Timeout">Timeout in milliseconds. Must be at least 1</param>
        /// <remarks>
        /// A <see cref="TimeoutException"/> is thrown if the timeout expires.
        /// The socket will not be disposed and due to system limitations,
        /// will continue to connect until 21 seconds expired (for TCP)
        /// </remarks>
        public static void Connect(this Socket S, EndPoint EP, int Timeout)
        {
            if (S is null)
            {
                throw new ArgumentNullException(nameof(S));
            }

            if (EP is null)
            {
                throw new ArgumentNullException(nameof(EP));
            }

            if (Timeout < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(Timeout), "Value must be at least 1");
            }

            if (Task.WaitAny(S.ConnectAsync(EP), Task.Delay(Timeout)) != 0)
            {
                throw new TimeoutException($"{EP} did not complete the connection within {Timeout} ms");
            }
        }

        /// <summary>
        /// Provides regular expression matching similar to JS
        /// </summary>
        /// <param name="S">String</param>
        /// <param name="Pattern">Regular expression</param>
        /// <param name="Options">Additional options</param>
        /// <returns>
        /// Matches. First entry is full match, all other entries are capture groups.
        /// null if not matched.
        /// </returns>
        public static string[] Match(this string S, string Pattern, RegexOptions Options = RegexOptions.None)
        {
            var M = Regex.Match(S, Pattern, Options);
            if (M.Success)
            {
                return M.Groups
                    .OfType<Group>()
                    .Select(m => m.Value)
                    .ToArray();
            }
            return null;
        }

        /// <summary>
        /// Checks if the current string matches the given pattern
        /// </summary>
        /// <param name="S">String</param>
        /// <param name="Pattern">Regular expression</param>
        /// <param name="Options">Additional options</param>
        /// <returns>true, if matching the pattern</returns>
        /// <remarks>
        /// This is the preferred way to check for a match.
        /// It's faster than calling Match and checking for null.
        /// You should use this if you're not interested in the actual content that matches.
        /// </remarks>
        public static bool IsMatch(this string S, string Pattern, RegexOptions Options = RegexOptions.None)
        {
            return Regex.IsMatch(S, Pattern, Options);
        }

        /// <summary>
        /// Copies two sockets into eachother
        /// </summary>
        /// <param name="A">Socket A</param>
        /// <param name="B">Socket B</param>
        /// <remarks>
        /// Copying ends as soon as one socket disconnects
        /// </remarks>
        public static void Cat(Socket A, Socket B)
        {
            using (var StreamA = new NetworkStream(A, true))
            {
                using (var StreamB = new NetworkStream(B, true))
                {
                    Log("Forwarder", $"Begin forwarding: {A.RemoteEndPoint}<-->{B.RemoteEndPoint}");
                    try
                    {
                        Task.WaitAny(StreamA.CopyToAsync(StreamB), StreamB.CopyToAsync(StreamA));
                        Log("Forwarder", $"Completed forwarding: {A.RemoteEndPoint}<-->{B.RemoteEndPoint}");
                    }
                    catch (Exception ex)
                    {
                        LogEx("HTTP<-->SOCKS copy error", ex);
                    }
                }
            }
        }

        /// <summary>
        /// Logs an exception to the console
        /// </summary>
        /// <param name="Message">Description</param>
        /// <param name="ex">Exception</param>
        public static void LogEx(string Message, Exception ex)
        {
            if (ex is null)
            {
                throw new ArgumentNullException(nameof(ex));
            }
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine("== Begin Error report ==");
            Console.Error.WriteLine(Message);
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Error.WriteLine("Type: {0}", ex.GetType());
            Console.Error.WriteLine("Desc: {0}", ex.Message);
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Error.WriteLine("Where: {0}", ex.StackTrace);
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine("== End Error report ==");
            Console.ResetColor();
        }

        /// <summary>
        /// Writes a message to the console
        /// </summary>
        /// <param name="Component">Component that created the message</param>
        /// <param name="Message">Message</param>
        public static void Log(string Component, string Message)
        {
            Console.WriteLine("[{0}]: {1}", Component, Message);
        }

        /// <summary>
        /// Terminates the process immediately and writes the given exception to the event log
        /// </summary>
        /// <param name="Message">Message</param>
        /// <param name="ex">Exception</param>
        /// <remarks>In debug mode will lock up instead of terminate</remarks>
        public static void ExitEx(string Message, Exception ex)
        {
            LogEx(Message, ex);
            Console.ForegroundColor = ConsoleColor.Black;
            Console.BackgroundColor = ConsoleColor.Red;
            Console.Write("=== APPLICATION HALTED BY EXCEPTION ===".PadRight(Console.BufferWidth));
            Console.ResetColor();
#if DEBUG
            Thread.CurrentThread.Join();
#else
            Environment.FailFast(Message, ex);
#endif
        }

        /// <summary>
        /// Gets the default configuration for this application
        /// </summary>
        /// <returns>Configuration with defaults</returns>
        public static Configuration DefaultConfig()
        {
            var C = new Configuration();
            //Tor backend
            C.Set("TOR", "IP", IPAddress.Loopback);
            C.Set("TOR", "Port", 9050);
            C.Set("TOR", "Timeout", DEFAULT_TIMEOUT);
            //HTTP server
            C.Set("HTTP", "IP", IPAddress.Loopback);
            C.Set("HTTP", "Port", 12243);
            //DNS configuration
            C.Set("DNS", "Suffix", "local");
            C.Set("DNS", "Blacklist", Path.Combine(AppDirectory, "blacklist.ini"));

            C.Set("Control", "IP", IPAddress.Loopback);
            C.Set("Control", "Port", 12244);
            C.Set("Control", "Cookie", Path.Combine(AppDirectory, "cookie.txt"));
            return C;
        }

        public static void ValidateConfig(Configuration C)
        {
            if (C is null)
            {
                throw new ArgumentNullException(nameof(C));
            }
            if (!IPAddress.TryParse(C.Get("TOR", "IP"), out _))
            {
                throw new InvalidDataException("TOR.IP value is invalid");
            }
            if (!IsPort(C.Get("TOR", "Port")))
            {
                throw new InvalidDataException("TOR.Port value is invalid");
            }
            if (!int.TryParse(C.Get("TOR", "Timeout"), out int TorTimeout) || TorTimeout < 1)
            {
                throw new InvalidDataException("TOR.Timeout value is invalid");
            }

            if (!IPAddress.TryParse(C.Get("HTTP", "IP"), out _))
            {
                throw new InvalidDataException("HTTP.IP value is invalid");
            }
            if (!IsPort(C.Get("HTTP", "Port")))
            {
                throw new InvalidDataException("HTTP.Port value is invalid");
            }

            if (string.IsNullOrEmpty(C.Get("DNS", "Suffix")))
            {
                throw new InvalidDataException("DNS.Suffix value is missing");
            }
            if (C.List().Contains("Control"))
            {
                if (!IPAddress.TryParse(C.Get("Control", "IP"), out _))
                {
                    throw new InvalidDataException("Control.IP value is invalid");
                }
                if (!IsPort(C.Get("Control", "Port")))
                {
                    throw new InvalidDataException("Control.Port value is invalid");
                }
                if (C.Get("Control", "Password") != null)
                {
                    var PW = C.Get("Control", "Password");
                    if (string.IsNullOrWhiteSpace(PW))
                    {
                        throw new InvalidDataException("Control.Password if present cannot be empty or whitespace only");
                    }
                    if (!IsHashedPassword(PW))
                    {
                        throw new InvalidDataException("Control.Password is not hashed");
                    }
                }
                else if (string.IsNullOrWhiteSpace(C.Get("Control", "Cookie")))
                {
                    throw new InvalidDataException("Control.Cookie must be set if Control.Password is absent");
                }
            }
        }

        public static bool IsHashedPassword(string Data)
        {
            if (Data == null)
            {
                return false;
            }
            return Data.IsMatch("^ENC:[^:]+:.+$");
        }

        public static bool HashControlPassword(Configuration C)
        {
            var Salt = GetSalt(18);
            var PW = C.Get("Control", "Password");
            if (PW == null || IsHashedPassword(PW))
            {
                return false;
            }
            PW = "ENC:" + Convert.ToBase64String(Salt) + ":" + HashPassword(Salt, PW);
            C.Set("Control", "Password", PW);
            return true;
        }

        private static bool IsPort(string S)
        {
            return int.TryParse(S, out int P) && P > IPEndPoint.MinPort && P < IPEndPoint.MaxPort;
        }

        public static byte[] GetSalt(int Count)
        {
            var b = new byte[Count];
            using (var RNG = RandomNumberGenerator.Create())
            {
                RNG.GetBytes(b);
            }
            return b;
        }

        public static bool CheckPassword(string Password, string HashLine)
        {
            if (Password is null)
            {
                throw new ArgumentNullException(nameof(Password));
            }

            if (HashLine is null)
            {
                throw new ArgumentNullException(nameof(HashLine));
            }

            var M = HashLine.Match(@"^ENC:([^:]+):(.+)$");
            if (M != null)
            {
                return HashPassword(Convert.FromBase64String(M[1]), Password) == M[2];
            }
            return false;
        }

        public static string HashPassword(byte[] Salt, string Password)
        {
            if (Salt is null)
            {
                throw new ArgumentNullException(nameof(Salt));
            }

            if (Password is null)
            {
                throw new ArgumentNullException(nameof(Password));
            }

            using (var hasher = new HMACSHA256(Salt))
            {
                return Convert.ToBase64String(hasher.ComputeHash(Encoding.UTF8.GetBytes(Password)));
            }
        }

        public static string NormalizeOnion(string Domain)
        {
            if (string.IsNullOrEmpty(Domain))
            {
                return null;
            }
            var M = Domain.Match(@"^(?:.*\.)?([a-z2-7]{56})(?:\.onion)?$", RegexOptions.IgnoreCase);
            if (M != null)
            {
                return M[1].ToLower() + ".onion";
            }
            return null;
        }

        public static string UrlDecode(string S)
        {
            if (string.IsNullOrEmpty(S))
            {
                return S;
            }
            return Uri.UnescapeDataString(S.Replace("+", " "));
        }

        public static string UrlEncode(string S)
        {
            if (string.IsNullOrEmpty(S))
            {
                return S;
            }
            return Uri.EscapeDataString(S).Replace("%20", "+");
        }

        public static BlacklistEntry[] GetBlacklistEntries(string FileName)
        {
            if (string.IsNullOrWhiteSpace(FileName))
            {
                throw new ArgumentException($"'{nameof(FileName)}' cannot be null or whitespace.", nameof(FileName));
            }

            var C = new Configuration(FileName);
            return C.List().Select(m => BlacklistEntry.FromConfig(C, m)).ToArray();
        }

        public static Configuration SaveBlacklistEntries(IEnumerable<BlacklistEntry> Entries)
        {
            if (Entries is null)
            {
                throw new ArgumentNullException(nameof(Entries));
            }
            var C = new Configuration();
            foreach (var E in Entries)
            {
                E.Save(C);
            }
            return C;
        }
    }
}
