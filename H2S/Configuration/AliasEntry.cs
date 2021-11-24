using System;

namespace H2S
{
    /// <summary>
    /// Represents a .onion alias
    /// </summary>
    public class AliasEntry : IValidateable
    {
        /// <summary>
        /// Alias of onion
        /// </summary>
        public string Alias { get; set; }
        /// <summary>
        /// Aliased onion
        /// </summary>
        public string Onion { get; set; }
        /// <summary>
        /// Alias type
        /// </summary>
        public AliasType Type { get; set; }

        /// <summary>
        /// Validates all properties of this instance
        /// </summary>
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(Alias))
            {
                throw new ValidationException("AliasEntry.Alias is empty");
            }

            if (!Tools.IsAlias(Alias))
            {
                throw new ValidationException("AliasEntry.Alias not a valid domain label");
            }
            if (!Tools.IsV3Onion(Onion))
            {
                throw new ValidationException("AliasEntry.Onion is not a valid V3 onion");
            }
            if (!Enum.IsDefined(Type.GetType(), Type))
            {
                throw new ValidationException("AliasEntry.Type is not a valid selection of AliasType");
            }
            //All validation passed. Normalize values now
            Onion = Tools.NormalizeOnion(Onion);
            Alias = Tools.ParseAlias(Alias);
        }

        /// <summary>
        /// Save the values to the given configuration file
        /// </summary>
        /// <param name="C">Configuration file</param>
        public void Save(Configuration C)
        {
            Validate();
            Onion = Tools.NormalizeOnion(Onion);
            C.Empty(Alias);
            C.Set(Alias, "Onion", Onion);
            C.Set(Alias, "Type", Type);
        }

        /// <summary>
        /// Reads and fills an instance from the given configuration file
        /// </summary>
        /// <param name="C">Configuration file</param>
        /// <param name="SectionName">Section name to read (also the alias)</param>
        /// <returns>Instance</returns>
        public static AliasEntry FromConfig(Configuration C, string SectionName)
        {
            if (string.IsNullOrWhiteSpace(SectionName))
            {
                throw new ArgumentException($"'{nameof(SectionName)}' cannot be null or whitespace.", nameof(SectionName));
            }

            var AE = new AliasEntry()
            {
                Onion = C.Get(SectionName, "Onion"),
                Alias = SectionName,
                Type = C.GetEnum(SectionName, "Type", AliasType.Rewrite),
            };
            AE.Validate();
            return AE;
        }
    }

    /// <summary>
    /// Supported types of aliases
    /// </summary>
    public enum AliasType : int
    {
        /// <summary>
        /// Rewrite host name.
        /// This leaves the address bar unchanged
        /// </summary>
        Rewrite = 0,
        /// <summary>
        /// Redirect to proper host.
        /// This changes the address bar contents
        /// </summary>
        Redirect = 1
    }
}
