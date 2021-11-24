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

        /// <summary>
        /// Directory that holds the main executable
        /// </summary>
        public static readonly string AppDirectory;
        /// <summary>
        /// File path to configuration file
        /// </summary>
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
        /// Gets a number of bytes by using cryptographically random number generator
        /// </summary>
        /// <param name="Count">Number of bytes</param>
        /// <returns>Random bytes</returns>
        public static byte[] GetRandomBytes(int Count)
        {
            var b = new byte[Count];
            using (var RNG = RandomNumberGenerator.Create())
            {
                RNG.GetBytes(b);
            }
            return b;
        }

        /// <summary>
        /// Normalizes a .onion domain so it's only made up of the public key.
        /// It removed the .onion TLD as well as any subdomain
        /// </summary>
        /// <param name="Domain">Domain name</param>
        /// <returns>Normalized name. null if not an onion or otherwise malformed</returns>
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

        /// <summary>
        /// Checks if the given domain is a V2 onion which are no longer supported by Tor.
        /// The .onion is optional, and the domain may contain subdomains
        /// </summary>
        /// <param name="Domain">Domain name</param>
        /// <returns>true, if V2.</returns>
        public static bool IsV2Onion(string Domain)
        {
            if (Domain is null)
            {
                throw new ArgumentNullException(nameof(Domain));
            }
            return Domain.IsMatch(@"^(?:.*\.)?([a-z2-7]{16})(?:\.onion)?$", RegexOptions.IgnoreCase);
        }

        /// <summary>
        /// URL decodes a string
        /// </summary>
        /// <param name="S">String</param>
        /// <returns>Decoded string</returns>
        public static string UrlDecode(string S)
        {
            if (string.IsNullOrEmpty(S))
            {
                return S;
            }
            return Uri.UnescapeDataString(S.Replace('+', ' '));
        }

        /// <summary>
        /// URL encodes a string
        /// </summary>
        /// <param name="S">String</param>
        /// <returns>URL encoded string</returns>
        /// <remarks>Encodes spaces as "+" for readability</remarks>
        public static string UrlEncode(string S)
        {
            if (string.IsNullOrEmpty(S))
            {
                return S;
            }
            return Uri.EscapeDataString(S).Replace("%20", "+");
        }

        /// <summary>
        /// Loads blacklist from file
        /// </summary>
        /// <param name="FileName">INI file</param>
        /// <returns>Blacklist</returns>
        public static BlacklistEntry[] GetBlacklistEntries(string FileName)
        {
            if (string.IsNullOrWhiteSpace(FileName))
            {
                throw new ArgumentException($"'{nameof(FileName)}' cannot be null or whitespace.", nameof(FileName));
            }

            var C = new Configuration(FileName);
            return C.List().Select(m => BlacklistEntry.FromConfig(C, m)).ToArray();
        }

        /// <summary>
        /// Saves blacklist to INI
        /// </summary>
        /// <param name="Entries">Blacklist entries</param>
        /// <returns>INI</returns>
        public static Configuration SaveBlacklistEntries(IEnumerable<BlacklistEntry> Entries)
        {
            if (Entries is null)
            {
                throw new ArgumentNullException(nameof(Entries));
            }
            var C = new Configuration();
            lock (Entries)
            {
                foreach (var E in Entries)
                {
                    E.Save(C);
                }
            }
            return C;
        }

        /// <summary>
        /// Loads aliases from file
        /// </summary>
        /// <param name="FileName">INI file</param>
        /// <returns>Alias list</returns>
        public static AliasEntry[] GetAliasEntries(string FileName)
        {
            if (string.IsNullOrWhiteSpace(FileName))
            {
                throw new ArgumentException($"'{nameof(FileName)}' cannot be null or whitespace.", nameof(FileName));
            }

            var C = new Configuration(FileName);
            var Aliases = C.List().Select(m => AliasEntry.FromConfig(C, m)).ToArray();
            for(var i = 0; i < Aliases.Length; i++)
            {
                if (Aliases.Count(m => m.Alias == Aliases[i].Alias) > 1)
                {
                    throw new Exception($"Duplicate alias found: {Aliases[1].Onion}");
                }
            }
            return Aliases;
        }

        /// <summary>
        /// Saves aliases to INI
        /// </summary>
        /// <param name="Entries">Alias entries</param>
        /// <returns>INI</returns>
        public static Configuration SaveAliasEntries(IEnumerable<AliasEntry> Entries)
        {
            if (Entries is null)
            {
                throw new ArgumentNullException(nameof(Entries));
            }
            var C = new Configuration();
            lock (Entries)
            {
                foreach (var E in Entries)
                {
                    E.Save(C);
                }
            }
            return C;
        }
    }
}
