using System.Xml.Linq;
using ModelDto.EditorialUnits;
using static WordParserCore.XLinqWrappers;

namespace WordParserCore.Services.Converters
{
    /// <summary>
    /// Konwertuje model Article na element XML.
    /// </summary>
    internal static class ArticleXmlConverter
    {
        internal static XElement ToXml(Article article)
        {
            var articleElement = Element(XmlConverterConstants.Contents.Article);

            // === Atrybuty ===
            articleElement.Add(new XAttribute(XmlConverterConstants.Contents.eId, article.Id));
            articleElement.Add(new XAttribute(XmlConverterConstants.Contents.GUID, article.Guid.ToString()));

            // Data wejścia w życie jednostki (opcjonalna)
            if (article.EffectiveDate != default)
                articleElement.Add(new XAttribute(XmlConverterConstants.Contents.EntryIntoForce, article.EffectiveDate.ToString("yyyy-MM-dd")));

            // Status jednostki
            // articleElement.Add(new XAttribute(XmlConverterConstants.Contents.UnitStatus, article.Status));

            // Opinie
            // articleElement.Add(new XAttribute(XmlConverterConstants.Contents.Opinions, article.Opinions));

            // Marginesy
            // articleElement.Add(new XAttribute(XmlConverterConstants.Contents.MarginLeft, article.MarginLeft));
            // articleElement.Add(new XAttribute(XmlConverterConstants.Contents.MarginRight, article.MarginRight));

            // === Elementy potomne ===

            // Formatowanie (opcjonalne)
            // articleElement.Add(Element(XmlConverterConstants.Contents.Formatting));

            // Numer artykułu
            articleElement.Add(Element(XmlConverterConstants.Contents.Num, article.Number == null ? "UNKNOWN" : article.Number.Value));

            // Tytuł artykułu (opcjonalny)
            // articleElement.Add(Element(XmlConverterConstants.Contents.Title, article.Title));

            foreach (var paragraph in article.Paragraphs)
                articleElement.Add(ParagraphXmlConverter.ToXml(paragraph));

            return articleElement;
        }
    }
}
