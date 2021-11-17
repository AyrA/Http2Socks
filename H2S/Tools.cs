using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
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

            Console.Error.WriteLine("== Begin Error report ==");
            Console.Error.WriteLine(Message);
            Console.Error.WriteLine("Type: {0}", ex.GetType());
            Console.Error.WriteLine("Desc: {0}", ex.Message);
            Console.Error.WriteLine("== End Error report ==");
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
    }

    /// <summary>
    /// Very simple INI configuration parser and writer
    /// </summary>
    /// <remarks>This will not preserve empty lines or comments when writing</remarks>
    public class Configuration
    {
        /// <summary>
        /// Gets the config file name
        /// </summary>
        public string FileName { get; set; }

        /// <summary>
        /// Holds sections with settings
        /// </summary>
        private Dictionary<string, Dictionary<string, string>> Settings;

        /// <summary>
        /// Loads configuration from the given file
        /// </summary>
        /// <param name="FileName"></param>
        public Configuration(string FileName)
        {
            if (FileName is null)
            {
                throw new ArgumentNullException(nameof(FileName));
            }
            this.FileName = FileName;

            Reload();
        }

        /// <summary>
        /// Creates an empty INI container
        /// </summary>
        public Configuration()
        {
            Settings = new Dictionary<string, Dictionary<string, string>>();
        }

        /// <summary>
        /// Reloads settings from the file.
        /// Discards any changes in memory
        /// </summary>
        public void Reload()
        {
            if (string.IsNullOrEmpty(FileName))
            {
                throw new InvalidOperationException(nameof(FileName) + " has not been set yet.");
            }
            Settings = new Dictionary<string, Dictionary<string, string>>();
            Dictionary<string, string> Section = null;
            foreach (var Line in File.ReadAllLines(FileName))
            {
                //Skip empty lines and comments
                if (string.IsNullOrWhiteSpace(Line) || Line.Trim().StartsWith(";"))
                {
                    continue;
                }
                var CheckSection = Line.Match(@"^\s*(\[[^\]]+\])\s*$");
                if (CheckSection != null)
                {
                    Section = new Dictionary<string, string>();
                    Settings[CheckSection[1]] = Section;
                }
                else
                {
                    var SettingMatch = Line.Match("^([^=]+)=(.*)$");
                    if (SettingMatch != null)
                    {
                        var K = SettingMatch[1];
                        var V = SettingMatch[2];
                        if (Section == null)
                        {
                            throw new InvalidDataException($"Found setting before first section: {Line}");
                        }
                        if (Section.ContainsKey(K))
                        {
                            throw new InvalidDataException($"Duplicate setting name: {Line}");
                        }
                        Section[K] = V;
                    }
                    else
                    {
                        throw new InvalidDataException($"Line is neither setting nor section nor comment: {Line}");
                    }
                }
            }
        }

        /// <summary>
        /// Writes current settings to file
        /// </summary>
        public void Write()
        {
            if (string.IsNullOrEmpty(FileName))
            {
                throw new InvalidOperationException(nameof(FileName) + " has not been set yet.");
            }
            var Lines = new List<string>();
            Lines.Add($";Last modified on {DateTime.UtcNow} UTC");
            foreach (var KV in Settings)
            {
                Lines.Add($"[{KV.Key}]");
                foreach (var Setting in KV.Value)
                {
                    Lines.Add($"{Setting.Key}={Setting.Value}");
                }
            }
            File.WriteAllLines(FileName, Lines);
        }

        /// <summary>
        /// Gets the specified value from the INI settings
        /// </summary>
        /// <param name="Section">Section name</param>
        /// <param name="Setting">Setting name</param>
        /// <param name="Default">Default value</param>
        /// <returns>Value, or default if setting or section is missing</returns>
        public string Get(string Section, string Setting, string Default = null)
        {
            if (Settings.ContainsKey(Section))
            {
                var S = Settings[Section];
                if (S.ContainsKey(Setting))
                {
                    return S[Setting];
                }
            }
            return Default;
        }

        /// <summary>
        /// Sets a setting to the given value.
        /// Creates section and setting if needed
        /// </summary>
        /// <param name="Section">Section name</param>
        /// <param name="Setting">Setting name</param>
        /// <param name="Value">Value</param>
        public void Set(string Section, string Setting, string Value)
        {
            if (!Settings.ContainsKey(Section))
            {
                Settings.Add(Section, new Dictionary<string, string>());
            }
            Settings[Section][Setting] = Value;
        }

        /// <summary>
        /// Deletes an entire section
        /// </summary>
        /// <param name="Section">Section name</param>
        /// <returns>true if deleted, false if not found</returns>
        public bool Delete(string Section)
        {
            return Settings.Remove(Section);
        }

        /// <summary>
        /// Deletes a setting
        /// </summary>
        /// <param name="Section">Section name</param>
        /// <param name="Setting">Setting name</param>
        /// <returns>true if deleted, false if not found</returns>
        public bool Delete(string Section, string Setting)
        {
            if (Settings.ContainsKey(Section))
            {
                return Settings[Section].Remove(Setting);
            }
            return false;
        }

        /// <summary>
        /// Lists all section names
        /// </summary>
        /// <returns>Section names</returns>
        public string[] List()
        {
            return Settings.Select(m => m.Key).ToArray();
        }

        /// <summary>
        /// Lists all settings of a section
        /// </summary>
        /// <param name="Section">Section name</param>
        /// <returns>Setting names</returns>
        public string[] List(string Section)
        {
            if (Settings.ContainsKey(Section))
            {
                return Settings[Section].Select(m => m.Key).ToArray();
            }
            return null;
        }

        /// <summary>
        /// Empties the given section.
        /// Creates an empty section if it doesn't exists
        /// </summary>
        /// <param name="Section">Section name</param>
        public void Empty(string Section)
        {
            Settings[Section] = new Dictionary<string, string>();
        }
    }
}
