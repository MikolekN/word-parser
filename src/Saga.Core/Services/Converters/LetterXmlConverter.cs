using System.Xml.Linq;
using Saga.Model.EditorialUnits;
using static Saga.Core.XLinqWrappers;
using static Saga.Core.Services.Converters.CommonPartXmlConverter;

namespace Saga.Core.Services.Converters
{
    /// <summary>
    /// Konwertuje model Letter na element XML.
    /// </summary>
    internal static class LetterXmlConverter
    {
        internal static XElement ToXml(Letter letter, bool isParentPathological=false, SentenceContext? previousSentenceContext = null)
        {
            var isPathological = IsPathological(letter, isParentPathological);

            var letterElement = GetLetterElement(isPathological);

            // === Atrybuty ===
            letterElement.Add(new XAttribute(XmlConverterConstants.Contents.eId, letter.Id));
            letterElement.Add(new XAttribute(XmlConverterConstants.Contents.GUID, letter.Guid.ToString()));

            // Data wejścia w życie jednostki (opcjonalna)
            if (letter.EffectiveDate != default)
                letterElement.Add(new XAttribute(XmlConverterConstants.Contents.EntryIntoForce, letter.EffectiveDate.ToString("yyyy-MM-dd")));

            // Status jednostki
            // letterElement.Add(new XAttribute(XmlConverterConstants.Contents.UnitStatus, letter.Status));

            // Opinie
            // letterElement.Add(new XAttribute(XmlConverterConstants.Contents.Opinions, letter.Opinions));

            // Marginesy
            // letterElement.Add(new XAttribute(XmlConverterConstants.Contents.MarginLeft, letter.MarginLeft));
            // letterElement.Add(new XAttribute(XmlConverterConstants.Contents.MarginRight, letter.MarginRight));

            // === Elementy potomne ===

            // Formatowanie (opcjonalne)
            // letterElement.Add(Element(XmlConverterConstants.Contents.Formatting));

            // Numer litery
            letterElement.Add(Element(XmlConverterConstants.Contents.Num, letter.Number == null ? "UNKNOWN" : letter.Number.Value));

            var textSectionElement = Element(XmlConverterConstants.Contents.TextSection);
            SentenceContext? contextToPass = SentenceProcessor.Process(letter.TextSegments, previousSentenceContext, textSectionElement);
            var amendmentElements = AmendmentXmlConverter.ToXml(letter.Amendment, textSectionElement.LastNode);
            if (!string.IsNullOrEmpty(letter.ContentText) || (amendmentElements != null && amendmentElements.Any()))
            {
                textSectionElement.Add(amendmentElements);
                letterElement.Add(textSectionElement);
            }

            foreach (var tiret in letter.Tirets)
                letterElement.Add(TiretXmlConverter.ToXml(tiret, isPathological, 1, contextToPass));

            HandleCommonPart(letter, letterElement, contextToPass);

            return letterElement;
        }

        private static XElement GetLetterElement(bool isPathological) =>
            Element(isPathological
                ? XmlConverterConstants.Contents.PatoLetter
                : XmlConverterConstants.Contents.Letter);

        private static bool IsPathological(Letter letter, bool isParentPathological)
        {
            // TODO: unhandled if the structure of letter is not respected - code doesn't expect that e.g. secondary tiret can appear in a letter
            if (isParentPathological || letter.Tirets.Count == 1) // Every letter is expected to have no tirets or more than 1 tiret
                return true;
            else
                return false;
        }
    }
}
