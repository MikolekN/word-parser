using System;
using System.Collections.Generic;
using System.Text;
using ModelDto;

#nullable enable

namespace ModelDto.EditorialUnits
{
    /// <summary>
    /// Model punktu ustepu - zawiera definicje struktury bez logiki parsowania.
    /// Punkt moze zawierac:
    /// - CommonParts (intro/wrapUp) na tym samym poziomie co litery
    /// - Litery lub tekst bezposrednio
    /// </summary>
    public class Point : BaseEntity, IHasCommonParts, IHasTextSegments, IHasAmendments
    {
        /// <summary>
        /// Czesci wspolne na poziomie punktu (np. intro/wrapUp wobec listy liter).
        /// Wystepuja jako rodzenstwo liter, nie jako ich element.
        /// </summary>
        public List<CommonPart> CommonParts { get; set; } = new();

        /// <summary>
        /// Segmenty tekstu (np. zdania) w obrebie punktu.
        /// Umozliwiaja kotwiczenie nowelizacji do konkretnego zdania.
        /// </summary>
        public List<TextSegment> TextSegments { get; set; } = new();

        /// <summary>
        /// Litery wchodzace w sklad punktu.
        /// </summary>
        public List<Letter> Letters { get; set; } = new();

        public Amendment? Amendment { get; set; }

        public Point()
        {
            UnitType = UnitType.Point;
            EIdPrefix = "pkt";
            DisplayLabel = "pkt";
        }

        public override string ToString()
        {
            const string indent = "      ";
            var builder = new StringBuilder();
            builder.Append($"{indent}[{Id}] {ContentText.Substring(0, Math.Min(48, ContentText.Length))}");

            foreach (var letter in Letters)
            {
                builder.AppendLine();
                var letterStr = letter.ToString();
                if (letterStr != null)
                {
                    builder.Append(letterStr);
                }
            }

            return builder.ToString();
        }
    }
}
