using System.Text.RegularExpressions;

namespace WordParserCore.Services.Classify.Document
{
	/// <summary>
	/// Współdzielone wzorce ZTP do klasyfikacji dokumentu i (docelowo) zbierania metadanych.
	/// Wszystkie prekompilowane; celowo BEZ IgnoreCase tam, gdzie wersaliki są sygnałem ZTP.
	/// Tekst wejściowy jest wcześniej znormalizowany (Sanitize: zwinięte białe znaki, en-dash→dywiz).
	/// Pełne brzmienia — Załącznik A planu przebudowy.
	/// </summary>
	internal static class ZtpPatterns
	{
		// === STREFA TYTUŁOWA (§ 16-19, § 96, § 102, § 120, § 138a) — dopasowanie do całej linii po Trim() ===

		/// <summary>
		/// § 16: samodzielny wiersz „USTAWA" (tolerancja rozstrzelenia „U S T A W A").
		/// IgnoreCase celowo: szczotki RCL renderują nagłówek jako „Ustawa" (kapitalizacja tytułowa),
		/// a wersaliki NIE są tu wiarygodnym sygnałem — walidacja korpusu (2320 aktów) wykazała, że
		/// forma „Ustawa" jest w praktyce dominująca. Wzorzec jest zakotwiczony na CAŁYM bloku
		/// (^…$), więc słowo „ustawa" w treści zdania nigdy nie stanowi całego akapitu → brak fałszywek.
		/// </summary>
		internal static readonly Regex StatuteHeaderPattern = new(
			@"^U\s*S\s*T\s*A\s*W\s*A$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

		/// <summary>
		/// § 120: „ROZPORZĄDZENIE" (także kapitalizacja tytułowa „Rozporządzenie" ze szczotek RCL)
		/// + opcjonalnie organ WIELKIMI w tej samej linii. Bez blankietowego IgnoreCase: grupa organu
		/// musi pozostać wielkoliterowa (odróżnia nazwę organu od prozy), więc wariant wielkości liter
		/// dotyczy tylko słowa kluczowego — spójnie z nagłówkami uchwały/zarządzenia.
		/// </summary>
		internal static readonly Regex RegulationHeaderPattern = new(
			@"^(?:ROZPORZĄDZENIE|Rozporządzenie)(?:\s+(?<organ>[A-ZĄĆĘŁŃÓŚŹŻ][A-ZĄĆĘŁŃÓŚŹŻ\s\-,\.]+))?$", RegexOptions.Compiled);

		/// <summary>
		/// § 102: nagłówek obwieszczenia (w tym tekstu jednolitego). Dopuszcza wersaliki „OBWIESZCZENIE",
		/// kapitalizację tytułową „Obwieszczenie" oraz zapis małą literą „obwieszczenie" — wszystkie trzy
		/// warianty występują w szczotkach RCL (różne szablony; bywa renderowany wersalikami przez w:caps).
		/// Opcjonalny organ w tej samej linii to sekwencja wyrazów rozpoczynających się WIELKĄ literą
		/// (nazwa organu w dopełniaczu: „Ministra Rodziny, Pracy i Polityki Społecznej") plus dozwolone
		/// spójniki małą literą (i/oraz/do/spraw…). KAŻDY wyraz małą literą spoza tej listy (np. czasownik)
		/// unieważnia dopasowanie — to odróżnia nagłówek od zdania prozy („Obwieszczenie Ministra… wywołało…").
		/// </summary>
		internal static readonly Regex AnnouncementHeaderPattern = new(
			@"^(?:OBWIESZCZENIE|[Oo]bwieszczenie)(?:\s+(?:[A-ZĄĆĘŁŃÓŚŹŻ][\p{L}0-9\-]*|i|oraz|do|ds|w|z|na|dla|spraw)[,\)]?)*$",
			RegexOptions.Compiled);

		// § 138a: uchwała / zarządzenie. Zakotwiczone na $ (jak pozostałe nagłówki) i tak zbudowane,
		// by NIE łapać zwykłych zdań („Uchwała wchodzi w życie…"): forma mieszana wymaga „Nr",
		// a po nazwie dopuszczalny jest tylko opcjonalny numer i organ WIELKIMI literami.
		internal static readonly Regex ResolutionHeaderPattern = new(
			@"^(?:UCHWAŁA|Uchwała)(?:\s+(?:NR|Nr|nr)\s*(?<no>[\w/\-\.]+))?(?:\s+[A-ZĄĆĘŁŃÓŚŹŻ][A-ZĄĆĘŁŃÓŚŹŻ0-9\s/\-\.,]*)?$", RegexOptions.Compiled);

		/// <summary>§ 138a: zarządzenie (nr opcjonalny).</summary>
		internal static readonly Regex OrderHeaderPattern = new(
			@"^(?:ZARZĄDZENIE|Zarządzenie)(?:\s+(?:NR|Nr|nr)\s*(?<no>[\w/\-\.]+))?(?:\s+[A-ZĄĆĘŁŃÓŚŹŻ][A-ZĄĆĘŁŃÓŚŹŻ0-9\s/\-\.,]*)?$", RegexOptions.Compiled);

		/// <summary>
		/// Organ wydający w osobnej linii (§ 120 ust. 4 — rozporządzenie).
		/// Zarezerwowane dla DocumentMetadataCollector (Etap 7) — jeszcze nieużywane przez klasyfikator.
		/// </summary>
		internal static readonly Regex IssuingOrganLinePattern = new(
			@"^(?:MINISTRA?|PREZESA\s+RADY\s+MINISTRÓW|RADY\s+MINISTRÓW|PREZYDENTA\s+RZECZYPOSPOLITEJ\s+POLSKIEJ|KRAJOWEJ\s+RADY|MARSZAŁKA\s+SEJMU)[A-ZĄĆĘŁŃÓŚŹŻ\s\-,\.]*$",
			RegexOptions.Compiled);

		/// <summary>
		/// § 102: organ wydający tekst jednolity ustawy — pełna urzędowa forma wiersza wydawcy
		/// „Marszałka Sejmu Rzeczypospolitej Polskiej" (dodatkowy sygnał TJ ustawy). Wymóg pełnej formy
		/// (a nie samego „Marszałek Sejmu") odrzuca prozę typu „Marszałek Sejmu zwołał…".
		/// </summary>
		internal static readonly Regex MarshalOfSejmIssuerPattern = new(
			@"^Marszałka\s+Sejmu\s+Rzeczypospolitej\s+Polskiej\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);

		/// <summary>§ 17: data aktu (miesiąc słownie). Dopuszcza końcowy odnośnik [N)]/[N] (TJ często opatruje datę przypisem).</summary>
		internal static readonly Regex ActDateLinePattern = new(
			@"^z\s+dnia\s+(?<day>\d{1,2})\s+(?<month>stycznia|lutego|marca|kwietnia|maja|czerwca|lipca|sierpnia|września|października|listopada|grudnia)\s+(?<year>\d{4})\s*r\.(?:\s*\[\d+\)?\])?$",
			RegexOptions.Compiled | RegexOptions.IgnoreCase);

		/// <summary>§ 18-19 / § 120 ust. 6: przedmiot aktu.</summary>
		internal static readonly Regex ActSubjectPattern = new(
			@"^(?:o\s+\p{Ll}.+|w\s+sprawie\s+.+|(?:Kodeks|Prawo|Ordynacja)\b.*|Przepisy\s+wprowadzające\b.+)$",
			RegexOptions.Compiled);

		/// <summary>§ 96: tytuł zmieniający (akt nowelizujący).</summary>
		internal static readonly Regex AmendingTitlePattern = new(
			@"o\s+zmianie\s+ustaw(?:y|)\b|oraz\s+niektórych\s+innych\s+ustaw|zmieniając[ea]\s+(?:rozporządzenie|uchwałę|zarządzenie)\s+w\s+sprawie",
			RegexOptions.Compiled | RegexOptions.IgnoreCase);

		/// <summary>
		/// § 102: obwieszczenie o ogłoszeniu tekstu jednolitego. Zakotwiczone na początku wiersza —
		/// to ma być samodzielny wiersz przedmiotu („w sprawie ogłoszenia jednolitego tekstu ustawy…"),
		/// a nie wzmianka w prozie („ukaże się obwieszczenie w sprawie ogłoszenia jednolitego tekstu…").
		/// </summary>
		internal static readonly Regex ConsolidatedTextTitlePattern = new(
			@"^w\s+sprawie\s+ogłoszenia\s+jednolitego\s+tekstu", RegexOptions.Compiled | RegexOptions.IgnoreCase);

		// === KORPUS (§ 121, § 106, § 45, § 162, § 82-85) ===

		/// <summary>§ 121: podstawa prawna aktu wykonawczego.</summary>
		internal static readonly Regex LegalBasisPattern = new(
			@"^Na\s+podstawie\s+art\.\s*\d+\w*", RegexOptions.Compiled | RegexOptions.IgnoreCase);

		/// <summary>§ 121: formuła kompetencyjna (czasownik różnicuje typ).</summary>
		internal static readonly Regex EnactmentFormulaPattern = new(
			@"(?<verb>zarządza|uchwala|postanawia)\s+się,?\s+co\s+następuje\s*:", RegexOptions.Compiled | RegexOptions.IgnoreCase);

		/// <summary>
		/// § 104: formuła obwieszczenia tekstu jednolitego (art. 16 ustawy o ogłaszaniu). Dowolny numer
		/// ustępu oraz opcjonalna wstawka „zdanie pierwsze/drugie" — realne brzmienie to „art. 16 ust. 1
		/// zdanie pierwsze ustawy z dnia 20 lipca 2000 r. …".
		/// </summary>
		internal static readonly Regex ConsolidatedTextFormulaPattern = new(
			@"Na\s+podstawie\s+art\.\s*16\s+ust\.\s*\d+(?:\s+zdanie\s+\w+)?\s+ustawy\s+z\s+dnia\s+20\s+lipca\s+2000\s+r\.\s+o\s+ogłaszaniu\s+aktów\s+normatywnych.*?ogłasza\s+się\s+w\s+załączniku",
			RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

		/// <summary>Jednostka podstawowa „Art." (ustawa).</summary>
		internal static readonly Regex ArticleUnitStatPattern = new(@"^Art\.\s*\d+", RegexOptions.Compiled);

		/// <summary>Jednostka podstawowa „§" (rozporządzenie/uchwała/APM).</summary>
		internal static readonly Regex SectionUnitStatPattern = new(@"^§\s*\d+\w*\.?\s+", RegexOptions.Compiled);

		/// <summary>
		/// § 45: wejście w życie (podmiot zdania różnicuje typ). Opcjonalny prefiks jednostki,
		/// bo w realnych aktach klauzula jest treścią artykułu/paragrafu („Art. 15. Ustawa wchodzi…").
		/// </summary>
		internal static readonly Regex EntryIntoForcePattern = new(
			@"^(?:(?:Art\.|§)\s*\d+[a-z]*\.?\s*)?(?<subject>Ustawa|Rozporządzenie|Uchwała|Zarządzenie|Niniejsz[ae]\s+(?:ustawa|rozporządzenie|uchwała|zarządzenie))\s+wchodzi\s+w\s+życie\s+(?:po\s+upływie|z\s+dniem|pierwszego\s+dnia|następnego\s+dnia|w\s+terminie)",
			RegexOptions.Compiled | RegexOptions.IgnoreCase);

		/// <summary>
		/// § 162: publikatory Dz. U. / M.P.
		/// Zarezerwowane dla DocumentMetadataCollector (Etap 7) — jeszcze nieużywane przez klasyfikator.
		/// </summary>
		internal static readonly Regex JournalCitationPattern = new(
			@"(?:Dz\.\s*U\.|M\.\s*P\.)\s*(?:z\s*\d{4}\s*r\.)?\s*(?:Nr\s*\d+[,\s]*)?poz\.\s*\d+", RegexOptions.Compiled);

		/// <summary>§ 162: publikator wojewódzki → akt prawa miejscowego.</summary>
		internal static readonly Regex VoivodeshipJournalPattern = new(@"Dz\.\s*Urz\.\s*Woj\.", RegexOptions.Compiled);

		/// <summary>§ 106/§ 106a-b: markery tekstu jednolitego (warianty rodzajowe).</summary>
		internal static readonly Regex RepealedMarkerPattern = new(
			@"\(\s*(?:uchylon[yae]|utracił[a]?\s+moc|uznan[yae]\s+za\s+nieważn[yae])\s*\)",
			RegexOptions.Compiled | RegexOptions.IgnoreCase);

		/// <summary>§ 82-85 (odpowiednio § 132/141/143): wprowadzenie zmian — wszystkie rodzaje aktów.</summary>
		internal static readonly Regex AmendmentIntroPattern = new(
			@"[Ww]\s+(?:ustawie|rozporządzeniu|uchwale|zarządzeniu)\s+.*?wprowadza\s+się\s+następujące\s+zmiany\s*:",
			RegexOptions.Compiled | RegexOptions.Singleline);

		/// <summary>Organy JST → akt prawa miejscowego.</summary>
		internal static readonly Regex LocalGovernmentOrganPattern = new(
			@"\b(?:RAD[AY]\s+(?:GMINY|MIASTA|MIEJSK(?:A|IE|IEJ)|POWIATU)|SEJMIK(?:U)?\s+WOJEWÓDZTWA|WOJEWOD[AY]|BURMISTRZ|WÓJT|PREZYDENT\s+MIASTA|STAROST[AY])\b",
			RegexOptions.Compiled | RegexOptions.IgnoreCase);

		/// <summary>
		/// Odnośniki tekstu jednolitego w kanale indeksu górnego [N)] (§ 163). Nawias wymagany —
		/// inaczej wzorzec łapałby też indeks górny numeru jednostki [N] (§ 89 ust. 6), zawyżając licznik.
		/// </summary>
		internal static readonly Regex FootnoteRefPattern = new(@"\[\d+\)\]", RegexOptions.Compiled);
	}
}
