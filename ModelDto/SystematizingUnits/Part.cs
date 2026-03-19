using System.Collections.Generic;

namespace ModelDto.SystematizingUnits
{
    /// <summary>
    /// Jednostka systematyzująca: część
    /// </summary>
    public class Part : BaseEntity, ISystematizingUnit
    {
        /// <summary>
        /// Tytuł części (np. "Przepisy ogólne", "Własność")
        /// </summary>
        public string Heading { get; set; } = string.Empty;

        /// <summary>
        /// Czy część jest domyślna/ukryta (niejawna w dokumencie).
        /// </summary>
        public bool IsImplicit { get; set; } = true;

        public Part()
        {
            UnitType = UnitType.Part;
            EIdPrefix = "cz";
            DisplayLabel = "cz.";
        }

        public List<Book> Books { get; set; } = new();
    }
}