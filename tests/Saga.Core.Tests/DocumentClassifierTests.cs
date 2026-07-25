using System.Collections.Generic;
using System.Linq;
using Saga.Model;
using Saga.Core.Ingest;
using Saga.Core.Services.Classify.Document;
using Xunit;

namespace Saga.Core.Tests
{
	/// <summary>
	/// Testy DocumentClassifier (Etap 4). Bloki budowane programowo — klasyfikator przyjmuje
	/// IReadOnlyList&lt;DocumentBlock&gt;, więc nie zależy od adapterów wejścia (DOCX/PDF/TXT).
	/// Pokrycie: pozytywy wszystkich rozpoznawanych rodzajów aktów + negatywy (§ 4 planu).
	/// </summary>
	public class DocumentClassifierTests
	{
		private static IReadOnlyList<DocumentBlock> Blocks(params string[] lines)
		{
			var list = new List<DocumentBlock>();
			for (int i = 0; i < lines.Length; i++)
				list.Add(new DocumentBlock { Text = lines[i], Source = new BlockSourceLocation { BlockIndex = i } });
			return list;
		}

		private static DocumentClassificationResult Classify(params string[] lines)
			=> new DocumentClassifier().Classify(Blocks(lines));

		// ============================================================
		// Pozytywy — poszczególne rodzaje aktów
		// ============================================================

		[Fact]
		public void Classify_Statute_IsRecognized()
		{
			var result = Classify(
				"USTAWA",
				"z dnia 5 marca 2024 r.",
				"o ochronie danych osobowych",
				"Art. 1. Ustawa reguluje ochronę danych osobowych.",
				"Art. 2. Organem właściwym jest Prezes Urzędu.",
				"Art. 3. Traci moc ustawa z dnia 1 stycznia 2000 r.",
				"Art. 4. Ustawa wchodzi w życie po upływie 14 dni od dnia ogłoszenia.");

			Assert.True(result.IsLegalAct);
			Assert.Equal(LegalActType.Statute, result.ActType);
			Assert.True(result.Confidence >= 75, $"Pewność: {result.Confidence}");
			Assert.False(result.IsAmending);
			Assert.False(result.IsConsolidatedText);
		}

		[Fact]
		public void Classify_Regulation_IsRecognized()
		{
			var result = Classify(
				"ROZPORZĄDZENIE MINISTRA FINANSÓW",
				"z dnia 12 czerwca 2023 r.",
				"w sprawie szczegółowych zasad rachunkowości",
				"Na podstawie art. 50 ust. 1 ustawy z dnia 29 września 1994 r. o rachunkowości zarządza się, co następuje:",
				"§ 1. Rozporządzenie określa zasady rachunkowości.",
				"§ 2. Ilekroć w rozporządzeniu jest mowa o jednostce, rozumie się przez to podmiot.",
				"§ 3. Rozporządzenie wchodzi w życie z dniem 1 stycznia 2024 r.");

			Assert.True(result.IsLegalAct);
			Assert.Equal(LegalActType.Regulation, result.ActType);
			Assert.True(result.Confidence >= 75, $"Pewność: {result.Confidence}");
		}

		[Fact]
		public void Classify_ConsolidatedText_IsAnnouncement_AndSecondaryHeaderIgnored()
		{
			var result = Classify(
				"OBWIESZCZENIE MARSZAŁKA SEJMU RZECZYPOSPOLITEJ POLSKIEJ",
				"z dnia 3 lutego 2022 r.",
				"w sprawie ogłoszenia jednolitego tekstu ustawy o systemie ubezpieczeń społecznych",
				"Na podstawie art. 16 ust. 1 ustawy z dnia 20 lipca 2000 r. o ogłaszaniu aktów normatywnych i niektórych innych aktów prawnych ogłasza się w załączniku do niniejszego obwieszczenia jednolity tekst ustawy.",
				"USTAWA",
				"z dnia 13 października 1998 r.",
				"o systemie ubezpieczeń społecznych",
				"Art. 1. Ubezpieczenia społeczne obejmują ubezpieczenie emerytalne.",
				"Art. 2. (uchylony)",
				"Art. 3. (utracił moc)",
				"Art. 4. Przepisy stosuje się do ubezpieczonych.");

			Assert.True(result.IsLegalAct);
			Assert.Equal(LegalActType.Announcement, result.ActType);
			Assert.True(result.IsConsolidatedText);
			Assert.Contains(result.Signals, s => s.Kind == DocumentSignalKind.IgnoredSecondaryHeader);
		}

		[Fact]
		public void Classify_Resolution_IsRecognized()
		{
			var result = Classify(
				"UCHWAŁA Nr 100",
				"RADY MINISTRÓW",
				"z dnia 10 maja 2023 r.",
				"w sprawie ustanowienia programu wieloletniego",
				"Na podstawie art. 136 ust. 2 ustawy z dnia 27 sierpnia 2009 r. o finansach publicznych uchwala się, co następuje:",
				"§ 1. Ustanawia się program wieloletni.",
				"§ 2. Program realizuje minister właściwy do spraw rozwoju.",
				"§ 3. Uchwała wchodzi w życie z dniem następującym po dniu ogłoszenia.");

			Assert.True(result.IsLegalAct);
			Assert.Equal(LegalActType.Resolution, result.ActType);
		}

		[Fact]
		public void Classify_ExecutiveOrder_IsRecognized()
		{
			var result = Classify(
				"ZARZĄDZENIE Nr 45",
				"PREZESA RADY MINISTRÓW",
				"z dnia 1 lipca 2023 r.",
				"w sprawie nadania statutu urzędowi",
				"Na podstawie art. 12 ust. 1 ustawy z dnia 8 sierpnia 1996 r. o Radzie Ministrów zarządza się, co następuje:",
				"§ 1. Nadaje się statut urzędowi.",
				"§ 2. Nadzór sprawuje Szef Kancelarii.",
				"§ 3. Zarządzenie wchodzi w życie z dniem ogłoszenia.");

			Assert.True(result.IsLegalAct);
			Assert.Equal(LegalActType.ExecutiveOrder, result.ActType);
		}

		[Fact]
		public void Classify_LocalLegalAct_IsRecognized()
		{
			var result = Classify(
				"UCHWAŁA Nr XV/123/2024",
				"RADY GMINY WIELKA WIEŚ",
				"z dnia 20 marca 2024 r.",
				"w sprawie miejscowego planu zagospodarowania przestrzennego",
				"Na podstawie art. 20 ust. 1 ustawy z dnia 27 marca 2003 r. o planowaniu i zagospodarowaniu przestrzennym uchwala się, co następuje:",
				"§ 1. Uchwala się miejscowy plan zagospodarowania przestrzennego.",
				"§ 2. Wykonanie uchwały powierza się Wójtowi Gminy.",
				"§ 3. Uchwała podlega ogłoszeniu w Dzienniku Urzędowym Województwa Małopolskiego.",
				"§ 4. Uchwała wchodzi w życie po upływie 14 dni od dnia ogłoszenia w Dz. Urz. Woj.");

			Assert.True(result.IsLegalAct);
			Assert.Equal(LegalActType.LocalLegalAct, result.ActType);
		}

		[Fact]
		public void Classify_AmendingStatute_IsRecognized_WithAmendingFlag()
		{
			var result = Classify(
				"USTAWA",
				"z dnia 15 września 2023 r.",
				"o zmianie ustawy o podatku dochodowym od osób fizycznych",
				"Art. 1. W ustawie z dnia 26 lipca 1991 r. o podatku dochodowym od osób fizycznych wprowadza się następujące zmiany:",
				"Art. 2. Ustawa wchodzi w życie po upływie 14 dni od dnia ogłoszenia.");

			Assert.True(result.IsLegalAct);
			Assert.Equal(LegalActType.AmendingStatute, result.ActType);
			Assert.True(result.IsAmending);
		}

		[Fact]
		public void Classify_TitleCaseStatuteHeader_IsMatched_AndLiftsShortAmendingStatuteOffThreshold()
		{
			// Realny format szczotek RCL (walidacja korpusu 2320 aktów): nagłówek to „Ustawa"
			// (kapitalizacja tytułowa), nie wersaliki „USTAWA". Krótka ustawa podwójnie zmieniająca
			// (2 artykuły) BEZ sygnału nagłówka lądowała dokładnie na progu conf=40 — o krok od
			// nieparsowania przy domyślnej polityce. Nagłówek „Ustawa" musi być rozpoznany.
			var result = Classify(
				"Ustawa",
				"z dnia 5 grudnia 2024 r.",
				"zmieniająca ustawę o zmianie ustawy o prawie autorskim i prawach pokrewnych oraz ustawy o grach hazardowych",
				"Art. 1. W ustawie z dnia 11 września 2015 r. o zmianie ustawy o prawie autorskim (Dz. U. poz. 1639) w art. 5 wprowadza się zmiany.",
				"Art. 2. Ustawa wchodzi w życie z dniem 1 stycznia 2025 r.");

			Assert.True(result.IsLegalAct);
			Assert.Equal(LegalActType.AmendingStatute, result.ActType);
			Assert.Contains(result.Signals, s => s.Kind == DocumentSignalKind.ActKindHeader);
			// Z nagłówkiem (+35) wynik jest wyraźnie ponad progiem 40 — nie wisi na krawędzi.
			Assert.True(result.Confidence >= 60, $"Pewność: {result.Confidence}");
		}

		[Fact]
		public void Classify_TitleCaseRegulationHeader_IsMatched()
		{
			// Analogicznie dla rozporządzenia: słowo kluczowe „Rozporządzenie" (kapitalizacja tytułowa),
			// organ w osobnym wierszu WIELKIMI (grupa organu pozostaje wielkoliterowa).
			var result = Classify(
				"Rozporządzenie",
				"MINISTRA FINANSÓW",
				"z dnia 12 czerwca 2023 r.",
				"w sprawie szczegółowych zasad rachunkowości",
				"Na podstawie art. 50 ust. 1 ustawy z dnia 29 września 1994 r. o rachunkowości zarządza się, co następuje:",
				"§ 1. Rozporządzenie określa zasady rachunkowości.",
				"§ 2. Ilekroć w rozporządzeniu jest mowa o jednostce, rozumie się przez to podmiot.",
				"§ 3. Rozporządzenie wchodzi w życie z dniem 1 stycznia 2024 r.");

			Assert.True(result.IsLegalAct);
			Assert.Equal(LegalActType.Regulation, result.ActType);
			Assert.Contains(result.Signals, s => s.Kind == DocumentSignalKind.ActKindHeader);
		}

		// ============================================================
		// Negatywy — nie rozpoznano rodzaju aktu
		// ============================================================

		[Fact]
		public void Classify_ShoppingList_IsNotAnAct()
		{
			var result = Classify(
				"Lista zakupów na weekend",
				"- mleko i masło",
				"- chleb razowy",
				"- warzywa sezonowe",
				"Do zobaczenia w sklepie.");

			Assert.False(result.IsLegalAct);
			Assert.Null(result.ActType);
		}

		[Fact]
		public void Classify_PressArticleCitingStatute_IsNotAnAct()
		{
			var result = Classify(
				"Nowe przepisy o ochronie danych już obowiązują",
				"Zgodnie z art. 5 ustawy z dnia 10 maja 2018 r. o ochronie danych osobowych administrator musi zgłosić naruszenie.",
				"Eksperci komentują wprowadzone zmiany jako korzystne dla obywateli.",
				"Więcej szczegółów w kolejnym wydaniu.");

			Assert.False(result.IsLegalAct);
			Assert.Null(result.ActType);
		}

		[Fact]
		public void Classify_ContractWithSectionSigns_IsNotAnAct()
		{
			// Najtrudniejszy negatyw: umowa używa „§" jak akt, ale bez nagłówka rodzaju,
			// formuły kompetencyjnej i klauzuli wejścia w życie z podmiotem aktu.
			var result = Classify(
				"UMOWA O DZIEŁO",
				"zawarta w dniu 5 marca 2024 r. w Warszawie",
				"§ 1. Przedmiotem umowy jest wykonanie projektu graficznego.",
				"§ 2. Wynagrodzenie wynosi 10 000 zł.",
				"§ 3. Umowa wchodzi w życie z dniem podpisania.",
				"§ 4. W sprawach nieuregulowanych stosuje się przepisy Kodeksu cywilnego.");

			Assert.False(result.IsLegalAct);
			Assert.Null(result.ActType);
		}

		[Fact]
		public void Classify_EmptyDocument_IsNotAnAct_WithNoTextLayerSignal()
		{
			var result = new DocumentClassifier().Classify(Blocks());

			Assert.False(result.IsLegalAct);
			Assert.Null(result.ActType);
			Assert.Contains(result.Signals, s => s.Kind == DocumentSignalKind.NoTextLayer);
		}

		[Fact]
		public void Classify_TooFewBlocks_IsNotAnAct_WithNoTextLayerSignal()
		{
			var result = Classify("USTAWA", "z dnia 1 stycznia 2020 r.");

			Assert.False(result.IsLegalAct);
			Assert.Contains(result.Signals, s => s.Kind == DocumentSignalKind.NoTextLayer);
		}

		[Fact]
		public void Classify_BlankBlocks_AreIgnored_TooFewRemain()
		{
			var result = Classify("USTAWA", "   ", "\t", "");

			Assert.False(result.IsLegalAct);
			Assert.Contains(result.Signals, s => s.Kind == DocumentSignalKind.NoTextLayer);
		}

		// ============================================================
		// Własności ogólne
		// ============================================================

		[Fact]
		public void Classify_Act_ProducesEvidenceSignalsAndJustification()
		{
			var result = Classify(
				"USTAWA",
				"z dnia 5 marca 2024 r.",
				"o ochronie danych osobowych",
				"Art. 1. Ustawa reguluje ochronę danych osobowych.",
				"Art. 2. Organem właściwym jest Prezes Urzędu.",
				"Art. 3. Ustawa wchodzi w życie po upływie 14 dni od dnia ogłoszenia.");

			Assert.NotEmpty(result.Signals);
			Assert.Contains(result.Signals, s => s.Kind == DocumentSignalKind.ActKindHeader);
			Assert.False(string.IsNullOrWhiteSpace(result.Justification));
			// Sygnały posortowane malejąco wg wagi
			var scores = result.Signals.Select(s => s.Score).ToList();
			Assert.True(scores.SequenceEqual(scores.OrderByDescending(x => x)));
		}

		// ============================================================
		// Regresje z przeglądu adwersaryjnego Etapu 4
		// ============================================================

		[Fact]
		public void Classify_NationalRegulationMentioningLocalOrganInBody_StaysRegulation()
		{
			// Rozporządzenie krajowe rutynowo nakłada obowiązki na wójta/radę gminy w treści —
			// pojedyncza wzmianka NIE może przekwalifikować aktu na akt prawa miejscowego.
			var result = Classify(
				"ROZPORZĄDZENIE MINISTRA FINANSÓW",
				"z dnia 12 czerwca 2023 r.",
				"w sprawie szczegółowych zasad sprawozdawczości",
				"Na podstawie art. 50 ust. 1 ustawy o finansach publicznych zarządza się, co następuje:",
				"§ 1. Rozporządzenie określa zasady sprawozdawczości.",
				"§ 2. Wójt sporządza sprawozdanie i przekazuje je wojewodzie.",
				"§ 3. Rozporządzenie wchodzi w życie z dniem 1 stycznia 2024 r.");

			Assert.True(result.IsLegalAct);
			Assert.Equal(LegalActType.Regulation, result.ActType);
		}

		[Fact]
		public void Classify_NationalResolutionMentioningGminaInBody_StaysResolution()
		{
			var result = Classify(
				"UCHWAŁA Nr 100",
				"RADY MINISTRÓW",
				"z dnia 10 maja 2023 r.",
				"w sprawie programu wsparcia samorządów",
				"Na podstawie art. 136 ust. 2 ustawy o finansach publicznych uchwala się, co następuje:",
				"§ 1. Ustanawia się program wsparcia.",
				"§ 2. Program obejmuje zadania nałożone na wójta każdej gminy.",
				"§ 3. Uchwała wchodzi w życie z dniem następującym po dniu ogłoszenia.");

			Assert.True(result.IsLegalAct);
			Assert.Equal(LegalActType.Resolution, result.ActType);
		}

		[Fact]
		public void Classify_PressRoundupQuotingActTitles_IsNotAnAct()
		{
			// Przegląd prasowy cytujący fragmenty tytułów aktów (tytuł zmieniający + obwieszczenie TJ)
			// nie ma żadnego silnego sygnału strukturalnego — musi zostać nie-aktem (bramka backbone).
			var result = Classify(
				"Legislacyjny przegląd tygodnia",
				"Sejm uchwalił ustawę o zmianie ustawy o VAT oraz niektórych innych ustaw.",
				"Wkrótce ukaże się obwieszczenie w sprawie ogłoszenia jednolitego tekstu ustawy o PIT.",
				"Zmiany skomentowali eksperci podatkowi.");

			Assert.False(result.IsLegalAct);
			Assert.Null(result.ActType);
		}

		[Fact]
		public void Classify_InternalRegulationWithSectionsAndDate_IsNotAnAct()
		{
			// Regulamin wewnętrzny: § od 1 + data kanoniczna, ale bez nagłówka rodzaju aktu,
			// formuły kompetencyjnej i klauzuli wejścia w życie z podmiotem aktu → nie-akt.
			var result = Classify(
				"REGULAMIN WYNAGRADZANIA",
				"z dnia 1 stycznia 2024 r.",
				"§ 1. Regulamin określa zasady wynagradzania pracowników.",
				"§ 2. Wynagrodzenie wypłaca się do 10. dnia miesiąca.",
				"§ 3. Regulamin obowiązuje od dnia ogłoszenia.");

			Assert.False(result.IsLegalAct);
			Assert.Null(result.ActType);
		}

		[Fact]
		public void Classify_MeetingResolutionSentence_IsNotAnAct()
		{
			// Zdanie zaczynające się od „Uchwała"/„Zarządzenie" nie jest nagłówkiem rodzaju aktu.
			var result = Classify(
				"Protokół z posiedzenia zarządu wspólnoty",
				"Uchwała wchodzi w życie z dniem podjęcia.",
				"Zarządzenie zostało przyjęte jednogłośnie.",
				"Na tym protokół zakończono.");

			Assert.False(result.IsLegalAct);
			Assert.Null(result.ActType);
		}

		[Fact]
		public void Classify_RadaMiejskaNominative_IsLocalLegalAct()
		{
			// „Rada Miejska" (mianownik) to standardowa nazwa organu JST — musi być rozpoznana.
			var result = Classify(
				"UCHWAŁA Nr XV/200/2024",
				"RADA MIEJSKA W ŁODZI",
				"z dnia 5 lutego 2024 r.",
				"w sprawie ustalenia stawek opłat lokalnych",
				"Na podstawie art. 40 ust. 1 ustawy o samorządzie gminnym uchwala się, co następuje:",
				"§ 1. Ustala się stawki opłat.",
				"§ 2. Uchwała podlega ogłoszeniu w Dzienniku Urzędowym Województwa Łódzkiego.",
				"§ 3. Uchwała wchodzi w życie po upływie 14 dni od dnia ogłoszenia w Dz. Urz. Woj.");

			Assert.True(result.IsLegalAct);
			Assert.Equal(LegalActType.LocalLegalAct, result.ActType);
		}

		[Fact]
		public void Classify_UnitSuperscriptNotCountedAsFootnoteDensity()
		{
			// Numery jednostek z indeksem górnym [N] (§ 89 ust. 6) NIE są odnośnikami TJ [N)] (§ 163) —
			// zwykła ustawa z jednostkami superskryptowymi nie może zyskiwać sygnału FootnoteDensity.
			var result = Classify(
				"USTAWA",
				"z dnia 5 marca 2024 r.",
				"o zmianach systemowych",
				"Art. 1. Przepis ogólny.",
				"Art. 2[1]. Przepis dodany pierwszy.",
				"Art. 2[2]. Przepis dodany drugi.",
				"Art. 3. Ustawa wchodzi w życie po upływie 14 dni od dnia ogłoszenia.");

			Assert.Equal(LegalActType.Statute, result.ActType);
			Assert.DoesNotContain(result.Signals, s => s.Kind == DocumentSignalKind.FootnoteDensity);
		}

		[Fact]
		public void Classify_IsDeterministic()
		{
			string[] lines =
			{
				"ROZPORZĄDZENIE MINISTRA ZDROWIA",
				"z dnia 2 lutego 2022 r.",
				"w sprawie standardów opieki",
				"Na podstawie art. 5 ustawy o świadczeniach zarządza się, co następuje:",
				"§ 1. Rozporządzenie określa standardy.",
				"§ 2. Rozporządzenie wchodzi w życie z dniem ogłoszenia.",
			};

			var first = new DocumentClassifier().Classify(Blocks(lines));
			var second = new DocumentClassifier().Classify(Blocks(lines));

			Assert.Equal(first.ActType, second.ActType);
			Assert.Equal(first.Confidence, second.Confidence);
			Assert.Equal(first.Signals.Count, second.Signals.Count);
			Assert.Equal(first.Justification, second.Justification);
		}

		// ============================================================
		// Regresje z korpusu aktów ogłoszonych (szczotki RCL) + przeglądu adwersaryjnego
		// ============================================================

		[Fact]
		public void Classify_ConsolidatedTextTitleCaseHeader_IsAnnouncement_NotAnnexStatute()
		{
			// Realna szczotka TJ: nagłówek „Obwieszczenie" (kapitalizacja tytułowa), organ „Marszałka Sejmu",
			// formuła „ust. 1 zdanie pierwsze", wykaz aktów zmieniających, a w załączniku pełna USTAWA.
			// Musi wyjść OBWIESZCZENIE (nie ustawa z załącznika) — zasada „nie przeinaczyć". Tekst jednolity
			// niczego nie nowelizuje, więc IsAmending musi być false mimo wykazu „o zmianie ustawy".
			var result = Classify(
				"Obwieszczenie",
				"Marszałka Sejmu Rzeczypospolitej Polskiej",
				"z dnia 21 czerwca 2024 r.",
				"w sprawie ogłoszenia jednolitego tekstu ustawy – Kodeks cywilny",
				"1. Na podstawie art. 16 ust. 1 zdanie pierwsze ustawy z dnia 20 lipca 2000 r. o ogłaszaniu aktów normatywnych i niektórych innych aktów prawnych ogłasza się w załączniku do niniejszego obwieszczenia jednolity tekst ustawy.",
				"1) ustawą z dnia 13 lipca 2023 r. o zmianie ustawy o udostępnianiu informacji o środowisku;",
				"USTAWA",
				"z dnia 23 kwietnia 1964 r.",
				"Kodeks cywilny",
				"Art. 1. Kodeks reguluje stosunki cywilnoprawne.",
				"Art. 2. (uchylony)",
				"Art. 3. (utracił moc)",
				"Art. 4. Ustawa wchodzi w życie po upływie 14 dni od dnia ogłoszenia.");

			Assert.True(result.IsLegalAct);
			Assert.Equal(LegalActType.Announcement, result.ActType);
			Assert.True(result.IsConsolidatedText);
			Assert.False(result.IsAmending);
			Assert.Contains(result.Signals, s => s.Kind == DocumentSignalKind.ActKindHeader);
			Assert.Contains(result.Signals, s => s.Kind == DocumentSignalKind.MarshalOfSejmIssuer);
			Assert.Contains(result.Signals, s => s.Kind == DocumentSignalKind.IgnoredSecondaryHeader);
		}

		[Fact]
		public void Classify_NonConsolidatedAnnouncement_TitleCaseHeader_IsAnnouncement()
		{
			// Obwieszczenie nie-TJ (waloryzacyjne „w sprawie wysokości…") — z formy to obwieszczenie,
			// więc rodzaj = Announcement, ale NIE tekst jednolity.
			var result = Classify(
				"Obwieszczenie",
				"Ministra Infrastruktury",
				"z dnia 25 lipca 2025 r.",
				"w sprawie wysokości stawki opłaty legalizacyjnej obowiązującej od dnia 1 stycznia 2026 r.",
				"Na podstawie art. 190 ust. 9 ustawy z dnia 20 lipca 2017 r. – Prawo wodne ogłasza się, co następuje:",
				"Stawka opłaty legalizacyjnej wynosi 5000 zł.",
				"Minister Infrastruktury: wz. P. Koperski");

			Assert.True(result.IsLegalAct);
			Assert.Equal(LegalActType.Announcement, result.ActType);
			Assert.False(result.IsConsolidatedText);
		}

		[Fact]
		public void Classify_LowercaseObwieszczenieHeader_IsAnnouncement()
		{
			// Wariant szablonu bez stylów: nagłówek zapisany małą literą „obwieszczenie" (realne szczotki RCL),
			// organ w osobnym wierszu. Musi być rozpoznany jako obwieszczenie.
			var result = Classify(
				"obwieszczenie",
				"Ministra Edukacji",
				"z dnia 5 września 2025 r.",
				"w sprawie ogólnopolskiej sieci branżowych centrów umiejętności na lata 2023–2028",
				"Na podstawie art. 8a ust. 8 ustawy z dnia 14 grudnia 2016 r. – Prawo oświatowe ogłasza się, co następuje:",
				"Ustala się ogólnopolską sieć branżowych centrów umiejętności.",
				"Minister Edukacji: B. Nowacka");

			Assert.True(result.IsLegalAct);
			Assert.Equal(LegalActType.Announcement, result.ActType);
			Assert.Contains(result.Signals, s => s.Kind == DocumentSignalKind.ActKindHeader);
		}

		[Fact]
		public void Classify_ProseStartingWithObwieszczenie_IsNotAnAct()
		{
			// „Obwieszczenie Ministra … wywołało …" to zdanie prozy (czasownik małą literą), NIE nagłówek
			// rodzaju aktu. Gdyby nagłówek trafił, data (+10) i przedmiot (+8) przekroczyłyby próg —
			// dlatego zaostrzona klasa znaków organu musi to odrzucić (bramka backbone/negatywów).
			var result = Classify(
				"Obwieszczenie Ministra Finansów wywołało reakcje rynku.",
				"z dnia 5 maja 2024 r.",
				"w sprawie nowych stawek podatkowych wypowiedzieli się analitycy",
				"Więcej szczegółów w kolejnym wydaniu.");

			Assert.False(result.IsLegalAct);
			Assert.Null(result.ActType);
		}

		[Fact]
		public void Classify_RegulationAmendingZtpQuotingConsolidationFormula_StaysRegulation()
		{
			// Rozporządzenie zmieniające ZTP przytacza wzór formuły TJ w CUDZYSŁOWIE, wcześnie w treści.
			// Cytowana formuła (blok zaczyna się od „) NIE może uczynić z aktu zmieniającego tekstu jednolitego.
			var result = Classify(
				"Rozporządzenie",
				"Prezesa Rady Ministrów",
				"z dnia 26 stycznia 2026 r.",
				"zmieniające rozporządzenie w sprawie „Zasad techniki prawodawczej”",
				"Na podstawie art. 14 ust. 4 pkt 1 ustawy z dnia 8 sierpnia 1996 r. o Radzie Ministrów zarządza się, co następuje:",
				"§ 1. W rozporządzeniu wprowadza się następujące zmiany:",
				"„1. Na podstawie art. 16 ust. 1 ustawy z dnia 20 lipca 2000 r. o ogłaszaniu aktów normatywnych i niektórych innych aktów prawnych ogłasza się w załączniku do niniejszego obwieszczenia jednolity tekst.”",
				"§ 2. Rozporządzenie wchodzi w życie z dniem 1 marca 2026 r.");

			Assert.Equal(LegalActType.Regulation, result.ActType);
			Assert.False(result.IsConsolidatedText);
		}

		[Fact]
		public void Classify_StatuteMentioningObwieszczenieInBody_StaysStatute()
		{
			// Strażnik: ustawa może w treści wspominać obwieszczenie / Marszałka Sejmu — pierwszy nagłówek
			// (USTAWA) wygrywa, wzmianka w treści nie może przekwalifikować aktu na obwieszczenie.
			var result = Classify(
				"USTAWA",
				"z dnia 5 marca 2024 r.",
				"o zmianach porządkowych",
				"Art. 1. Ustawa porządkuje przepisy.",
				"Art. 2. Traci moc obwieszczenie Marszałka Sejmu z dnia 1 stycznia 2020 r.",
				"Art. 3. Ustawa wchodzi w życie po upływie 14 dni od dnia ogłoszenia.");

			Assert.True(result.IsLegalAct);
			Assert.Equal(LegalActType.Statute, result.ActType);
			Assert.False(result.IsConsolidatedText);
		}
	}
}
