using System.Xml.Linq;
using Saga.Core.Services.Converters;

namespace Saga.Core
{
    /// <summary>
    /// Nakładki na API XLinq dodające przestrzeń nazw do tworzonych elementów.
    /// </summary>
    internal static class XLinqWrappers
    {
        private static readonly XNamespace Ns = XmlConverterConstants.Namespace;

        internal static XElement Element(string name, params object[] content) =>
            new XElement(Ns + name, content);
    }
}
