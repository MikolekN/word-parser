using System.Text.RegularExpressions;
using ModelDto;

namespace WordParserCore.Services
{
    /// <summary>
    /// Serwis do parsowania i formatowania numerów encji (artykuł, ustęp, punkt, litera, tiret).
    /// Odpowiada za rozbijanie numeru na komponenty: część liczbowa, tekstowa i indeks górny.
    /// </summary>
    public class EntityNumberService
    {
        private static readonly Regex NumericPrefix = new(@"^(\d+)(.*)$", RegexOptions.Compiled);

        // Indeks górny na końcu numeru — dwa kanały zapisu:
        //   "5a^1"  — separator '^' (zapis historyczny),
        //   "5a[1]" — nawiasy kwadratowe (kanał GetFullText; notacja § 89 ust. 6 ZTP)
        private static readonly Regex SuperscriptSuffix = new(
            @"^(?<base>.*?)(?:\^(?<sup>\w+)|\[(?<sup>\w+)\])\s*\.?\s*$", RegexOptions.Compiled);

        /// <summary>
        /// Parsuje ciąg znaków na EntityNumberDto, wyodrębniając komponenty numeru.
        /// </summary>
        /// <param name="rawValue">Oryginalna wartość numeru (np. "5a[1]" lub "5a^1")</param>
        /// <returns>EntityNumberDto z wypełnionymi polami</returns>
        public EntityNumber Parse(string? rawValue)
        {
            var dto = new EntityNumber { RawValue = rawValue };

            if (string.IsNullOrWhiteSpace(rawValue))
            {
                dto.Value = string.Empty;
                return dto;
            }

            var v = rawValue.Trim();

            // Wyodrębnij indeks górny (oba kanały: '^' oraz '[x]')
            var supMatch = SuperscriptSuffix.Match(v);
            if (supMatch.Success)
            {
                dto.Superscript = supMatch.Groups["sup"].Value.Trim();
                v = supMatch.Groups["base"].Value.Trim();
            }

            // Usuń kropkę na końcu i zbędne białe znaki
            v = v.TrimEnd('.').Trim();

            // Wyodrębnij część liczbową na początkuu
            var match = NumericPrefix.Match(v);
            if (match.Success)
            {
                if (int.TryParse(match.Groups[1].Value, out var num))
                {
                    dto.NumericPart = num;
                }
                dto.LexicalPart = match.Groups[2].Value.Trim();
                dto.Value = dto.NumericPart > 0
                    ? (string.IsNullOrEmpty(dto.LexicalPart)
                        ? dto.NumericPart.ToString()
                        : dto.NumericPart + dto.LexicalPart)
                    : v;
            }
            else
            {
                // Brak części liczbowej na początku
                dto.NumericPart = 0;
                dto.LexicalPart = v;
                dto.Value = v;
            }

            return dto;
        }

        /// <summary>
        /// Konwertuje EntityNumberDto z powrotem do sformatowanego ciągu znaków.
        /// </summary>
        /// <param name="dto">EntityNumberDto do konwersji</param>
        /// <returns>Sformatowany numer (np. "5a[1]")</returns>
        public string FormatToString(EntityNumber dto)
        {
            var sb = new System.Text.StringBuilder();

            if (dto.NumericPart > 0)
            {
                sb.Append(dto.NumericPart);
            }

            if (!string.IsNullOrEmpty(dto.LexicalPart))
            {
                sb.Append(dto.LexicalPart);
            }

            if (!string.IsNullOrEmpty(dto.Superscript))
            {
                // Notacja § 89 ust. 6 ZTP: fragment w indeksie górnym w nawiasach kwadratowych
                sb.Append($"[{dto.Superscript}]");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Tworzy EntityNumberDto z poszczególnych komponentów.
        /// </summary>
        public EntityNumber Create(int? numericPart = null, string? lexicalPart = null, string? superscript = null)
        {
            var dto = new EntityNumber
            {
                NumericPart = numericPart ?? 0,
                LexicalPart = lexicalPart ?? string.Empty,
                Superscript = superscript ?? string.Empty
            };

            // Zbuduj wartość na podstawie komponentów
            var sb = new System.Text.StringBuilder();
            if (dto.NumericPart > 0) sb.Append(dto.NumericPart);
            if (!string.IsNullOrEmpty(dto.LexicalPart)) sb.Append(dto.LexicalPart);
            dto.Value = sb.ToString();

            return dto;
        }
    }
}
