using System.Xml.Linq;
using Saga.Model;
using Saga.Model.EditorialUnits;
using Serilog;
using static Saga.Core.XLinqWrappers;

namespace Saga.Core.Services.Converters
{
    /// <summary>
    /// Konwertuje fragment CommonPart encji na XML.
    /// </summary>
    internal static class CommonPartXmlConverter
    {
        internal static void HandleCommonPart(IHasCommonParts entity, XElement element, SentenceContext? context, int? nesting = null)
        {
            if (entity.CommonParts.Any())
            {
                var wrapUp = entity.CommonParts.FirstOrDefault(cp => cp.Type == CommonPartType.WrapUp);
                if (wrapUp != null)
                {
                    var textElement = Element(XmlConverterConstants.Contents.TextSection);
                    if (context != null)
                    {
                        context.CurrentFragment += 1;
                        textElement.Add(SentenceContext.ToXml(context, wrapUp.ContentText));
                    }
                    else
                    {
                        textElement.Add(SentenceContext.ToXml(1, Guid.NewGuid().ToString(), 1, wrapUp.ContentText));
                    }
                    var commonPartElement = Element(GetCommonPartName(entity, nesting));

                    // === Atrybuty ===
                    // commonPartElement.Add(new XAttribute(XmlConverterConstants.Contents.eId, entity.Id));
                    commonPartElement.Add(new XAttribute(XmlConverterConstants.Contents.GUID, wrapUp.Guid.ToString()));

                    // Data wejścia w życie jednostki (opcjonalna)
                    if (wrapUp.EffectiveDate != default)
                        commonPartElement.Add(new XAttribute(XmlConverterConstants.Contents.EntryIntoForce, wrapUp.EffectiveDate.ToString("yyyy-MM-dd")));

                    // Status jednostki
                    // commonPartElement.Add(new XAttribute(XmlConverterConstants.Contents.UnitStatus, wrapUp.Status));

                    // Opinie
                    // commonPartElement.Add(new XAttribute(XmlConverterConstants.Contents.Opinions, wrapUp.Opinions));

                    // Marginesy
                    // commonPartElement.Add(new XAttribute(XmlConverterConstants.Contents.MarginLeft, wrapUp.MarginLeft));
                    // commonPartElement.Add(new XAttribute(XmlConverterConstants.Contents.MarginRight, wrapUp.MarginRight));

                    commonPartElement.Add(textElement);
                    element.Add(commonPartElement);
                }
            }
        }

        private static string GetCommonPartName(IHasCommonParts entity, int? nesting)
        {
            return (entity, nesting) switch
            {
                (Paragraph, null) => XmlConverterConstants.Contents.CommonPart.Point,
                (Point, null) => XmlConverterConstants.Contents.CommonPart.Letter,
                (Letter, null) => XmlConverterConstants.Contents.CommonPart.Tiret,
                (Tiret, 1) => XmlConverterConstants.Contents.CommonPart.DoubleTiret,
                (Tiret, 2) => XmlConverterConstants.Contents.CommonPart.TripleTiret,
                (Tiret, 3) => XmlConverterConstants.Contents.CommonPart.QuadrupleTiret,
                (Tiret, 4) => XmlConverterConstants.Contents.CommonPart.QuintupleTiret,
                _ => throw new InvalidOperationException($"Unsupported entity type {entity.GetType()} with nesting {nesting}")
            };
        }
    }
}
