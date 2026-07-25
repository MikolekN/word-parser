using Saga.Core.Services.Parsing;
using Xunit;

namespace Saga.Core.Tests
{
	/// <summary>
	/// Testy bilansu cudzysłowów (Etap 8a): uzbrojenie na pierwszym bezstylowym bloku od „,
	/// rozbrojenie stylem/brakiem cudzysłowu, głębokość przez wiele bloków, zamknięcie
	/// z interpunkcją listy, cudzysłowy wewnętrzne, tłumienie zagnieżdżonych komend.
	/// </summary>
	public class QuoteBalanceTrackerTests
	{
		[Fact]
		public void FirstUnstyledBlockStartingWithQuote_Arms()
		{
			var t = new QuoteBalanceTracker();
			t.Observe("„Art. 5. Nowe brzmienie:", hasStyle: false);

			Assert.True(t.IsArmed);
			Assert.Equal(1, t.Depth);
			Assert.True(t.IsInsideQuote);
			Assert.False(t.ClosureReached);
		}

		[Fact]
		public void FirstBlockWithoutQuote_DisarmsPermanently()
		{
			var t = new QuoteBalanceTracker();
			t.Observe("Art. 5. Bez cudzysłowu.", hasStyle: false);
			t.Observe("„Teraz z cudzysłowem.”", hasStyle: false);

			Assert.False(t.IsArmed);
			Assert.False(t.ClosureReached);
		}

		[Fact]
		public void StyledBlock_DisarmsPermanently()
		{
			var t = new QuoteBalanceTracker();
			t.Observe("„Art. 5. Nowe brzmienie:", hasStyle: true);
			t.Observe("„Kolejny blok.”", hasStyle: false);

			Assert.False(t.IsArmed);
			Assert.False(t.ClosureReached);
		}

		[Fact]
		public void StyledBlockMidCollection_Disarms()
		{
			var t = new QuoteBalanceTracker();
			t.Observe("„Art. 5. Nowe brzmienie:", hasStyle: false);
			Assert.True(t.IsArmed);

			t.Observe("1) punkt ze stylem;", hasStyle: true);
			Assert.False(t.IsArmed);
			Assert.False(t.IsInsideQuote);
		}

		[Theory]
		[InlineData("2) ostatni punkt.”;")]
		[InlineData("2) ostatni punkt.”.")]
		[InlineData("2) ostatni punkt.”")]
		[InlineData("2) ostatni punkt.\";")]  // prosty cudzysłów zamykający (sloppy TXT)
		public void ClosingQuoteAtBlockEnd_ReachesClosure(string closing)
		{
			var t = new QuoteBalanceTracker();
			t.Observe("„Art. 5. Nowe brzmienie:", hasStyle: false);
			t.Observe("1) punkt pierwszy;", hasStyle: false);
			Assert.True(t.IsInsideQuote);

			t.Observe(closing, hasStyle: false);
			Assert.True(t.ClosureReached);
			Assert.Equal(0, t.Depth);
		}

		[Fact]
		public void InnerQuotedWords_DoNotCloseOuterQuote()
		{
			var t = new QuoteBalanceTracker();
			t.Observe("„2. Wyraz „dom” oznacza budynek mieszkalny;", hasStyle: false);

			// „(1) „(2) ”(1) — cytat zewnętrzny nadal otwarty; blok nie kończy się zamknięciem cytatu.
			Assert.Equal(1, t.Depth);
			Assert.True(t.IsInsideQuote);
			Assert.False(t.ClosureReached);
		}

		[Fact]
		public void ClosingQuoteMidBlock_DoesNotReachClosure()
		{
			// Głębokość spada do zera W ŚRODKU bloku, ale blok nie KOŃCZY się cudzysłowem —
			// zamknięcie wymaga obu warunków (ochrona przed cudzysłowami w prozie).
			var t = new QuoteBalanceTracker();
			t.Observe("„krótki cytat” oraz dalsza treść bloku", hasStyle: false);

			Assert.Equal(0, t.Depth);
			Assert.False(t.ClosureReached);
		}

		[Fact]
		public void DepthNeverDropsBelowZero()
		{
			var t = new QuoteBalanceTracker();
			t.Observe("„tekst” z nadmiarowym ” zamknięciem”", hasStyle: false);

			Assert.Equal(0, t.Depth);
		}

		[Fact]
		public void SingleBlockQuote_ClosesImmediately()
		{
			var t = new QuoteBalanceTracker();
			t.Observe("„Art. 5. Całość w jednym bloku.”;", hasStyle: false);

			Assert.True(t.ClosureReached);
		}

		// ============================================================
		// Regresje z przeglądu adwersaryjnego Etapu 8
		// ============================================================

		[Theory]
		[InlineData("„Art. 5. W programie \"Rodzina 500+\" wprowadza się zmiany;")]  // para prostych w środku
		[InlineData("„2. Przepisy ustawy zwanej dalej \"ustawą\".")]                  // para prostych na końcu bloku
		public void InnerStraightQuotePair_DoesNotCloseOuterQuote(string block)
		{
			// Prosty cudzysłów parami (parzysta liczba w bloku) jest neutralny — nie zamyka cytatu „…".
			var t = new QuoteBalanceTracker();
			t.Observe(block, hasStyle: false);

			Assert.Equal(1, t.Depth);
			Assert.True(t.IsInsideQuote);
			Assert.False(t.ClosureReached);
		}

		[Fact]
		public void MixedConvention_OpenTypographicCloseStraight_Closes()
		{
			// Konwencja mieszana „…": nieparzysty prosty cudzysłów na końcu bloku zamyka.
			var t = new QuoteBalanceTracker();
			t.Observe("„Art. 5. Nowe brzmienie:", hasStyle: false);
			t.Observe("2) ostatni punkt.\";", hasStyle: false);

			Assert.True(t.ClosureReached);
		}

		[Fact]
		public void TrailingComma_MeansMultiUnitContinuation_NoClosure()
		{
			// „art. 5 i 6 otrzymują brzmienie:" — po pierwszej jednostce „…", następuje kolejna cytowana.
			var t = new QuoteBalanceTracker();
			t.Observe("„Art. 5. Brzmienie piąte.”,", hasStyle: false);

			Assert.False(t.ClosureReached);

			t.Observe("„Art. 6. Brzmienie szóste.”;", hasStyle: false);
			Assert.True(t.ClosureReached);
		}

		[Fact]
		public void LeftDoubleQuotationMark_U201C_ClosesQuote()
		{
			// Niedomyślny wariant edytora: „…“ (U+201C jako zamykający) — bilans musi się domknąć,
			// inaczej tłumienie ZZ wchłaniałoby resztę dokumentu.
			var t = new QuoteBalanceTracker();
			t.Observe("„Art. 5. Nowe brzmienie.“;", hasStyle: false);

			Assert.True(t.ClosureReached);
		}

		[Fact]
		public void ClosingQuoteBeforeParenthesis_Closes()
		{
			var t = new QuoteBalanceTracker();
			t.Observe("„Art. 5. Nowe brzmienie.”);", hasStyle: false);

			Assert.True(t.ClosureReached);
		}

		[Fact]
		public void MarkNestedTrigger_AndReset_RestoresInitialState()
		{
			var t = new QuoteBalanceTracker();
			t.Observe("„Art. 5. Nowe brzmienie:", hasStyle: false);
			t.MarkNestedTrigger();
			Assert.True(t.NestedTriggerSuppressed);

			t.Reset();
			Assert.False(t.IsArmed);
			Assert.Equal(0, t.Depth);
			Assert.False(t.NestedTriggerSuppressed);
			Assert.False(t.ClosureReached);

			// Po resecie uzbraja się na nowo.
			t.Observe("„Nowe zbieranie.”", hasStyle: false);
			Assert.True(t.IsArmed);
			Assert.True(t.ClosureReached);
		}
	}
}
