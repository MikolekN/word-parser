using System.Xml.Linq;
using ModelDto.EditorialUnits;
using static WordParserCore.XLinqWrappers;
using static WordParserCore.Services.Converters.CommonPartXmlConverter;

namespace WordParserCore.Services.Converters
{
    /// <summary>
    /// Konwertuje model Paragraph na element XML.
    /// </summary>
    internal static class ParagraphXmlConverter
    {
        internal static XElement? ToXml(Paragraph paragraph, SentenceContext? previousSentenceContext = null)
        {
            var isImplicit = IsParagraphImplicit(paragraph);
            var isPathological = IsPathological(paragraph);

            // Parsowane dokumenty potrafią zawierać puste ustępy
            if(isImplicit
                && paragraph.ContentText == string.Empty
                && !paragraph.Points.Any()
                && (paragraph.Amendment == null || paragraph.Amendment.Content == null))
                return null;

            var paragraphElement = GetParagraphElement(isImplicit, isPathological);

            // === Atrybuty ===

            if (!isImplicit)
                paragraphElement.Add(new XAttribute(XmlConverterConstants.Contents.eId, paragraph.Id));

            paragraphElement.Add(new XAttribute(XmlConverterConstants.Contents.GUID, paragraph.Guid.ToString()));

            // Data wejścia w życie jednostki (opcjonalna)
            if (paragraph.EffectiveDate != default)
                paragraphElement.Add(new XAttribute(XmlConverterConstants.Contents.EntryIntoForce, paragraph.EffectiveDate.ToString("yyyy-MM-dd")));

            // Status jednostki
            // paragraphElement.Add(new XAttribute(XmlConverterConstants.Contents.UnitStatus, paragraph.Status));

            // Opinie
            // paragraphElement.Add(new XAttribute(XmlConverterConstants.Contents.Opinions, paragraph.Opinions));

            // Marginesy
            // paragraphElement.Add(new XAttribute(XmlConverterConstants.Contents.MarginLeft, paragraph.MarginLeft));
            // paragraphElement.Add(new XAttribute(XmlConverterConstants.Contents.MarginRight, paragraph.MarginRight));

            // === Elementy potomne ===

            // Formatowanie (opcjonalne)
            // paragraphElement.Add(Element(XmlConverterConstants.Contents.Formatting));

            // Numer ustępu (tylko dla ustępów jawnych)
            if (!isImplicit)
                paragraphElement.Add(Element(XmlConverterConstants.Contents.Num, paragraph.Number == null ? "UNKNOWN" : paragraph.Number.Value));

            var textSectionElement = Element(XmlConverterConstants.Contents.TextSection);
            SentenceContext? contextToPass = SentenceProcessor.Process(paragraph.TextSegments, previousSentenceContext, textSectionElement);
            var amendmentElements = AmendmentXmlConverter.ToXml(paragraph.Amendment, textSectionElement.LastNode);
            if (!string.IsNullOrEmpty(paragraph.ContentText) || (amendmentElements != null && amendmentElements.Any()))
            {
                textSectionElement.Add(amendmentElements);
                paragraphElement.Add(textSectionElement);
            }

            foreach (var point in paragraph.Points)
                paragraphElement.Add(PointXmlConverter.ToXml(point, isPathological, contextToPass));

            HandleCommonPart(paragraph, paragraphElement, contextToPass);

            return paragraphElement;
        }

        private static XElement GetParagraphElement(bool isImplicit, bool isPathological)
        {
            return (isImplicit, isPathological) switch
            {
                (true, true)   => Element(XmlConverterConstants.Contents.PatoParagraphImplicit),
                (true, false)  => Element(XmlConverterConstants.Contents.ParagraphImplicit),
                (false, true)  => Element(XmlConverterConstants.Contents.PatoParagraph),
                (false, false) => Element(XmlConverterConstants.Contents.Paragraph)
            };
        }

        // Use parent article to check for number of child paragraphs and assigned IsImplicit value as fallback.
        private static bool IsParagraphImplicit(Paragraph paragraph) =>
            paragraph.IsImplicitComputed == null ? paragraph.IsImplicit : paragraph.IsImplicitComputed.Value;

        private static bool IsPathological(Paragraph paragraph)
        {
            // TODO: unhandled if the structure of paragraph is not respected - code doesn't expect that e.g. letter can appear in a paragraph
            if (paragraph.Points.Count == 1) // Every paragraph is expected to have no points or more than 1 point
                return true;
            else
                return false;
        }
    }
}
