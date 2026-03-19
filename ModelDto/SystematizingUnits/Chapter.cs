using System.Collections.Generic;

namespace ModelDto.SystematizingUnits
{
    /// <summary>
    /// Jednostka systematyzująca: rozdział
    /// 
    /// W modelu tworzona jest minimalna pełna hierarchia, dlatego rozdział zawsze istnieje.
    /// Jeśli nie występuje jawnie w tekście, jest oznaczony jako IsImplicit = true.
    /// </summary>
    public class Chapter : BaseEntity, ISystematizingUnit
    {
        /// <summary>
        /// Tytuł rozdziału (np. "Warunki przyjęcia", "Odpowiedzialność przewoźnika")
        /// Dla rozdziału domyślnego (IsImplicit = true) może być pusty.
        /// </summary>
        public string Heading { get; set; } = string.Empty;

        /// <summary>
        /// Czy rozdział jest domyślny/ukryty (niejawny w dokumencie).
        /// </summary>
        public bool IsImplicit { get; set; } = true;

        public Chapter()
        {
            UnitType = UnitType.Chapter;
            EIdPrefix = "rozd";
            DisplayLabel = "rozd.";
        }

        /// <summary>
        /// Oddział (Subchapter) — najniższy poziom organizacji zawierający artykuły.
        /// W modelu zawsze istnieje co najmniej jeden oddział (domyślny, gdy brak jawnego oddziału).
        /// </summary>
        public List<Subchapter> Subchapters { get; set; } = new();
    }
}