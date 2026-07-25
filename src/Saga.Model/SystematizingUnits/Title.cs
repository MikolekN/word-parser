using System.Collections.Generic;

namespace ModelDto.SystematizingUnits
{
    /// <summary>
    /// Jednostka systematyzująca: tytuł
    /// </summary>
    public class Title : BaseEntity, ISystematizingUnit
    {
        /// <summary>
        /// Tytuł sekcji (np. "Podmioty prawa cywilnego", "Spadki")
        /// </summary>
        public string Heading { get; set; } = string.Empty;

        /// <summary>
        /// Czy tytuł jest domyślny/ukryty (niejawny w dokumencie).
        /// </summary>
        public bool IsImplicit { get; set; } = true;

        public Title()
        {
            UnitType = UnitType.Title;
            EIdPrefix = "tyt";
            DisplayLabel = "tyt.";
        }

        public List<Division> Divisions { get; set; } = new();
    }
}