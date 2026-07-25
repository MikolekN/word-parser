using System.Collections.Generic;
using System.IO;

namespace Saga.Core.Ingest
{
	/// <summary>
	/// Adapter formatu wejściowego → lista bloków reprezentacji pośredniej.
	/// Lista jest zmaterializowana (nie lazy): ten sam odczyt konsumują
	/// klasyfikator dokumentu i parser strukturalny.
	/// </summary>
	public interface IDocumentBlockReader
	{
		SourceFormat Format { get; }

		/// <summary>
		/// Czyta wszystkie bloki dokumentu. Strumień musi być seekowalny;
		/// reader nie przejmuje własności strumienia.
		/// </summary>
		IReadOnlyList<DocumentBlock> ReadBlocks(Stream stream);
	}
}
