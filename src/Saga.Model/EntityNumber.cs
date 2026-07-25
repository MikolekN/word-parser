using System;
using System.Text.RegularExpressions;

#nullable enable

namespace ModelDto
{
    /// <summary>
    /// Model opisujący numer encji (artykuł, ustęp, punkt, litera, tiret).
    /// Numer może składać się z trzech komponentów:
    /// 1. Część liczbowa (np. "5" w "5a¹")
    /// 2. Część tekstowa (np. "a" w "5a¹")
    /// 3. Indeks górny - alfanumeryczny (np. "1" w "5a¹")
    /// </summary>
    public class EntityNumber
    {
        /// <summary>
        /// Sformatowana wartość numeru (np. "5a" lub "5a¹").
        /// </summary>
        public string Value { get; set; } = string.Empty;

        /// <summary>
        /// Oryginalna wartość przed parsowaniem.
        /// </summary>
        public string? RawValue { get; set; }

        /// <summary>
        /// Część liczbowa numeru (np. 5 w "5a¹").
        /// </summary>
        public int NumericPart { get; set; } = 0;

        /// <summary>
        /// Część tekstowa numeru (np. "a" w "5a¹").
        /// </summary>
        public string LexicalPart { get; set; } = string.Empty;

        /// <summary>
        /// Indeks górny - część alfanumeryczna (np. "1" w "5a¹").
        /// </summary>
        public string Superscript { get; set; } = string.Empty;
    }
}
