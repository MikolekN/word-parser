using System;
using System.Collections.Generic;
using ModelDto;
using System.Text;

#nullable enable

namespace ModelDto.EditorialUnits
{
    /// <summary>
    /// Model litery punktu - zawiera definicje struktury bez logiki parsowania.
    /// Litera moze zawierac:
    /// - CommonParts (intro/wrapUp) na tym samym poziomie co tirety
    /// - Tirety lub tekst bezposrednio
    /// Numer litery przechowywany jest w EntityNumber (dziedziczony z BaseEntity),
    /// gdzie czesc liczbowa jest pusta, a wartosc zawiera symbol litery (np. "a", "b", "aa" itp.).
    /// </summary>
    public class Letter : BaseEntity, IHasCommonParts, IHasTextSegments, IHasAmendments
    {
        /// <summary>
        /// Czesci wspolne na poziomie litery (np. intro/wrapUp wobec listy tiretow).
        /// Wystepuja jako rodzenstwo tiretow, nie jako ich element.
        /// </summary>
        public List<CommonPart> CommonParts { get; set; } = new();

        /// <summary>
        /// Segmenty tekstu (np. zdania) w obrebie litery.
        /// Umozliwiaja kotwiczenie nowelizacji do konkretnego zdania.
        /// </summary>
        public List<TextSegment> TextSegments { get; set; } = new();

        /// <summary>
        /// Tirety wchodzace w sklad litery.
        /// </summary>
        public List<Tiret> Tirets { get; set; } = new();

        public Amendment? Amendment { get; set; }

        public Letter()
        {
            UnitType = UnitType.Letter;
            EIdPrefix = "lit";
            DisplayLabel = "lit";
        }

        public override string ToString()
        {
            const string indent = "        ";
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
