# Backlog — znane luki i świadomie odroczone prace

Rzeczy, o których wiemy, że ich nie ma. Każdy punkt był rozważony i odłożony z podanego powodu —
to nie lista życzeń ani „to do", tylko rejestr świadomych dziur, żeby nikt nie odkrywał ich
od nowa i nie brał za błąd czegoś, co jest decyzją.

Stan zweryfikowany w kodzie 2026-07-25. Uzasadnienia decyzji projektowych: [architecture.md](architecture.md),
sekcja 13. Reguły ZTP stojące za tymi punktami: [ztp-struktura-aktow.md](ztp-struktura-aktow.md).

## 1. Gałąź bezstylowa (DOCX bez szablonu / PDF / TXT)

Styl Word pozostaje najsilniejszym sygnałem; poniższe zachowania są dostępne **wyłącznie** przy
stylach szablonu i nie mają odpowiednika w rozpoznawaniu z samej treści.

| Luka | Stan faktyczny | Dlaczego odłożone |
|---|---|---|
| **Część wspólna (WrapUp) bez stylu** | Wykrywana wyłącznie przez styl `CZ_WSP_*` ([ParagraphClassifier.cs:132-135](../WordParserCore/Services/Classify/ParagraphClassifier.cs#L132-L135)); `IsWrapUpByText` tylko potwierdza lub obniża pewność. W gałęzi bezstylowej „– tekst" nadal klasyfikuje się jako Tiret. | Nie da się rozstrzygnąć samym tekstem: prawdziwe tirety bywają pisane półpauzą, więc treść nie różnicuje tiretu od części wspólnej. Potrzebny sygnał układu (wcięcie na wysokości wprowadzenia do wyliczenia, § 58 ZTP) albo kontekst otwartych list — a `ClassificationInput` to dziś rekord `(Text, StyleId)` i layout do klasyfikatora nie dochodzi (trafia osobnym parametrem tylko do `StructureProcessor.GetTiretDepth`). |
| **Głębokość tiretu bez sygnału** | Dwa poziomy priorytetu: styl (`2TIR`/`3TIR`/`TIR`) → wcięcie z `ParsingContext.OpenTiretIndents` (tolerancja ±120 twips, cap 3). Brak obu (czysty TXT) → zawsze poziom 1. | TXT nie niesie wcięć w sposób wiarygodny. Świadomie wybrano deterministyczne „poziom 1" zamiast heurystyki na interpunkcji. |
| **Nowelizacje zagnieżdżone (`ZZ/`)** | Trigger wykryty wewnątrz otwartego cytatu jest tylko flagowany (`QuoteBalanceTracker.MarkNestedTrigger`) — zagnieżdżony obiekt `Amendment` nie jest budowany. | Wymaga rekurencyjnego zbierania w cytacie; bez stylów nie ma sygnału zamknięcia wewnętrznego poziomu. |
| **`CommonPartOf` w nowelizacji** | Dostępne wyłącznie z mapy stylów ([AmendmentStyleDecoder.cs:79](../WordParserCore/Helpers/AmendmentStyleDecoder.cs#L79), ~240 wpisów `StyleLibraryMapper`). Bez stylu — nierozpoznawane. | Informacja „część wspólna czego" jest w szablonie zakodowana w nazwie stylu i nie ma odpowiednika w treści. |

Miejsca oznaczone w kodzie: `[Fact(Skip = "Etap 6 …")]` w
[ParserEquivalenceTests.cs:97](../WordParserCore.Tests/ParserEquivalenceTests.cs#L97) oraz testy
charakteryzujące w `ParagraphClassifierStylelessTests` (utrwalają obecne, częściowo błędne wyniki —
zmiana któregokolwiek wymaga jawnej aktualizacji z uzasadnieniem ZTP).

## 2. Klasyfikacja dokumentu

- **Sygnał ujemny „przepisy karne"** — rozporządzenie nie może zawierać przepisów karnych ani
  o administracyjnych karach pieniężnych (§ 117 ZTP), więc sygnatury „podlega karze …",
  „administracyjnej karze pieniężnej" powinny odejmować punkty typowi `Regulation`. Brak wzorca
  w `ZtpPatterns` i brak wartości w `DocumentSignalKind` — sygnał nie istnieje. Materiał źródłowy
  gotowy: [ztp-struktura-aktow.md](ztp-struktura-aktow.md) sekcja 5.7.

## 3. Testy, których plan przewidywał, a nie powstały

- **Korpus ekwiwalencji plikowej** — zakładano ten sam akt w czterech wariantach
  (DOCX-szablon / DOCX bez stylów / PDF / TXT) w `DocRepo/equivalence/`. Katalog nie istnieje;
  `ParserEquivalenceTests` porównuje modele z dokumentów budowanych w pamięci.
- **Property-testy ciągłości numeracji** — `NumberingContinuityPropertyTests` (łańcuchy następników
  n→n+1, n→na→nb, wstawki § 89, w tym wieloliterowe Xa→Xaa→Xab) nie istnieją.
- **Smoke-test na samym ZTP** — konwersja [ZTP-2026-300.md](ZTP-2026-300.md) → TXT z oczekiwaniem
  „rozporządzenie, wysoka pewność" i sparsowanymi jednostkami `§`.

## 4. Ryzyka do pilnowania przy zmianach

- **Tiret vs część wspólna** — półpauza na początku wiersza jest niejednoznaczna; każda zmiana
  w tym obszarze rusza gałąź bezstylową i wymaga przejrzenia testów charakteryzujących.
- **Cytaty w części wstępnej obwieszczenia** — obwieszczenie TJ (§ 103–104 ZTP) cytuje przepisy
  zmieniające i pominięte, więc śledzenie cudzysłowów (`QuoteBalanceTracker`) obowiązuje także przy
  parsowaniu obwieszczeń, nie tylko nowelizacji. Fałszywy trigger w cytacie = błędna nowelizacja.

---

Uwagi zewnętrzne (PG) mają własne, niewersjonowane plany w `docs/internal/` — nie dubluj ich tutaj.
