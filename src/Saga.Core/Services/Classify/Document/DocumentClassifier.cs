using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Saga.Model;
using Saga.Core.Ingest;

namespace Saga.Core.Services.Classify.Document
{
	/// <summary>
	/// Klasyfikator dokumentu wg ZTP. Dwufazowo: (A) strefa tytułowa = pierwsze 25 niepustych
	/// bloków (nagłówki, data, przedmiot), (B) statystyka korpusu (formuły, jednostka podstawowa,
	/// wejście w życie, markery TJ, organy). Każdy dopasowany wzoriec = DocumentSignal z dowodem.
	///
	/// Punktacja rozdziela sygnały RÓŻNICUJĄCE typ (per typ) od sygnałów „aktowości" (wspólnych),
	/// które podnoszą pewność, że to JAKIŚ akt, nie zmieniając, KTÓRY. Zwycięzcą jest typ o
	/// najwyższym wyniku różnicującym; wynik = jego punkty + bonus aktowości.
	/// </summary>
	public sealed class DocumentClassifier : IDocumentClassifier
	{
		private const int TitleZoneSize = 25;
		// Formuła obwieszczenia TJ pojawia się tuż po tytule (nagłówek/organ/data/przedmiot/„1. Na podstawie…").
		// Wąskie okno + strażnik cudzysłowu chronią przed cytowanym wzorem formuły w treści aktu zmieniającego.
		private const int FormulaLeadZone = 12;
		private const int ActThreshold = 40;
		private const int AmbiguityMargin = 15;
		private const int MinBlocks = 3;
		private const int MatchPreviewLength = 80;

		private static readonly Regex LeadingBaseUnitNumber = new(@"^(?:Art\.|§)\s*(\d+)", RegexOptions.Compiled);

		private readonly record struct Block(int Index, string Text, string? StyleId);

		public DocumentClassificationResult Classify(IReadOnlyList<DocumentBlock> blocks)
		{
			var normalized = Normalize(blocks);

			if (normalized.Count < MinBlocks)
				return TooFewBlocks(normalized.Count);

			var titleZone = normalized.Take(TitleZoneSize).ToList();

			var scores = new Dictionary<LegalActType, int>();
			var signals = new List<DocumentSignal>();
			int actnessBonus = 0;
			bool isAmending = false;
			bool isConsolidated = false;
			bool isLocal = false;
			bool hasBackbone = false; // silny sygnał strukturalny — warunek konieczny uznania za akt

			void AddType(LegalActType type, int score, DocumentSignalKind kind, int index, string matched, string desc)
			{
				scores[type] = scores.GetValueOrDefault(type) + score;
				signals.Add(new DocumentSignal
				{
					Kind = kind, Score = score, SupportsType = type,
					BlockIndex = index, MatchedText = matched, Description = desc,
				});
			}

			void AddActness(int score, DocumentSignalKind kind, int index, string matched, string desc)
			{
				actnessBonus += score;
				signals.Add(new DocumentSignal
				{
					Kind = kind, Score = score, SupportsType = null,
					BlockIndex = index, MatchedText = matched, Description = desc,
				});
			}

			// ---- Faza A: strefa tytułowa ----

			// Nagłówek rodzaju aktu — liczy się PIERWSZY (pułapka: załącznik TJ zawiera własny „USTAWA").
			LegalActType? primaryHeader = null;
			foreach (var b in titleZone)
			{
				var headerType = MatchHeaderType(b.Text);
				if (headerType is null)
					continue;

				if (primaryHeader is null)
				{
					primaryHeader = headerType;
					hasBackbone = true;
					AddType(headerType.Value, 35, DocumentSignalKind.ActKindHeader, b.Index,
						Preview(b.Text), $"Nagłówek rodzaju aktu: {headerType.Value.ToFriendlyString()}");
				}
				else
				{
					signals.Add(new DocumentSignal
					{
						Kind = DocumentSignalKind.IgnoredSecondaryHeader, Score = 0, SupportsType = null,
						BlockIndex = b.Index, MatchedText = Preview(b.Text),
						Description = "Kolejny nagłówek rodzaju aktu w strefie tytułowej — zignorowany (załącznik TJ)",
					});
				}
			}

			bool seenConsolidatedTitle = false, seenDate = false, seenAmendingTitle = false,
				seenSubject = false, seenOrgan = false, seenMarshalIssuer = false;
			foreach (var b in titleZone)
			{
				if (!seenMarshalIssuer && ZtpPatterns.MarshalOfSejmIssuerPattern.IsMatch(b.Text))
				{
					// Marszałek Sejmu wydaje teksty jednolite ustaw (§ 102) — dodatkowy sygnał obwieszczenia.
					AddType(LegalActType.Announcement, 10, DocumentSignalKind.MarshalOfSejmIssuer, b.Index,
						Preview(b.Text), "Organ wydający: Marszałek Sejmu — obwieszczenie/tekst jednolity ustawy (§ 102)");
					seenMarshalIssuer = true;
				}
				if (!seenConsolidatedTitle && ZtpPatterns.ConsolidatedTextTitlePattern.IsMatch(b.Text))
				{
					AddType(LegalActType.Announcement, 25, DocumentSignalKind.ConsolidatedTextTitle, b.Index,
						Preview(b.Text), "Tytuł: w sprawie ogłoszenia jednolitego tekstu (§ 102)");
					isConsolidated = true;
					// Tytuł „w sprawie ogłoszenia jednolitego tekstu" (§ 102) to definitywny sygnał
					// strukturalny obwieszczenia TJ — sam w sobie stanowi backbone.
					hasBackbone = true;
					seenConsolidatedTitle = true;
				}
				if (!seenDate && ZtpPatterns.ActDateLinePattern.IsMatch(b.Text))
				{
					AddActness(10, DocumentSignalKind.ActDate, b.Index, Preview(b.Text), "Data aktu: z dnia ... r. (§ 17)");
					seenDate = true;
				}
				if (!seenAmendingTitle && ZtpPatterns.AmendingTitlePattern.IsMatch(b.Text))
				{
					AddActness(15, DocumentSignalKind.AmendingTitle, b.Index, Preview(b.Text), "Tytuł zmieniający (§ 96)");
					isAmending = true;
					seenAmendingTitle = true;
				}
				if (!seenSubject && ZtpPatterns.ActSubjectPattern.IsMatch(b.Text))
				{
					AddActness(8, DocumentSignalKind.ActSubject, b.Index, Preview(b.Text), "Przedmiot aktu (§ 18-19)");
					seenSubject = true;
				}
				if (!seenOrgan)
				{
					// Wydawca JST liczy się WYŁĄCZNIE ze strefy tytułowej i tylko jako wiersz wydawcy:
					// organ na początku wiersza (osobny wiersz wydawcy) albo w wierszu nagłówka rodzaju aktu.
					// Wzmianka o organie w treści merytorycznej („§ 2. Wójt sporządza…") NIE czyni aktu miejscowym.
					var organ = ZtpPatterns.LocalGovernmentOrganPattern.Match(b.Text);
					if (organ.Success && (organ.Index == 0 || MatchHeaderType(b.Text) is not null))
					{
						AddType(LegalActType.LocalLegalAct, 25, DocumentSignalKind.LocalGovernmentOrgan, b.Index,
							Preview(b.Text), "Organ JST jako wydawca (rada/sejmik/wójt/burmistrz…)");
						isLocal = true;
						seenOrgan = true;
					}
				}
			}

			// ---- Faza B: korpus ----

			int repealedCount = 0, footnoteCount = 0;
			bool seenConsolidatedFormula = false, seenEnactment = false, seenLegalBasis = false,
				seenEntry = false, seenAmendCmd = false, seenVoivodeship = false, seenStyleHint = false;

			int corpusPos = -1;
			foreach (var b in normalized)
			{
				corpusPos++;
				repealedCount += ZtpPatterns.RepealedMarkerPattern.Matches(b.Text).Count;
				footnoteCount += ZtpPatterns.FootnoteRefPattern.Matches(b.Text).Count;

				// Formuła obwieszczenia TJ stoi tuż po tytule (§ 104) i jest własną treścią obwieszczenia.
				// Odrzucamy: (a) dopasowania poza wąską strefą wiodącą, (b) blok zaczynający się cudzysłowem
				// otwierającym — to cytowany wzór (np. rozporządzenie zmieniające ZTP przytacza formułę),
				// a nie formuła dokumentu; inaczej akt zmieniający udawałby tekst jednolity.
				if (!seenConsolidatedFormula && corpusPos < FormulaLeadZone
					&& !StartsWithOpeningQuote(b.Text)
					&& ZtpPatterns.ConsolidatedTextFormulaPattern.IsMatch(b.Text))
				{
					AddType(LegalActType.Announcement, 30, DocumentSignalKind.ConsolidatedTextFormula, b.Index,
						Preview(b.Text), "Formuła obwieszczenia TJ (art. 16 ustawy o ogłaszaniu, § 104)");
					isConsolidated = true;
					hasBackbone = true;
					seenConsolidatedFormula = true;
				}
				if (!seenEnactment)
				{
					var enactment = ZtpPatterns.EnactmentFormulaPattern.Match(b.Text);
					if (enactment.Success)
					{
						var verb = enactment.Groups["verb"].Value;
						var desc = $"Formuła kompetencyjna: {verb} się, co następuje (§ 121)";
						// „zarządza się" jest wspólne dla rozporządzenia I zarządzenia — nagłówek rozstrzyga.
						foreach (var type in EnactmentVerbToTypes(verb))
							AddType(type, 20, DocumentSignalKind.EnactmentFormula, b.Index, Preview(b.Text), desc);
						hasBackbone = true;
						seenEnactment = true;
					}
				}
				if (!seenLegalBasis && ZtpPatterns.LegalBasisPattern.IsMatch(b.Text))
				{
					AddActness(8, DocumentSignalKind.LegalBasis, b.Index, Preview(b.Text), "Podstawa prawna: Na podstawie art. ... (§ 121)");
					seenLegalBasis = true;
				}
				if (!seenEntry)
				{
					var entry = ZtpPatterns.EntryIntoForcePattern.Match(b.Text);
					if (entry.Success)
					{
						AddActness(10, DocumentSignalKind.EntryIntoForce, b.Index, Preview(b.Text), "Formuła wejścia w życie (§ 45)");
						hasBackbone = true;
						var subjectType = SubjectToType(entry.Groups["subject"].Value);
						if (subjectType is { } st)
							AddType(st, 10, DocumentSignalKind.EntryIntoForce, b.Index,
								Preview(b.Text), $"Podmiot wejścia w życie wskazuje: {st.ToFriendlyString()}");
						seenEntry = true;
					}
				}
				if (!seenAmendCmd && ZtpPatterns.AmendmentIntroPattern.IsMatch(b.Text))
				{
					AddActness(15, DocumentSignalKind.AmendmentCommands, b.Index, Preview(b.Text), "Komendy nowelizacyjne: wprowadza się następujące zmiany (§ 82-85)");
					isAmending = true;
					hasBackbone = true;
					seenAmendCmd = true;
				}
				if (!seenVoivodeship && ZtpPatterns.VoivodeshipJournalPattern.IsMatch(b.Text))
				{
					AddType(LegalActType.LocalLegalAct, 20, DocumentSignalKind.VoivodeshipJournal, b.Index,
						Preview(b.Text), "Publikator Dz. Urz. Woj. (§ 162)");
					isLocal = true;
					seenVoivodeship = true;
				}
				if (!seenStyleHint && IsMetadataStyle(b.StyleId))
				{
					AddActness(5, DocumentSignalKind.WordStyleHint, b.Index, b.StyleId ?? "", "Styl Word metadanych aktu");
					seenStyleHint = true;
				}
			}

			if (repealedCount >= 2)
				AddType(LegalActType.Announcement, 10, DocumentSignalKind.RepealedMarkers, -1,
					$"x{repealedCount}", "Markery TJ (uchylony / utracił moc) >=2 (§ 106)");
			if (footnoteCount >= 3)
				AddType(LegalActType.Announcement, 5, DocumentSignalKind.FootnoteDensity, -1,
					$"×{footnoteCount}", "Gęste odnośniki [N)] w indeksie górnym (§ 163)");

			// ---- Dominacja jednostki podstawowej + ciągłość numeracji ----
			int articleCount = 0, sectionCount = 0;
			int? firstBaseNumber = null;
			bool firstBaseIsArticle = false;
			foreach (var b in normalized)
			{
				bool isArticle = ZtpPatterns.ArticleUnitStatPattern.IsMatch(b.Text);
				bool isSection = !isArticle && ZtpPatterns.SectionUnitStatPattern.IsMatch(b.Text);
				if (!isArticle && !isSection)
					continue;

				if (isArticle) articleCount++; else sectionCount++;

				if (firstBaseNumber is null)
				{
					var m = LeadingBaseUnitNumber.Match(b.Text);
					if (m.Success && int.TryParse(m.Groups[1].Value, out var n))
					{
						firstBaseNumber = n;
						firstBaseIsArticle = isArticle;
					}
				}
			}

			int totalBase = articleCount + sectionCount;
			LegalActType? baseType = null;
			if (totalBase >= 3)
			{
				// ≥80% jednego rodzaju (arytmetyka całkowita: count*5 >= total*4)
				if (articleCount > sectionCount && articleCount * 5 >= totalBase * 4)
				{
					baseType = LegalActType.Statute;
					AddType(LegalActType.Statute, 20, DocumentSignalKind.BaseUnitDominance, -1,
						$"Art×{articleCount}/{totalBase}", "Dominacja jednostki podstawowej Art. (≥80%)");
				}
				else if (sectionCount > articleCount && sectionCount * 5 >= totalBase * 4)
				{
					baseType = SectionFamilyTarget(scores);
					AddType(baseType.Value, 20, DocumentSignalKind.BaseUnitDominance, -1,
						$"§×{sectionCount}/{totalBase}", $"Dominacja jednostki podstawowej § (≥80%) → {baseType.Value.ToFriendlyString()}");
				}
			}

			// Akt pierwotny zaczyna numerację od 1; TJ/fragment zwykle nie (§: „nie zaczynają od 1").
			if (baseType is { } bt && firstBaseNumber == 1 && firstBaseIsArticle == (bt == LegalActType.Statute))
				AddType(bt, 10, DocumentSignalKind.NumberingContinuity, -1, "1",
					"Numeracja jednostki podstawowej od 1 (akt pierwotny)");

			return Decide(scores, signals, actnessBonus, isAmending, isConsolidated, isLocal, hasBackbone);
		}

		// ============================================================
		// Decyzja
		// ============================================================

		private static DocumentClassificationResult Decide(
			Dictionary<LegalActType, int> scores,
			List<DocumentSignal> signals,
			int actnessBonus,
			bool isAmending,
			bool isConsolidated,
			bool isLocal,
			bool hasBackbone)
		{
			var ordered = scores.OrderByDescending(kv => kv.Value).ThenBy(kv => (int)kv.Key).ToList();
			int winnerTypeScore = ordered.Count > 0 ? ordered[0].Value : 0;
			int secondTypeScore = ordered.Count > 1 ? ordered[1].Value : 0;
			int winnerTotal = winnerTypeScore + actnessBonus;

			// Warunek konieczny aktu: silny sygnał strukturalny (backbone), rozpoznany typ i wynik ≥ próg.
			// Sam zbiór słabych sygnałów (numeracja §, data, przedmiot) NIE czyni dokumentu aktem —
			// inaczej umowa/regulamin/statut wewnętrzny fałszywie przekraczałyby próg.
			if (!hasBackbone || ordered.Count == 0 || winnerTotal < ActThreshold)
			{
				int notActConfidence = Math.Clamp(90 - winnerTotal, 40, 90);
				return new DocumentClassificationResult
				{
					ActType = null,
					IsLegalAct = false,
					IsConsolidatedText = isConsolidated,
					// Tekst jednolity niczego nie nowelizuje — wykaz aktów zmieniających w obwieszczeniu
					// („N) ustawą … o zmianie ustawy …") nie czyni obwieszczenia aktem zmieniającym (§ 102).
					IsAmending = isAmending && !isConsolidated,
					Confidence = notActConfidence,
					Signals = SortSignals(signals),
					Justification = !hasBackbone
						? "Brak silnego sygnału strukturalnego (nagłówek rodzaju aktu, formuła kompetencyjna, komendy nowelizacyjne, formuła wejścia w życie) — nie rozpoznano rodzaju aktu prawnego."
						: ordered.Count == 0
							? "Brak sygnału wskazującego rodzaj aktu — nie rozpoznano rodzaju aktu prawnego."
							: $"Najwyższy wynik ({winnerTotal}) poniżej progu {ActThreshold} — nie rozpoznano rodzaju aktu prawnego.",
				};
			}

			var winnerType = ordered[0].Key;
			var finalType = ResolveFinalType(winnerType, isAmending, isLocal, isConsolidated);

			int confidence = Math.Clamp(winnerTotal, 1, 100);
			bool ambiguous = (winnerTypeScore - secondTypeScore) < AmbiguityMargin;
			if (ambiguous)
			{
				confidence = Math.Clamp(confidence - 10, 1, 100);
				signals.Add(new DocumentSignal
				{
					Kind = DocumentSignalKind.AmbiguousType, Score = 0, SupportsType = null,
					BlockIndex = -1, MatchedText = "",
					Description = $"Przewaga nad drugim typem ({winnerTypeScore - secondTypeScore}) poniżej marginesu {AmbiguityMargin}",
				});
			}

			return new DocumentClassificationResult
			{
				ActType = finalType,
				IsLegalAct = true,
				IsConsolidatedText = isConsolidated,
				// Tekst jednolity niczego nie nowelizuje — wykaz aktów zmieniających w obwieszczeniu
				// nie czyni obwieszczenia aktem zmieniającym (§ 102).
				IsAmending = isAmending && !isConsolidated,
				Confidence = confidence,
				Signals = SortSignals(signals),
				Justification = $"Rozpoznano: {finalType.ToFriendlyString()} (wynik {winnerTotal}, pewność {confidence})"
					+ (ambiguous ? "; typ niejednoznaczny." : "."),
			};
		}

		// ============================================================
		// Pomocnicze
		// ============================================================

		private static List<Block> Normalize(IReadOnlyList<DocumentBlock> blocks)
		{
			var result = new List<Block>();
			if (blocks is null)
				return result;

			int fallbackIndex = 0;
			foreach (var block in blocks)
			{
				int index = fallbackIndex++;
				if (block is null || block.IsEmpty)
					continue;

				var text = block.Text.Sanitize().Trim();
				if (text.Length == 0)
					continue;

				result.Add(new Block(block.Source.BlockIndex ?? index, text, block.StyleId));
			}
			return result;
		}

		private static DocumentClassificationResult TooFewBlocks(int count) =>
			new()
			{
				ActType = null,
				IsLegalAct = false,
				IsConsolidatedText = false,
				IsAmending = false,
				Confidence = 90,
				Signals = new List<DocumentSignal>
				{
					new()
					{
						Kind = DocumentSignalKind.NoTextLayer, Score = 0, SupportsType = null,
						BlockIndex = -1, MatchedText = "",
						Description = $"Za mało bloków tekstu do klasyfikacji ({count} < {MinBlocks}).",
					},
				},
				Justification = $"Dokument ma {count} niepustych bloków — zbyt mało, by rozpoznać rodzaj aktu.",
			};

		/// <summary>
		/// Doprecyzowanie typu po zsumowaniu punktów:
		/// - tekst jednolity → obwieszczenie (§ 102-106), nadrzędnie wobec dominacji jednostki z załącznika
		///   (zasada „nie przeinaczyć": TJ nie może być etykietowany jako ustawa/rozporządzenie z załącznika);
		/// - ustawa + tytuł/komendy zmieniające → ustawa zmieniająca (§ 96);
		/// - akt §-owy w kontekście organu JST / Dz. Urz. Woj. → akt prawa miejscowego (§ 143).
		/// </summary>
		private static LegalActType ResolveFinalType(LegalActType winner, bool isAmending, bool isLocal, bool isConsolidated)
		{
			if (isConsolidated)
				return LegalActType.Announcement;

			if (isAmending && winner == LegalActType.Statute)
				return LegalActType.AmendingStatute;

			if (isLocal && winner is LegalActType.Regulation or LegalActType.Resolution or LegalActType.ExecutiveOrder)
				return LegalActType.LocalLegalAct;

			return winner;
		}

		private static LegalActType? MatchHeaderType(string text)
		{
			if (ZtpPatterns.StatuteHeaderPattern.IsMatch(text)) return LegalActType.Statute;
			if (ZtpPatterns.RegulationHeaderPattern.IsMatch(text)) return LegalActType.Regulation;
			if (ZtpPatterns.AnnouncementHeaderPattern.IsMatch(text)) return LegalActType.Announcement;
			if (ZtpPatterns.ResolutionHeaderPattern.IsMatch(text)) return LegalActType.Resolution;
			if (ZtpPatterns.OrderHeaderPattern.IsMatch(text)) return LegalActType.ExecutiveOrder;
			return null;
		}

		private static LegalActType[] EnactmentVerbToTypes(string verb) =>
			verb.ToLowerInvariant() switch
			{
				"uchwala" => new[] { LegalActType.Resolution },
				"postanawia" => new[] { LegalActType.ExecutiveOrder },
				// „zarządza się, co następuje" jest wspólne dla rozporządzenia I zarządzenia —
				// oba dostają punkty, a nagłówek rodzaju aktu rozstrzyga.
				_ => new[] { LegalActType.Regulation, LegalActType.ExecutiveOrder },
			};

		private static LegalActType? SubjectToType(string subject)
		{
			var s = subject.ToLowerInvariant();
			if (s.Contains("ustaw")) return LegalActType.Statute;
			if (s.Contains("rozporządzeni")) return LegalActType.Regulation;
			if (s.Contains("uchwał")) return LegalActType.Resolution;
			if (s.Contains("zarządzeni")) return LegalActType.ExecutiveOrder;
			return null;
		}

		/// <summary>Rodzina „§": jeśli jakiś typ §-owy ma już punkty z nagłówka/organów — wzmocnij go; inaczej rozporządzenie.</summary>
		private static LegalActType SectionFamilyTarget(Dictionary<LegalActType, int> scores)
		{
			LegalActType best = LegalActType.Regulation;
			int bestScore = -1;
			foreach (var candidate in new[]
			{
				LegalActType.Regulation, LegalActType.Resolution,
				LegalActType.ExecutiveOrder, LegalActType.LocalLegalAct,
			})
			{
				int s = scores.GetValueOrDefault(candidate);
				if (s > bestScore)
				{
					bestScore = s;
					best = candidate;
				}
			}
			return best;
		}

		private static bool IsMetadataStyle(string? styleId)
		{
			if (string.IsNullOrEmpty(styleId))
				return false;
			return styleId.StartsWith("OZNRODZAKTU", StringComparison.OrdinalIgnoreCase)
				|| styleId.StartsWith("DATAAKTU", StringComparison.OrdinalIgnoreCase)
				|| styleId.StartsWith("TYTUAKTU", StringComparison.OrdinalIgnoreCase);
		}

		private static string Preview(string text) =>
			text.Length <= MatchPreviewLength ? text : text[..MatchPreviewLength] + "…";

		/// <summary>Czy blok zaczyna się cudzysłowem otwierającym (treść cytowana, np. przytoczony wzór/nowelizacja).</summary>
		private static bool StartsWithOpeningQuote(string text) =>
			text.Length > 0 && text[0] is '„' or '“' or '«' or '"';

		private static IReadOnlyList<DocumentSignal> SortSignals(List<DocumentSignal> signals) =>
			signals.OrderByDescending(s => s.Score).ToList();
	}
}
