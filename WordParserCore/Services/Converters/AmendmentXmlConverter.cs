using System.Xml.Linq;
using ModelDto;
using ModelDto.EditorialUnits;
using Serilog;
using static WordParserCore.XLinqWrappers;

namespace WordParserCore.Services.Converters
{
    /// <summary>
    /// Konwertuje model Amendment na XML.
    /// </summary>
    internal static class AmendmentXmlConverter
    {
        internal static IEnumerable<XElement>? ToXml(Amendment? amendment, XNode? sentence = null)
        {
            if (amendment is null)
                return null;

            if (amendment.OperationType == AmendmentOperationType.Repeal)
            {
                AddRepealInstruction(amendment, sentence);
                return null;
            }

            if (amendment.Content is null)
                return null;

            var citationElements = new List<XElement>();

            AddPlainTextContent(amendment, sentence);
            AddStructuredContentItems(amendment, sentence);
            AddCommonParts(amendment, sentence);

            return citationElements;
        }

        private static string BuildActUri(Amendment amendment) =>
            XmlConverterConstants.ELIUrl + "eli/" + (amendment.TargetLegalAct.GetELIStrings().FirstOrDefault() ?? string.Empty);

        private static void ApplyAttributes(XElement element, Amendment amendment, bool isStructural)
        {
            XNamespace now = XmlConverterConstants.AmendmentNamespace;

            element.Add(new XAttribute(now + XmlConverterConstants.Contents.Citation.Quotation.ActAttribute, BuildActUri(amendment)));

            element.Add(new XAttribute(XmlConverterConstants.Contents.Citation.Quotation.QuotationAttribute, 3));

            if (amendment.Guid != Guid.Empty)
                element.Add(new XAttribute(XmlConverterConstants.Contents.Citation.Quotation.GUIDAttribute, amendment.Guid.ToString()));
            else
                element.Add(new XAttribute(XmlConverterConstants.Contents.Citation.Quotation.GUIDAttribute, Guid.NewGuid().ToString()));

            // if (amendment.RefNowGuid != Guid.Empty)
                // element.Add(new XAttribute(XmlConverterConstants.QuotationRefGUIDAttribute, "UNKNOWN"));

            var targets = amendment.Targets?.ToList() ?? new List<StructuralAmendmentReference>();
            var start_target = targets.FirstOrDefault()?.ToString() ?? string.Empty;
            var end_target = targets.LastOrDefault()?.ToString() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(start_target))
                element.Add(new XAttribute(XmlConverterConstants.Contents.Citation.Quotation.ElementAttribute, start_target));
            else
                element.Add(new XAttribute(XmlConverterConstants.Contents.Citation.Quotation.ElementAttribute, "UNKNOWN"));

            if (!string.IsNullOrWhiteSpace(end_target) && string.Compare(start_target, end_target) != 0)
                element.Add(new XAttribute(XmlConverterConstants.Contents.Citation.Quotation.ElementDoAttribute, end_target));

            element.Add(new XAttribute(XmlConverterConstants.Contents.Citation.Quotation.FunctionAttribute, GetFunctionType(amendment.OperationType, isStructural)));

            // element.Add(new XAttribute(XmlConverterConstants.Contents.Citation.Quotation.SentenceAttribute, sentence.ReferenceSentenceNumber));

            // string appearance = string.IsNullOrWhiteSpace(amendment.Appearance) ? "wszystkie" : amendment.Appearance;
            // element.Add(new XAttribute(XmlConverterConstants.QuotationAppearanceAttribute, "UNKNOWN"));
        }

        private static string GetFunctionType(AmendmentOperationType operation, bool isStructural) =>
            operation switch
            {
                AmendmentOperationType.Repeal => XmlConverterConstants.Contents.Citation.Quotation.Function.RepealText,
                AmendmentOperationType.Insertion => XmlConverterConstants.Contents.Citation.Quotation.Function.AddAfter,
                AmendmentOperationType.Modification => isStructural
                    ? XmlConverterConstants.Contents.Citation.Quotation.Function.Edit
                    : XmlConverterConstants.Contents.Citation.Quotation.Function.EditText,
                AmendmentOperationType.Error => XmlConverterConstants.Contents.Citation.Quotation.Function.NewText,
                _ => XmlConverterConstants.Contents.Citation.Quotation.Function.NewText
            };

        private static void AddRepealInstruction(Amendment amendment, XNode? sentence)
        {
            if (sentence is not XElement zdanie)
                return;

            XNamespace now = XmlConverterConstants.AmendmentNamespace;

            var uchyl = new XElement(now + XmlConverterConstants.Contents.Instruction.Repeal);
            uchyl.Add(new XAttribute(XmlConverterConstants.Contents.Citation.Quotation.ActAttribute, BuildActUri(amendment)));

            var targets = amendment.Targets ?? new List<StructuralAmendmentReference>();
            var startTarget = targets.FirstOrDefault()?.ToString();
            var endTarget = targets.LastOrDefault()?.ToString();
            uchyl.Add(new XAttribute(XmlConverterConstants.Contents.Citation.Quotation.ElementAttribute,
                string.IsNullOrWhiteSpace(startTarget) ? "UNKNOWN" : startTarget));
            if (!string.IsNullOrWhiteSpace(endTarget) && endTarget != startTarget)
                uchyl.Add(new XAttribute(XmlConverterConstants.Contents.Citation.Quotation.ElementDoAttribute, endTarget));

            uchyl.Add(new XAttribute(XmlConverterConstants.Contents.Citation.Quotation.GUIDAttribute,
                amendment.Guid != Guid.Empty ? amendment.Guid.ToString() : Guid.NewGuid().ToString()));

            zdanie.Add(uchyl);
        }

        private static void AddPlainTextContent(Amendment? amendment, XNode? sentence)
        {
            var plainText = amendment?.Content?.PlainText;
            if (plainText == null) return;
            AddTextCitation(amendment!, sentence, plainText);
        }

        private static void AddCommonParts(Amendment? amendment, XNode? sentence)
        {
            var commonParts = amendment?.Content?.CommonParts;
            if (commonParts == null) return;

            foreach (var commonPart in commonParts)
                AddTextCitation(amendment!, sentence, commonPart.ContentText);
        }

        private static void AddTextCitation(Amendment amendment, XNode? sentence, string text)
        {
            var citation = Element(XmlConverterConstants.Contents.Citation.Text.Element,
                Element(XmlConverterConstants.Contents.Citation.Text.Content, text));
            ApplyAttributes(citation, amendment, isStructural: false);
            if (sentence is XElement zdanie)
                zdanie.Add(citation);
        }

        private static void AddStructuredContentItems(Amendment? amendment, XNode? sentence)
        {
            var content = amendment?.Content;
            if (content == null) return;

            foreach (var group in EnumerateAmendmentContentItems(content).GroupBy(e => e.GetType()))
            {
                var elements = group
                    .Select(BuildContentElement)
                    .Where(element => element is not null)
                    .Select(element => element!)
                    .ToList();

                var citation = Element(XmlConverterConstants.Contents.Citation.StructElement);
                ApplyAttributes(citation, amendment!, isStructural: true);
                citation.Add(elements);
                if (sentence is XElement zdanie)
                    zdanie.Add(citation);
            }
        }

        private static XElement? BuildContentElement(BaseEntity entity) =>
            entity switch
            {
                Article article     => ArticleXmlConverter.ToXml(article),
                Paragraph paragraph => ParagraphXmlConverter.ToXml(paragraph),
                Point point         => PointXmlConverter.ToXml(point),
                Letter letter       => LetterXmlConverter.ToXml(letter),
                Tiret tiret         => TiretXmlConverter.ToXml(tiret),
                _ => throw new InvalidOperationException($"Unsupported entity type: {entity.GetType().Name}")
            };

        private static IEnumerable<BaseEntity> EnumerateAmendmentContentItems(AmendmentContent? content)
        {
            if (content is null) return [];

            return Enumerable.Empty<BaseEntity>()
                .Concat(content.Articles   ?? [])
                .Concat(content.Paragraphs ?? [])
                .Concat(content.Points     ?? [])
                .Concat(content.Letters    ?? [])
                .Concat(content.Tirets     ?? []);
        }
    }
}
