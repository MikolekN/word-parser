using System;
using System.Collections.Generic;
using System.Linq;
using DocumentFormat.OpenXml.Wordprocessing;
using Saga.Model;
using Saga.Core.Helpers;
using Saga.Core.Services.Classify;
using Saga.Core.Services.Parsing;
using Xunit;

namespace Saga.Core.Tests
{
	/// <summary>
	/// Testy jednostek systematyzacyjnych (Etap 6b, § 60-62 ZTP): rozpoznanie z treści bez stylu,
	/// przejęcie węzła niejawnego / tworzenie rodzeństwa, wzorzec dwuwierszowy (tytuł) oraz wpływ na eId.
	/// </summary>
	public class SystematizingUnitTests
	{
		private static ParsingContext ConnectedContext()
		{
			// Kontekst z PODŁĄCZONYM oddziałem z drzewa dokumentu (jak LegalDocumentParser).
			var document = new LegalDocument
			{
				Type = LegalActType.Statute,
				SourceJournal = new JournalInfo { Year = 2024, Positions = { 1 } },
			};
			var subchapter = document.RootPart.Books[0].Titles[0].Divisions[0].Chapters[0].Subchapters[0];
			return new ParsingContext(document, subchapter);
		}

		private static Paragraph P(string text)
		{
			var p = new Paragraph();
			p.Append(new Run(new Text(text)));
			return p;
		}

		private static void Feed(ParserOrchestrator orch, ParsingContext ctx, params string[] lines)
		{
			foreach (var line in lines)
				orch.ProcessParagraph(P(line), ctx);
		}

		// ============================================================
		// RomanNumeralConverter
		// ============================================================

		[Theory]
		[InlineData("I", 1)]
		[InlineData("IV", 4)]
		[InlineData("IX", 9)]
		[InlineData("XII", 12)]
		[InlineData("XL", 40)]
		public void Roman_Parses(string roman, int expected) =>
			Assert.Equal(expected, RomanNumeralConverter.RomanToInt(roman));

		[Theory]
		[InlineData("IIII")]  // niekanoniczne
		[InlineData("VX")]    // niekanoniczne
		[InlineData("ABC")]   // nie-rzymskie
		[InlineData("")]
		public void Roman_Invalid_ReturnsNull(string roman) =>
			Assert.Null(RomanNumeralConverter.RomanToInt(roman));

		[Theory]
		[InlineData("pierwsza", 1)]
		[InlineData("PIERWSZA", 1)]
		[InlineData("trzecia", 3)]
		public void WordOrdinal_Parses(string word, int expected) =>
			Assert.Equal(expected, RomanNumeralConverter.WordOrdinalToInt(word));

		// ============================================================
		// Budowanie drzewa
		// ============================================================

		[Fact]
		public void Chapters_TakeoverThenSibling_WithHeadingsAndArticleEIds()
		{
			var orch = new ParserOrchestrator();
			var ctx = ConnectedContext();

			Feed(orch, ctx,
				"Rozdział 1", "Przepisy ogólne",
				"Art. 1. Tresc pierwsza.",
				"Art. 2. Tresc druga.",
				"Rozdział 2", "Odpowiedzialność",
				"Art. 3. Tresc trzecia.");

			var division = ctx.Document.RootPart.Books[0].Titles[0].Divisions[0];
			Assert.Equal(2, division.Chapters.Count);

			// Rozdział 1 przejął węzeł niejawny; Rozdział 2 to rodzeństwo.
			Assert.False(division.Chapters[0].IsImplicit);
			Assert.Equal("1", division.Chapters[0].Number?.Value);
			Assert.Equal("Przepisy ogólne", division.Chapters[0].Heading);
			Assert.False(division.Chapters[1].IsImplicit);
			Assert.Equal("2", division.Chapters[1].Number?.Value);
			Assert.Equal("Odpowiedzialność", division.Chapters[1].Heading);

			var ids = ctx.Document.Articles.Select(a => a.Id).ToList();
			Assert.Contains("rozd_1__art_1", ids);
			Assert.Contains("rozd_1__art_2", ids);
			Assert.Contains("rozd_2__art_3", ids);
		}

		[Fact]
		public void Division_Roman_TakesOverImplicit_AndKeepsRomanInEId()
		{
			var orch = new ParserOrchestrator();
			var ctx = ConnectedContext();

			Feed(orch, ctx, "DZIAŁ II", "Art. 5. Tresc.");

			var division = ctx.Document.RootPart.Books[0].Titles[0].Divisions[0];
			Assert.False(division.IsImplicit);
			Assert.Equal("II", division.Number?.Value);
			Assert.Equal(2, division.Number?.NumericPart);

			var article = ctx.Document.Articles.Single();
			Assert.Equal("dz_II__art_5", article.Id);
		}

		[Fact]
		public void Subchapter_UnderChapter_ProducesNestedEId()
		{
			var orch = new ParserOrchestrator();
			var ctx = ConnectedContext();

			Feed(orch, ctx, "Rozdział 1", "Oddział 1", "Art. 7. Tresc.");

			var article = ctx.Document.Articles.Single();
			Assert.Equal("rozd_1__oddz_1__art_7", article.Id);
		}

		[Fact]
		public void ArticleBeforeFirstChapter_IsNotReparented()
		{
			// Strażnik: przejęcie węzła niejawnego tylko gdy nie ma jeszcze artykułów pod nim —
			// artykuł sprzed pierwszego Rozdziału nie może zyskać prefiksu rozdziału.
			var orch = new ParserOrchestrator();
			var ctx = ConnectedContext();

			Feed(orch, ctx, "Art. 1. Przed rozdzialem.", "Rozdział 1", "Art. 2. W rozdziale.");

			var ids = ctx.Document.Articles.Select(a => a.Id).ToList();
			Assert.Contains("art_1", ids);
			Assert.Contains("rozd_1__art_2", ids);
			Assert.DoesNotContain("rozd_1__art_1", ids);
		}

		[Fact]
		public void NamedPartWithoutNumber_DoesNotBecomeUnit()
		{
			var orch = new ParserOrchestrator();
			var ctx = ConnectedContext();

			Feed(orch, ctx, "Rozdział 1", "Art. 1. Tresc.");
			// „CZĘŚĆ OGÓLNA" (bez numeru) nie jest jednostką — RootPart pozostaje niejawny.
			Assert.True(ctx.Document.RootPart.IsImplicit);
		}

		// ============================================================
		// Regresje z przeglądu adwersaryjnego 6b
		// ============================================================

		[Theory]
		[InlineData("Rozdział 5 stosuje się odpowiednio do umów.")]
		[InlineData("Oddział 2 niniejszej ustawy stosuje się odpowiednio.")]
		[InlineData("Tytuł V wprowadza się z dniem ogłoszenia.")]
		[InlineData("Dział III otrzymuje brzmienie:")]
		[InlineData("Część I stosuje się odpowiednio")]
		public void ProseReferencingUnit_IsNotClassifiedAsUnit(string text)
		{
			// Kotwica $ na całej linii: proza odwołująca się do jednostki nie jest brana za nagłówek
			// (inaczej gubiłaby treść i tworzyła widmowe jednostki).
			var result = new ParagraphClassifier().Classify(new ClassificationInput(text, null));

			Assert.False(IsSystematizing(result.Kind), $"'{text}' nie powinno być jednostką, jest {result.Kind}");
		}

		[Theory]
		[InlineData("DZIAŁ I.")]
		[InlineData("CZĘŚĆ II,")]
		[InlineData("ROZDZIAŁ 3.")]
		public void UnitWithTrailingPunctuation_IsStillRecognized(string text)
		{
			// Końcowa kropka/przecinek przyklejone do markera nie mogą zgubić jednostki.
			var result = new ParagraphClassifier().Classify(new ClassificationInput(text, null));

			Assert.True(IsSystematizing(result.Kind), $"'{text}' powinno być jednostką, jest {result.Kind}");
		}

		[Fact]
		public void ChapterWithUppercaseLetterSuffix_IsRecognized()
		{
			var orch = new ParserOrchestrator();
			var ctx = ConnectedContext();
			Feed(orch, ctx, "ROZDZIAŁ 1A", "Art. 1. Tresc.");

			Assert.Equal("rozd_1A__art_1", ctx.Document.Articles.Single().Id);
		}

		[Fact]
		public void DivisionWithRomanLetterSuffix_IsRecognized_WithNormalizedEId()
		{
			var orch = new ParserOrchestrator();
			var ctx = ConnectedContext();
			Feed(orch, ctx, "DZIAŁ IVa", "Art. 5. Tresc.");

			var division = ctx.Document.RootPart.Books[0].Titles[0].Divisions[0];
			Assert.Equal("IVa", division.Number?.Value);
			Assert.Equal(4, division.Number?.NumericPart);
			Assert.Equal("dz_IVa__art_5", ctx.Document.Articles.Single().Id);
		}

		[Fact]
		public void BookWithWordOrdinal_HasCanonicalRomanEId()
		{
			// „KSIĘGA PIERWSZA" i „KSIĘGA I" muszą dać ten sam eId (ks_I) — normalizacja do rzymskiej.
			var orch = new ParserOrchestrator();
			var ctx = ConnectedContext();
			Feed(orch, ctx, "KSIĘGA PIERWSZA", "Art. 1. Tresc.");

			var book = ctx.Document.RootPart.Books[0];
			Assert.False(book.IsImplicit);
			Assert.Equal("I", book.Number?.Value);
			Assert.Equal("ks_I__art_1", ctx.Document.Articles.Single().Id);
		}

		[Fact]
		public void PartAfterArticle_IsIgnored_ArticleNotReparented()
		{
			// Strażnik EnterPart: artykuł sprzed pierwszej CZĘŚCI nie może zyskać prefiksu części (eId).
			var orch = new ParserOrchestrator();
			var ctx = ConnectedContext();
			Feed(orch, ctx, "Art. 1. Przed czescia.", "CZĘŚĆ I", "Art. 2. W czesci.");

			var ids = ctx.Document.Articles.Select(a => a.Id).ToList();
			Assert.Contains("art_1", ids);
			Assert.DoesNotContain("cz_I__art_1", ids);
			Assert.True(ctx.Document.RootPart.IsImplicit);
		}

		[Fact]
		public void TitlelessChapter_DoesNotAbsorbSentenceAsHeading()
		{
			// Zdanie treści (kończy się kropką) po nagłówku bez tytułu NIE może zostać tytułem jednostki.
			var orch = new ParserOrchestrator();
			var ctx = ConnectedContext();
			Feed(orch, ctx,
				"Rozdział 1",
				"Do postepowan wszczetych przed dniem wejscia w zycie stosuje sie przepisy dotychczasowe.");

			var chapter = ctx.Document.RootPart.Books[0].Titles[0].Divisions[0].Chapters[0];
			Assert.Equal(string.Empty, chapter.Heading);
		}

		private static bool IsSystematizing(ParagraphKind kind) => kind is
			ParagraphKind.PartUnit or ParagraphKind.BookUnit or ParagraphKind.TitleUnit or
			ParagraphKind.DivisionUnit or ParagraphKind.ChapterUnit or ParagraphKind.SubchapterUnit;

		// ============================================================
		// Audyt kompletności ParagraphKind
		// ============================================================

		[Fact]
		public void ParagraphKind_AllValuesAccountedFor()
		{
			// Kanarek: dodanie wartości ParagraphKind wymusza rewizję obsługi w StructureProcessor.Process
			// (jednostki redakcyjne, systematyzacyjne, UnitHeading, Unknown) i tej listy.
			var expected = new HashSet<ParagraphKind>
			{
				ParagraphKind.Article, ParagraphKind.Paragraph, ParagraphKind.Point, ParagraphKind.Letter,
				ParagraphKind.Tiret, ParagraphKind.WrapUp,
				ParagraphKind.PartUnit, ParagraphKind.BookUnit, ParagraphKind.TitleUnit,
				ParagraphKind.DivisionUnit, ParagraphKind.ChapterUnit, ParagraphKind.SubchapterUnit,
				ParagraphKind.UnitHeading, ParagraphKind.Unknown,
			};

			Assert.Equal(expected, Enum.GetValues<ParagraphKind>().ToHashSet());
		}
	}
}
