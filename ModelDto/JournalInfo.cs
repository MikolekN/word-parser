using System.Collections.Generic;

namespace ModelDto
{
    /// <summary>
    /// Model informacji o publikatorze (np. Dziennik Ustaw).
    /// </summary>
    public class JournalInfo
    {
        /// <summary>
        /// Rok wydania dziennika.
        /// </summary>
        public int Year { get; set; }

        /// <summary>
        /// Numery pozycji publikatora.
        /// </summary>
        public List<int> Positions { get; set; } = new();

        /// <summary>
        /// Fragment źródłowy opisu dziennika (np. "Dz.U. z 2020 r. poz. 1234 i 5678").
        /// </summary>
        public string SourceString { get; set; } = string.Empty;

        public override string ToString()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var position in Positions)
            {
                sb.AppendLine($"DU.{Year}.{position}");
            }
            return sb.ToString();
        }

        public string ToStringLong()
        {
            return $"Rok: {Year}, Pozycje: {string.Join(", ", Positions)} (Fragment źródłowy: \"{SourceString}\")";
        }
    }
}