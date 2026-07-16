using System.Xml.Linq;
using ModelDto.EditorialUnits;
using static WordParserCore.XLinqWrappers;

namespace WordParserCore.Services.Converters
{
    /// <summary>
    /// Konwertuje model Tiret na element XML.
    /// </summary>
    internal static class TiretXmlConverter
    {
        internal static XElement ToXml(Tiret tiret, bool isParentPathological = false, int nesting = 1, SentenceContext? previousSentenceContext = null)
        {
            var isPathological = IsPathological(tiret, nesting, isParentPathological);

            var tiretElement = Element(GetTiretElementName(nesting, isPathological));

            // === Atrybuty ===
            tiretElement.Add(new XAttribute(XmlConverterConstants.Contents.eId, tiret.Id));
            tiretElement.Add(new XAttribute(XmlConverterConstants.Contents.GUID, tiret.Guid.ToString()));

            // Data wejścia w życie jednostki (opcjonalna)
            if (tiret.EffectiveDate != default)
                tiretElement.Add(new XAttribute(XmlConverterConstants.Contents.EntryIntoForce, tiret.EffectiveDate.ToString("yyyy-MM-dd")));

            // Status jednostki
            // tiretElement.Add(new XAttribute(XmlConverterConstants.Contents.UnitStatus, tiret.Status));

            // Opinie
            // tiretElement.Add(new XAttribute(XmlConverterConstants.Contents.Opinions, tiret.Opinions));

            // Marginesy
            // tiretElement.Add(new XAttribute(XmlConverterConstants.Contents.MarginLeft, tiret.MarginLeft));
            // tiretElement.Add(new XAttribute(XmlConverterConstants.Contents.MarginRight, tiret.MarginRight));

            // === Elementy potomne ===
            // Tiret nie posiada numeru (typ bez elementu <nr>).

            // formatowanie (opcjonalne)
            // tiretElement.Add(Element(XmlConverterConstants.Contents.Formatting));

            var textSectionElement = Element(XmlConverterConstants.Contents.TextSection);
            SentenceContext? contextToPass = SentenceProcessor.Process(tiret.TextSegments, previousSentenceContext, textSectionElement);
            var amendmentElements = AmendmentXmlConverter.ToXml(tiret.Amendment, textSectionElement.LastNode);
            if (!string.IsNullOrEmpty(tiret.ContentText) || (amendmentElements != null && amendmentElements.Any()))
            {
                textSectionElement.Add(amendmentElements);
                tiretElement.Add(textSectionElement);
            }

            foreach (var nextTiret in tiret.Tirets)
                tiretElement.Add(TiretXmlConverter.ToXml(nextTiret, isPathological, nesting + 1, contextToPass));

            return tiretElement;
        }

        private static string GetTiretElementName(int nesting, bool isPathological) =>
            nesting switch
            {
                1 => isPathological ? XmlConverterConstants.Contents.PatoTiret : XmlConverterConstants.Contents.Tiret,
                2 => isPathological ? XmlConverterConstants.Contents.PatoDoubleTiret : XmlConverterConstants.Contents.DoubleTiret,
                3 => XmlConverterConstants.Contents.TripleTiret,
                4 => XmlConverterConstants.Contents.QuadrupleTiret,
                5 => XmlConverterConstants.Contents.QuintupleTiret,
                _ => XmlConverterConstants.Contents.Tiret
            };

        private static bool IsPathological(Tiret tiret, int nesting, bool isParentPathological)
        {
            // TODO: unhandled if the structure of letter is not respected - code doesn't expect that e.g. secondary tiret can appear in a letter
            if (isParentPathological || tiret.Tirets.Count == 1) // Every letter is expected to have no tirets or more than 1 tiret
                return true;
            if (nesting == 2 && tiret.Tirets.Any()) // Nesting beyond level 2 is not allowed, so any appearence of level 3 indicates pathology
                return true;
            return false;
        }
    }
}
