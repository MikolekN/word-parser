using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using WordParserCore;
using WordParserCore.Ingest;
using WordParserCore.Ingest.Pdf;
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

		[Fact]
		public void SimpleAct_PdfAndTxt_AreEquivalent()
		{
			// Etap 9: ten sam akt jako PDF (warstwa tekstowa, bloki z geometrii) i jako TXT
			// musi dać równoważny model — wspólny potok, format wejścia jest tylko adapterem.
			var lines = new[]
			{
				"Art. 1. Artykuł pierwszy ma tylko jeden ustęp.",
				"Art. 2.",
				"1. Ustęp pierwszy artykułu drugiego.",
				"2. Ustęp drugi zawiera wyliczenie:",
				"1) punkt pierwszy;",
				"2) punkt drugi zawiera litery:",
				"a) litera pierwsza,",
				"b) litera druga.",
			};

			var pdfBuilder = new TestPdfBuilder().AddPage();
			for (int i = 0; i < lines.Length; i++)
				pdfBuilder.AddText(lines[i], 57, 700 - i * 14);
			// Wariant PDF dodatkowo ZAWIJA ostatni wiersz na dwie linie (wcięcie wiszące) —
			// ekwiwalencja musi dowodzić także sklejania kontynuacji, nie tylko markerów 1:1.
			pdfBuilder.AddText("c) litera trzecia o treści dłuższej, która w wariancie PDF", 57, 700 - lines.Length * 14);
			pdfBuilder.AddText("zawija się do drugiego wiersza.", 71, 700 - (lines.Length + 1) * 14);

			using var pdfStream = new MemoryStream(pdfBuilder.Build());
			var pdfModel = LegalDocumentParser.ParseBlocks(new PdfBlockReader().ReadBlocks(pdfStream));
			var txtLines = lines.Append(
				"c) litera trzecia o treści dłuższej, która w wariancie PDF zawija się do drugiego wiersza.");
			var txtModel = LegalDocumentParser.ParseBlocks(ReadTxt(string.Join("\n", txtLines)));

			var pdfDescription = ModelEquivalenceComparer.Describe(pdfModel);
			var txtDescription = ModelEquivalenceComparer.Describe(txtModel);

			Assert.Equal(txtDescription, pdfDescription);

			// Strażnik pustej równoważności — struktura naprawdę istnieje.
			Assert.Contains("art 1", pdfDescription);
			Assert.Contains("lit c", pdfDescription);
			Assert.Contains("zawija się do drugiego wiersza", pdfDescription);
			Assert.Equal(2, pdfModel.Articles.Count());
		}

		[Fact]
		public void AmendingAct_DocxAndTxt_AreEquivalent()
		{
			// Etap 8: ten sam akt zmieniający ze stylami (Z/* wyznacza treść nowelizacji) i jako TXT
			// (granice z bilansu cudzysłowów „…") musi dać równoważny model — łącznie z nowelizacjami.
			var lines = new (string Text, string Style)[]
			{
				("Art. 1. W ustawie z dnia 6 września 2001 r. o dostępie do informacji publicznej " +
					"(Dz. U. z 2022 r. poz. 902) wprowadza się następujące zmiany:", "ART"),
				("1) art. 5 otrzymuje brzmienie:", "PKT"),
				("„Art. 5. Prawo do informacji podlega ograniczeniu:", "Z/ART"),
				("1) w zakresie tajemnic ustawowo chronionych;", "Z/PKT"),
				("2) ze względu na prywatność osoby fizycznej.”;", "Z/PKT"),
				("2) w art. 7 uchyla się ust. 2;", "PKT"),
				("3) w art. 9 wyrazy „trzech dni” zastępuje się wyrazami „siedmiu dni”.", "PKT"),
				("Art. 2. Ustawa wchodzi w życie po upływie 14 dni od dnia ogłoszenia.", "ART"),
			};

			var docxBlocks = lines.Select((l, i) => Styled(l.Text, l.Style, i)).ToList();
			var docxModel = LegalDocumentParser.ParseBlocks(docxBlocks);

			var txt = string.Join("\n", lines.Select(l => l.Text));
			var txtModel = LegalDocumentParser.ParseBlocks(ReadTxt(txt));

			var docxDescription = ModelEquivalenceComparer.Describe(docxModel);
			var txtDescription = ModelEquivalenceComparer.Describe(txtModel);

			Assert.Equal(docxDescription, txtDescription);

			// Strażnik pustej równoważności: nowelizacje naprawdę istnieją i mają właściwe operacje,
			// a końcowy artykuł aktu zmieniającego NIE został połknięty do treści nowelizacji.
			Assert.Contains("AMENDMENT Modification obj=Article", docxDescription);
			Assert.Contains("AMENDMENT Repeal", docxDescription);
			Assert.Contains("plaintext | siedmiu dni", docxDescription);
			Assert.Equal(2, docxModel.Articles.Count());
			Assert.Equal(2, txtModel.Articles.Count());
		}
	}
}
