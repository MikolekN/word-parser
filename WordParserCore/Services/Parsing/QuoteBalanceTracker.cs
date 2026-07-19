using System.Text.RegularExpressions;

namespace WordParserCore.Services.Parsing
{
	/// <summary>
	/// Bilans cudzysłowów treści nowelizacji bez stylów Z/* (Etap 8, § 94 ZTP: nowe brzmienie
	/// jest ujęte w cudzysłów „…"). Wyznacza granicę końca cytowanej treści tam, gdzie ścieżka
	/// stylowa ma styl ustawy matki, a bezstylowa dotąd nie miała żadnego sygnału.
	///
	/// Cykl życia (w obrębie jednego zbierania <see cref="AmendmentCollector"/>):
	/// - UZBROJENIE: pierwszy zbierany blok jest bezstylowy i zaczyna się od „ (U+201E);
	///   w przeciwnym razie tracker trwale rozbrojony — obowiązuje dotychczasowe zachowanie
	///   (wyjście na stylu ustawy matki / kolejnym triggerze / końcu dokumentu).
	/// - KAŻDY blok ze stylem rozbraja trwale — cyklem nowelizacji stylowej rządzą style (doc001 bez zmian).
	/// - BILANS: „ (U+201E) otwiera; " (U+201D) i " (U+201C, niedomyślny wariant edytorów) zamykają
	///   (głębokość nie schodzi poniżej zera). Cytaty zagnieżdżone „…„…"…" utrzymują Depth &gt; 0.
	///   Prosty cudzysłów " (U+0022) jest DWUZNACZNY (otwiera i zamyka): pary wewnątrz bloku
	///   („nazwa "programu"") są neutralne; dopiero NIEPARZYSTA liczba prostych cudzysłowów w bloku
	///   zakończonym prostym cudzysłowem liczy się jako jedno zamknięcie (konwencja mieszana „…").
	/// - ZAMKNIĘCIE: Depth == 0 i blok kończy się cudzysłowem zamykającym, z opcjonalną interpunkcją
	///   „…";  „…".  „…") — ale NIE przecinkiem: „…", oznacza kontynuację komendy mnogiej
	///   („art. 5 i 6 otrzymują brzmienie:" — kolejna cytowana jednostka następuje).
	///   → <see cref="ClosureReached"/>; orkiestrator finalizuje nowelizację.
	/// - Trigger nowelizacyjny przy Depth &gt; 0 to komenda ZAGNIEŻDŻONA (ZZ) — cytat trwa,
	///   a fakt odnotowuje <see cref="MarkNestedTrigger"/> (Warning przy finalizacji).
	///
	/// Ograniczenie (udokumentowane): dokument cytujący wyłącznie prostym " (bez „) nie uzbraja
	/// trackera — degradacja do dotychczasowego zachowania zamiast zgadywania granic.
	/// </summary>
	public sealed class QuoteBalanceTracker
	{
		/// <summary>
		/// Cudzysłów zamykający na końcu bloku (typograficzny lub prosty), z opcjonalnym nawiasem
		/// i interpunkcją kończącą (; .) po nim. Celowo BEZ przecinka — „…", kontynuuje komendę mnogą.
		/// </summary>
		private static readonly Regex ClosingQuoteAtEnd = new(
			@"[""”“]\s*\)?\s*[;.]?$", RegexOptions.Compiled);

		/// <summary>Prosty cudzysłów " na końcu bloku (z opcjonalnym nawiasem/interpunkcją) — kandydat zamknięcia konwencji mieszanej.</summary>
		private static readonly Regex StraightQuoteAtEnd = new(
			@"""\s*\)?\s*[;.]?$", RegexOptions.Compiled);

		private static bool EndsWithStraightQuote(string trimmed) => StraightQuoteAtEnd.IsMatch(trimmed);

		private bool _first = true;
		private bool _disarmed;

		/// <summary>Czy tracker jest uzbrojony (pierwszy blok bezstylowy zaczynał się od „).</summary>
		public bool IsArmed { get; private set; }

		/// <summary>Bieżąca głębokość cudzysłowów po ostatnim obserwowanym bloku (≥ 0).</summary>
		public int Depth { get; private set; }

		/// <summary>Czy ostatni obserwowany blok domknął cytat (Depth == 0 i kończy się cudzysłowem zamykającym).</summary>
		public bool ClosureReached { get; private set; }

		/// <summary>Czy wewnątrz otwartego cytatu (Depth &gt; 0) — tłumi wyjście na fałszywym triggerze.</summary>
		public bool IsInsideQuote => IsArmed && Depth > 0;

		/// <summary>Czy wewnątrz cytatu stłumiono komendę nowelizacyjną (zagnieżdżona ZZ — nierozkładalna bez stylów).</summary>
		public bool NestedTriggerSuppressed { get; private set; }

		/// <summary>
		/// Obserwuje kolejny zbierany blok treści nowelizacji.
		/// <paramref name="hasStyle"/> = blok niesie styl akapitu (rozbraja tracker na stałe).
		/// </summary>
		public void Observe(string text, bool hasStyle)
		{
			ClosureReached = false;

			if (_disarmed)
				return;

			if (hasStyle)
			{
				Disarm();
				return;
			}

			if (_first)
			{
				_first = false;
				if (!text.TrimStart().StartsWith('„'))
				{
					Disarm();
					return;
				}
				IsArmed = true;
			}

			int straightQuotes = 0;
			foreach (var c in text)
			{
				if (c == '„')
					Depth++;
				else if (c is '”' or '“')
					Depth = System.Math.Max(0, Depth - 1);
				else if (c == '"')
					straightQuotes++;
			}

			// Prosty cudzysłów: pary wewnętrzne neutralne; nieparzysta liczba w bloku zakończonym
			// prostym cudzysłowem = jedno zamknięcie cytatu zewnętrznego (konwencja mieszana „…").
			var trimmed = text.TrimEnd();
			if (straightQuotes % 2 == 1 && EndsWithStraightQuote(trimmed))
				Depth = System.Math.Max(0, Depth - 1);

			ClosureReached = IsArmed && Depth == 0 && ClosingQuoteAtEnd.IsMatch(trimmed);
		}

		/// <summary>Odnotowuje stłumioną komendę nowelizacyjną wewnątrz cytatu (ZZ).</summary>
		public void MarkNestedTrigger() => NestedTriggerSuppressed = true;

		/// <summary>Przywraca stan początkowy (nowe zbieranie nowelizacji).</summary>
		public void Reset()
		{
			_first = true;
			_disarmed = false;
			IsArmed = false;
			Depth = 0;
			ClosureReached = false;
			NestedTriggerSuppressed = false;
		}

		private void Disarm()
		{
			_disarmed = true;
			IsArmed = false;
			Depth = 0;
			ClosureReached = false;
		}
	}
}
