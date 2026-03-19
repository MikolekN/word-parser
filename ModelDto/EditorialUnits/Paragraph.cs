using System;
using System.Collections.Generic;
using System.Text;

#nullable enable

namespace ModelDto.EditorialUnits
{
    /// <summary>
    /// Model ustepu artykulu (Paragraph) - definicja struktury bez logiki parsowania.
    /// Paragraph zawiera co najmniej jeden punkt lub tekst bezposrednio.
    /// Czesci wspolne dla listy punktow wystepuja na tym samym poziomie co punkty.
    /// </summary>
    public class Paragraph : BaseEntity, IHasCommonParts, IHasTextSegments, IHasAmendments
    {
        /// <summary>
        /// Oznacza ustep niejawny (tworzony na podstawie reguly, bez jawnego oznaczenia w tekscie).
        /// </summary>
        public bool IsImplicit { get; set; }

        /// <summary>
        /// Czesci wspolne na poziomie ustepu (np. intro/wrapUp wobec listy punktow).
        /// Wystepuja jako rodzenstwo punktow.
        /// </summary>
        public List<CommonPart> CommonParts { get; set; } = new();

        /// <summary>
        /// Segmenty tekstu (np. zdania) w obrebie ustepu.
        /// Umozliwiaja kotwiczenie nowelizacji do konkretnego zdania.
        /// </summary>
        public List<TextSegment> TextSegments { get; set; } = new();

        public List<Point> Points { get; set; } = new();
        public Amendment? Amendment { get; set; }

        /// <summary>
        /// Role moze okreslac specjalne mapowania/nazewnictwo (np. "prg" dla paragrafu w kodeksie).
        /// </summary>
        public string? Role { get; set; }

        public Paragraph()
        {
            UnitType = UnitType.Paragraph;
            EIdPrefix = "ust";
            DisplayLabel = "ust.";
        }

        protected override string GetLocalIdSegment()
        {
            if (IsImplicit)
            {
                return string.Empty;
            }

            return base.GetLocalIdSegment();
        }

        public override string ToString()
        {
            const string indent = "    ";
            var builder = new StringBuilder();
            builder.Append($"{indent}[{Id}] {ContentText.Substring(0, Math.Min(48, ContentText.Length))}");

            foreach (var point in Points)
            {
                builder.AppendLine();
                var pointStr = point.ToString();
                if (pointStr != null)
                {
                    builder.Append(pointStr);
                }
            }

            return builder.ToString();
        }
    }
}
