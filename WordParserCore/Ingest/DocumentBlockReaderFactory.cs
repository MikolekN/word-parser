using WordParserCore.Exceptions;
using WordParserCore.Ingest.Pdf;

namespace WordParserCore.Ingest
{
	/// <summary>
	/// Fabryka adapterów wejścia: format źródłowy → reader bloków reprezentacji pośredniej.
	/// </summary>
	public static class DocumentBlockReaderFactory
	{
		public static IDocumentBlockReader Create(SourceFormat format) =>
			format switch
			{
				SourceFormat.Docx => new DocxBlockReader(),
				SourceFormat.Pdf => new PdfBlockReader(),
				SourceFormat.PlainText => new PlainTextBlockReader(),
				_ => throw new UnsupportedDocumentFormatException(
					"Nie rozpoznano formatu dokumentu. Obsługiwane formaty: DOCX, PDF z warstwą tekstową, TXT."),
			};
	}
}
