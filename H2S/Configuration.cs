using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace H2S
{
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
                var CheckSection = Line.Match(@"^\s*\[([^\]]+)\]\s*$");
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
            File.WriteAllText(FileName, ToString());
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
            if (Section is null)
            {
                throw new ArgumentNullException(nameof(Section));
            }

            if (Setting is null)
            {
                throw new ArgumentNullException(nameof(Setting));
            }

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
            if (Section is null)
            {
                throw new ArgumentNullException(nameof(Section));
            }

            if (Setting is null)
            {
                throw new ArgumentNullException(nameof(Setting));
            }

            if (Value is null)
            {
                throw new ArgumentNullException(nameof(Value));
            }

            if (!Settings.ContainsKey(Section))
            {
                Settings.Add(Section, new Dictionary<string, string>());
            }
            Settings[Section][Setting] = Value;
        }

        /// <summary>
        /// Sets a setting to the given value.
        /// Creates section and setting if needed
        /// </summary>
        /// <param name="Section">Section name</param>
        /// <param name="Setting">Setting name</param>
        /// <param name="Value">Value</param>
        /// <remarks>
        /// This internally calls .ToString() on the supplied value.
        /// Implement .ToString() in a way that makes your value serializable.
        /// </remarks>
        public void Set(string Section, string Setting, object Value)
        {
            Set(Section, Setting, Value?.ToString());
        }

        /// <summary>
        /// Deletes an entire section
        /// </summary>
        /// <param name="Section">Section name</param>
        /// <returns>true if deleted, false if not found</returns>
        public bool Delete(string Section)
        {
            if (Section is null)
            {
                throw new ArgumentNullException(nameof(Section));
            }

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
            if (Section is null)
            {
                throw new ArgumentNullException(nameof(Section));
            }

            if (Setting is null)
            {
                throw new ArgumentNullException(nameof(Setting));
            }

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
            if (Section is null)
            {
                throw new ArgumentNullException(nameof(Section));
            }

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
            if (Section is null)
            {
                throw new ArgumentNullException(nameof(Section));
            }

            Settings[Section] = new Dictionary<string, string>();
        }

        /// <summary>
        /// Checks for invalid section or settings
        /// </summary>
        /// <returns>true if formally valid</returns>
        public bool ValidateConfig()
        {
            foreach (var Section in Settings)
            {
                if (!IsValidIniString(Section.Key))
                {
                    return false;
                }
                foreach (var Setting in Section.Value)
                {
                    if (!IsValidIniString(Setting.Key) || Setting.Key.Contains("="))
                    {
                        return false;
                    }
                    if (!IsValidIniString(Setting.Value))
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        private static bool IsValidIniString(string s)
        {
            return string.IsNullOrEmpty(s) || s.IsMatch(@"^[^\x0A\x0D]*$");
        }

        public override string ToString()
        {
            using (var MS = new MemoryStream())
            {
                WriteToStream(MS);
                return Encoding.UTF8.GetString(MS.ToArray());
            }
        }

        public void WriteToStream(Stream S)
        {
            ValidateConfig();
            using (var SW = new StreamWriter(S, new UTF8Encoding(false, false), 1024, true))
            {
                SW.WriteLine($";Last modified on {DateTime.UtcNow} UTC");
                foreach (var KV in Settings)
                {
                    SW.WriteLine($"[{KV.Key}]");
                    foreach (var Setting in KV.Value)
                    {
                        SW.WriteLine($"{Setting.Key}={Setting.Value}");
                    }
                }
                SW.Flush();
            }
        }
    }
}
