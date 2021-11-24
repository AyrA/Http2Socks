using System;

namespace H2S
{
    /// <summary>
    /// Represents an entry in the blacklist file
    /// </summary>
    public class BlacklistEntry : IValidateable
    {
        /// <summary>
        /// Blocked onion domain
        /// </summary>
        public string Domain { get; set; }
        /// <summary>
        /// Name of the onion service (optional)
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// Internal notes (optional)
        /// </summary>
        public string InternalNotes { get; set; }
        /// <summary>
        /// Blacklist reason code
        /// </summary>
        public BlacklistType Type { get; set; }
        /// <summary>
        /// URL with info about why this is blocked (optional)
        /// </summary>
        public string URL { get; set; }

        /// <summary>
        /// Validates all properties of this instance
        /// </summary>
        public void Validate()
        {
            var Temp = Tools.NormalizeOnion(Domain);
            if (string.IsNullOrEmpty(Temp))
            {
                throw new ValidationException("Domain is not a valid onion domain");
            }
            if (!Enum.IsDefined(Type.GetType(), Type))
            {
                throw new ValidationException("Type is not a valid value");
            }
            if (!string.IsNullOrWhiteSpace(URL))
            {
                if (!Uri.TryCreate(URL, UriKind.Absolute, out _))
                {
                    throw new ValidationException("URL is not a valid absolute URL");
                }
            }
            else
            {
                URL = null;
            }
            if (string.IsNullOrWhiteSpace(Name))
            {
                Name = null;
            }
            else if (Name.Contains("\r") || Name.Contains("\n"))
            {
                throw new FormatException("Name cannot contain line breaks");
            }
            if (string.IsNullOrWhiteSpace(InternalNotes))
            {
                InternalNotes = null;
            }
            else if (InternalNotes.Contains("\r") || InternalNotes.Contains("\n"))
            {
                throw new FormatException("InternalNotes cannot contain line breaks");
            }

            Domain = Temp;
        }

        /// <summary>
        /// Save the values to the given configuration file
        /// </summary>
        /// <param name="C">Configuration file</param>
        public void Save(Configuration C)
        {
            Validate();
            C.Empty(Domain);
            if (Name != null)
            {
                C.Set(Domain, "Name", Name);
            }
            if (InternalNotes != null)
            {
                C.Set(Domain, "Notes", InternalNotes);
            }
            C.Set(Domain, "Reason", (int)Type);
            if (URL != null)
            {
                C.Set(Domain, "URL", URL);
            }
        }

        /// <summary>
        /// Reads and fills an instance from the given configuration file
        /// </summary>
        /// <param name="C">Configuration file</param>
        /// <param name="SectionName">Section name to read</param>
        /// <returns>Instance</returns>
        public static BlacklistEntry FromConfig(Configuration C, string SectionName)
        {
            if (Tools.NormalizeOnion(SectionName) == null)
            {
                throw new FormatException($"{SectionName} is not a valid onion domain");
            }
            var BL = new BlacklistEntry()
            {
                Domain = SectionName,
                InternalNotes = C.Get(SectionName, "Notes"),
                Type = C.GetEnum(SectionName, "Reason", BlacklistType.Forbidden),
                Name = C.Get(SectionName, "Name"),
                URL = C.Get(SectionName, "URL"),
            };
            BL.Validate();
            return BL;
        }
    }

    /// <summary>
    /// Block reasons
    /// </summary>
    /// <remarks>
    /// Currently, the codes specify the HTTP response code but this is not guaranteed to always be like this
    /// </remarks>
    public enum BlacklistType : int
    {
        /// <summary>
        /// Generic "Access denied" message
        /// </summary>
        Forbidden = 403,
        /// <summary>
        /// Unavailable For Legal Reasons
        /// </summary>
        UFLR = 451
    }
}
