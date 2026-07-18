using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using ModelDto;
using WordParserCore.Helpers;
using WordParserCore.Services.Parsing;

namespace WordParserCore.Services.Classify
{
	/// <summary>
	/// Klasyfikator akapitów. Rozpoznaje typ jednostki redakcyjnej i oblicza pewność (1–100).
	/// Rozwiązywanie konfliktów deleguje do <see cref="IConflictResolver"/>.
	/// </summary>
	public sealed class ParagraphClassifier : IParagraphClassifier
	{
		private readonly IConflictResolver       _conflictResolver;
		private readonly ConfidencePenaltyConfig _cfg;

		public ParagraphClassifier(
			IConflictResolver?       conflictResolver = null,
			ConfidencePenaltyConfig? penaltyConfig    = null)
		{
			_conflictResolver = conflictResolver ?? new DefaultConflictResolver();
			_cfg              = penaltyConfig    ?? ConfidencePenaltyConfig.Default;
		}

		// ============================================================
		// Współdzielone wzorce regex (reużywane przez ParsingFactories i AmendmentBuilder)
		// ============================================================

		/// <summary>Opcjonalny prefiks cytatu otwierającego („ " ") w tekście akapitu.</summary>
		internal const string OptionalQuotePrefix = "(?:[\"\\u201E\\u201C\\u201D]\\s*)?";

		/// <summary>
		/// Opcjonalny indeks górny numeru jednostki w notacji [x] (§ 89 ust. 6 ZTP; kanał GetFullText).
		/// Celowo tylko cyfry — odnośniki przypisów mają postać [N)] i nie mogą tu wpadać.
		/// </summary>
		internal const string OptionalSuperscript = @"(?:\[\d+\])?";

		internal static readonly Regex ArticlePattern = new(
			$@"^{OptionalQuotePrefix}Art\.?\s*\d+", RegexOptions.Compiled | RegexOptions.IgnoreCase);

		internal static readonly Regex ParagraphPattern = new(
			$@"^{OptionalQuotePrefix}\d+[a-zA-Z]*{OptionalSuperscript}\.\s+", RegexOptions.Compiled | RegexOptions.IgnoreCase);

		internal static readonly Regex PointPattern = new(
			$@"^{OptionalQuotePrefix}\d+[a-zA-Z]*{OptionalSuperscript}\)\s*", RegexOptions.Compiled | RegexOptions.IgnoreCase);

		internal static readonly Regex LetterPattern = new(
			$@"^{OptionalQuotePrefix}[a-zA-Z]{{1,5}}{OptionalSuperscript}\)\s*", RegexOptions.Compiled | RegexOptions.IgnoreCase);

		// Półpauza – dopuszczona dla tekstu niesanityzowanego (Sanitize normalizuje ją do dywizu)
		internal static readonly Regex TiretPattern = new(
			$@"^{OptionalQuotePrefix}[-–]+\s+", RegexOptions.Compiled);

		/// <summary>Artykuł z grupą przechwytującą numer (do ParseArticleNumber).</summary>
		internal static readonly Regex ArticleNumberCapture = new(
			$@"^{OptionalQuotePrefix}Art\.?\s*(\d+[a-zA-Z]*{OptionalSuperscript})", RegexOptions.Compiled | RegexOptions.IgnoreCase);

		/// <summary>Artykuł z przechwyceniem ogona (do GetArticleTail).</summary>
		internal static readonly Regex ArticleTailCapture = new(
			$@"^{OptionalQuotePrefix}Art\.?\s*\d+[a-zA-Z]*{OptionalSuperscript}\.?\s*(.*)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

		/// <summary>Numer ustępu z grupą przechwytującą (do ParseParagraphNumber).</summary>
		internal static readonly Regex ParagraphNumberCapture = new(
			$@"^{OptionalQuotePrefix}(\d+[a-zA-Z]*{OptionalSuperscript})\.\s+", RegexOptions.Compiled | RegexOptions.IgnoreCase);

		/// <summary>Numer punktu z grupą przechwytującą (do ParsePointNumber).</summary>
		internal static readonly Regex PointNumberCapture = new(
			$@"^{OptionalQuotePrefix}(\d+[a-zA-Z]*{OptionalSuperscript})\)\s*", RegexOptions.Compiled | RegexOptions.IgnoreCase);

		/// <summary>Numer litery z grupą przechwytującą (do ParseLetterNumber).</summary>
		internal static readonly Regex LetterNumberCapture = new(
			$@"^{OptionalQuotePrefix}([a-zA-Z]{{1,5}}{OptionalSuperscript})\)\s*", RegexOptions.Compiled | RegexOptions.IgnoreCase);

		/// <summary>Prefiks tiretu do usuwania (bez wymagania spacji; dywiz lub półpauza).</summary>
		internal static readonly Regex TiretStripPattern = new(
			$@"^{OptionalQuotePrefix}[-–]+\s*", RegexOptions.Compiled);

		// === Jednostki systematyzacyjne (§ 60-62 ZTP) ===
		// Wzorce ZAKOTWICZONE na całej linii ($): marker jednostki stoi w osobnym wierszu (§ 60), więc
		// proza „Rozdział 5 stosuje się…"/„Tytuł V wprowadza się…" NIE jest brana za nagłówek. Dozwolona
		// końcowa kropka/przecinek. Wersaliki to sygnał ZTP; dopuszczamy też kapitalizację tytułową
		// (nie IgnoreCase — słowo małą literą to proza). Numer wyższych jednostek: cyfra rzymska
		// (opcjonalny sufiks litery jednostki wtrąconej, „IVa") lub liczebnik słowny; rozstrzyga walidacja,
		// więc „Część majątku…" nie zostanie uznane za jednostkę. Rozdział/Oddział: cyfra arabska + sufiks.
		internal static readonly Regex PartUnitPattern = new(
			@"^(?:CZĘŚĆ|Część)\s+(?<n>[IVXLCDM]+[a-z]?|\p{L}+)\s*[.,]?\s*$", RegexOptions.Compiled);
		internal static readonly Regex BookUnitPattern = new(
			@"^(?:KSIĘGA|Księga)\s+(?<n>[IVXLCDM]+[a-z]?|\p{L}+)\s*[.,]?\s*$", RegexOptions.Compiled);
		internal static readonly Regex TitleUnitPattern = new(
			@"^(?:TYTUŁ|Tytuł)\s+(?<n>[IVXLCDM]+[a-z]?|\p{L}+)\s*[.,]?\s*$", RegexOptions.Compiled);
		internal static readonly Regex DivisionUnitPattern = new(
			@"^(?:DZIAŁ|Dział)\s+(?<n>[IVXLCDM]+[a-z]?|\p{L}+)\s*[.,]?\s*$", RegexOptions.Compiled);
		internal static readonly Regex ChapterUnitPattern = new(
			@"^(?:ROZDZIAŁ|Rozdział)\s+(?<n>\d+[a-zA-Z]{0,3})\s*[.,]?\s*$", RegexOptions.Compiled);
		internal static readonly Regex SubchapterUnitPattern = new(
			@"^(?:ODDZIAŁ|Oddział)\s+(?<n>\d+[a-zA-Z]{0,3})\s*[.,]?\s*$", RegexOptions.Compiled);

		// ============================================================
		// Implementacja IParagraphClassifier
		// ============================================================

		/// <summary>
		/// Klasyfikuje akapit do typu jednostki redakcyjnej i oblicza pewność (1–100).
		/// Confidence = 100 gdy styl, regex i numeracja są w pełnej zgodzie.
		/// </summary>
		public ClassificationResult Classify(ClassificationInput input)
		{
			var text      = input.Text;
			var styleType = GetStyleType(input.StyleId);
			var isAmend   = styleType == "AMENDMENT";

			// WrapUp — priorytet przed pozostałą logiką.
			// Wykrywany wyłącznie przez styl (CZ_WSP_*); tekst jedynie potwierdza lub obniża pewność.
			if (styleType == "WRAPUP")
				return BuildWrapUpResult(styleType, isAmend, byStyle: true, IsWrapUpByText(text));

			// Sygnał ze stylu (null gdy AMENDMENT lub nieznany)
			ParagraphKind? styleKind = isAmend ? null : MapStyleToKind(styleType);

			// Sygnał syntaktyczny (regex)
			ParagraphKind? syntacticKind = MatchRegex(text);

			return BuildResult(input, styleType, isAmend, styleKind, syntacticKind);
		}

		// ============================================================
		// Metody pomocnicze — publiczne statyczne (reużywane zewnętrznie)
		// ============================================================

		public static string? GetStyleType(string? styleId)
		{
			if (string.IsNullOrEmpty(styleId))
				return null;

			// Wykryj style nowelizacji po mapie styli (priorytet nad heurystyką)
			if (StyleLibraryMapper.TryGetStyleInfo(styleId, out var info) && info != null)
			{
				if (info.IsAmendment)
					return "AMENDMENT";

				// WrapUp — styl zamknięcia listy (CZ_WSP_*)
				if (info.DisplayName.StartsWith("CZ_WSP_", StringComparison.OrdinalIgnoreCase))
					return "WRAPUP";
			}

			// Fallback dla styli nowelizacji spoza mapy
			if (styleId.StartsWith("Z/",  StringComparison.OrdinalIgnoreCase) ||
			    styleId.StartsWith("ZZ",  StringComparison.OrdinalIgnoreCase) ||
			    styleId.StartsWith("Z_",  StringComparison.OrdinalIgnoreCase))
				return "AMENDMENT";

			if (styleId.StartsWith("ART", StringComparison.OrdinalIgnoreCase)) return "ART";
			if (styleId.StartsWith("UST", StringComparison.OrdinalIgnoreCase)) return "UST";
			if (styleId.StartsWith("PKT", StringComparison.OrdinalIgnoreCase)) return "PKT";
			if (styleId.StartsWith("LIT", StringComparison.OrdinalIgnoreCase)) return "LIT";
			if (styleId.StartsWith("TIR", StringComparison.OrdinalIgnoreCase)) return "TIR";
			if (styleId.StartsWith("2TIR", StringComparison.OrdinalIgnoreCase)) return "TIR";
			if (styleId.StartsWith("3TIR", StringComparison.OrdinalIgnoreCase)) return "TIR";

			return null;
		}

		public static bool IsArticleByText(string text)   => ArticlePattern.IsMatch(text.Trim());
		public static bool IsParagraphByText(string text) => ParagraphPattern.IsMatch(text);
		public static bool IsPointByText(string text)     => PointPattern.IsMatch(text);
		public static bool IsLetterByText(string text)    => LetterPattern.IsMatch(text);
		public static bool IsTiretByText(string text)     => TiretPattern.IsMatch(text);

		/// <summary>
		/// Sprawdza czy tekst to WrapUp (rozpoczyna się półpauzą lub dywizem po których stoi spacja).
		/// </summary>
		public static bool IsWrapUpByText(string text)
		{
			if (string.IsNullOrWhiteSpace(text))
				return false;
			var trimmed = text.TrimStart();
			if (trimmed.Length < 2)
				return false;
			var first = trimmed[0];
			return (first == '\u2013' || first == '-') && char.IsWhiteSpace(trimmed[1]);
		}

		// ============================================================
		// Prywatne metody budowania wyniku
		// ============================================================

		private ClassificationResult BuildWrapUpResult(
			string? styleType, bool isAmend, bool byStyle, bool byText)
		{
			var penalties  = new List<ClassificationPenalty>();
			int confidence = 100;

			if (!byStyle)
			{
				penalties.Add(new ClassificationPenalty
				{
					Reason = "Brak stylu WrapUp — typ ustalony wyłącznie z tekstu",
					Value  = _cfg.StyleAbsentPenalty,
				});
				confidence -= _cfg.StyleAbsentPenalty;
			}
			if (!byText)
			{
				penalties.Add(new ClassificationPenalty
				{
					Reason = "Brak półpauzy w tekście — typ ustalony wyłącznie ze stylu",
					Value  = _cfg.SyntaxAbsentPenalty,
				});
				confidence -= _cfg.SyntaxAbsentPenalty;
			}

			return new ClassificationResult
			{
				Kind               = ParagraphKind.WrapUp,
				Confidence         = Math.Clamp(confidence, 1, 100),
				IsAmendmentContent = isAmend,
				StyleType          = styleType,
				Penalties          = penalties,
			};
		}

		private ClassificationResult BuildResult(
			ClassificationInput input,
			string?             styleType,
			bool                isAmend,
			ParagraphKind?      styleKind,
			ParagraphKind?      syntacticKind)
		{
			var   penalties  = new List<ClassificationPenalty>();
			int   confidence = 100;
			ParagraphKind kind;

			if (styleKind == null && syntacticKind == null)
			{
				// Brak obu sygnałów
				kind       = ParagraphKind.Unknown;
				confidence = 1;
			}
			else if (styleKind != null && syntacticKind != null)
			{
				if (styleKind == syntacticKind)
				{
					// Oba sygnały zgodne
					kind = syntacticKind.Value;
				}
				else
				{
					// Konflikt — delegacja do IConflictResolver
					kind = _conflictResolver.Resolve(styleKind.Value, syntacticKind.Value, input.Text, input.StyleId);
					penalties.Add(new ClassificationPenalty
					{
						Reason = $"Konflikt: styl={styleKind}, regex={syntacticKind}; rozstrzygnięto: {kind}",
						Value  = _cfg.StyleSyntaxConflictPenalty,
					});
					confidence -= _cfg.StyleSyntaxConflictPenalty;
				}
			}
			else if (syntacticKind != null)
			{
				// Tylko regex
				kind = syntacticKind.Value;
				penalties.Add(new ClassificationPenalty
				{
					Reason = "Brak rozpoznanego stylu Word",
					Value  = _cfg.StyleAbsentPenalty,
				});
				confidence -= _cfg.StyleAbsentPenalty;
			}
			else
			{
				// Tylko styl — artykuł wymaga sygnatury tekstowej
				if (styleKind == ParagraphKind.Article)
				{
					kind       = ParagraphKind.Unknown;
					confidence = 1;
				}
				else
				{
					kind = styleKind!.Value;
					penalties.Add(new ClassificationPenalty
					{
						Reason = "Brak dopasowania regex — typ z samego stylu",
						Value  = _cfg.SyntaxAbsentPenalty,
					});
					confidence -= _cfg.SyntaxAbsentPenalty;
				}
			}

			// Sprawdzenie ciągłości numeracji (NumberingHint)
			if (input.NumberingHint is { } hint && hint.ExpectedKind == kind && kind != ParagraphKind.Unknown)
			{
				var parsedNumber = ParseNumberForKind(kind, input.Text);
				if (parsedNumber != null && !hint.IsContinuous(parsedNumber))
				{
					penalties.Add(new ClassificationPenalty
					{
						Reason = $"Nieciągłość numeracji {kind}: oczekiwano następnika {hint.ExpectedNumber?.Value}",
						Value  = _cfg.NumberingBreakPenalty,
					});
					confidence -= _cfg.NumberingBreakPenalty;
				}
			}

			return new ClassificationResult
			{
				Kind               = kind,
				Confidence         = Math.Clamp(confidence, 1, 100),
				IsAmendmentContent = isAmend,
				StyleType          = styleType,
				Penalties          = penalties,
			};
		}

		private static ParagraphKind? MapStyleToKind(string? styleType) =>
			styleType switch
			{
				"ART" => ParagraphKind.Article,
				"UST" => ParagraphKind.Paragraph,
				"PKT" => ParagraphKind.Point,
				"LIT" => ParagraphKind.Letter,
				"TIR" => ParagraphKind.Tiret,
				_     => null,
			};

		private static ParagraphKind? MatchRegex(string text)
		{
			// Jednostki systematyzacyjne sprawdzane najpierw (§ 60-62); rozstrzyga walidacja numeru.
			var systematizing = MatchSystematizingRegex(text);
			if (systematizing != null) return systematizing;

			if (IsArticleByText(text))   return ParagraphKind.Article;
			if (IsParagraphByText(text)) return ParagraphKind.Paragraph;
			if (IsPointByText(text))     return ParagraphKind.Point;
			if (IsLetterByText(text))    return ParagraphKind.Letter;
			if (IsTiretByText(text))     return ParagraphKind.Tiret;
			return null;
		}

		/// <summary>
		/// Rozpoznaje jednostkę systematyzacyjną. Dla jednostek wyższych (Część/Księga/Tytuł/Dział)
		/// wymaga poprawnego numeru rzymskiego lub liczebnika słownego; Rozdział/Oddział — arabskiego.
		/// </summary>
		private static ParagraphKind? MatchSystematizingRegex(string text)
		{
			if (MatchesHigherUnit(PartUnitPattern, text))     return ParagraphKind.PartUnit;
			if (MatchesHigherUnit(BookUnitPattern, text))     return ParagraphKind.BookUnit;
			if (MatchesHigherUnit(TitleUnitPattern, text))    return ParagraphKind.TitleUnit;
			if (MatchesHigherUnit(DivisionUnitPattern, text)) return ParagraphKind.DivisionUnit;
			if (ChapterUnitPattern.IsMatch(text))    return ParagraphKind.ChapterUnit;
			if (SubchapterUnitPattern.IsMatch(text)) return ParagraphKind.SubchapterUnit;
			return null;
		}

		private static bool MatchesHigherUnit(Regex pattern, string text)
		{
			var m = pattern.Match(text);
			return m.Success && ParseHigherUnitToken(m.Groups["n"].Value) != null;
		}

		/// <summary>
		/// Parsuje numer jednostki wyższej: cyfra rzymska lub liczebnik słowny, z opcjonalnym sufiksem
		/// litery jednostki wtrąconej nowelizacją („IVa"). Zwraca wartość liczbową i sufiks; null gdy nie-numer.
		/// </summary>
		private static (int numeric, string suffix)? ParseHigherUnitToken(string token)
		{
			var n = RomanNumeralConverter.Parse(token);
			if (n != null) return (n.Value, string.Empty);

			int b = token.Length;
			while (b > 0 && char.IsLower(token[b - 1])) b--;
			if (b > 0 && b < token.Length)
			{
				var baseNum = RomanNumeralConverter.RomanToInt(token[..b]);
				if (baseNum != null) return (baseNum.Value, token[b..]);
			}
			return null;
		}

		/// <summary>
		/// Wyodrębnia numer jednostki systematyzacyjnej jako EntityNumber. Dla jednostek wyższych
		/// Value pozostaje oryginalnym oznaczeniem (rzymskim/słownym) — zasada „nie przeinaczyć" —
		/// a NumericPart niesie wartość liczbową do kontroli ciągłości. Rozdział/Oddział: numer arabski.
		/// </summary>
		public static EntityNumber? ParseSystematizingNumber(ParagraphKind kind, string text)
		{
			switch (kind)
			{
				case ParagraphKind.ChapterUnit:
				case ParagraphKind.SubchapterUnit:
					var pattern = kind == ParagraphKind.ChapterUnit ? ChapterUnitPattern : SubchapterUnitPattern;
					var am = pattern.Match(text);
					if (!am.Success) return null;
					var token = am.Groups["n"].Value;
					int di = 0;
					while (di < token.Length && char.IsDigit(token[di])) di++;
					var digits = token[..di];
					return new EntityNumber
					{
						Value = token, RawValue = token, LexicalPart = token[di..],
						NumericPart = int.TryParse(digits, out var n) ? n : 0,
					};

				default:
					var higher = kind switch
					{
						ParagraphKind.PartUnit     => PartUnitPattern,
						ParagraphKind.BookUnit     => BookUnitPattern,
						ParagraphKind.TitleUnit    => TitleUnitPattern,
						ParagraphKind.DivisionUnit => DivisionUnitPattern,
						_ => null,
					};
					if (higher == null) return null;
					var hm = higher.Match(text);
					if (!hm.Success) return null;
					var parsed = ParseHigherUnitToken(hm.Groups["n"].Value);
					if (parsed == null) return null;
					// Value = kanoniczna cyfra rzymska (znormalizowana z liczebnika/wielkości liter) + sufiks,
					// by eId był spójny („KSIĘGA PIERWSZA" i „KSIĘGA I" → ks_I).
					var canonical = RomanNumeralConverter.ToRoman(parsed.Value.numeric) + parsed.Value.suffix;
					return new EntityNumber
					{
						Value = canonical, RawValue = hm.Groups["n"].Value,
						NumericPart = parsed.Value.numeric, LexicalPart = parsed.Value.suffix,
					};
			}
		}

		private static EntityNumber? ParseNumberForKind(ParagraphKind kind, string text) =>
			kind switch
			{
				ParagraphKind.Article   => ParsingFactories.ParseArticleNumber(text),
				ParagraphKind.Paragraph => ParsingFactories.ParseParagraphNumber(text),
				ParagraphKind.Point     => ParsingFactories.ParsePointNumber(text),
				ParagraphKind.Letter    => ParsingFactories.ParseLetterNumber(text),
				_                       => null,
			};
	}
}
