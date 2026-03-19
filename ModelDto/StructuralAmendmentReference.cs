using System;
using System.Collections.Generic;

#nullable enable

namespace ModelDto
{
    /// <summary>
    /// Reprezentuje pojedynczy cel nowelizacji.
    /// Bez żadnych powiązań z XML/ELI.
    /// </summary>
    public class StructuralAmendmentReference
    {   
        /// <summary>
        /// Strukturalna ścieżka jednostki redakcyjnej będącej celem zmiany:
        /// art. → ust. → pkt → lit. → tiret
        /// </summary>
        public StructuralReference Structure { get; set; } = new();

        /// <summary>
        /// Surowy tekst źródłowy powołania, przydatny dla walidacji
        /// lub celów diagnostycznych parsera.
        /// </summary>
        public string? RawText { get; set; }

        public override string ToString()
        {
            var parts = new List<string>();

            // Dz.U.
            //parts.Add($"Dz.U. {Journal.Year}, poz. {string.Join(",", Journal.Positions)}");

            // Jednostka redakcyjna
            var s = Structure;
            if (s.Article   != null) parts.Add($"art_{s.Article}");
            if (s.Paragraph != null) parts.Add($"ust_{s.Paragraph}");
            if (s.Point     != null) parts.Add($"pkt_{s.Point}");
            if (s.Letter    != null) parts.Add($"lit_{s.Letter}");
            if (s.Tiret     != null) parts.Add($"tiret_{s.Tiret}");

            return string.Join("__", parts);
        }
    }

    /// <summary>
    /// Strukturalna pozycja w hierarchii jednostek redakcyjnych (art. → ust. → pkt → lit. → tiret).
    /// Mutacja dozwolona wyłącznie wewnętrznie i przez serwisy parsujące.
    /// Do nawigacji po hierarchii używaj metod With*, które zwracają nową instancję
    /// z wyzerowanymi poziomami podrzędnymi.
    /// </summary>
    public class StructuralReference
    {
        public string? Article { get; set; }
        public string? Paragraph { get; set; }
        public string? Point { get; set; }
        public string? Letter { get; set; }
        public string? Tiret { get; set; }

        /// <summary>Ustawia artykuł i zeruje wszystkie poziomy podrzędne.</summary>
        public StructuralReference WithArticle(string value) =>
            new() { Article = value };

        /// <summary>Ustawia ustęp i zeruje wszystkie poziomy podrzędne.</summary>
        public StructuralReference WithParagraph(string value) =>
            new() { Article = Article, Paragraph = value };

        /// <summary>Ustawia punkt i zeruje wszystkie poziomy podrzędne.</summary>
        public StructuralReference WithPoint(string value) =>
            new() { Article = Article, Paragraph = Paragraph, Point = value };

        /// <summary>Ustawia literę i zeruje tiret.</summary>
        public StructuralReference WithLetter(string value) =>
            new() { Article = Article, Paragraph = Paragraph, Point = Point, Letter = value };

        /// <summary>Ustawia tiret, zachowując wszystkie poziomy nadrzędne.</summary>
        public StructuralReference WithTiret(string value) =>
            new() { Article = Article, Paragraph = Paragraph, Point = Point, Letter = Letter, Tiret = value };

        public StructuralReference Clone() =>
            new() { Article = Article, Paragraph = Paragraph, Point = Point, Letter = Letter, Tiret = Tiret };
    }
}
