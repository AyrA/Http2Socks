using System;

namespace H2S
{
    public class BlacklistEntry
    {
        public string Domain { get; set; }
        public string Name { get; set; }
        public string InternalNotes { get; set; }
        public BlacklistType Type { get; set; }
        public string URL { get; set; }

        public void Validate()
        {
            var Temp = Tools.NormalizeOnion(Domain);
            if (string.IsNullOrEmpty(Temp))
            {
                throw new FormatException("Domain is not a valid onion domain");
            }
            if (!Enum.IsDefined(Type.GetType(), Type))
            {
                throw new FormatException("Type is not a valid value");
            }
            if (!string.IsNullOrWhiteSpace(URL))
            {
                if (!Uri.TryCreate(URL, UriKind.Absolute, out _))
                {
                    throw new FormatException("URL is not a valid absolute URL");
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
                Type = (BlacklistType)int.Parse(C.Get(SectionName, "Reason")),
                Name = C.Get(SectionName, "Name"),
                URL = C.Get(SectionName, "URL"),
            };
            BL.Validate();
            return BL;
        }
    }

    public enum BlacklistType : int
    {
        Forbidden = 403,
        /// <summary>
        /// Unavailable For Legal Reasons
        /// </summary>
        UFLR = 451
    }
}
