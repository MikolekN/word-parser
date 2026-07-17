using System;
using System.Linq;
using DocumentFormat.OpenXml.Packaging;
using ModelDto;
using ModelDto.SystematizingUnits;
using WordParserCore.Ingest;
using WordParserCore.Services.Parsing;

namespace WordParserCore
{
	public static class LegalDocumentParser
	{
		public static LegalDocument Parse(string filePath)
		{
			using var wordDoc = WordprocessingDocument.Open(filePath, false);
			return Parse(wordDoc);
		}

		public static LegalDocument Parse(WordprocessingDocument wordDocument)
		{
			var blocks = new DocxBlockReader().ReadBlocks(wordDocument);
			return ParseBlocks(blocks);
		}

		/// <summary>
		/// Rdzeń parsowania: buduje model z bloków reprezentacji pośredniej niezależnie od formatu
		/// źródłowego (DOCX/TXT/PDF). Publiczna koperta wyniku (ParseResult) i routing formatów
		/// dochodzą w Etapie 10 — do tego czasu metoda jest wewnętrzna.
		/// </summary>
		internal static LegalDocument ParseBlocks(IReadOnlyList<DocumentBlock> blocks)
		{
			var document = new LegalDocument();
			var subchapter = GetDefaultSubchapter(document);

			var context = new ParsingContext(document, subchapter);
			var orchestrator = new ParserOrchestrator();

			foreach (var block in blocks)
			{
				orchestrator.ProcessBlock(block, context);
			}

			// Finalizacja — wypróżnienie bufora nowelizacji jeśli dokument
			// kończy się wewnątrz treści nowelizacji
			orchestrator.Finalize(context);

			return document;
		}

		private static Subchapter GetDefaultSubchapter(LegalDocument document)
		{
			var part = document.RootPart;
			var book = part.Books.First();
			var title = book.Titles.First();
			var division = title.Divisions.First();
			var chapter = division.Chapters.First();
			return chapter.Subchapters.First();
		}

	}
}
