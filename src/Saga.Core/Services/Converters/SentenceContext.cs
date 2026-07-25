using System.Xml.Linq;
using static Saga.Core.XLinqWrappers;

namespace Saga.Core.Services.Converters
{
	/// <summary>
	/// Kontekst parsowania zdania przechowujacy numer i GUID aktualnie przetwarzanego
	/// zdania oraz numer ostatnio napotkanego fragmentu zdania.
	/// </summary>
	internal sealed class SentenceContext
	{
		/// <summary>
        /// Numer ostatnio przetworzonego fragmentu zdania
        /// </summary>
        internal int CurrentFragment { get; set; } = 1;

        /// <summary>
        /// Numer przetwarzanego zdania
        /// </summary>
        internal int ReferenceSentenceNumber { get; init;}

        /// <summary>
        /// GUID przetwarzanego zdania
        /// </summary>
        internal string ReferenceSentenceGUID { get; init; }

        internal SentenceContext(int currentFragment, int referenceSentenceNumber, string referenceSentenceGUID)
		{
			CurrentFragment = currentFragment;
			ReferenceSentenceNumber = referenceSentenceNumber;
			ReferenceSentenceGUID = referenceSentenceGUID;
		}

        internal static XElement ToXml(SentenceContext context, string text) =>
            Element(XmlConverterConstants.Contents.Sentence.Element,
                new XAttribute(XmlConverterConstants.Contents.Sentence.Number, context.ReferenceSentenceNumber),
                new XAttribute(XmlConverterConstants.Contents.Sentence.GUID, context.ReferenceSentenceGUID),
                new XAttribute(XmlConverterConstants.Contents.Sentence.Fragment, context.CurrentFragment),
                new XAttribute(XmlConverterConstants.Contents.Sentence.FragmentGUID, Guid.NewGuid().ToString()),
                // new XAttribute(XmlConverterConstants.Contents.EntryIntoForce, "yyyy-MM-dd"),
                // new XAttribute(XmlConverterConstants.Contents.UnitStatus, "obowiazuje"),
                text);

        internal static XElement ToXml(int number, string guid, int fragment, string text) =>
            Element(XmlConverterConstants.Contents.Sentence.Element,
                new XAttribute(XmlConverterConstants.Contents.Sentence.Number, number),
                new XAttribute(XmlConverterConstants.Contents.Sentence.GUID, guid),
                new XAttribute(XmlConverterConstants.Contents.Sentence.Fragment, fragment),
                new XAttribute(XmlConverterConstants.Contents.Sentence.FragmentGUID, Guid.NewGuid().ToString()),
                // new XAttribute(XmlConverterConstants.Contents.EntryIntoForce, "yyyy-MM-dd"),
                // new XAttribute(XmlConverterConstants.Contents.UnitStatus, "obowiazuje"),
                text);
    };
}
