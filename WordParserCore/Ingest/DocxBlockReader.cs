using System.Collections.Generic;
using System.IO;
using DocumentFormat.OpenXml.Packaging;
using Word = DocumentFormat.OpenXml.Wordprocessing;
using WordParserCore.Exceptions;
using WordParserCore.Helpers;

#nullable enable

namespace WordParserCore.Ingest
{
	/// <summary>
	/// Adapter DOCX → bloki reprezentacji pośredniej.
	/// Jedyny most OpenXml→IR w potoku; iteracja identyczna z dotychczasową
	/// (Descendants — obejmuje także akapity w tabelach, parytet z golden doc001).
	/// Metadane układu (Layout) będą wypełniane w Etapie 3 planu — na razie null.
	/// </summary>
	public sealed class DocxBlockReader : IDocumentBlockReader
	{
		public SourceFormat Format => SourceFormat.Docx;

		public IReadOnlyList<DocumentBlock> ReadBlocks(Stream stream)
		{
			using var wordDocument = WordprocessingDocument.Open(stream, false);
			return ReadBlocks(wordDocument);
		}

		public IReadOnlyList<DocumentBlock> ReadBlocks(WordprocessingDocument wordDocument)
		{
			var mainPart = wordDocument.MainDocumentPart ??
				throw new ParsingException("MainDocumentPart dokumentu jest null - plik moze byc uszkodzony lub pusty.");

			var blocks = new List<DocumentBlock>();
			var index = 0;
			foreach (var paragraph in mainPart.Document.Descendants<Word.Paragraph>())
			{
				blocks.Add(ToBlock(paragraph, index++));
			}

			return blocks;
		}

		/// <summary>
		/// Konwersja pojedynczego akapitu OpenXml na blok IR.
		/// Tekst surowy (bez Trim/Sanitize) — normalizację wykonuje orkiestrator.
		/// </summary>
		internal static DocumentBlock ToBlock(Word.Paragraph paragraph, int? blockIndex)
			=> new()
			{
				Text    = paragraph.GetFullText(),
				StyleId = paragraph.StyleId(),
				Source  = new BlockSourceLocation { BlockIndex = blockIndex },
			};
	}
}
