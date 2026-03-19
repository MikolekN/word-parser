using System.Collections.Generic;

namespace ModelDto.SystematizingUnits
{
    /// <summary>
    /// Jednostka systematyzująca: dział
    /// </summary>
    public class Division : BaseEntity, ISystematizingUnit
    {
        /// <summary>
        /// Tytuł działu (np. "Ochrona środowiska", "Bezpieczeństwo i higiena pracy")
        /// </summary>
        public string Heading { get; set; } = string.Empty;

        /// <summary>
        /// Czy dział jest domyślny/ukryty (niejawny w dokumencie).
        /// </summary>
        public bool IsImplicit { get; set; } = true;

        public Division()
        {
            UnitType = UnitType.Division;
            EIdPrefix = "dz";
            DisplayLabel = "dz.";
        }

        public List<Chapter> Chapters { get; set; } = new();
    }
}