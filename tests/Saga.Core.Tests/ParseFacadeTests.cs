using System;
using System.IO;
using System.Linq;
using System.Text;
using DocumentFormat.OpenXml.Packaging;
using Saga.Model;
using Saga.Core;
using Saga.Core.Exceptions;
using Saga.Core.Ingest;
using Xunit;
using Word = DocumentFormat.OpenXml.Wordprocessing;

namespace Saga.Core.Tests
{
	/// <summary>
	/// Testy kanonicznej fasady Parse (Etap 10): detekcja formatu → klasyfikacja → polityka →
	/// koperta ParseResult. Ten sam akt podany jako TXT/PDF/DOCX ze strumienia musi przejść
	/// przez wspólny potok; polityka decyduje wyłącznie o budowie modelu, nigdy o klasyfikacji.
	/// </summary>
	public class ParseFacadeTests
	{
		// Sprawdzony pozytyw klasyfikatora (por. DocumentClassifierTests.Classify_Statute_IsRecognized).
		private static readonly string[] StatuteLines =
		{
			"USTAWA",
			"z dnia 5 marca 2024 r.",
			"o ochronie danych osobowych",
			"Art. 1. Ustawa reguluje ochronę danych osobowych.",
			"Art. 2. Organem właściwym jest Prezes Urzędu.",
			"Art. 3. Traci moc ustawa z dnia 1 stycznia 2000 r.",
			"Art. 4. Ustawa wchodzi w życie po upływie 14 dni od dnia ogłoszenia.",
		};

		private static readonly string[] NonActLines =
		{
			"Notatka ze spotkania zespołu projektowego",
			"Omówiono postępy prac nad wdrożeniem systemu.",
			"Ustalono termin kolejnego spotkania na przyszły wtorek.",
			"Lista zakupów biurowych: papier, toner, spinacze.",
		};

		private static MemoryStream TxtStream(params string[] lines)
			=> new(new UTF8Encoding(false).GetBytes(string.Join("\n", lines)));

		private static byte[] BuildDocx(params string[] paragraphs)
		{
			using var ms = new MemoryStream();
			using (var doc = WordprocessingDocument.Create(ms, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
			{
				var main = doc.AddMainDocumentPart();
				main.Document = new Word.Document(new Word.Body(
					paragraphs.Select(t => (DocumentFormat.OpenXml.OpenXmlElement)
						new Word.Paragraph(new Word.Run(new Word.Text(t)))).ToArray()));
			}
			return ms.ToArray();
		}

		// ============================================================
		// Polityki parsowania
		// ============================================================

		[Fact]
		public void TxtStatute_DefaultPolicy_ClassifiesAndBuildsModel()
		{
			using var stream = TxtStream(StatuteLines);

			var result = LegalDocumentParser.Parse(stream, "ustawa.txt");

			Assert.True(result.Classification.IsLegalAct);
			Assert.Equal(LegalActType.Statute, result.Classification.ActType);
			Assert.Equal(SourceFormat.PlainText, result.SourceFormat);
			Assert.True(result.BlockCount > 0);

			Assert.NotNull(result.Document);
			Assert.Equal(4, result.Document!.Articles.Count());
			// Model niesie raport klasyfikacji dla konsumentów trzymających tylko LegalDocument.
			Assert.Same(result.Classification, result.Document.Classification);
		}

		[Fact]
		public void NonAct_DefaultPolicy_SkipsModelButReportsClassification()
		{
			using var stream = TxtStream(NonActLines);

			var result = LegalDocumentParser.Parse(stream, "notatka.txt");

			Assert.False(result.Classification.IsLegalAct);
			Assert.Null(result.Classification.ActType);
			Assert.Null(result.Document);
			Assert.NotEmpty(result.Classification.Justification);
		}

		[Fact]
		public void NonAct_AlwaysParse_BuildsModel()
		{
			using var stream = TxtStream(NonActLines);

			var result = LegalDocumentParser.Parse(stream, "notatka.txt",
				new ParseOptions { Policy = ParsePolicy.AlwaysParse });

			Assert.False(result.Classification.IsLegalAct);
			Assert.NotNull(result.Document);
			Assert.False(result.Document!.Classification!.IsLegalAct);
		}

		[Fact]
		public void Statute_ClassifyOnly_SkipsModel()
		{
			using var stream = TxtStream(StatuteLines);

			var result = LegalDocumentParser.Parse(stream, "ustawa.txt",
				new ParseOptions { Policy = ParsePolicy.ClassifyOnly });

			Assert.True(result.Classification.IsLegalAct);
			Assert.Null(result.Document);
		}

		// ============================================================
		// Routing formatów
		// ============================================================

		[Fact]
		public void PdfStatute_EndToEnd_UsesCommonPipeline()
		{
			var pdf = new TestPdfBuilder().AddPage();
			for (int i = 0; i < StatuteLines.Length; i++)
				pdf.AddText(StatuteLines[i], 57, 700 - i * 14);

			using var stream = new MemoryStream(pdf.Build());
			var result = LegalDocumentParser.Parse(stream, "ustawa.pdf");

			Assert.Equal(SourceFormat.Pdf, result.SourceFormat);
			Assert.True(result.Classification.IsLegalAct);
			Assert.Equal(LegalActType.Statute, result.Classification.ActType);
			Assert.NotNull(result.Document);
			Assert.Equal(4, result.Document!.Articles.Count());
		}

		[Fact]
		public void DocxStatute_WithoutTemplateStyles_EndToEnd()
		{
			using var stream = new MemoryStream(BuildDocx(StatuteLines));

			var result = LegalDocumentParser.Parse(stream, "ustawa.docx");

			Assert.Equal(SourceFormat.Docx, result.SourceFormat);
			Assert.True(result.Classification.IsLegalAct);
			Assert.NotNull(result.Document);
			Assert.Equal(4, result.Document!.Articles.Count());
		}

		[Fact]
		public void ForcedFormat_OverridesDetection_BothWays()
		{
			// Tekst zaczynający się nagłówkiem PDF: autodetekcja kieruje do adaptera PDF,
			// który odrzuca plik jako uszkodzony; wymuszenie TXT parsuje go jako tekst.
			var content = "%PDF-udawany nagłówek\nTo jest zwykła notatka tekstowa.\nNic więcej tu nie ma.";
			var bytes = new UTF8Encoding(false).GetBytes(content);

			using (var autodetected = new MemoryStream(bytes))
			{
				Assert.Throws<UnsupportedDocumentFormatException>(
					() => LegalDocumentParser.Parse(autodetected, "notatka.txt"));
			}

			using (var forced = new MemoryStream(bytes))
			{
				var result = LegalDocumentParser.Parse(forced, "notatka.txt",
					new ParseOptions { ForcedFormat = SourceFormat.PlainText });

				Assert.Equal(SourceFormat.PlainText, result.SourceFormat);
				Assert.False(result.Classification.IsLegalAct);
			}
		}

		[Fact]
		public void UnknownBinary_NoHint_ThrowsUnsupportedFormat()
		{
			var bytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x00, 0x00, 0x00, 0x0D };
			using var stream = new MemoryStream(bytes);

			Assert.Throws<UnsupportedDocumentFormatException>(
				() => LegalDocumentParser.Parse(stream, fileNameHint: null));
		}

		[Fact]
		public void CorruptZipWithDocxSignature_ThrowsUnsupportedFormat_NotRawException()
		{
			// Sygnatura PK → detekcja Docx, ale archiwum jest śmieciem: adapter ma opakować
			// błąd kontenera w UnsupportedDocumentFormatException (nie FileFormatException,
			// która ominęłaby obsługę błędów CLI/Web jako FormatException).
			var bytes = new byte[] { 0x50, 0x4B, 0x03, 0x04 }
				.Concat(Enumerable.Repeat((byte)0xAB, 64))
				.ToArray();
			using var stream = new MemoryStream(bytes);

			Assert.Throws<UnsupportedDocumentFormatException>(
				() => LegalDocumentParser.Parse(stream, "uszkodzony.docx"));
		}

		[Fact]
		public void ForcedFormat_Unknown_BehavesLikeAutodetection()
		{
			// Naturalna pętla konsumenta: ForcedFormat = poprzedni ParseResult.SourceFormat,
			// który dla Parse(blocks) jest Unknown — ma oznaczać „wykryj", nie „odmów".
			using var stream = TxtStream(StatuteLines);

			var result = LegalDocumentParser.Parse(stream, "ustawa.txt",
				new ParseOptions { ForcedFormat = SourceFormat.Unknown });

			Assert.Equal(SourceFormat.PlainText, result.SourceFormat);
			Assert.True(result.Classification.IsLegalAct);
		}

		[Fact]
		public void NullBlockInList_IsSkippedByAllPolicies()
		{
			// Parytet z klasyfikatorem (Normalize pomija null-e): budowa modelu też nie może
			// wybuchać NullReferenceException na liście składanej ręcznie.
			var blocks = StatuteLines
				.Select((text, i) => (DocumentBlock?)new DocumentBlock
				{
					Text = text,
					Source = new BlockSourceLocation { BlockIndex = i },
				})
				.ToList();
			blocks.Insert(2, null);

			var result = LegalDocumentParser.Parse(blocks!,
				new ParseOptions { Policy = ParsePolicy.AlwaysParse });

			Assert.NotNull(result.Document);
			Assert.Equal(4, result.Document!.Articles.Count());
		}

		[Fact]
		public void PdfFootnoteZone_IsExcludedFromModel()
		{
			// Kontrakt adapter↔parser: przypis „1) Zmiany tekstu jednolitego…" z dołu strony PDF
			// (Role=FootnoteText) NIE może stać się punktem 1) ani żadną treścią jednostki —
			// przeinaczałby akt. Klasyfikator dokumentu dostaje go osobno (pełna lista bloków).
			var pdf = new TestPdfBuilder().AddPage();
			for (int i = 0; i < StatuteLines.Length; i++)
				pdf.AddText(StatuteLines[i], 57, 700 - i * 14);
			pdf.AddText("1) Zmiany tekstu jednolitego wymienionej ustawy ogłoszone zostały", 57, 90, 8);
			pdf.AddText("w Dz. U. z 2023 r. poz. 100 oraz z 2024 r. poz. 5.", 57, 80, 8);

			using var stream = new MemoryStream(pdf.Build());
			var result = LegalDocumentParser.Parse(stream, "ustawa.pdf",
				new ParseOptions { Policy = ParsePolicy.AlwaysParse });

			Assert.NotNull(result.Document);
			var document = result.Document!;
			Assert.Equal(4, document.Articles.Count());

			var allTexts = document.Articles
				.SelectMany(a => a.Paragraphs)
				.SelectMany(p => new[] { p.ContentText }.Concat(p.Points.Select(pt => pt.ContentText)))
				.ToList();
			Assert.DoesNotContain(allTexts, t => t != null && t.Contains("Zmiany tekstu jednolitego"));

			// Blok przypisu ISTNIEJE w IR (klasyfikator dokumentu go widzi) — pominął go tylko parser.
			using var rawStream = new MemoryStream(pdf.Build());
			var rawBlocks = new Saga.Core.Ingest.Pdf.PdfBlockReader().ReadBlocks(rawStream);
			var footnote = Assert.Single(rawBlocks, b => b.Role == BlockRole.FootnoteText);
			Assert.Contains("Zmiany tekstu jednolitego", footnote.Text);
		}

		// ============================================================
		// Kontrakty strumienia i przeciążeń
		// ============================================================

		[Fact]
		public void NonSeekableStream_IsBufferedInternally()
		{
			var bytes = new UTF8Encoding(false).GetBytes(string.Join("\n", StatuteLines));
			using var stream = new NonSeekableReadStream(bytes);

			var result = LegalDocumentParser.Parse(stream, "ustawa.txt");

			Assert.True(result.Classification.IsLegalAct);
			Assert.NotNull(result.Document);
		}

		[Fact]
		public void ParseFilePath_ReadsFileReadOnly()
		{
			var path = Path.Combine(Path.GetTempPath(), $"saga_test_{Guid.NewGuid():N}.txt");
			File.WriteAllText(path, string.Join("\n", StatuteLines), new UTF8Encoding(false));
			var writtenAt = File.GetLastWriteTimeUtc(path);
			try
			{
				var result = LegalDocumentParser.Parse(path);

				Assert.True(result.Classification.IsLegalAct);
				Assert.NotNull(result.Document);
				// Nowa ścieżka nie modyfikuje pliku źródłowego (bez kopii zapasowych i zapisu).
				Assert.Equal(writtenAt, File.GetLastWriteTimeUtc(path));
			}
			finally
			{
				File.Delete(path);
			}
		}

		[Fact]
		public void ParseBlocksOverload_ReportsUnknownSourceFormat()
		{
			var blocks = StatuteLines
				.Select((text, i) => new DocumentBlock
				{
					Text = text,
					Source = new BlockSourceLocation { BlockIndex = i },
				})
				.ToList();

			var result = LegalDocumentParser.Parse(blocks);

			Assert.Equal(SourceFormat.Unknown, result.SourceFormat);
			Assert.Equal(blocks.Count, result.BlockCount);
			Assert.True(result.Classification.IsLegalAct);
			Assert.NotNull(result.Document);
		}
	}
}
