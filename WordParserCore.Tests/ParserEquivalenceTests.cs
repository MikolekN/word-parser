using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using WordParserCore;
using WordParserCore.Ingest;
using Xunit;

namespace WordParserCore.Tests
{
	/// <summary>
	/// Rama ekwiwalencji (Etap 5): ten sam akt podany jako bloki DOCX (ze stylami szablonu)
	/// i jako TXT (bez stylów, ścieżka regex-only) musi dać RÓWNOWAŻNY model
	/// (struktura + numery + treść). Dowodzi ujednoliconego potoku — styl jest jednym z sygnałów.
	///
	/// Luki gałęzi bezstylowej (WrapUp, jednostki systematyzacyjne, nowelizacje) są jawnym
	/// backlogiem — testy oznaczone Skip z numerem etapu, w którym mają zostać domknięte.
	/// </summary>
	public class ParserEquivalenceTests
	{
		private static DocumentBlock Styled(string text, string styleId, int index)
			=> new() { Text = text, StyleId = styleId, Source = new BlockSourceLocation { BlockIndex = index } };

		private static IReadOnlyList<DocumentBlock> ReadTxt(string text)
		{
			using var ms = new MemoryStream(new UTF8Encoding(false).GetBytes(text));
			return new PlainTextBlockReader().ReadBlocks(ms);
		}

		[Fact]
		public void SimpleAct_DocxBlocksAndTxt_ProduceEquivalentModel()
		{
			// Akt świadomie bez luk bezstylowych (bez części wspólnej, jednostek systematyzacyjnych,
			// nowelizacji) — obejmuje artykuł z ustępem niejawnym, ustępy jawne, punkty, litery.
			var lines = new (string Text, string Style)[]
			{
				("Art. 1. Artykuł pierwszy ma tylko jeden ustęp.", "ART"),
				("Art. 2.", "ART"),
				("1. Ustęp pierwszy artykułu drugiego.", "UST"),
				("2. Ustęp drugi zawiera wyliczenie:", "UST"),
				("1) punkt pierwszy;", "PKT"),
				("2) punkt drugi zawiera litery:", "PKT"),
				("a) litera pierwsza,", "LIT"),
				("b) litera druga.", "LIT"),
			};

			var docxBlocks = lines.Select((l, i) => Styled(l.Text, l.Style, i)).ToList();
			var docxModel = LegalDocumentParser.ParseBlocks(docxBlocks);

			var txt = string.Join("\n", lines.Select(l => l.Text));
			var txtModel = LegalDocumentParser.ParseBlocks(ReadTxt(txt));

			Assert.Equal(
				ModelEquivalenceComparer.Describe(docxModel),
				ModelEquivalenceComparer.Describe(txtModel));
		}

		[Fact]
		public void SimpleAct_Txt_ProducesExpectedStructure()
		{
			// Kotwica bezwzględna (nie tylko względem DOCX): potwierdza, że ścieżka TXT
			// buduje oczekiwaną hierarchię artykuł/ustęp/punkt/litera.
			var txt = string.Join("\n",
				"Art. 1. Artykuł pierwszy ma tylko jeden ustęp.",
				"Art. 2.",
				"1. Ustęp pierwszy artykułu drugiego.",
				"2. Ustęp drugi zawiera wyliczenie:",
				"1) punkt pierwszy;",
				"2) punkt drugi zawiera litery:",
				"a) litera pierwsza,",
				"b) litera druga.");

			var model = LegalDocumentParser.ParseBlocks(ReadTxt(txt));

			var articles = model.Articles.ToList();
			Assert.Equal(2, articles.Count);
			Assert.Equal("1", articles[0].Number?.Value);
			Assert.Equal("2", articles[1].Number?.Value);

			var art2Paragraphs = articles[1].Paragraphs;
			Assert.Equal(2, art2Paragraphs.Count);

			var ust2 = art2Paragraphs.First(p => p.Number?.Value == "2");
			Assert.Equal(2, ust2.Points.Count);

			var pkt2 = ust2.Points.First(p => p.Number?.Value == "2");
			Assert.Equal(2, pkt2.Letters.Count);
			Assert.Equal("a", pkt2.Letters[0].Number?.Value);
			Assert.Equal("b", pkt2.Letters[1].Number?.Value);
		}

		// ============================================================
		// Backlog luk bezstylowych — jawnie oznaczony (Skip z numerem etapu)
		// ============================================================

		[Fact(Skip = "Etap 6 — WrapUp (część wspólna) i jednostki systematyzacyjne bez stylu")]
		public void ActWithWrapUpAndSystematizingUnits_DocxAndTxt_AreEquivalent()
		{
			// Do domknięcia w Etapie 6: bez stylu część wspólna („– …") jest dziś mylona z tiretem,
			// a jednostki systematyzacyjne (Rozdział/Oddział) są nierozpoznane.
		}

		[Fact(Skip = "Etap 8 — nowelizacje bez stylów Z/* (QuoteBalanceTracker + AmendmentCommandParser)")]
		public void AmendingAct_DocxAndTxt_AreEquivalent()
		{
			// Do domknięcia w Etapie 8: bez stylu treść nowelizacji w cudzysłowie nie jest
			// rozpoznawana jako IsAmendmentContent (granice wyznaczają dziś tylko triggery tekstowe).
		}
	}
}
