# Architektura projektu WordParser

> Dokument opisuje domenę projektową, moduły, role klas, przepływy danych, zależności między warstwami oraz konwencje stosowane w projekcie WordParser.

> Aktualny na dzień: 2026-03-11

---

## 1. Przegląd domeny

WordParser to toolkit .NET 10 służący do **parsowania polskich aktów prawnych** (dokumenty Word/DOCX) do modelu obiektowego (DTO), a następnie do formatów wyjściowych (XML/XLSX). Domena obejmuje:

- **Hierarchię jednostek redakcyjnych**: `Article → Paragraph → Point → Letter → Tiret → DoubleTiret`
- **Hierarchię jednostek systematyzujących**: `Part → Book → Title → Division → Chapter → Subchapter`
- **Nowelizacje** (amendments): zmiany legislacyjne wewnątrz aktów prawnych
- **Metadane publikatorów**: odniesienia do Dziennika Ustaw (Dz. U.)

---

## 2. Moduły (projekty)

### 2.1 `ModelDto` — Warstwa modelu danych


Czyste klasy DTO bez logiki biznesowej. Definiuje strukturę drzewa aktu prawnego.

| Podkatalog / Plik | Opis |
|---|---|
| `EditorialUnits/` | Jednostki redakcyjne: `Article`, `Paragraph`, `Point`, `Letter`, `Tiret`, `CommonPart` |
| `SystematizingUnits/` | Jednostki systematyzujące: `Part`, `Book`, `Title`, `Division`, `Chapter`, `Subchapter` |
| `BaseEntity.cs` | Abstrakcyjna klasa bazowa z wspólnymi właściwościami (Guid, Number, ContentText, Parent, eId) |
| `LegalDocument.cs` | Korzeń modelu — wrapper całego aktu prawnego z metadanymi i hierarchią |
| `EntityNumber.cs` | Model numeru encji z rozbiciem na: `NumericPart`, `LexicalPart`, `Superscript` |
| `Amendment.cs` | Model nowelizacji (typ operacji, treść, cel, data wejścia w życie) |
| `AmendmentContent.cs` | Treść nowelizacji — hierarchiczny fragment aktu (artykuły, ustępy, punkty...) |
| `StructuralAmendmentReference.cs` | Cel nowelizacji — ścieżka strukturalna do zmienianej jednostki |
| `TextSegment.cs` / `TextSegmentType.cs` | Segmenty tekstu (zdania) wewnątrz jednostek redakcyjnych |
| `ValidationMessage.cs` | Komunikaty diagnostyczne (Info/Warning/Error/Critical) |
| `JournalInfo.cs` | Metadane publikatora (Dz. U. — rok, pozycje) |
| `CommonPartType.cs` | Enum: `Intro` / `WrapUp` |
| `LegalActType.cs` | Enum: `Statute` / `Regulation` / `Code` |

**Interfejsy kontraktowe:**

| Interfejs | Opis |
|---|---|
| `IHasAmendments` | Encja może posiadać nowelizację (`Amendment? Amendment`) |
| `IHasCommonParts` | Encja może posiadać części wspólne (`List<CommonPart>`) |
| `IHasTextSegments` | Encja może być dzielona na segmenty tekstu (`List<TextSegment>`) |
| `ISystematizingUnit` | Kontrakt jednostek systematyzujących (`Heading`, `IsImplicit`) |

### 2.2 `WordParserCore` — Warstwa logiki parsowania

Silnik parsujący dokumenty DOCX. Zależy od `ModelDto` i `DocumentFormat.OpenXml`.

| Ścieżka | Rola |
|---|---|
| `LegalDocumentParser.cs` | **Punkt wejścia** — statyczna metoda `Parse(filePath)` lub `Parse(WordprocessingDocument)` |
| `LoggerConfig.cs` | Konfiguracja Serilog (konsola + plik `logs/log.txt`) |
| `Exceptions/` | Wyjątki parsowania (`ParsingException`) |
| `Helpers/` | Metody rozszerzające, dekodery styli, helpery |
| `Services/Classify/` | **Warstwa klasyfikacji** — klasyfikator akapitów, wzorce regex, system kar, rozwiązywanie konfliktów |
| `Services/` | Serwisy domenowe (numeracja, referencje, publikatory) |
| `Services/Parsing/` | **Pipeline parsowania** — orkiestrator, buildery, przetwarzanie struktury, zarządzanie nowelizacjami |
| `Services/Converters/` | Konwertery do XML (w trakcie implementacji, nieaktywne) |

### 2.3 `WordParser` — Narzędzie konsolowe (CLI)

Interfejs wiersza poleceń korzystający z `WordParserCore`. Tworzy kopie zapasowe przed modyfikacją.

### 2.4 `WordParserApi` — Web API (wstrzymany)

ASP.NET Core Web API z Swagger/OpenAPI. Projekt wstrzymany — zalecane użycie CLI.

### 2.5 `WordParserCore.Tests` — Testy jednostkowe

Testy xUnit pokrywające kluczowe scenariusze parsowania.

---

## 3. Diagram zależności między projektami

```
WordParser (CLI)  ──────►  WordParserCore  ──────►  ModelDto
WordParserApi (API)  ───►  WordParserCore  ──────►  ModelDto
WordParserCore.Tests ►  WordParserCore  ──────►  ModelDto
```

Zależności zewnętrzne:
- `DocumentFormat.OpenXml` — parsowanie dokumentów DOCX (tylko w `WordParserCore`)
- `Serilog` — strukturalne logowanie (w `WordParserCore`)
- `xUnit` — framework testowy (w `WordParserCore.Tests`)

---

## 4. Hierarchia modelu danych

### 4.1 Jednostki systematyzujące (organizacyjne)

```
Part (Część)
 └── Book (Księga)
      └── Title (Tytuł)
           └── Division (Dział)
                └── Chapter (Rozdział)
                     └── Subchapter (Oddział)
                          └── [Articles...]
```

Każdy dokument ma **pełną minimalną hierarchię** — jednostki nieobecne w tekście mają `IsImplicit = true` i są pomijane w generowaniu eId.

### 4.2 Jednostki redakcyjne (treściowe)

```
Article (art.)           — kontener; zawsze ≥1 Paragraph
 └── Paragraph (ust.)   — może być implicit (jedyny ustęp w artykule)
      ├── CommonPart     — intro (przed listą) lub wrapUp (po liście)
      └── Point (pkt)
           ├── CommonPart
           └── Letter (lit.)
                ├── CommonPart
                └── Tiret (tir.)
                     └── Tiret (podwójny tiret / 2TIR)
```

### 4.3 Budowanie identyfikatorów (eId)

Format: `art_5__ust_2__pkt_3__lit_a__tir_1`

- Separator segmentów: `__` (double underscore)
- Separator prefiks-numer: `_` (single underscore)
- Jednostki implicit (`IsImplicit = true`) są **pomijane** w eId
- Logika w `BaseEntity.Id` (getter) — buduje od bieżącej encji do korzenia, odwraca kolejność

### 4.4 Klasa bazowa `BaseEntity`

Wspólne cechy wszystkich encji:

| Właściwość | Typ | Opis |
|---|---|---|
| `Guid` | `Guid` | Unikalny identyfikator |
| `UnitType` | `UnitType` | Typ semantyczny (Article, Paragraph, Point...) |
| `DisplayLabel` | `string` | Etykieta wyświetlana ("art.", "ust.", "pkt"...) |
| `EIdPrefix` | `string` | Prefiks eId ("art", "ust", "pkt", "lit", "tir") |
| `Number` | `EntityNumber?` | Numer encji z rozbiciem na komponenty |
| `ContentText` | `string` | Pełny tekst jednostki |
| `Parent` | `BaseEntity?` | Referencja do rodzica |
| `ValidationMessages` | `List<ValidationMessage>` | Diagnostyka parsowania |
| `Id` | `string` (virtual) | Hierarchiczny eId budowany dynamicznie |

---

## 5. Pipeline parsowania — przepływ danych

### 5.1 Punkt wejścia

```
LegalDocumentParser.Parse(filePath)
    │
    ├── Otwiera DOCX przez OpenXml SDK
    ├── Tworzy LegalDocument z domyślną hierarchią systematyzującą
    ├── Tworzy ParsingContext (stan parsowania)
    ├── Iteruje po Paragraph[] z MainDocumentPart
    │   └── ParserOrchestrator.ProcessParagraph(paragraph, context)
    └── ParserOrchestrator.Finalize(context)  — flush bufora nowelizacji
```

### 5.2 Orkiestrator (`ParserOrchestrator`)

Jednoprzebiegowy pipeline ze stanem. Dla każdego akapitu:

```
1. Sanityzacja tekstu (StringExtensions.Sanitize)
2. Pobranie StyleId (ParagraphExtensions.StyleId)
3. Budowanie NumberingHint (oczekiwana numeracja na bieżącym poziomie)
4. Klasyfikacja (ParagraphClassifier.Classify) → ClassificationResult (Kind, Confidence, Penalties)
5. Zarządzanie stanem nowelizacji (AmendmentStateManager.UpdateState)
   ├── Detekcja triggera: "otrzymuje brzmienie:", "dodaje się", "uchyla się"
   └── Przejście do/z trybu nowelizacji
6. Zbieranie treści nowelizacji (AmendmentStateManager.Collect) lub finalizacja (Flush)
7. Jeśli poza nowelizacją → budowanie struktury (StructureProcessor.Process):
   ├── Article  → ArticleBuilder.Build()
   ├── Paragraph → ParagraphBuilder.Build()
   ├── Point    → PointBuilder.Build()
   ├── Letter   → LetterBuilder.Build()
   ├── Tiret    → TiretBuilder.Build()
   └── WrapUp   → handleowanie CommonPart.WrapUp (dla CZ_WSP_*)
8. Diagnostyka klasyfikacji (ValidationReporter.AddClassificationWarning)
9. Aktualizacja kontekstu (CurrentArticle/Paragraph/Point/Letter/TiretStack)
10. Aktualizacja referencji strukturalnej (LegalReferenceService)
11. Parsowanie publikatorów (JournalReferenceService) — tylko dla artykułów
12. ParserOrchestrator.Finalize() — flush ostatniej bufora nowelizacji (jeśli istnieje)
```

### 5.3 Kontekst parsowania (`ParsingContext`)

Stan mutowalny przechowujący bieżącą pozycję w drzewie:

| Pole | Opis |
|---|---|
| `Document` | Aktualny `LegalDocument` (korzeń) |
| `Subchapter` | Bieżący oddział (kontener artykułów) |
| `CurrentArticle` | Ostatnio przetworzony artykuł |
| `CurrentParagraph` | Bieżący ustęp |
| `CurrentPoint` | Bieżący punkt |
| `CurrentLetter` | Bieżąca litera |
| `TiretStack` | Stos tiretów — `List<DtoTiret>` wspierający zagnieżdżone tirety (1TIR/2TIR/3TIR); `CurrentTiret` jako property `TiretStack[^1]` |
| `InsideAmendment` | Flaga: czy jesteśmy wewnątrz treści nowelizacji |
| `AmendmentTriggerDetected` | Flaga: czy wykryto zwrot "otrzymuje brzmienie:" |
| `AmendmentCollector` | Bufor akapitów nowelizacji |
| `AmendmentOwner` | Encja-właściciel bieżącej nowelizacji |
| `ReferenceService` | Serwis referencji strukturalnych |
| `CurrentStructuralReference` | Bieżąca pozycja (art/ust/pkt/lit/tir) — aktualizowana przez `LegalReferenceService` |
| `DetectedAmendmentTargets` | Słownik: `Guid` → `StructuralAmendmentReference` — cele nowelizacji detektowane przez `LegalReferenceService` |

---

## 6. Role poszczególnych klas

### 6.1 Warstwa parsowania (`Services/Parsing/`)

| Klasa | Rola |
|---|---|
| **`ParserOrchestrator`** | Orkiestrator — jednoprzebiegowy pipeline sterujący całym procesem parsowania. Konstruktor: `(IParagraphClassifier? classifier = null)` z DI. Koordynuje klasyfikację, zarządzanie nowelizacjami i budowanie struktury. |
| **`StructureProcessor`** | Przetwarzanie struktury — buduje encje na podstawie `ClassificationResult`. Obsługuje mapowanie `Kind` na typ buildera, tworzenie implicit encji, obsługę `WrapUp`. |
| **`AmendmentStateManager`** | Zarządzanie cyklem życia nowelizacji — `UpdateState()`, `Collect()`, `Flush()`, `DetectTrigger()`. Wyodrębniony z orkiestratora dla lepszej separacji concerns. |
| **`ParsingContext`** | Stan parsowania — przechowuje bieżącą pozycję w drzewie i metadane nowelizacji. Mutowany przez orkiestrator. |
| **`ParsingFactories`** | Fabryki statyczne — tworzenie encji, parsowanie numerów, usuwanie prefiksów numeracyjnych, podział tekstu na zdania (`SplitIntoSentences`). |
| **`AmendmentCollector`** | Bufor nowelizacji — zbiera akapity treści nowelizacji od momentu wejścia (trigger) do momentu powrotu do stylu ustawy matki. |
| **`AmendmentFinalizer`** | Finalizator nowelizacji — wykrywa typ operacji (Modification/Insertion/Repeal), tworzy obiekt `Amendment`, łączy z JournalInfo, przypisuje do encji-właściciela. |
| **`ValidationReporter`** | Reporter diagnostyczny — statyczne metody do rejestrowania ostrzeżeń o konfliktach styl/treść i brakujących stylach na encjach DTO. |

### 6.1b Warstwa klasyfikacji (`Services/Classify/`)

Wyodrębniona warstwa zajmująca się klasyfikacją akapitów i oceną pewności klasyfikacji.

| Klasa | Rola |
|---|---|
| **`IParagraphClassifier`** | Interfejs klasyfikatora — `Classify(ClassificationInput) → ClassificationResult`. Umożliwia DI i testowanie. |
| **`ParagraphClassifier`** | Główna implementacja klasyfikatora — łączy sygnały: styl OpenXml + regex + NumberingHint, obsługuje rozwiązywanie konfliktów. |
| **`ClassificationInput`** | Record wejściowy klasyfikatora: `(Text: string, StyleId: string?, NumberingHint: NumberingHint?)`. |
| **`ClassificationResult`** | Record wyniku klasyfikacji: `Kind` (enum), `Confidence` (1–100), `Penalties: IReadOnlyList<ClassificationPenalty>`, `StyleType: string?`, `IsAmendmentContent: bool`. |
| **`ParagraphKind`** | Enum typów akapitów: `Article`, `Paragraph`, `Point`, `Letter`, `Tiret`, `WrapUp` (nowe — dla `CZ_WSP_*`), `Unknown`. |
| **`ClassificationPenalty`** | Model kary: `(Reason: string, Value: int)` — obniża Confidence. |
| **`ConfidencePenaltyConfig`** | Statyczne konfiguracje kar: `StyleAbsentPenalty`, `SyntaxAbsentPenalty`, `StyleTextConflictPenalty`, `NumberingBreakPenalty`. |
| **`NumberingHint`** | Model wskazówki numeracji — `(ExpectedKind, ExpectedNumber?, IsContinuous(), GetNextLetterValue())`. Przeniesiony z `NumberingContinuityValidator` — logika ciągłości numeracji. |
| **`IConflictResolver`** | Interfejs rozwiązywania konfliktów styl↔treść. |
| **`DefaultConflictResolver`** | Domyślna implementacja: syntaktyka (regex) wygrywa nad stylem. |

---

### 6.2 Buildery encji (`Services/Parsing/Builders/`)

Pattern: `IEntityBuilder<TInput, TResult>` — wspólny kontrakt.

| Builder | Input | Output | Opis |
|---|---|---|---|
| `ArticleBuilder` | `ArticleBuildInput(Subchapter, text)` | `ArticleBuildResult(Article, Paragraph)` | Tworzy artykuł i pierwszy ustęp z ogona tekstu "Art." |
| `ParagraphBuilder` | `ParagraphBuildInput(...)` | `Paragraph` | Tworzy ustęp; metoda `EnsureForPoint()` tworzy implicit ustęp jeśli brak |
| `PointBuilder` | `PointBuildInput(...)` | `Point` | Tworzy punkt; `EnsureForLetter()` tworzy implicit punkt jeśli brak |
| `LetterBuilder` | `LetterBuildInput(...)` | `Letter` | Tworzy literę; `EnsureForTiret()` tworzy implicit literę jeśli brak |
| `TiretBuilder` | `TiretBuildInput(...)` | `Tiret` | Tworzy tiret z indeksem sekwencyjnym |
| `AmendmentBuilder` | `AmendmentCollector` | `AmendmentContent` | Buduje hierarchiczną treść nowelizacji z zebranych akapitów |

Kaskadowe tworzenie encji implicit:
- Punkt wymaga ustępu → `ParagraphBuilder.EnsureForPoint()`
- Litera wymaga punktu → `PointBuilder.EnsureForLetter()`
- Tiret wymaga litery i punktu → kaskadowe `Ensure*()`

### 6.3 Serwisy domenowe (`Services/`)

| Serwis | Rola |
|---|---|
| **`EntityNumberService`** | Parsowanie numerów encji z tekstu na `EntityNumber` (rozbicie na `NumericPart` / `LexicalPart` / `Superscript`) i formatowanie zwrotne. |
| **`LegalReferenceService`** | Parsowanie odwołań strukturalnych z tekstu ("art. 5", "ust. 2", "pkt 3a", "lit. b") do `StructuralReference`. |
| **`JournalReferenceService`** | Parsowanie publikatorów (Dz. U.) z treści artykułów nowelizujących i uzupełnianie listy `JournalInfo`. |

### 6.4 Helpery (`Helpers/`)

| Klasa | Rola |
|---|---|
| **`ParagraphExtensions`** | Metody rozszerzające `Paragraph` (OpenXml) — bezpieczne pobieranie `StyleId` z null-checkiem na `ParagraphProperties`. |
| **`StringExtensions`** | Metody rozszerzające `string` — `Sanitize()` (kolapsowanie białych znaków, zamiana en-dash), `ExtractOrdinal()`, `ExtractDate()`. |
| **`StyleLibraryMapper`** | Statyczna mapa styli dokumentów prawnych — mapuje StyleId na czytelne nazwy. Zawiera `AmendmentStyleInfoMap` z metadanymi styli nowelizacyjnych. |
| **`AmendmentStyleDecoder`** | Dekoder styli nowelizacyjnych — rozpoznaje instrument zmiany (`AmendmentInstrument`), typ celu (`AmendmentTargetKind`), kontekst nadrzędny. |
| **`EnumExtensions`** | Metoda `ToDescription()` na enumach — odczyt atrybutu `[EnumDescription]`. |
| **`SpreadsheetHelper`** | Helper do tworzenia komórek w arkuszu XLSX (OpenXml Spreadsheet). |

### 6.5 Konwertery XML (`Services/Converters/`)

| Konwerter | Status |
|---|---|
| `ArticleXmlConverter` | W trakcie implementacji (kod zakomentowany) |
| `ParagraphXmlConverter` | W trakcie implementacji |
| `PointXmlConverter` | W trakcie implementacji |
| `LetterXmlConverter` | W trakcie implementacji |
| `TiretXmlConverter` | W trakcie implementacji |

Docelowo transformują drzewo DTO do formatu XML (AKN/ELI).

---

## 7. Klasyfikacja warstwowa akapitów — system pewności i kar

Parser stosuje wielowarstwowe rozpoznawanie typu akapitu z oceną pewności (`Confidence` 1–100) i systemem kar (`Penalties`).

### Warstwy klasyfikacji

#### Warstwa 1 — Treść tekstowa (regex)

Skompilowane wzorce regex w `ParagraphClassifier`:

| Wzorzec | Typ | Przykład dopasowania |
|---|---|---|
| `ArticlePattern` | Artykuł | `Art. 5`, `Art.10a` |
| `ParagraphPattern` | Ustęp | `1. Tekst`, `2a. Tekst` |
| `PointPattern` | Punkt | `1) tekst`, `3a) tekst` |
| `LetterPattern` | Litera | `a) tekst`, `ab) tekst` |
| `TiretPattern` | Tiret | `– tekst` (en-dash + spacja) |
| `WrapUpPattern` | WrapUp | Akapity ze stylem `CZ_WSP_*` (parte wspólna po wyliczeniu) |

Wzorce obsługują opcjonalny prefiks cytatu (`„`, `"`, `"`) dla treści nowelizacji.

#### Warstwa 2 — Style OpenXml

Mapowanie prefiksów StyleId:

| Prefiks | Typ |
|---|---|
| `ART*` | Artykuł |
| `UST*` | Ustęp |
| `PKT*` | Punkt |
| `LIT*` | Litera |
| `TIR*` | Tiret |
| `CZ_WSP_*` | WrapUp (część wspólna) |
| `Z/*`, `ZZ*`, `Z_*` | Treść nowelizacji |

#### Warstwa 3 — Ciągłość numeracji

`NumberingHint` weryfikuje spójność numeracji (przeprowadzane jako kara):
- Oczekiwany typ (`ExpectedKind`): jeśli rozpoznany `Kind != ExpectedKind`, kara `NumberingBreakPenalty`
- Oczekiwany numer (`ExpectedNumber`): jeśli numer nie jest kolejny, kara `NumberingBreakPenalty`
- Metody: `IsContinuous(actual)`, `GetNextLetterValue()` (inkrementacja liter w stylu arkusza)

#### Warstwa 4 — Rozstrzyganie konfliktów

Gdy styl i treść się nie zgadzają:
- Używa `IConflictResolver` (domyślnie `DefaultConflictResolver`)
- **Reguła**: syntaktyka (regex) wygrywa nad stylem
- Konflikt generuje `ClassificationPenalty` z `StyleTextConflictPenalty` i `ValidationMessage` na encji

### System kar (Confidence Penalties)

Każda kara obniża `Confidence`:

| Kara | Wartość | Warunki |
|---|---|---|
| `StyleAbsentPenalty` | konfigurowana | Brak StyleId — użyto fallback regex |
| `SyntaxAbsentPenalty` | konfigurowana | Brak dopasowania regex |
| `StyleTextConflictPenalty` | konfigurowana | Konflikt: styl mówi X, treść mówi Y |
| `NumberingBreakPenalty` | konfigurowana | Numeracja nieciągła (zła sekwencja) |

**Wynik**: `Confidence = 100 - sum(penalties)`. Jeśli `Confidence <= 0` i brak żadnego sygnału, `Kind = Unknown`.

### Diagnozy klasyfikacji

- `ValidationReporter.AddClassificationWarning()` — rejestruje ostrzeżenia o konfliktach na encji DTO
- `ValidationMessage.Level = Warning` — dla niskich Confidence lub konfliktów styl/treść

---

## 8. System nowelizacji (amendments)

### 8.1 Cykl życia nowelizacji

```
1. Orkiestrator przetwarza akapit ustawy matki (np. ustęp)
2. Detekcja triggera: treść zawiera "otrzymuje brzmienie:" / "dodaje się"
   → AmendmentTriggerDetected = true, AmendmentOwner = bieżąca encja
3. Następne akapity ze stylem Z/* lub bezstylowe
   → InsideAmendment = true
   → AmendmentCollector.Begin(owner, target)
   → AmendmentCollector.AddParagraph(text, styleId)
4. Powrót do stylu ustawy matki (ART/UST/PKT/LIT/TIR)
   → InsideAmendment = false
   → FlushAmendmentCollector():
       a) AmendmentBuilder buduje AmendmentContent (hierarchiczna treść)
       b) AmendmentFinalizer tworzy Amendment, wykrywa typ operacji,
          łączy z JournalInfo, przypisuje do właściciela (IHasAmendments)
       c) AmendmentCollector.Reset()
```

### 8.2 Typy operacji nowelizacyjnych

| `AmendmentOperationType` | Trigger w tekście |
|---|---|
| `Modification` | "otrzymuje brzmienie", "zastępuje się" |
| `Insertion` | "dodaje się" |
| `Repeal` | "uchyla się" |

### 8.3 Dekodowanie styli nowelizacyjnych

`AmendmentStyleDecoder` + `StyleLibraryMapper.AmendmentStyleInfoMap` rozpoznaje:

- **Instrument** (`AmendmentInstrument`): czym jest dokonywana zmiana
  - `Z/` → artykułem/punktem
  - `Z_LIT/` → literą
  - `Z_TIR/` → tiretem
  - `Z_2TIR/` → podwójnym tiretem
  - `ZZ/` → zmiana zmiany (zagnieżdżona)
- **Cel** (`AmendmentTargetKind`): co jest zmieniane (ART, UST, PKT, LIT, TIR, CommonPart, Fragment...)
- **Kontekst** (`ParentContext`): np. `TIR_w_LIT` → tiret wewnątrz litery

---

## 9. Walidacja i diagnostyka

### 9.1 `ValidationMessage`

Każda encja (`BaseEntity`) przechowuje listę komunikatów diagnostycznych:

| Poziom | Znaczenie |
|---|---|
| `Info` | Informacja diagnostyczna |
| `Warning` | Potencjalny problem, struktura zrozumiała |
| `Error` | Problem z parsowaniem, jednostka częściowo użyteczna |
| `Critical` | Struktura zniekształcona, jednostka nieprzydatna |

### 9.2 `NumberingHint` — logika ciągłości numeracji

Walidacja numeracji odbywa się poprzez `NumberingHint` w warstwie klasyfikacji (z karą `NumberingBreakPenalty`):

- Oczekiwany typ (`ExpectedKind`): klasyfikator porównuje rozpoznany `Kind` z oczekiwanym
- Oczekiwany numer (`ExpectedNumber`): sprawdzany przez `IsContinuous(actual)`, sprawdza:
  - Ten sam `NumericPart` dozwolony (warianty: 2 → 2a)
  - Kolejny `NumericPart` (prev + 1) dozwolony
  - Litery: ciągłość `LexicalPart` (a → b → c), inkrementacja przez `GetNextLetterValue()`
- Reset hierarchiczny: nowy artykuł → zerowanie oczekiwanego numeru dla podrzędnych poziomów

### 9.3 `ValidationReporter`

Rejestruje ostrzeżenia klasyfikacyjne:
- Brak stylu — użyto reguły tekstowej
- Konflikt styl↔treść — użyto treści

---

## 10. Konwencje projektowe

### 10.1 Język

- **Kod** (zmienne, metody, klasy): angielski
- **Komentarze, XML-doc, UI, diagnostyka**: polski
- **Commity**: angielski (format: `feat: ...`, `fix: ...`, `refactor: ...`)

### 10.2 Encje implicit

Encja implicit (niejawna) to jednostka, która fizycznie nie występuje w tekście, ale jest wymagana przez hierarchię:
- Artykuł zawsze ma ≥1 ustęp → jedyny ustęp jest `IsImplicit = true`
- Litera wymaga punktu → punkt może być `IsImplicit = true`
- Tiret wymaga litery → litera może być `IsImplicit = true`

Encje implicit:
- Są **pomijane** w generowaniu eId
- Są **pomijane** w walidacji numeracji
- Są **pomijane** w warstwie prezentacji

### 10.3 Części wspólne (CommonPart)

`CommonPart` to wirtualna jednostka redakcyjna reprezentująca tekst przed (`Intro`) lub po (`WrapUp`) liście elementów wyliczeniowych:
- Tworzone automatycznie przez `ParsingFactories.AttachIntroCommonPart()` przed dodaniem pierwszego elementu do listy
- Są rodzeństwem elementów wyliczeniowych (nie ich dziećmi)
- Nie pojawiają się jawnie w XML — ich zawartość trafia do `<intro>` / `<wrapUp>`

### 10.4 Null safety w OpenXml

Zawsze sprawdzaj null przed dostępem do styli:
```csharp
paragraph.ParagraphProperties?.ParagraphStyleId?.Val?.ToString()
```
Centralnie obsługiwane przez `ParagraphExtensions.StyleId()`.

### 10.5 Wzorce regex

- Wszystkie wzorce są `RegexOptions.Compiled` i przechowywane jako `static readonly`
- Współdzielone między `ParagraphClassifier` i `ParsingFactories`
- Obsługują opcjonalny prefiks cytatu (`OptionalQuotePrefix`)

### 10.6 Logowanie

- Serilog konfigurowany przez `LoggerConfig.ConfigureLogger()`
- Poziom minimalny: `Warning`
- Wyjście: konsola + plik `logs/log.txt` (rolling daily)
- Kluczowe decyzje parsera (niepewne klasyfikacje, naprawy) logowane przez Serilog

### 10.7 Testy

- Framework: xUnit (`WordParserCore.Tests`)
- Konwencja nazewnictwa plików: `*Tests.cs` (np. `EIdTests.cs`, `ParagraphClassifierTests.cs`)
- Każda zmiana w logice parsowania wymaga aktualizacji lub dodania testów
- Artefakty testowe (pliki DOCX) w `Artifacts/`

### 10.8 Buildery z wzorcem Input/Result

Buildery encji stosują konwencję:
- `*BuildInput` — sealed record z danymi wejściowymi
- `*BuildResult` — sealed class z wynikami (jeśli >1 wartość zwracana)
- Interfejs `IEntityBuilder<TInput, TResult>`

### 10.9 Kopie zapasowe

CLI tworzy kopie z sygnaturą czasową: `nazwa-pliku_YYYYMMDD_HHmmss.ext`

---

## 11. Mapa plików — szybki przegląd

```
ModelDto/
├── BaseEntity.cs                    # Klasa bazowa wszystkich encji
├── LegalDocument.cs                 # Korzeń modelu aktu prawnego
├── EntityNumber.cs                  # Model numeru encji
├── Amendment.cs                     # Model nowelizacji
├── AmendmentContent.cs              # Hierarchiczna treść nowelizacji
├── AmendmentOperationType.cs        # Enum: Modification/Insertion/Repeal
├── AmendmentObjectType.cs           # Enum: Article/Paragraph/Point/...
├── StructuralAmendmentReference.cs  # Cel nowelizacji (ścieżka strukturalna)
├── TextSegment.cs                   # Segment tekstu (zdanie)
├── TextSegmentType.cs               # Enum: Sentence/...
├── ValidationMessage.cs             # Komunikat diagnostyczny
├── JournalInfo.cs                   # Metadane publikatora (Dz.U.)
├── LegalActType.cs                  # Enum: Statute/Regulation/Code
├── CommonPartType.cs                # Enum: Intro/WrapUp
├── IHasAmendments.cs                # Interfejs: encja z nowelizacją
├── IHasCommonParts.cs               # Interfejs: encja z częściami wspólnymi
├── IHasTextSegments.cs              # Interfejs: encja z segmentami tekstu
├── EditorialUnits/
│   ├── Article.cs                   # Artykuł (art.)
│   ├── Paragraph.cs                 # Ustęp (ust.) — może być implicit
│   ├── Point.cs                     # Punkt (pkt)
│   ├── Letter.cs                    # Litera (lit.)
│   ├── Tiret.cs                     # Tiret (tir.) — wspiera zagnieżdżanie
│   └── CommonPart.cs                # Część wspólna (intro/wrapUp)
└── SystematizingUnits/
    ├── ISystematizingUnit.cs        # Interfejs jednostek organizacyjnych
    ├── Part.cs                      # Część
    ├── Book.cs                      # Księga
    ├── Title.cs                     # Tytuł
    ├── Division.cs                  # Dział
    ├── Chapter.cs                   # Rozdział
    └── Subchapter.cs                # Oddział (kontener artykułów)

WordParserCore/
├── LegalDocumentParser.cs           # PUNKT WEJŚCIA — Parse()
├── LoggerConfig.cs                  # Konfiguracja Serilog
├── Exceptions/
│   └── ParsingException.cs          # Wyjątek bazowy parsowania
├── Helpers/
│   ├── ParagraphExtensions.cs       # Bezpieczne StyleId() z null-check; GetFullText() z obsługą superscriptu
│   ├── StringExtensions.cs          # Sanitize(), ExtractDate(), ExtractOrdinal()
│   ├── StyleLibraryMapper.cs        # Mapa styli dokumentów prawnych
│   ├── AmendmentStyleDecoder.cs     # Dekoder styli nowelizacyjnych
│   ├── EnumExtensions.cs            # ToDescription() dla enumów
│   └── SpreadsheetHelper.cs         # Helper do tworzenia komórek XLSX
└── Services/
    ├── EntityNumberService.cs       # Parsowanie/formatowanie numerów encji
    ├── LegalReferenceService.cs     # Parsowanie odwołań strukturalnych
    ├── JournalReferenceService.cs   # Parsowanie publikatorów (Dz.U.)
    ├── Classify/                    # WARSTWA KLASYFIKACJI
    │   ├── IParagraphClassifier.cs  # Interfejs klasyfikatora
    │   ├── ParagraphClassifier.cs   # Główna implementacja klasyfikatora
    │   ├── ClassificationInput.cs   # Record wejściowy klasyfikatora
    │   ├── ClassificationResult.cs  # Record wyniku klasyfikacji
    │   ├── ParagraphKind.cs         # Enum: Article/Paragraph/Point/Letter/Tiret/WrapUp/Unknown
    │   ├── ClassificationPenalty.cs # Model kary
    │   ├── ConfidencePenaltyConfig.cs  # Konfiguracja kar: Style/Syntax/Conflict/NumberingBreak
    │   ├── NumberingHint.cs         # Logika ciągłości numeracji (przeniesiona z NumberingContinuityValidator)
    │   ├── IConflictResolver.cs     # Interfejs rozwiązywania konfliktów
    │   └── DefaultConflictResolver.cs  # Domyślna implementacja: syntaktyka wygrywa
    ├── Parsing/
    │   ├── ParserOrchestrator.cs    # Orkiestrator parsowania (pipeline) z DI konstruktorem
    │   ├── StructureProcessor.cs    # Przetwarzanie struktury — budowanie encji z ClassificationResult
    │   ├── AmendmentStateManager.cs # Zarządzanie cyklem życia nowelizacji (UpdateState, Collect, Flush, DetectTrigger)
    │   ├── ParsingContext.cs        # Stan parsowania (mutowalny)
    │   ├── ParsingFactories.cs      # Fabryki: numery, prefiksy, zdania
    │   ├── AmendmentCollector.cs    # Bufor akapitów nowelizacji
    │   ├── AmendmentFinalizer.cs    # Finalizator: typ operacji, Assignment
    │   ├── ValidationReporter.cs    # Reporter diagnostyczny (statyczne metody)
    │   └── Builders/
    │       ├── IEntityBuilder.cs    # Kontrakt buildera
    │       ├── ArticleBuilder.cs    # Builder artykułu + pierwszy ustęp
    │       ├── ParagraphBuilder.cs  # Builder ustępu + EnsureForPoint()
    │       ├── PointBuilder.cs      # Builder punktu + EnsureForLetter()
    │       ├── LetterBuilder.cs     # Builder litery + EnsureForTiret()
    │       ├── TiretBuilder.cs      # Builder tiretu (z obsługą ParentTiret dla zagnieżdżenia)
    │       └── AmendmentBuilder.cs  # Builder treści nowelizacji
    └── Converters/
        └── [nieaktywne — kod zakomentowany] Konwertery do XML/XLSX (przyszłościowo)
```

---

## 12. Podsumowanie przepływu danych

```
  DOCX (plik Word)
       │
       ▼
  OpenXml SDK (DocumentFormat.OpenXml)
       │
       ▼
  LegalDocumentParser.Parse()
       │
       ├── Iteracja po akapitach (Word.Paragraph)
       │     │
       │     ▼
       │   ParserOrchestrator.ProcessParagraph(paragraph, context)
       │     ├── StringExtensions.Sanitize() — normalizacja tekstu
       │     ├── ParagraphExtensions.StyleId() — bezpieczne pobranie StyleId
       │     ├── BuildNumberingHint() — oczekiwana numeracja na bieżącym poziomie
       │     ├── ParagraphClassifier.Classify() → ClassificationResult
       │     │   ├── Warstwa 1: regex matching (syntaktyka)
       │     │   ├── Warstwa 2: StyleId mapping (style OpenXml)
       │     │   ├── Warstwa 3: NumberingHint validation (ciągłość numeracji)
       │     │   ├── Warstwa 4: IConflictResolver (rozwiązanie konfliktów)
       │     │   └── Rezultat: Kind, Confidence, Penalties[], StyleType
       │     │
       │     ├── AmendmentStateManager.UpdateState() — zarządzanie nowelizacją
       │     │   ├── DetectTrigger() — "otrzymuje brzmienie:", "dodaje się", etc.
       │     │   ├── Collect() — zbieranie treści nowelizacji
       │     │   └── Flush() — finalizacja nowelizacji
       │     │
       │     ├── StructureProcessor.Process() — budowanie struktury
       │     │   ├── *Builder.Build() → encja DTO (Article/Paragraph/Point/Letter/Tiret/WrapUp)
       │     │   ├── Kaskadowe tworzenie implicit encji
       │     │   └── Obsługa CommonPart (intro/wrapUp)
       │     │
       │     ├── ValidationReporter.AddClassificationWarning() — diagnostyka
       │     ├── LegalReferenceService.UpdateLegalReference() — pozycja strukturalna
       │     └── JournalReferenceService.ParseJournalReferences() — publikatory (tylko Article)
       │
       ├── ParserOrchestrator.Finalize() — flush ostatniej bufora nowelizacji
       │
       ▼
  LegalDocument (model DTO)
       │
       ├── Articles[] → Paragraphs[] → Points[] → Letters[] → Tirets[]
       ├── CommonParts[] (intro/wrapUp)
       ├── ValidationMessages (diagnostyka klasyfikacji)
       ├── Journals (publikatory z Dz.U.)
       └── Amendments (nowelizacje z operacjami)
       │
       ▼
  [Przyszłościowo] Converters → XML / XLSX
```
