#nullable enable

using System.Collections.Generic;

namespace Saga.Model
{
    /// <summary>
    /// Kontrakt dla jednostek redakcyjnych, które mogą być dzielone na segmenty tekstu (np. zdania).
    /// </summary>
    public interface IHasTextSegments
    {
        /// <summary>
        /// Segmenty tekstu w kolejności występowania.
        /// </summary>
        List<TextSegment> TextSegments { get; set; }
    }
}
