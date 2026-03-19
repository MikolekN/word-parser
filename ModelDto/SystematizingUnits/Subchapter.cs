using System.Collections.Generic;
using ModelDto.EditorialUnits;

namespace ModelDto.SystematizingUnits
{
    /// <summary>
    /// Jednostka systematyzująca: oddział (najniższa, zawiera artykuły)
    /// </summary>
    public class Subchapter : BaseEntity, ISystematizingUnit
    {
        /// <summary>
        /// Tytuł oddziału (np. "Zakres stosowania", "Definicje")
        /// </summary>
        public string Heading { get; set; } = string.Empty;

        /// <summary>
        /// Czy oddział jest domyślny/ukryty (niejawny w dokumencie).
        /// </summary>
        public bool IsImplicit { get; set; } = true;

        public Subchapter()
        {
            UnitType = UnitType.Subchapter;
            EIdPrefix = "oddz";
            DisplayLabel = "oddz.";
        }

        // oddziały zawierają artykuły
        public List<Article> Articles { get; set; } = new();
    }
}