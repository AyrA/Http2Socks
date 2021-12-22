using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace H2S
{
    /// <summary>
    /// Provides access to application settings
    /// </summary>
    public class Settings : IValidateable
    {
        /// <summary>
        /// DNS specific settings
        /// </summary>
        public class DnsSettings : IValidateable
        {
            /// <summary>
            /// DNS suffix
            /// </summary>
            public string Suffix { get; set; }
            /// <summary>
            /// Blacklist file
            /// </summary>
            public string Blacklist { get; set; }
            /// <summary>
            /// Alias file
            /// </summary>
            public string Alias { get; set; }

            /// <summary>
            /// Validates this instance
            /// </summary>
            public void Validate()
            {
                if (string.IsNullOrWhiteSpace(Suffix))
                {
                    throw new ValidationException("DNS.Suffix not properly configured");
                }
                if (Suffix.StartsWith(".") || Suffix.EndsWith("."))
                {
                    throw new ValidationException("DNS.Suffix contains leading or trailing dots");
                }
                if (Blacklist != null && !Directory.Exists(Path.GetDirectoryName(Blacklist)))
                {
                    throw new ValidationException("DNS.Blacklist points to non-existing directory");
                }
                if (Alias != null && !Directory.Exists(Path.GetDirectoryName(Alias)))
                {
                    throw new ValidationException("DNS.Alias points to non-existing directory");
                }
            }

            /// <summary>
            /// Saves this instance to INI
            /// </summary>
            /// <param name="c">INI</param>
            public void Save(Configuration c)
            {
                if (c is null)
                {
                    throw new ArgumentNullException(nameof(c));
                }
                Validate();
                c.Empty("DNS");
                c.Set("DNS", "Suffix", Suffix);
                if (!string.IsNullOrEmpty(Blacklist))
                {
                    c.Set("DNS", "Blacklist", Blacklist);
                }
                if (!string.IsNullOrEmpty(Alias))
                {
                    c.Set("DNS", "Alias", Alias);
                }
            }
        }

        /// <summary>
        /// HTTP listener specific settings
        /// </summary>
        public class HttpSettings : IValidateable
        {
            /// <summary>
            /// Listening IP
            /// </summary>
            public IPAddress IP { get; set; }
            /// <summary>
            /// Listening port
            /// </summary>
            public int Port { get; set; }

            /// <summary>
            /// Validates this instance
            /// </summary>
            public void Validate()
            {
                if (IP == null)
                {
                    throw new ValidationException("HTTP.IP is not properly configured");
                }
                if (Port <= IPEndPoint.MinPort || Port >= IPEndPoint.MaxPort)
                {
                    throw new ValidationException("HTTP.Port outside of permitted range");
                }
            }

            /// <summary>
            /// Saves this instance to INI
            /// </summary>
            /// <param name="c">INI</param>
            public void Save(Configuration c)
            {
                if (c is null)
                {
                    throw new ArgumentNullException(nameof(c));
                }
                Validate();
                c.Empty("HTTP");
                c.Set("HTTP", "IP", IP);
                c.Set("HTTP", "Port", Port);
            }
        }

        /// <summary>
        /// Security related settings
        /// </summary>
        public class SecuritySettings : IValidateable
        {
            /// <summary>
            /// Default header set for which to reject clients.
            /// Contains headers that usually contain visitor IP/host and/or country information
            /// </summary>
            public const string DEFAULT_REJECTED = "x-forwarded-for,x-forwarded-ip,x-forwarded-host,cf-connecting-ip,cf-ipcountry";

            private string[] nonAnonymous;

            /// <summary>
            /// Gets list of headers that cause a request to be rejected.
            /// </summary>
            public string NonAnonymousHeaders
            {
                get
                {
                    return nonAnonymous == null ? null : string.Join(",", nonAnonymous);
                }
                set
                {
                    if (string.IsNullOrEmpty(value))
                    {
                        nonAnonymous = null;
                    }
                    else
                    {
                        nonAnonymous = value
                            .Split(',')
                            .Select(v => v.Trim().ToLower())
                            .Where(m => !string.IsNullOrWhiteSpace(m))
                            .Distinct()
                            .ToArray();
                    }
                }
            }

            public bool ContainsRejectedHeaders(string[] HeaderList)
            {
                return HeaderList.Any(IsRejected);
            }

            public bool IsRejected(string Header)
            {
                //Do not validate if param is empty or no headers are rejected
                if (string.IsNullOrEmpty(Header) || nonAnonymous == null)
                {
                    return false;
                }
                return nonAnonymous.Contains(Header.Trim().ToLower());
            }

            /// <summary>
            /// Validates this instance
            /// </summary>
            public void Validate()
            {
                //There is currently no validation. The header list can be any string
            }

            /// <summary>
            /// Saves this instance to INI
            /// </summary>
            /// <param name="c">INI</param>
            public void Save(Configuration c)
            {
                if (c is null)
                {
                    throw new ArgumentNullException(nameof(c));
                }
                Validate();
                c.Empty("Security");
                c.Set("Security", "NonAnonymousHeaders", NonAnonymousHeaders);
            }
        }

        /// <summary>
        /// Tor specific settings
        /// </summary>
        public class TorSettings : IValidateable
        {
            /// <summary>
            /// TOR Socks proxy listener
            /// </summary>
            public IPAddress IP { get; set; }
            /// <summary>
            /// TOR Socks proxy port
            /// </summary>
            public int Port { get; set; }
            /// <summary>
            /// TCP Connection timeout
            /// </summary>
            public int Timeout { get; set; }

            /// <summary>
            /// Validates this instance
            /// </summary>
            public void Validate()
            {
                if (IP == null || IP == IPAddress.Any || IP == IPAddress.IPv6Any)
                {
                    throw new ValidationException("TOR.IP is not properly configured");
                }
                if (Port <= IPEndPoint.MinPort || Port >= IPEndPoint.MaxPort)
                {
                    throw new ValidationException("TOR.Port outside of permitted range");
                }
                if (Timeout < 1)
                {
                    throw new ValidationException("TOR.Timeout is too small");
                }
            }

            /// <summary>
            /// Saves this instance to INI
            /// </summary>
            /// <param name="c">INI</param>
            public void Save(Configuration c)
            {
                if (c is null)
                {
                    throw new ArgumentNullException(nameof(c));
                }
                Validate();
                c.Empty("TOR");
                c.Set("TOR", "IP", IP);
                c.Set("TOR", "Port", Port);
                c.Set("TOR", "Timeout", Timeout);
            }
        }

        /// <summary>
        /// Control connection settings
        /// </summary>
        public class ControlSettings : IValidateable
        {
            /// <summary>
            /// Control listener IP
            /// </summary>
            public IPAddress IP { get; set; }
            /// <summary>
            /// Control listener port
            /// </summary>
            public int Port { get; set; }
            /// <summary>
            /// Cookie file location
            /// </summary>
            public string CookieFile { get; set; }
            /// <summary>
            /// Password
            /// </summary>
            public string Password { get; set; }
            /// <summary>
            /// Gets if the listener should be enabled
            /// </summary>
            public bool Enabled
            {
                get
                {
                    return IP != null;
                }
            }

            /// <summary>
            /// Validates this instance
            /// </summary>
            public void Validate()
            {
                if (IP != null)
                {
                    if (Port <= IPEndPoint.MinPort || Port >= IPEndPoint.MaxPort)
                    {
                        throw new ValidationException("Control.Port outside of permitted range");
                    }
                    if (string.IsNullOrWhiteSpace(CookieFile) && string.IsNullOrWhiteSpace(Password))
                    {
                        throw new ValidationException("Control.Cookie and Control.Password cannot both be unset at the same time");
                    }
                    if (!string.IsNullOrWhiteSpace(CookieFile) && !Directory.Exists(Path.GetDirectoryName(CookieFile)))
                    {
                        throw new ValidationException("Control.CookieFile points to non-existing directory");
                    }
                }
                else if (Port != 0 || CookieFile != null || Password != null)
                {
                    throw new ValidationException("Control configuration incomplete");
                }
            }

            /// <summary>
            /// Gets if the current password is hashed
            /// </summary>
            /// <returns>true, if hashed</returns>
            public bool IsHashedPassword()
            {
                return Password != null && Password.IsMatch("^ENC:[^:]+:.+$");
            }

            /// <summary>
            /// Hashes the current password if necessary
            /// </summary>
            /// <returns>true, if hashed</returns>
            /// <remarks>If this returns true, the original config should be overwritten</remarks>
            public bool HashPassword()
            {
                if (Password != null && !IsHashedPassword())
                {
                    var Salt = Tools.GetRandomBytes(18);
                    Password = "ENC:" + Convert.ToBase64String(Salt) + ":" + HashPassword(Salt, Password);
                    return true;

                }
                return false;
            }

            /// <summary>
            /// Checks a password against the stored password
            /// </summary>
            /// <param name="UserPassword">User supplied password</param>
            /// <returns>true, if valid</returns>
            public bool CheckPassword(string UserPassword)
            {
                if (string.IsNullOrEmpty(Password))
                {
                    return false;
                }
                if (!IsHashedPassword())
                {
                    throw new InvalidOperationException("Control password has not been hashed yet. Call Control.HashPassword() first.");
                }

                var M = Password.Match(@"^ENC:([^:]+):(.+)$");
                if (M != null)
                {
                    return HashPassword(Convert.FromBase64String(M[1]), UserPassword) == M[2];
                }
                return false;
            }

            /// <summary>
            /// Writes the cookie file and returns the cookie file contents written to disk
            /// </summary>
            /// <returns>Cookie file contents</returns>
            public string WriteCookieFile()
            {
                if (string.IsNullOrEmpty(CookieFile))
                {
                    throw new InvalidOperationException("Control.CookieFile has not been set");
                }
                var Contents = Convert.ToBase64String(Tools.GetRandomBytes(33));
                File.WriteAllText(CookieFile, Contents);
                return Contents;
            }

            /// <summary>
            /// Hashes a password
            /// </summary>
            /// <param name="Salt">Salt</param>
            /// <param name="Password">Password</param>
            /// <returns>hashed password string</returns>
            /// <remarks>
            /// This just returns the hashed password.
            /// The user has to save the salt too.
            /// </remarks>
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

            /// <summary>
            /// Saves this instance to INI
            /// </summary>
            /// <param name="c">INI</param>
            public void Save(Configuration c)
            {
                if (c is null)
                {
                    throw new ArgumentNullException(nameof(c));
                }
                Validate();
                c.Empty("Control");
                if (IP != null)
                {
                    c.Set("Control", "IP", IP);
                    c.Set("Control", "Port", Port);
                    if (IsHashedPassword())
                    {
                        c.Set("Control", "Password", Password);
                    }
                    if (!string.IsNullOrWhiteSpace(CookieFile))
                    {
                        c.Set("Control", "Cookie", CookieFile);
                    }
                }
            }
        }

        /// <summary>
        /// DNS Settings
        /// </summary>
        public DnsSettings Dns { get; private set; }

        /// <summary>
        /// HTTP Settings
        /// </summary>
        public HttpSettings Http { get; private set; }

        /// <summary>
        /// TOR Settings
        /// </summary>
        public TorSettings Tor { get; private set; }

        /// <summary>
        /// Control Settings
        /// </summary>
        public ControlSettings Control { get; private set; }

        /// <summary>
        /// Security settings
        /// </summary>
        public SecuritySettings Security { get; private set; }

        /// <summary>
        /// Creates an instance with default settings
        /// </summary>
        public Settings()
        {
            Dns = new DnsSettings()
            {
                Suffix = "local",
                Alias = Path.Combine(Tools.AppDirectory, "alias.ini"),
                Blacklist = Path.Combine(Tools.AppDirectory, "blacklist.ini")
            };
            Http = new HttpSettings()
            {
                IP = IPAddress.Loopback,
                Port = 12243
            };
            Tor = new TorSettings()
            {
                IP = IPAddress.Loopback,
                Port = 9050,
                Timeout = 5000
            };
            Control = new ControlSettings()
            {
                IP = IPAddress.Loopback,
                Port = 12244,
                CookieFile = Path.Combine(Tools.AppDirectory, "cookie.txt"),
            };
            Security = new SecuritySettings()
            {
                NonAnonymousHeaders = SecuritySettings.DEFAULT_REJECTED
            };
        }

        /// <summary>
        /// Creates an instance from the given settings
        /// </summary>
        /// <param name="C">INI</param>
        public Settings(Configuration C)
        {
            if (C is null)
            {
                throw new ArgumentNullException(nameof(C));
            }
            Dns = new DnsSettings();
            Http = new HttpSettings();
            Tor = new TorSettings();
            Control = new ControlSettings();
            Security = new SecuritySettings();

            #region DNS
            if (!string.IsNullOrEmpty(C.Get("DNS", "Suffix")))
            {
                Dns.Suffix = C.Get("DNS", "Suffix");
            }
            else
            {
                throw new InvalidDataException("Confifugration misses mandatory DNS.Suffix entry");
            }
            if (!string.IsNullOrEmpty(C.Get("DNS", "Blacklist")))
            {
                Dns.Blacklist = C.Get("DNS", "Blacklist");
            }
            if (!string.IsNullOrEmpty(C.Get("DNS", "Alias")))
            {
                Dns.Alias = C.Get("DNS", "Alias");
            }
            #endregion

            #region HTTP
            if (!string.IsNullOrEmpty(C.Get("HTTP", "IP")))
            {
                Http.IP = IPAddress.Parse(C.Get("HTTP", "IP"));
            }
            else
            {
                Http.IP = IPAddress.Loopback;
            }
            if (!string.IsNullOrEmpty(C.Get("HTTP", "Port")))
            {
                Http.Port = int.Parse(C.Get("HTTP", "Port"));
            }
            else
            {
                Http.Port = 12243;
            }
            #endregion

            #region TOR
            if (!string.IsNullOrEmpty(C.Get("TOR", "IP")))
            {
                Tor.IP = IPAddress.Parse(C.Get("TOR", "IP"));
            }
            else
            {
                Tor.IP = IPAddress.Loopback;
            }
            if (!string.IsNullOrEmpty(C.Get("TOR", "Port")))
            {
                Tor.Port = int.Parse(C.Get("TOR", "Port"));
            }
            else
            {
                Tor.Port = 9050;
            }
            if (!string.IsNullOrEmpty(C.Get("TOR", "Timeout")))
            {
                Tor.Timeout = int.Parse(C.Get("TOR", "Timeout"));
            }
            else
            {
                Tor.Timeout = 5000;
            }
            #endregion

            #region CONTROL
            if (C.List().Contains("Control"))
            {
                if (!string.IsNullOrEmpty(C.Get("Control", "IP")))
                {
                    Control.IP = IPAddress.Parse(C.Get("Control", "IP"));
                }
                if (!string.IsNullOrEmpty(C.Get("Control", "Port")))
                {
                    Control.Port = int.Parse(C.Get("Control", "Port"));
                }
                if (!string.IsNullOrEmpty(C.Get("Control", "Password")))
                {
                    Control.Password = C.Get("Control", "Password");
                }
                if (!string.IsNullOrEmpty(C.Get("Control", "Cookie")))
                {
                    Control.CookieFile = C.Get("Control", "Cookie");
                }
            }

            #endregion

            #region SECURITY
            if (C.List().Contains("Security"))
            {
                Security.NonAnonymousHeaders = C.Get("Security", "NonAnonymousHeaders", SecuritySettings.DEFAULT_REJECTED);
            }
            #endregion

            Validate();
        }

        /// <summary>
        /// Creates an instance from the given INI file
        /// </summary>
        /// <param name="FileName">INI file name</param>
        public Settings(string FileName) : this(new Configuration(FileName))
        {
            if (FileName is null)
            {
                throw new ArgumentNullException(nameof(FileName));
            }
        }

        /// <summary>
        /// Saves all settings to a configuration file
        /// </summary>
        /// <returns>Configuration file</returns>
        public Configuration Save()
        {
            Validate();
            var C = new Configuration();
            Control.Save(C);
            Dns.Save(C);
            Http.Save(C);
            Tor.Save(C);
            Security.Save(C);
            return C;
        }

        /// <summary>
        /// Validates this instance
        /// </summary>
        /// <remarks>This will call .Validate() of all properties</remarks>
        public void Validate()
        {
            Control.Validate();
            Dns.Validate();
            Http.Validate();
            Tor.Validate();
            Security.Validate();
        }
    }
}
