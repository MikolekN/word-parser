using DocumentFormat.OpenXml.Wordprocessing;
using WordParserCore.Ingest;
using WordParserCore.Services.Classify;

namespace WordParserCore.Services.Parsing
{
	/// <summary>
	/// Koordynuje jednoprzebiegowe parsowanie dokumentu prawnego.
	/// Deleguje logikę nowelizacji do <see cref="AmendmentStateManager"/>
	/// oraz budowanie encji do <see cref="StructureProcessor"/>.
	/// </summary>
	public sealed class ParserOrchestrator
	{
		private readonly IParagraphClassifier    _classifier;
		private readonly AmendmentStateManager   _amendmentManager    = new();
		private readonly StructureProcessor      _structureProcessor  = new();

		/// <summary>
		/// Konstruktor domyślny — używa ParagraphClassifier.
		/// </summary>
		public ParserOrchestrator(IParagraphClassifier? classifier = null)
		{
			_classifier = classifier ?? new ParagraphClassifier();
		}

		/// <summary>
		/// Przetwarza pojedynczy akapit OpenXml — cienki adapter na ProcessBlock,
		/// zachowany dla wstecznej kompatybilności (testy i dotychczasowi wywołujący).
		/// </summary>
		public void ProcessParagraph(Paragraph paragraph, ParsingContext context)
			=> ProcessBlock(DocxBlockReader.ToBlock(paragraph, blockIndex: null), context);

		/// <summary>
		/// Przetwarza pojedynczy blok reprezentacji pośredniej i aktualizuje stan kontekstu.
		/// Flow:
		/// 1. Obliczenie NumberingHint na podstawie bieżącego stanu kontekstu.
		/// 2. Klasyfikacja akapitu (Kind + Confidence).
		/// 3. Aktualizacja stanu nowelizacji (AmendmentStateManager).
		/// 4. Budowanie encji lub dołączanie treści nowelizacji (StructureProcessor).
		/// 5. Wykrywanie triggerów nowelizacji po zbudowaniu encji.
		/// </summary>
		public void ProcessBlock(DocumentBlock block, ParsingContext context)
		{
			if (block.IsEmpty)
				return;

			// Kontrakt adapterów (PdfBlockReader): bloki przypisów NIE są treścią jednostek
			// redakcyjnych — bez tego filtra przypis „1) Niniejsza ustawa…" z dołu strony PDF
			// zostałby sklasyfikowany tekstowo jako punkt 1) i przeinaczył treść aktu.
			// Klasyfikator dokumentu czyta je osobno (sygnały TJ) — z pełnej listy bloków.
			if (block.Role == BlockRole.FootnoteText)
				return;

			var text    = block.Text.Sanitize().Trim();
			var styleId = block.StyleId;

			var hint           = BuildNumberingHint(context);
			var classification = _classifier.Classify(new ClassificationInput(text, styleId)
			{
				NumberingHint = hint,
			});

			if (HandleAmendmentFlow(context, classification, text, styleId)) return;

			if (_structureProcessor.Process(context, classification, text, styleId, block.Layout))
				_amendmentManager.DetectTrigger(context, text);
		}

		/// <summary>
		/// Finalizuje przetwarzanie — wypróżnia bufor nowelizacji jeśli dokument
		/// kończy się wewnątrz treści nowelizacji.
		/// </summary>
		public void Finalize(ParsingContext context)
		{
			if (context.InsideAmendment || context.AmendmentCollector.IsCollecting)
			{
				_amendmentManager.Flush(context);
				context.InsideAmendment = false;
			}

			// Zapisz metadane aktu zebrane ze strefy tytułowej (rodzaj/data/przedmiot → Title/ActDate).
			context.Metadata.ApplyTo(context.Document);
		}

		// ============================================================
		// Obliczanie NumberingHint
		// ============================================================

		/// <summary>
		/// Oblicza podpowiedź numeracyjną na podstawie aktualnego stanu kontekstu.
		/// Zwraca hint dla najbardziej szczegółowego aktywnego poziomu hierarchii.
		/// </summary>
		private static NumberingHint? BuildNumberingHint(ParsingContext context)
		{
			// Poziom: litera (jeśli jest aktywna litera z numerem)
			if (context.CurrentLetter?.Number != null)
				return new NumberingHint
				{
					ExpectedKind   = ParagraphKind.Letter,
					ExpectedNumber = context.CurrentLetter.Number,
				};

			// Poziom: punkt
			if (context.CurrentPoint?.Number != null)
				return new NumberingHint
				{
					ExpectedKind   = ParagraphKind.Point,
					ExpectedNumber = context.CurrentPoint.Number,
				};

			// Poziom: ustęp (pomijamy ustępy niejawne)
			if (context.CurrentParagraph?.Number != null &&
			    context.CurrentParagraph is { IsImplicit: false })
				return new NumberingHint
				{
					ExpectedKind   = ParagraphKind.Paragraph,
					ExpectedNumber = context.CurrentParagraph.Number,
				};

			// Poziom: artykuł
			if (context.CurrentArticle?.Number != null)
				return new NumberingHint
				{
					ExpectedKind   = ParagraphKind.Article,
					ExpectedNumber = context.CurrentArticle.Number,
				};

			return null;
		}

		// ============================================================
		// Obsługa cyklu stanu nowelizacji
		// ============================================================

		/// <summary>
		/// Hermetyzuje cały cykl stanu nowelizacji. Zwraca true jeśli akapit został skonsumowany.
		/// </summary>
		private bool HandleAmendmentFlow(
			ParsingContext       context,
			ClassificationResult classification,
			string               text,
			string?              styleId)
		{
			bool wasInsideAmendment = context.InsideAmendment;
			_amendmentManager.UpdateState(context, classification, text);

			if (wasInsideAmendment && !context.InsideAmendment)
				_amendmentManager.Flush(context);

			// Guard: ShouldExitForNewParentLawTrigger jest sensowne tylko gdy bylismy JUZ w nowelizacji
			// przed wywolaniem UpdateState. Jesli wlasnie weszlismy (wasInsideAmendment=false),
			// to jestesmy przy pierwszym akapicie tresci — nie wolno go natychmiast wyrzucic,
			// nawet jesli jego tekst zawiera zwrot nowelizacyjny (np. "w brzmieniu").
			var quoteTracker = context.AmendmentCollector.QuoteTracker;
			if (wasInsideAmendment && _amendmentManager.ShouldExitForNewParentLawTrigger(context, classification, text))
			{
				if (quoteTracker.IsInsideQuote)
				{
					// Komenda nowelizacyjna WEWNATRZ otwartego cytatu to nowelizacja zagniezdzona (ZZ) —
					// cytat trwa; bez stylow nie da sie jej rozlozyc (Warning przy finalizacji).
					quoteTracker.MarkNestedTrigger();
				}
				else
				{
					_amendmentManager.Flush(context);
					context.InsideAmendment = false;
				}
			}

			if (classification.IsAmendmentContent || context.InsideAmendment)
			{
				_amendmentManager.Collect(context, text, styleId);

				// Bilans cudzyslowow domknal cytowana tresc („…") — koniec nowelizacji bez stylow (§ 94 ZTP).
				if (quoteTracker.ClosureReached)
				{
					_amendmentManager.Flush(context);
					context.InsideAmendment = false;
				}
				return true;
			}
			return false;
		}
	}
}
