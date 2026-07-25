using System.IO;
using System.Linq;
using WordParserCore.Exceptions;
using WordParserCore.Ingest;
using WordParserCore.Ingest.Pdf;
using Xunit;

namespace WordParserCore.Tests
{
	/// <summary>
	/// Testy adaptera PDF (Etap 9): odtwarzanie wierszy i bloków z warstwy tekstowej, polskie znaki,
	/// filtr artefaktów stron, dehyfenacja, jednostka przecięta granicą stron, superscript → [x],
	/// strefa przypisów, detekcja skanu i wcięcia → twips. Fikstury: <see cref="TestPdfBuilder"/>
	/// (Helvetica + /Differences — bez plików fontów).
	///
	/// LUKA (udokumentowana): gałąź PDF zaszyfrowanego hasłem (PdfDocumentEncryptedException →
	/// UnsupportedDocumentFormatException) nie ma testu — TestPdfBuilder nie buduje szyfrowanych
	/// PDF-ów, a binarna fikstura wymagałaby odstępstwa od zasady „bez binariów w repo".
	/// Typ wyjątku PdfPig 0.1.15 zweryfikowany w przeglądzie adwersaryjnym Etapu 9.
	/// </summary>
	public class PdfBlockReaderTests
	{
		private static System.Collections.Generic.IReadOnlyList<DocumentBlock> Read(byte[] pdf)
		{
			using var ms = new MemoryStream(pdf);
			return new PdfBlockReader().ReadBlocks(ms);
		}

		// Gęsty tekst zapełniający stronę (detekcja skanu wymaga ≥ 20 znaków/stronę średnio).
		private static TestPdfBuilder WithBodyPadding(TestPdfBuilder b, double fromY = 400, int lines = 5)
		{
			for (int i = 0; i < lines; i++)
				b.AddText($"Art. {90 + i}. Przepis wypełniający strony do celów testowych.", 57, fromY - i * 14);
			return b;
		}

		[Fact]
		public void PolishDiacritics_RoundTripThroughTextLayer()
		{
			var pdf = WithBodyPadding(new TestPdfBuilder().AddPage()
				.AddText("Art. 1. Świadczeń pieniężnych udziela się żołnierzom oraz ich małżonkom.", 57, 700))
				.Build();

			var blocks = Read(pdf);

			var first = blocks.First(b => b.Text.StartsWith("Art. 1."));
			Assert.Contains("Świadczeń pieniężnych", first.Text);
			Assert.Contains("małżonkom", first.Text);
		}

		[Fact]
		public void ReadingOrder_TopToBottom_BlockPerUnitLine()
		{
			var pdf = new TestPdfBuilder().AddPage()
				.AddText("Art. 1. Artykuł pierwszy.", 57, 700)
				.AddText("Art. 2. Artykuł drugi zawiera wyliczenie:", 57, 680)
				.AddText("1) punkt pierwszy;", 57, 666)
				.AddText("2) punkt drugi.", 57, 652)
				.Build();

			var blocks = Read(pdf);

			Assert.Equal(4, blocks.Count);
			Assert.StartsWith("Art. 1.", blocks[0].Text);
			Assert.StartsWith("Art. 2.", blocks[1].Text);
			Assert.StartsWith("1)", blocks[2].Text);
			Assert.StartsWith("2)", blocks[3].Text);
		}

		[Fact]
		public void ContinuationLines_JoinIntoSingleBlock()
		{
			// Zawinięty ustęp: kontynuacja zaczyna się małą literą, bez markera, w typowej interlinii.
			var pdf = new TestPdfBuilder().AddPage()
				.AddText("Art. 1. Organ prowadzi rejestr dokumentów oraz", 57, 700)
				.AddText("udostępnia go na wniosek zainteresowanego.", 57, 686)
				.AddText("Art. 2. Przepis odrębny.", 57, 672)
				.Build();

			var blocks = Read(pdf);

			Assert.Equal(2, blocks.Count);
			Assert.Equal("Art. 1. Organ prowadzi rejestr dokumentów oraz udostępnia go na wniosek zainteresowanego.",
				blocks[0].Text);
		}

		[Fact]
		public void TrailingHyphen_IsKeptAndJoinedDirectly()
		{
			// DECYZJA UŻYTKOWNIKA (zamiast dehyfenacji z planu): dywiz złożenia jest nieodróżnialny
			// od dzielenia wyrazów — zachowujemy dywiz i sklejamy bez spacji („nie zgubić informacji").
			var pdf = new TestPdfBuilder().AddPage()
				.AddText("Art. 1. Wspiera się rozwój społeczno-", 57, 700)
				.AddText("gospodarczy kraju oraz regionów.", 57, 686)
				.Build();

			var blocks = Read(pdf);

			Assert.Contains("społeczno-gospodarczy kraju", blocks[0].Text);
		}

		[Fact]
		public void TrailingHyphen_BeforeUppercase_JoinsCompoundName()
		{
			var pdf = new TestPdfBuilder().AddPage()
				.AddText("Art. 1. Siedzibą sądu jest Warszawa-", 57, 700)
				.AddText("Praga oraz miasto stołeczne.", 57, 686)
				.Build();

			var blocks = Read(pdf);

			Assert.Contains("Warszawa-Praga oraz", blocks[0].Text);
		}

		[Fact]
		public void UnitCutByPageBoundary_JoinsAcrossPages()
		{
			var builder = new TestPdfBuilder();
			builder.AddPage();
			WithBodyPadding(builder, fromY: 700);
			builder.AddText("Art. 2. Przepis stosuje się do umów", 57, 100);
			builder.AddPage();
			builder.AddText("zawartych przed dniem wejścia w życie ustawy.", 57, 780);
			WithBodyPadding(builder, fromY: 700);

			var blocks = Read(builder.Build());

			var cut = blocks.Single(b => b.Text.StartsWith("Art. 2."));
			Assert.Equal("Art. 2. Przepis stosuje się do umów zawartych przed dniem wejścia w życie ustawy.", cut.Text);
		}

		[Fact]
		public void PageArtifacts_HeaderAndPagination_AreFiltered()
		{
			// Nagłówek wydawniczy (góra) i paginacja (dół) powtórzone na obu stronach + whitelist.
			var builder = new TestPdfBuilder();
			for (int p = 1; p <= 2; p++)
			{
				builder.AddPage();
				builder.AddText($"Dziennik Ustaw – {p} – Poz. 902", 200, 820, 9);
				WithBodyPadding(builder, fromY: 700);
				builder.AddText($"– {p} –", 280, 30, 9);
			}

			var blocks = Read(builder.Build());

			Assert.DoesNotContain(blocks, b => b.Text.Contains("Dziennik Ustaw"));
			Assert.DoesNotContain(blocks, b => b.Text.Contains("Poz. 902"));
			Assert.DoesNotContain(blocks, b => b.Text.Trim().StartsWith("–") && b.Text.Contains("1 –"));
		}

		[Fact]
		public void Superscript_UnitNumber_YieldsBracketChannel()
		{
			// „Art. 15¹" — indeks górny mniejszym fontem z podniesioną linią bazową → kanał [1].
			// x=89,6: koniec „Art. 15" wg metryk AFM Helvetiki = 57 + 2,946 em × 11 pt ≈ 89,4 pt;
			// odstęp 0,2 pt < próg spacji (0,2 em = 2,2 pt), więc indeks klei się do numeru.
			var pdf = WithBodyPadding(new TestPdfBuilder().AddPage()
				.AddText("Art. 15", 57, 700, 11)
				.AddText("1", 89.6, 703.5, 7)
				.AddText(". Nowa jednostka dodana nowelizacją.", 94, 700, 11))
				.Build();

			var blocks = Read(pdf);

			Assert.Contains(blocks, b => b.Text.Contains("Art. 15[1]"));
		}

		[Fact]
		public void FootnoteZone_SmallerFontAtBottom_BecomesFootnoteBlocksAtEnd()
		{
			var builder = new TestPdfBuilder().AddPage();
			WithBodyPadding(builder, fromY: 700);
			builder.AddText("1) Zmiany tekstu jednolitego wymienionej ustawy ogłoszone zostały", 57, 90, 8);
			builder.AddText("w Dz. U. z 2023 r. poz. 100 oraz z 2024 r. poz. 5.", 57, 80, 8);

			var blocks = Read(builder.Build());

			var footnotes = blocks.Where(b => b.Role == BlockRole.FootnoteText).ToList();
			var footnote = Assert.Single(footnotes);
			Assert.StartsWith("1)", footnote.Text);
			Assert.Contains("poz. 100", footnote.Text);

			// Przypisy emitowane NA KOŃCU i nie mieszają się z punktami treści.
			Assert.Equal(blocks.Count - 1, blocks.ToList().IndexOf(footnote));
			Assert.DoesNotContain(blocks, b => b.Role == BlockRole.Body && b.Text.StartsWith("1) Zmiany"));
		}

		[Fact]
		public void ScannedPdf_NoTextLayer_ThrowsScannedPdfException()
		{
			var pdf = new TestPdfBuilder()
				.AddPage().AddText("x", 57, 700)
				.AddPage()
				.Build();

			using var ms = new MemoryStream(pdf);
			var ex = Assert.Throws<ScannedPdfException>(() => new PdfBlockReader().ReadBlocks(ms));
			Assert.Contains("skan", ex.Message);
		}

		[Fact]
		public void Indents_TiretLines_CarryLeftIndentTwips()
		{
			var pdf = WithBodyPadding(new TestPdfBuilder().AddPage()
				.AddText("Art. 1. Wyliczenie obejmuje:", 57, 700)
				.AddText("1) punkt z literami:", 57, 686)
				.AddText("a) litera z tiretami:", 71, 672)
				.AddText("– tiret pierwszy,", 85, 658)
				.AddText("– tiret drugi.", 85, 644))
				.Build();

			var blocks = Read(pdf);

			var tiret = blocks.First(b => b.Text.StartsWith("– tiret pierwszy"));
			// 85pt - 57pt (dominujący lewy margines) = 28pt = 560 twips.
			Assert.Equal(560, tiret.Layout?.LeftIndentTwips);

			var article = blocks.First(b => b.Text.StartsWith("Art. 1."));
			Assert.Equal(0, article.Layout?.LeftIndentTwips);
		}

		// ============================================================
		// Regresje z przeglądu adwersaryjnego Etapu 9
		// ============================================================

		[Fact]
		public void HangingIndentContinuation_JoinsIntoUnitBlock()
		{
			// KRYTYCZNE z przeglądu: skład DU używa wcięcia WISZĄCEGO — kontynuacja zawiniętego
			// punktu zaczyna się GŁĘBIEJ niż marker; granica wcięciowa cięłaby ją i gubiła tekst.
			var pdf = WithBodyPadding(new TestPdfBuilder().AddPage()
				.AddText("Art. 1. Wyliczenie obejmuje:", 57, 700)
				.AddText("1) punkt pierwszy o treści dłuższej, która", 57, 686)
				.AddText("zawija się do wcięcia wiszącego pod tekstem;", 71, 672)
				.AddText("2) punkt drugi.", 57, 658))
				.Build();

			var blocks = Read(pdf);

			var point = blocks.Single(b => b.Text.StartsWith("1)"));
			Assert.Equal("1) punkt pierwszy o treści dłuższej, która zawija się do wcięcia wiszącego pod tekstem;",
				point.Text);
		}

		[Fact]
		public void FootnoteContinuedOnNextPage_StaysInFootnoteZone()
		{
			// Odnośnik wykazu zmian TJ regularnie zajmuje 2+ stron; kontynuacja zaczyna się
			// środkiem zdania (bez „N)") i nie może wpaść do treści aktu.
			var builder = new TestPdfBuilder();
			builder.AddPage();
			WithBodyPadding(builder, fromY: 700);
			builder.AddText("1) Zmiany tekstu jednolitego wymienionej ustawy ogłoszone zostały", 57, 90, 8);
			builder.AddPage();
			WithBodyPadding(builder, fromY: 700);
			builder.AddText("w Dz. U. z 2023 r. poz. 100 oraz z 2024 r. poz. 5.", 57, 90, 8);

			var blocks = Read(builder.Build());

			var footnote = Assert.Single(blocks, b => b.Role == BlockRole.FootnoteText);
			Assert.Contains("ogłoszone zostały w Dz. U. z 2023 r.", footnote.Text);
			Assert.DoesNotContain(blocks, b => b.Role == BlockRole.Body && b.Text.StartsWith("w Dz. U."));
		}

		[Fact]
		public void WrappedJournalReference_DoesNotBecomeFalsePointBoundary()
		{
			// Zawinięcie wewnątrz „(Dz. U. poz. ⏎ 1234) wprowadza się…" nie może rozciąć zdania
			// (fragment „1234)…" wyglądałby jak punkt 1234).
			var pdf = WithBodyPadding(new TestPdfBuilder().AddPage()
				.AddText("Art. 1. W ustawie z dnia 5 maja 2020 r. (Dz. U. poz.", 57, 700)
				.AddText("1234) wprowadza się następujące zmiany:", 57, 686))
				.Build();

			var blocks = Read(pdf);

			var intro = blocks.Single(b => b.Text.StartsWith("Art. 1."));
			Assert.Contains("(Dz. U. poz. 1234) wprowadza się", intro.Text);
			Assert.DoesNotContain(blocks, b => b.Text.StartsWith("1234)"));
		}

		[Fact]
		public void WrappedQuotedWording_AfterOpenPhrase_DoesNotSplit()
		{
			// Komenda zamiany wyrazów zawinięta przed cytowanym brzmieniem: poprzedni wiersz kończy
			// się środkiem frazy (bez interpunkcji) → „ nie otwiera nowego bloku.
			var pdf = WithBodyPadding(new TestPdfBuilder().AddPage()
				.AddText("1) w art. 3 wyrazy „dom pomocy” zastępuje się wyrazami", 57, 700)
				.AddText("„centrum usług społecznych”;", 57, 686))
				.Build();

			var blocks = Read(pdf);

			var command = blocks.Single(b => b.Text.StartsWith("1)"));
			Assert.Contains("zastępuje się wyrazami „centrum usług społecznych”;", command.Text);
		}

		[Fact]
		public void QuotedContentAfterColon_StillStartsNewBlock()
		{
			// Kanoniczny cytat po „brzmienie:" — poprzedni wiersz domknięty dwukropkiem → „ otwiera blok.
			var pdf = WithBodyPadding(new TestPdfBuilder().AddPage()
				.AddText("1) art. 5 otrzymuje brzmienie:", 57, 700)
				.AddText("„Art. 5. Nowe brzmienie.”;", 57, 686))
				.Build();

			var blocks = Read(pdf);

			Assert.Contains(blocks, b => b.Text.StartsWith("„Art. 5."));
		}

		[Fact]
		public void BareNumberInMarginZone_SinglePage_IsKept()
		{
			// Goła liczba (komórka tabeli załącznika) na dole strony NIE może zostać skasowana
			// pojedynczo; kasowana jest tylko jednoznaczna paginacja „– N –".
			var builder = new TestPdfBuilder().AddPage();
			WithBodyPadding(builder, fromY: 700);
			builder.AddText("500", 57, 30);
			builder.AddText("– 5 –", 280, 20, 9);

			var blocks = Read(builder.Build());

			Assert.Contains(blocks, b => b.Text == "500");
			Assert.DoesNotContain(blocks, b => b.Text.Contains("– 5 –"));
		}

		[Fact]
		public void NumericAnnexRows_AcrossPages_AreNotMergedByDigitNormalization()
		{
			// Wiersze tabeli liczbowej („1 15 200" vs „2 16 300") normalizują się do tej samej
			// postaci „# ## ###" — sygnał powtórzeniowy nie może ich skasować jako artefaktu.
			var builder = new TestPdfBuilder();
			for (int p = 0; p < 2; p++)
			{
				builder.AddPage();
				WithBodyPadding(builder, fromY: 700);
				builder.AddText($"{p + 1} 1{5 + p} {200 + p * 100}", 57, 30);
			}

			var blocks = Read(builder.Build());

			Assert.Contains(blocks, b => b.Text == "1 15 200");
			Assert.Contains(blocks, b => b.Text == "2 16 300");
		}

		[Fact]
		public void HeaderOnlyTextLayer_AfterFiltering_ThrowsScannedPdfException()
		{
			// Skan częściowo cyfrowy: warstwa tekstowa to wyłącznie nagłówki/paginacja — po filtrze
			// artefaktów nie zostaje nic; to skan, nie pusty sukces.
			var builder = new TestPdfBuilder();
			for (int p = 1; p <= 3; p++)
			{
				builder.AddPage();
				builder.AddText($"Dziennik Ustaw – {p} – Poz. 902", 180, 820, 9);
				builder.AddText($"– {p} –", 280, 30, 9);
			}

			using var ms = new MemoryStream(builder.Build());
			var ex = Assert.Throws<ScannedPdfException>(() => new PdfBlockReader().ReadBlocks(ms));
			Assert.Contains("artefakty", ex.Message);
		}

		[Fact]
		public void FootnoteHeavyPage_BodySizeStillDominant_FootnotesDetected()
		{
			// Krótka nowela z długim odnośnikiem: wiersze przypisów przeważają liczebnie —
			// dominanta treści nie może spaść do rozmiaru przypisów (wyłączałoby to ich strefę).
			var builder = new TestPdfBuilder().AddPage();
			builder.AddText("Art. 1. W ustawie o podatku rolnym wprowadza się zmiany.", 57, 700, 11);
			builder.AddText("Art. 2. Ustawa wchodzi w życie po upływie 14 dni.", 57, 686, 11);
			for (int i = 0; i < 7; i++)
				builder.AddText(i == 0
					? "1) Zmiany tekstu jednolitego wymienionej ustawy ogłoszone zostały"
					: $"w Dz. U. z 20{10 + i} r. poz. {100 + i} oraz kolejne zmiany wymienione dalej,", 57, 150 - i * 10, 8);

			var blocks = Read(builder.Build());

			Assert.Contains(blocks, b => b.Role == BlockRole.FootnoteText);
			Assert.DoesNotContain(blocks, b => b.Role == BlockRole.Body && b.Text.Contains("Zmiany tekstu jednolitego"));
		}

		[Fact]
		public void GraphicAnnexPages_EmptyMajority_DoesNotRejectFullTextAct()
		{
			// Akt z pełnym tekstem + wielostronicowy załącznik graficzny (mapy/wzory bez tekstu):
			// większość stron pusta, ale strony niepuste są bogate w treść → NIE skan.
			var builder = new TestPdfBuilder();
			builder.AddPage();
			WithBodyPadding(builder, fromY: 700, lines: 8);
			for (int i = 0; i < 4; i++)
				builder.AddPage();

			var blocks = Read(builder.Build());

			Assert.NotEmpty(blocks);
		}

		[Fact]
		public void SeparateTextRuns_OnOneLine_GetSpaceFromGap()
		{
			// PDF-y bez glifów spacji pozycjonują słowa osobnymi operacjami — spacja musi wynikać
			// z odstępu geometrycznego.
			var pdf = WithBodyPadding(new TestPdfBuilder().AddPage()
				.AddText("Art.", 57, 700)
				.AddText("1. Przepis ogólny.", 80, 700))
				.Build();

			var blocks = Read(pdf);

			Assert.Contains(blocks, b => b.Text.StartsWith("Art. 1. Przepis"));
		}

		[Fact]
		public void NestedTiretsFromPdfIndentation_ProduceNestedModel()
		{
			// Integracja z Etapem 7a: wcięcia z PDF zasilają wnioskowanie głębokości tiretu.
			var pdf = WithBodyPadding(new TestPdfBuilder().AddPage()
				.AddText("Art. 1. Wyliczenie obejmuje:", 57, 700)
				.AddText("1) punkt z literami:", 57, 686)
				.AddText("a) litera z tiretami:", 71, 672)
				.AddText("– tiret pierwszy,", 85, 658)
				.AddText("– tiret zagnieżdżony,", 110, 644)
				.AddText("– tiret powrotny.", 85, 630))
				.Build();

			var model = WordParserCore.LegalDocumentParser.ParseBlocks(Read(pdf));

			var letter = model.Articles.First().Paragraphs[0].Points[0].Letters[0];
			Assert.Equal(2, letter.Tirets.Count);
			Assert.Single(letter.Tirets[0].Tirets);
		}
	}
}
