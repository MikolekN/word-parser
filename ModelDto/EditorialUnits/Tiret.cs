using System;
using System.Collections.Generic;
using ModelDto;
using System.Text;

#nullable enable

namespace ModelDto.EditorialUnits
{
    /// <summary>
    /// Model tiret - zawiera definicje struktury bez logiki parsowania.
    /// Tiret to wyliczeniowa jednostka w hierarchii.
    /// Czesci wspolne wystepuja jako rodzenstwo tiretow na poziomie litery.
    /// </summary>
    public class Tiret : BaseEntity, IHasTextSegments, IHasAmendments
    {
        /// <summary>
        /// Segmenty tekstu (np. zdania) w obrebie tiretu.
        /// Umozliwiaja kotwiczenie nowelizacji do konkretnego zdania.
        /// </summary>
        public List<TextSegment> TextSegments { get; set; } = new();

        /// <summary>
        /// Tirety wchodzace w skład tiret. Podwójne tirety.
        /// </summary>
        public List<Tiret> Tirets { get; set; } = new();
        public Amendment? Amendment { get; set; }

        public Tiret()
        {
            UnitType = UnitType.Tiret;
            EIdPrefix = "tir";
            DisplayLabel = "tiret";
        }

        public override string ToString()
        {
            const string indent = "          ";
            var builder = new StringBuilder();
            builder.Append($"{indent}[{Id}] {ContentText.Substring(0, Math.Min(48, ContentText.Length))}");

            foreach (var tiret in Tirets)
            {
                builder.AppendLine();
                var tiretStr = tiret.ToString();
                if (tiretStr != null)
                {
                    builder.Append(tiretStr);
                }
            }

            return builder.ToString();
        }
    }
}
