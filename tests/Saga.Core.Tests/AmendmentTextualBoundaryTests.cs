using System.Linq;
using ModelDto;
using WordParserCore.Ingest;
using WordParserCore.Services.Parsing;
using Xunit;

namespace WordParserCore.Tests
{
	/// <summary>
	/// Testy granic nowelizacji bez stylów Z/* (Etap 8): bilans cudzysłowów wyznacza koniec
	/// cytowanej treści, treść po zamknięciu wraca do ustawy matki, komendy zagnieżdżone
	/// w cytacie są tłumione, a zamiana wyrazów tworzy natychmiastową nowelizację.
	/// </summary>
	public class AmendmentTextualBoundaryTests
	{
		private static (ParserOrchestrator orch, ParsingContext ctx) NewPipeline()
		{
			var document = new LegalDocument { Type = LegalActType.Statute };
			var subchapter = document.RootPart.Books[0].Titles[0].Divisions[0].Chapters[0].Subchapters[0];
			return (new ParserOrchestrator(), new ParsingContext(document, subchapter));
		}

		private static DocumentBlock Blk(string text) => new()
		{
			Text = text,
			Source = new BlockSourceLocation { BlockIndex = null },
		};

		private static void Feed(ParserOrchestrator orch, ParsingContext ctx, params string[] lines)
		{
			foreach (var line in lines)
				orch.ProcessBlock(Blk(line), ctx);
		}

		[Fact]
		public void QuotedAmendment_ClosesOnQuoteBalance_FollowingArticleNotSwallowed()
		{
			// KLUCZOWY scenariusz Etapu 8: bez bilansu cudzysłowów końcowy artykuł aktu zmieniającego
			// (bez triggera w treści) byłby połknięty do treści nowelizacji.
			var (orch, ctx) = NewPipeline();

			Feed(orch, ctx,
				"Art. 1. W ustawie wprowadza się następujące zmiany:",
				"1) art. 5 otrzymuje brzmienie:",
				"„Art. 5. Nowe brzmienie artykułu.”;",
				"Art. 2. Ustawa wchodzi w życie po upływie 14 dni od dnia ogłoszenia.");
			orch.Finalize(ctx);

			var articles = ctx.Document.Articles.ToList();
			Assert.Equal(2, articles.Count);
			Assert.Equal("2", articles[1].Number?.Value);

			var point = articles[0].Paragraphs[0].Points.Single();
			Assert.NotNull(point.Amendment);
			Assert.Equal(AmendmentOperationType.Modification, point.Amendment!.OperationType);
			Assert.Single(point.Amendment.Content!.Articles);
			Assert.False(ctx.InsideAmendment);
		}

		[Fact]
		public void MultiBlockQuotedAmendment_StaysOpenUntilBalanceCloses()
		{
			var (orch, ctx) = NewPipeline();

			Feed(orch, ctx,
				"Art. 1. W ustawie wprowadza się następujące zmiany:",
				"1) art. 5 otrzymuje brzmienie:",
				"„Art. 5. Prawo podlega ograniczeniu:",
				"1) w zakresie tajemnic chronionych;",
				"2) ze względu na prywatność.”;",
				"2) w art. 7 uchyla się ust. 2;");
			orch.Finalize(ctx);

			var points = ctx.Document.Articles.First().Paragraphs[0].Points;
			Assert.Equal(2, points.Count);

			// Punkt 1: nowelizacja z artykułem i dwoma punktami cytowanej treści.
			var amendment = points[0].Amendment;
			Assert.NotNull(amendment);
			var quotedArticle = amendment!.Content!.Articles.Single();
			Assert.Equal(2, quotedArticle.Paragraphs[0].Points.Count);

			// Punkt 2 wrócił do ustawy matki i sam ma nowelizację (uchylenie).
			Assert.Equal(AmendmentOperationType.Repeal, points[1].Amendment?.OperationType);
		}

		[Fact]
		public void NestedTriggerInsideQuote_IsSuppressed_SingleAmendmentWithWarning()
		{
			// Cytowana treść sama zawiera komendę nowelizacyjną (ZZ) — cytat nie może zostać rozcięty.
			var (orch, ctx) = NewPipeline();

			Feed(orch, ctx,
				"Art. 1. W ustawie wprowadza się następujące zmiany:",
				"1) art. 5 otrzymuje brzmienie:",
				"„Art. 5. W ustawie o podatku rolnym wprowadza się zmiany:",
				"1) art. 3 otrzymuje brzmienie:",
				"2) uchyla się art. 4.”;",
				"2) w art. 7 uchyla się ust. 2;");
			orch.Finalize(ctx);

			var points = ctx.Document.Articles.First().Paragraphs[0].Points;
			Assert.Equal(2, points.Count);

			// Jedna nowelizacja na punkcie 1 (nierozcięta) + Warning o zagnieżdżeniu.
			Assert.NotNull(points[0].Amendment);
			Assert.Contains(points[0].ValidationMessages,
				m => m.Level == ValidationLevel.Warning && m.Message.Contains("zagnieżdżoną"));

			// Punkt 2 wrócił do ustawy matki.
			Assert.Equal(AmendmentOperationType.Repeal, points[1].Amendment?.OperationType);
		}

		[Fact]
		public void UnquotedStylelessAmendment_KeepsLegacyTriggerBoundary()
		{
			// Treść bez cudzysłowu (tracker nieuzbrojony) — obowiązuje dotychczasowa granica:
			// wyjście na kolejnym akapicie z komendą nowelizacyjną.
			var (orch, ctx) = NewPipeline();

			Feed(orch, ctx,
				"Art. 1. W ustawie wprowadza się następujące zmiany:",
				"1) art. 5 otrzymuje brzmienie:",
				"Art. 5. Nowe brzmienie bez cudzysłowu.",
				"2) w art. 7 uchyla się ust. 2;");
			orch.Finalize(ctx);

			var points = ctx.Document.Articles.First().Paragraphs[0].Points;
			Assert.Equal(2, points.Count);
			Assert.NotNull(points[0].Amendment);
			Assert.Equal(AmendmentOperationType.Repeal, points[1].Amendment?.OperationType);
		}

		[Fact]
		public void ReplaceWordsCommand_CreatesImmediateModificationAmendment()
		{
			var (orch, ctx) = NewPipeline();

			Feed(orch, ctx,
				"Art. 1. W ustawie wprowadza się następujące zmiany:",
				"1) w art. 9 ust. 1 wyrazy „trzech dni” zastępuje się wyrazami „siedmiu dni”;",
				"2) w art. 7 uchyla się ust. 2;");
			orch.Finalize(ctx);

			var points = ctx.Document.Articles.First().Paragraphs[0].Points;
			Assert.Equal(2, points.Count);

			var replaceAmendment = points[0].Amendment;
			Assert.NotNull(replaceAmendment);
			Assert.Equal(AmendmentOperationType.Modification, replaceAmendment!.OperationType);
			Assert.Equal("siedmiu dni", replaceAmendment.Content?.PlainText);
			Assert.False(ctx.InsideAmendment);

			// Cel z referencji strukturalnej triggera (art. 9 ust. 1).
			var target = replaceAmendment.Targets.Single();
			Assert.Equal("9", target.Structure.Article);
			Assert.Equal("1", target.Structure.Paragraph);
		}

		[Fact]
		public void TiretTarget_EnrichedFromCommand()
		{
			// LegalReferenceService nie zna jednostki „tiret" — komponent celu uzupełnia komenda tekstowa.
			var (orch, ctx) = NewPipeline();

			Feed(orch, ctx,
				"Art. 1. W ustawie wprowadza się następujące zmiany:",
				"1) w art. 5 ust. 2 pkt 3 lit. b tiret 2 otrzymuje brzmienie:",
				"„– nowe brzmienie tiretu,”;",
				"Art. 2. Ustawa wchodzi w życie z dniem ogłoszenia.");
			orch.Finalize(ctx);

			var point = ctx.Document.Articles.First().Paragraphs[0].Points.Single();
			var target = point.Amendment?.Targets.Single();
			Assert.NotNull(target);
			Assert.Equal("5", target!.Structure.Article);
			Assert.Equal("2", target.Structure.Tiret);
		}

		[Fact]
		public void TiretsInQuotedContent_FlaggedAsDepthUnresolvable()
		{
			var (orch, ctx) = NewPipeline();

			Feed(orch, ctx,
				"Art. 1. W ustawie wprowadza się następujące zmiany:",
				"1) w art. 5 lit. b otrzymuje brzmienie:",
				"„b) litera z tiretami:",
				"– tiret pierwszy,",
				"– tiret drugi.”;",
				"Art. 2. Ustawa wchodzi w życie z dniem ogłoszenia.");
			orch.Finalize(ctx);

			var point = ctx.Document.Articles.First().Paragraphs[0].Points.Single();
			Assert.NotNull(point.Amendment);
			Assert.Contains(point.ValidationMessages,
				m => m.Level == ValidationLevel.Warning && m.Message.Contains("głębokość", System.StringComparison.OrdinalIgnoreCase));
		}

		// ============================================================
		// Regresje z przeglądu adwersaryjnego Etapu 8
		// ============================================================

		[Fact]
		public void RepealVerbInsideReplacedWording_IsModification_NotRepeal()
		{
			// „wyrazy „uchyla się" zastępuje się…" — czasownik uchylenia WEWNĄTRZ cytowanych wyrazów
			// nie może przeinaczyć operacji na Repeal (i zgubić nowego brzmienia).
			var (orch, ctx) = NewPipeline();

			Feed(orch, ctx,
				"Art. 1. W ustawie wprowadza się następujące zmiany:",
				"1) w art. 3 wyrazy „uchyla się” zastępuje się wyrazami „traci moc”;");
			orch.Finalize(ctx);

			var point = ctx.Document.Articles.First().Paragraphs[0].Points.Single();
			Assert.NotNull(point.Amendment);
			Assert.Equal(AmendmentOperationType.Modification, point.Amendment!.OperationType);
			Assert.Equal("traci moc", point.Amendment.Content?.PlainText);
		}

		[Fact]
		public void CompoundReplaceAndChange_TriggerWins_QuotedBlockCollected_WithWarning()
		{
			// Komenda złożona: zamiana wyrazów + „otrzymuje brzmienie:" w jednym punkcie —
			// cytowany blok NIE może wyciec do struktury ustawy matki (regresja krytyczna przeglądu).
			var (orch, ctx) = NewPipeline();

			Feed(orch, ctx,
				"Art. 1. W ustawie wprowadza się następujące zmiany:",
				"1) w art. 5 w ust. 1 wyrazy „trzech dni” zastępuje się wyrazami „siedmiu dni”, a ust. 2 otrzymuje brzmienie:",
				"„2. Nowe brzmienie ustępu drugiego.”;",
				"Art. 2. Ustawa wchodzi w życie z dniem ogłoszenia.");
			orch.Finalize(ctx);

			var articles = ctx.Document.Articles.ToList();
			Assert.Equal(2, articles.Count);

			var point = articles[0].Paragraphs[0].Points.Single();
			Assert.NotNull(point.Amendment);
			Assert.Equal(AmendmentOperationType.Modification, point.Amendment!.OperationType);
			// Cytowany blok trafił do treści nowelizacji, nie do ustawy zmieniającej.
			Assert.Single(point.Amendment.Content!.Paragraphs);
			Assert.Contains(point.ValidationMessages,
				m => m.Level == ValidationLevel.Warning && m.Message.Contains("złożona"));
		}

		[Fact]
		public void CompoundRepealAndReplace_RepealWins_WithWarning()
		{
			var (orch, ctx) = NewPipeline();

			Feed(orch, ctx,
				"Art. 1. W ustawie wprowadza się następujące zmiany:",
				"1) w art. 7 uchyla się ust. 2 oraz w ust. 4 wyrazy „trzech dni” zastępuje się wyrazami „siedmiu dni”.");
			orch.Finalize(ctx);

			var point = ctx.Document.Articles.First().Paragraphs[0].Points.Single();
			Assert.Equal(AmendmentOperationType.Repeal, point.Amendment?.OperationType);
			Assert.Contains(point.ValidationMessages,
				m => m.Level == ValidationLevel.Warning && m.Message.Contains("złożona"));
		}

		[Fact]
		public void WordDeletionCommand_DoesNotBecomeFalseUnitRepeal()
		{
			// § 87 ust. 3 pkt 3: „skreśla się wyrazy „X"" usuwa WYRAZY, nie jednostkę — nie może
			// stworzyć fałszywego uchylenia art. 5; ślad zostaje jako Warning, tekst werbatim.
			var (orch, ctx) = NewPipeline();

			Feed(orch, ctx,
				"Art. 1. W ustawie wprowadza się następujące zmiany:",
				"1) w art. 5 skreśla się wyrazy „po zasięgnięciu opinii”;");
			orch.Finalize(ctx);

			var point = ctx.Document.Articles.First().Paragraphs[0].Points.Single();
			Assert.Null(point.Amendment);
			Assert.Contains(point.ValidationMessages,
				m => m.Level == ValidationLevel.Warning && m.Message.Contains("wyrazowa"));
		}

		[Fact]
		public void LegacyRepealVerb_SkreslaSie_CreatesRepealAmendment()
		{
			var (orch, ctx) = NewPipeline();

			Feed(orch, ctx,
				"Art. 1. W ustawie wprowadza się następujące zmiany:",
				"1) w art. 5 skreśla się ust. 2;");
			orch.Finalize(ctx);

			var point = ctx.Document.Articles.First().Paragraphs[0].Points.Single();
			Assert.Equal(AmendmentOperationType.Repeal, point.Amendment?.OperationType);
		}

		[Fact]
		public void MultiUnitChange_SeparatelyQuotedUnits_AllStayInOneAmendment()
		{
			// „art. 5 i 6 otrzymują brzmienie:" — jednostki cytowane OSOBNO, rozdzielone „…", —
			// przecinek oznacza kontynuację; „Art. 6 nie może stać się artykułem ustawy matki.
			var (orch, ctx) = NewPipeline();

			Feed(orch, ctx,
				"Art. 1. W ustawie wprowadza się następujące zmiany:",
				"1) art. 5 i 6 otrzymują brzmienie:",
				"„Art. 5. Brzmienie piąte.”,",
				"„Art. 6. Brzmienie szóste.”;",
				"Art. 2. Ustawa wchodzi w życie z dniem ogłoszenia.");
			orch.Finalize(ctx);

			var articles = ctx.Document.Articles.ToList();
			Assert.Equal(2, articles.Count);   // TYLKO art. 1 i art. 2 ustawy zmieniającej
			Assert.Equal("2", articles[1].Number?.Value);

			var amendment = articles[0].Paragraphs[0].Points.Single().Amendment;
			Assert.Equal(2, amendment!.Content!.Articles.Count);
			Assert.Equal("5", amendment.Content.Articles[0].Number?.Value);
			Assert.Equal("6", amendment.Content.Articles[1].Number?.Value);
		}

		[Fact]
		public void DefaultEditorStyle_DoesNotDisarmQuoteTracking()
		{
			// Styl „Normalny" (domyślny styl edytora, bez semantyki) nie może wyłączyć granic
			// cudzysłowowych — inaczej cały Etap 8 byłby martwy dla DOCX bez szablonu.
			var (orch, ctx) = NewPipeline();

			void FeedStyled(string text) => orch.ProcessBlock(new DocumentBlock
			{
				Text = text, StyleId = "Normalny",
				Source = new BlockSourceLocation { BlockIndex = null },
			}, ctx);

			FeedStyled("Art. 1. W ustawie wprowadza się następujące zmiany:");
			FeedStyled("1) art. 5 otrzymuje brzmienie:");
			FeedStyled("„Art. 5. Nowe brzmienie artykułu.”;");
			FeedStyled("Art. 2. Ustawa wchodzi w życie z dniem ogłoszenia.");
			orch.Finalize(ctx);

			Assert.Equal(2, ctx.Document.Articles.Count());
			Assert.NotNull(ctx.Document.Articles.First().Paragraphs[0].Points.Single().Amendment);
		}

		[Fact]
		public void UnclosedQuoteAtEndOfDocument_GetsBoundaryWarning()
		{
			// Cytat niedomknięty (literówka/OCR) — granica niepewna, wchłonięcie reszty dokumentu
			// musi zostawić twardy ślad diagnostyczny.
			var (orch, ctx) = NewPipeline();

			Feed(orch, ctx,
				"Art. 1. W ustawie wprowadza się następujące zmiany:",
				"1) art. 5 otrzymuje brzmienie:",
				"„Art. 5. Brzmienie bez domknięcia cudzysłowu;",
				"Art. 2. Ustawa wchodzi w życie z dniem ogłoszenia.");
			orch.Finalize(ctx);

			var point = ctx.Document.Articles.First().Paragraphs[0].Points.Single();
			Assert.Contains(point.ValidationMessages,
				m => m.Level == ValidationLevel.Warning && m.Message.Contains("niedomknięty"));
		}

		[Fact]
		public void TiretTargetWithWordOrdinal_EnrichedFromCanonicalForm()
		{
			// § 57 ust. 6 ZTP: tiret powołuje się liczebnikiem porządkowym — „tiret drugie" to forma
			// KANONICZNA i musi zasilić referencję celu (nie generować warningu).
			var (orch, ctx) = NewPipeline();

			Feed(orch, ctx,
				"Art. 1. W ustawie wprowadza się następujące zmiany:",
				"1) w art. 5 ust. 2 pkt 3 lit. b tiret drugie otrzymuje brzmienie:",
				"„– nowe brzmienie tiretu,”;",
				"Art. 2. Ustawa wchodzi w życie z dniem ogłoszenia.");
			orch.Finalize(ctx);

			var point = ctx.Document.Articles.First().Paragraphs[0].Points.Single();
			var target = point.Amendment?.Targets.Single();
			Assert.Equal("2", target?.Structure.Tiret);
			Assert.DoesNotContain(point.ValidationMessages,
				m => m.Message.Contains("liczebnik słowny"));
		}

		[Fact]
		public void ReplaceWordsPoint_AfterDisarmedCollection_ClosesPreviousAmendment()
		{
			// Symetria z DetectTrigger: punkt z komendą zamiany wyrazów zamyka poprzednią nowelizację
			// (tracker nieuzbrojony — treść bez cudzysłowu otwierającego).
			var (orch, ctx) = NewPipeline();

			Feed(orch, ctx,
				"Art. 1. W ustawie wprowadza się następujące zmiany:",
				"1) art. 5 otrzymuje brzmienie:",
				"Art. 5. Brzmienie bez cudzysłowu otwierającego.",
				"2) w art. 9 wyrazy „trzech dni” zastępuje się wyrazami „siedmiu dni”;");
			orch.Finalize(ctx);

			var points = ctx.Document.Articles.First().Paragraphs[0].Points;
			Assert.Equal(2, points.Count);
			Assert.NotNull(points[0].Amendment);
			Assert.Equal("siedmiu dni", points[1].Amendment?.Content?.PlainText);
		}

		[Fact]
		public void QuoteInProse_DoesNotCloseAmendmentPrematurely()
		{
			// Cudzysłów wewnętrzny zamykający się W ŚRODKU bloku nie kończy nowelizacji —
			// zamknięcie wymaga cudzysłowu na KOŃCU bloku przy głębokości zero.
			var (orch, ctx) = NewPipeline();

			Feed(orch, ctx,
				"Art. 1. W ustawie wprowadza się następujące zmiany:",
				"1) art. 5 otrzymuje brzmienie:",
				"„Art. 5. Wyraz „dom” oznacza budynek;",
				"2. Przepis stosuje się odpowiednio.”;",
				"Art. 2. Ustawa wchodzi w życie z dniem ogłoszenia.");
			orch.Finalize(ctx);

			var point = ctx.Document.Articles.First().Paragraphs[0].Points.Single();
			var quotedArticle = point.Amendment!.Content!.Articles.Single();

			// Oba bloki cytatu weszły do treści nowelizacji (drugi jako ust. 2).
			Assert.Equal(2, quotedArticle.Paragraphs.Count);
			Assert.Equal(2, ctx.Document.Articles.Count());
		}
	}
}
