using System.Collections.Generic;
using System.Linq;

namespace Saga.Model
{
    /// <summary>
    /// Model informacji o publikatorze (np. Dziennik Ustaw).
    /// </summary>
    public sealed class JournalInfo
    {
        /// <summary>
        /// Nazwa dziennika urzędowego.
        /// </summary>
        public PublisherType? Publisher { get; set; }

        /// <summary>
        /// Rok wydania dziennika.
        /// </summary>
        public int? Year { get; set; }

        /// <summary>
        /// Numery pozycji publikatora.
        /// </summary>
        public List<int> Positions { get; set; } = new();

        /// <summary>
        /// Fragment źródłowy opisu dziennika (np. "Dz.U. z 2020 r. poz. 1234 i 5678").
        /// </summary>
        public string SourceString { get; set; } = string.Empty;

        public IEnumerable<string> GetELIStrings()
        {
            if (Year is null || !Positions.Any()) yield break;
            foreach (var position in Positions)
            {
                yield return $"{PublisherSymbol()}/{Year}/{position}";
            }
        }

        public string PublisherSymbol() =>
            Publisher switch
            {
                PublisherType.DziennikUstaw => "DU",
                PublisherType.MonitorPolski => "MP",
                _ => ""
            };

        public string PublisherShort() =>
            Publisher switch
            {
                PublisherType.DziennikUstaw => "Dz.U.",
                PublisherType.MonitorPolski => "M.P.",
                _ => ""
            };

        public override string ToString()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var position in Positions)
            {
                sb.AppendLine($"{PublisherSymbol()}.{Year}.{position}");
            }
            return sb.ToString();
        }

        public string ToStringLong() => $"Rok: {Year}, Pozycje: {string.Join(", ", Positions)} (Fragment źródłowy: \"{SourceString}\")";
    }
}
