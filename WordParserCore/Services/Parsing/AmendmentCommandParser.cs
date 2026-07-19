using System.Text.RegularExpressions;
using WordParserCore.Helpers;

namespace WordParserCore.Services.Parsing
{
	/// <summary>
	/// Rodzaj komendy nowelizacyjnej rozpoznanej z treści (§ 82-97 ZTP).
	/// </summary>
	public enum AmendmentCommandKind
	{
		/// <summary>„… otrzymuje brzmienie:" — zmiana brzmienia (cytowana treść następuje po komendzie).</summary>
		Change,
		/// <summary>„po art. X dodaje się art. Xa w brzmieniu:" — dodanie jednostki (cytowana treść następuje).</summary>
		Add,
		/// <summary>„uchyla się …" — uchylenie (bez treści).</summary>
		Repeal,
		/// <summary>„wyrazy „X" zastępuje się wyrazami „Y"" — zamiana wyrazów (komenda kompletna w jednym akapicie).</summary>
		ReplaceWords,
	}

	/// <summary>
	/// Komenda nowelizacyjna wywnioskowana z treści akapitu-triggera.
	/// Źródło o NIŻSZYM priorytecie niż mapa stylów (styl zawsze dominuje).
	/// </summary>
	public sealed record AmendmentCommand
	{
		public required AmendmentCommandKind Kind { get; init; }

		/// <summary>Rodzaj jednostki będącej bezpośrednim przedmiotem komendy (token przy czasowniku).</summary>
		public AmendmentTargetKind TargetKind { get; init; } = AmendmentTargetKind.Unknown;

		/// <summary>Oznaczenie jednostki-przedmiotu komendy (np. „7a", „2", „b"); null gdy nierozpoznane.</summary>
		public string? TargetNumber { get; init; }

		/// <summary>ReplaceWords: brzmienie zastępowane (stare wyrazy).</summary>
		public string? OldWording { get; init; }

		/// <summary>ReplaceWords: brzmienie zastępujące (nowe wyrazy).</summary>
		public string? NewWording { get; init; }

		/// <summary>Pełny dopasowany tekst komendy (do wycinania przy analizie komend złożonych).</summary>
		public string? MatchedCommandText { get; init; }

		/// <summary>
		/// Wartość liczbowa oznaczenia jednostki: cyfrowa („2", „7a"→7) lub z liczebnika porządkowego
		/// nijakiego („tiret pierwsze"→1, kanoniczna forma powołania tiretu wg § 57 ust. 6 ZTP).
		/// Null gdy oznaczenie nieobecne/nierozpoznane.
		/// </summary>
		public int? TargetOrdinalValue { get; init; }
	}

	/// <summary>
	/// Parser komend nowelizacyjnych z treści akapitu (Etap 8): rozpoznaje rodzaj komendy oraz
	/// jednostkę-przedmiot z tokenu stojącego bezpośrednio przy czasowniku komendy. Uzupełnia
	/// mechanizmy stylowe TYLKO tam, gdzie stylów nie ma — wynik jest źródłem o niższym priorytecie
	/// niż mapa stylów (<see cref="StyleLibraryMapper.AmendmentStyleInfoMap"/>).
	///
	/// Kontekst nadrzędny („w art. 5 w ust. 2 …") pozostaje domeną LegalReferenceService
	/// (DetectedAmendmentTargets); ten parser dokłada wyłącznie to, czego tam brakuje
	/// (m.in. jednostkę „tiret", której LegalReferenceService nie zna).
	/// </summary>
	public static class AmendmentCommandParser
	{
		// Token jednostki redakcyjnej + opcjonalne oznaczenie: cyfrowe („7a", „5a-5g"), literowe („b", „za")
		// lub słowne — liczebnik porządkowy („zdanie drugie", „tiret trzecie" — forma kanoniczna § 57 ust. 6 ZTP).
		// Formy mnogie („tirety", „zdania") w alternatywie jednostki, by sufiks nie wpadał do oznaczenia.
		private const string UnitToken =
			@"(?<unit>art\.|§|ust\.|pkt|lit\.|tirety|tiret|zdani[ea])\s*(?<no>\d+[a-zA-Z]*(?:[-–]\d+[a-zA-Z]*)?|[a-z]{1,2}\b|\p{Ll}+\b)?";

		/// <summary>„ust. 2 otrzymuje brzmienie:" / „art. 5 i 6 otrzymują brzmienie:" — token przy czasowniku.</summary>
		internal static readonly Regex ChangeCommandPattern = new(
			UnitToken + @"[^\p{L}]*(?:i\s+\d+[a-zA-Z]*\s*)?otrzymuj[eą]\s+brzmienie\s*:",
			RegexOptions.Compiled | RegexOptions.IgnoreCase);

		/// <summary>„(po art. X) dodaje się art. Xa (w brzmieniu:)" — przedmiotem jest jednostka DODAWANA.</summary>
		internal static readonly Regex AddCommandPattern = new(
			@"dodaje\s+się\s+" + UnitToken,
			RegexOptions.Compiled | RegexOptions.IgnoreCase);

		/// <summary>„uchyla się art. 281" — przedmiotem jest jednostka uchylana.</summary>
		internal static readonly Regex RepealCommandPattern = new(
			@"uchyla\s+się\s+" + UnitToken,
			RegexOptions.Compiled | RegexOptions.IgnoreCase);

		/// <summary>
		/// „wyrazy „X" zastępuje się wyrazami „Y"" (§ 87 ZTP) — komenda kompletna w akapicie,
		/// bez cytowanego bloku po niej. Stare/nowe brzmienie w grupach old/new.
		/// Obsługuje też formę § 88 („użyte … wyrazy „X" zastępuje się użytymi w odpowiedniej liczbie
		/// i przypadku wyrazami „Y"") — dopuszczalna wstawka między „zastępuje się" a „wyrazami" —
		/// oraz proste cudzysłowy "…" jako ogranicznik brzmień (kanał TXT/PDF; Sanitize nie normalizuje cudzysłowów).
		/// </summary>
		internal static readonly Regex ReplaceWordsPattern = new(
			@"wyraz(?:y|ów|em)?\s+[„""](?<old>[^„”“""]*)[”“""]\s+zastępuje\s+się\s+(?:[^„”“""]{0,80}?)wyraz(?:ami|em|y)?\s+[„""](?<new>[^„”“""]*)[”“""]",
			RegexOptions.Compiled | RegexOptions.IgnoreCase);

		/// <summary>
		/// Rozpoznaje komendę nowelizacyjną w treści akapitu. Zwraca null, gdy akapit nie zawiera
		/// żadnej znanej komendy. Kolejność: ReplaceWords → Repeal → Add → Change (komenda „dodaje się …
		/// w brzmieniu:" zawiera też frazę zmiany, więc Add musi być sprawdzone przed Change).
		/// </summary>
		public static AmendmentCommand? Parse(string? text)
		{
			if (string.IsNullOrWhiteSpace(text))
				return null;

			var rw = ReplaceWordsPattern.Match(text);
			if (rw.Success)
			{
				// Jednostka-przedmiot zamiany wyrazów (jeśli wskazana przed komendą, np. „w art. 3 wyrazy …").
				var unitBefore = LastUnitBefore(text, rw.Index);
				return new AmendmentCommand
				{
					Kind = AmendmentCommandKind.ReplaceWords,
					TargetKind = unitBefore?.Kind ?? AmendmentTargetKind.Unknown,
					TargetNumber = unitBefore?.Number,
					TargetOrdinalValue = OrdinalValue(unitBefore?.Number),
					OldWording = rw.Groups["old"].Value,
					NewWording = rw.Groups["new"].Value,
					MatchedCommandText = rw.Value,
				};
			}

			var repeal = RepealCommandPattern.Match(text);
			if (repeal.Success)
				return Build(AmendmentCommandKind.Repeal, repeal);

			var add = AddCommandPattern.Match(text);
			if (add.Success)
				return Build(AmendmentCommandKind.Add, add);

			var change = ChangeCommandPattern.Match(text);
			if (change.Success)
				return Build(AmendmentCommandKind.Change, change);

			return null;
		}

		// Liczebniki porządkowe nijakie („tiret pierwsze", „zdanie drugie") — § 57 ust. 6 ZTP nakazuje
		// powoływać tirety liczebnikiem porządkowym, więc to forma KANONICZNA, nie anomalia.
		private static readonly System.Collections.Generic.Dictionary<string, int> NeuterOrdinals =
			new(System.StringComparer.OrdinalIgnoreCase)
		{
			["pierwsze"] = 1, ["drugie"] = 2, ["trzecie"] = 3, ["czwarte"] = 4, ["piąte"] = 5,
			["szóste"] = 6, ["siódme"] = 7, ["ósme"] = 8, ["dziewiąte"] = 9, ["dziesiąte"] = 10,
			["jedenaste"] = 11, ["dwunaste"] = 12, ["trzynaste"] = 13, ["czternaste"] = 14,
			["piętnaste"] = 15, ["szesnaste"] = 16, ["siedemnaste"] = 17, ["osiemnaste"] = 18,
			["dziewiętnaste"] = 19, ["dwudzieste"] = 20,
		};

		/// <summary>Wartość liczbowa oznaczenia: wiodące cyfry („7a"→7) lub liczebnik nijaki („pierwsze"→1).</summary>
		private static int? OrdinalValue(string? designation)
		{
			if (string.IsNullOrEmpty(designation))
				return null;

			if (char.IsDigit(designation[0]))
			{
				int i = 0;
				while (i < designation.Length && char.IsDigit(designation[i])) i++;
				return int.TryParse(designation[..i], out var n) ? n : null;
			}

			return NeuterOrdinals.TryGetValue(designation, out var ord) ? ord : null;
		}

		private static AmendmentCommand Build(AmendmentCommandKind kind, Match m)
		{
			var designation = m.Groups["no"].Success && m.Groups["no"].Value.Length > 0 ? m.Groups["no"].Value : null;
			return new AmendmentCommand
			{
				Kind = kind,
				TargetKind = MapUnit(m.Groups["unit"].Value),
				TargetNumber = designation,
				TargetOrdinalValue = OrdinalValue(designation),
				MatchedCommandText = m.Value,
			};
		}

		/// <summary>Ostatni token jednostki przed pozycją <paramref name="beforeIndex"/> (np. „w art. 3 wyrazy…").</summary>
		private static (AmendmentTargetKind Kind, string? Number)? LastUnitBefore(string text, int beforeIndex)
		{
			Match? last = null;
			foreach (Match m in Regex.Matches(text[..beforeIndex], UnitToken, RegexOptions.IgnoreCase))
				last = m;
			if (last == null)
				return null;
			return (MapUnit(last.Groups["unit"].Value),
				last.Groups["no"].Success && last.Groups["no"].Value.Length > 0 ? last.Groups["no"].Value : null);
		}

		private static AmendmentTargetKind MapUnit(string unit) =>
			unit.ToLowerInvariant().TrimEnd('.') switch
			{
				"art" or "§" => AmendmentTargetKind.Article,
				"ust"        => AmendmentTargetKind.Paragraph,
				"pkt"        => AmendmentTargetKind.Point,
				"lit"        => AmendmentTargetKind.Letter,
				"tiret" or "tirety" => AmendmentTargetKind.Tiret,
				"zdanie" or "zdania" => AmendmentTargetKind.Fragment,
				_            => AmendmentTargetKind.Unknown,
			};
	}
}
