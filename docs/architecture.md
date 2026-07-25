# Architektura projektu WordParser

> Dokument opisuje domenę projektową, moduły, role klas, przepływy danych, zależności między warstwami oraz konwencje stosowane w projekcie WordParser.

> Aktualny na dzień: 2026-07-25

---

## 1. Przegląd domeny

WordParser to toolkit .NET 10 służący do **parsowania polskich aktów prawnych** z dokumentów Word/DOCX, PDF (z warstwą tekstową) oraz TXT do modelu obiektowego (DTO), a następnie do formatów wyjściowych (XML/XLSX). Wejście jest uniwersalne: format źródłowy jest wykrywany sygnaturowo, a dokument jest klasyfikowany wg rodzaju aktu (ZTP) przed (opcjonalnym) zbudowaniem modelu. Domena obejmuje:

- **Hierarchię jednostek redakcyjnych**: `Article → Paragraph → Point → Letter → Tiret → Tiret (zagnieżdżony)` — „podwójny tiret" (2TIR/3TIR) to nie osobna klasa, tylko zagnieżdżona lista `Tiret.Tirets`
- **Hierarchię jednostek systematyzujących**: `Part → Book → Title → Division → Chapter → Subchapter`
- **Klasyfikację rodzaju dokumentu**: rozpoznanie, czy wejście jest aktem prawnym i jakiego rodzaju (ustawa, rozporządzenie, obwieszczenie TJ, uchwała, zarządzenie, akt prawa miejscowego…)
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
| `LegalDocument.cs` | Korzeń modelu — wrapper całego aktu prawnego z metadanymi, hierarchią i `Classification` (`DocumentClassificationResult?`, ustawiane po budowie modelu) |
| `EntityNumber.cs` | Model numeru encji z rozbiciem na: `NumericPart`, `LexicalPart`, `Superscript` |
| `Amendment.cs` | Model nowelizacji (typ operacji, treść, cel, data wejścia w życie) |
| `AmendmentContent.cs` | Treść nowelizacji — hierarchiczny fragment aktu (artykuły, ustępy, punkty...) |
| `StructuralAmendmentReference.cs` | Cel nowelizacji — ścieżka strukturalna do zmienianej jednostki |
| `TextSegment.cs` / `TextSegmentType.cs` | Segmenty tekstu (zdania) wewnątrz jednostek redakcyjnych |
| `ValidationMessage.cs` | Komunikaty diagnostyczne (Info/Warning/Error/Critical) |
| `JournalInfo.cs` | Metadane publikatora (Dz. U. — rok, pozycje) |
| `CommonPartType.cs` | Enum: `Intro` / `WrapUp` |
| `LegalActType.cs` | Enum rodzaju aktu: `Statute`, `Bill`, `Regulation`, `Code`, `Ordinance`, `RegulatoryImpactAssessment`, `AmendingStatute`, `Announcement`, `Resolution`, `ExecutiveOrder`, `LocalLegalAct` |
| `DocumentClassificationResult.cs` | Wynik klasyfikacji dokumentu (rodzaj aktu, pewność, sygnały, uzasadnienie) |
| `DocumentSignal.cs` / `DocumentSignalKind.cs` | Pojedynczy dowód klasyfikacji dokumentu (rodzaj sygnału, wynik, dopasowany fragment) |
| `PublisherType.cs` | Enum typu publikatora (Dz. U. / Dz. Urz. Woj. itp.) |

**Interfejsy kontraktowe:**

| Interfejs | Opis |
|---|---|
| `IHasAmendments` | Encja może posiadać nowelizację (`Amendment? Amendment`) |
| `IHasCommonParts` | Encja może posiadać części wspólne (`List<CommonPart>`) |
| `IHasTextSegments` | Encja może być dzielona na segmenty tekstu (`List<TextSegment>`) |
| `ISystematizingUnit` | Kontrakt jednostek systematyzujących (`Heading`, `IsImplicit`) |

### 2.2 `WordParserCore` — Warstwa logiki parsowania

Silnik parsujący dokumenty DOCX/PDF (z warstwą tekstową)/TXT. Zależy od `ModelDto`,
`DocumentFormat.OpenXml` (DOCX), `PdfPig` (PDF) i `Serilog`.

| Ścieżka | Rola |
|---|---|
| `LegalDocumentParser.cs` | **Punkt wejścia** — statyczna metoda `Parse(Stream, fileNameHint?, ParseOptions?)` / `Parse(filePath, ParseOptions?)` → `ParseResult`. `Parse(WordprocessingDocument)` jest `[Obsolete]` (pomija klasyfikację dokumentu i kopertę `ParseResult`) |
| `ParseResult.cs` | Koperta wyniku: `Classification`, `Document` (nullable), `SourceFormat`, `BlockCount` |
| `ParseOptions.cs` | Opcje: `Policy` (`ParsePolicy`), `ForcedFormat` (`SourceFormat?`) |
| `LoggerConfig.cs` | Konfiguracja Serilog (konsola + plik `logs/log.txt`) |
| `Exceptions/` | Wyjątki parsowania (`ParsingException`, `UnsupportedDocumentFormatException`) |
| `Helpers/` | Metody rozszerzające, dekodery styli, helpery |
| `Ingest/` | **Warstwa odczytu formatu** — detekcja formatu (`SourceFormatDetector`), adaptery DOCX/PDF/TXT do wspólnej reprezentacji pośredniej (`DocumentBlock`) |
| `Services/Classify/` | **Warstwa klasyfikacji akapitów** — klasyfikator (`ParagraphClassifier`, jednoprzebiegowy), wzorce regex, system kar, rozwiązywanie konfliktów |
| `Services/Classify/Document/` | **Warstwa klasyfikacji dokumentu** — `DocumentClassifier` rozpoznaje rodzaj aktu (ZTP) na podstawie bloków, przed budową modelu |
| `Services/` | Serwisy domenowe (numeracja, referencje, publikatory) |
| `Services/Parsing/` | **Pipeline parsowania** — orkiestrator, buildery, przetwarzanie struktury, zarządzanie nowelizacjami |
| `Services/Converters/` | Konwertery do XML (w trakcie implementacji, nieaktywne) |

### 2.3 `WordParser` — Narzędzie konsolowe (CLI)

Interfejs wiersza poleceń korzystający z uniwersalnego `LegalDocumentParser.Parse`. Domyślnie czyta
plik **read-only**, bez kopii zapasowej. Kopia zapasowa z sygnaturą czasową jest tworzona wyłącznie
w trybie wstecznej kompatybilności `--docx` (legacy), który parsuje zawsze (`ParsePolicy.AlwaysParse`)
i wymusza format DOCX.

### 2.4 `WordParserWeb` — Aplikacja webowa (aktywna)

ASP.NET 10 — renderowanie HTML dokumentów sparsowanych przez `WordParserCore`. Korzysta z uniwersalnego `LegalDocumentParser.Parse(Stream, ...)` (raport klasyfikacji dokumentu + tryb wymuszonego parsowania).

### 2.5 `WordParserApi` — Web API (wstrzymany)

ASP.NET Core Web API z Swagger/OpenAPI. Projekt wstrzymany — zalecane użycie CLI/`WordParserWeb`.

### 2.6 `WordParserCore.Tests` — Testy jednostkowe

Testy xUnit pokrywające kluczowe scenariusze parsowania.

---

## 3. Diagram zależności między projektami

```
WordParser (CLI)           ──►  WordParserCore  ──────►  ModelDto
WordParserWeb (Web)        ──►  WordParserCore  ──────►  ModelDto
WordParserCore.Tests       ──►  WordParserCore  ──────►  ModelDto
WordParserApi (wstrzymany) ──►  WordParserCore  ──────►  ModelDto
```

Zależności zewnętrzne:
- `DocumentFormat.OpenXml` — parsowanie dokumentów DOCX (tylko w `WordParserCore`)
- `PdfPig` — odczyt warstwy tekstowej PDF (tylko w `WordParserCore`; UWAGA: paczka NuGet
  „UglyToad.PdfPig" to obcy fork — używana jest `PdfPig`)
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

### 5.1 Punkt wejścia (uniwersalne wejście — DOCX/PDF/TXT)

```
LegalDocumentParser.Parse(Stream, fileNameHint?, ParseOptions?)  →  ParseResult
    │
    ├── Strumień nieseekowalny → buforowany w pamięci (MemoryStream), read-only
    ├── Detekcja formatu:
    │     ├── options.ForcedFormat, jeśli podany i != Unknown
    │     └── w przeciwnym razie SourceFormatDetector.Detect(stream, fileNameHint) — sniffing sygnatur
    │         (ZIP/OOXML → Docx; nagłówek „%PDF-" → Pdf; heurystyka BOM/NUL → PlainText;
    │          rozszerzenie pliku rozstrzyga wyłącznie przypadki niekonkluzywne)
    ├── DocumentBlockReaderFactory.Create(format) → IDocumentBlockReader
    │     (DocxBlockReader / PdfBlockReader / PlainTextBlockReader)
    ├── reader.ReadBlocks(stream) → IReadOnlyList<DocumentBlock>
    │     (format-agnostyczna reprezentacja pośrednia — patrz 5.1b)
    └── Parse(blocks, options, format) — rdzeń:
          ├── DocumentClassifier.Classify(blocks) → DocumentClassificationResult
          │     (rodzaj aktu wg ZTP, IsLegalAct, pewność, sygnały — patrz 6.1c)
          ├── Decyzja o budowie modelu wg options.Policy (ParsePolicy):
          │     ├── AlwaysParse        → zawsze buduj
          │     ├── ClassifyOnly       → nigdy nie buduj (Document = null)
          │     └── ParseWhenLegalAct  → buduj tylko gdy classification.IsLegalAct (domyślna)
          ├── jeśli budowa → ParseBlocks(blocks) (rdzeń niezależny od formatu, patrz 5.2)
          │     i przypisanie document.Classification = classification
          └── zwraca ParseResult { Classification, Document, SourceFormat, BlockCount }

LegalDocumentParser.Parse(filePath, ParseOptions?)
    → otwiera plik read-only (File.OpenRead) i deleguje do Parse(Stream, ...)

[Obsolete] LegalDocumentParser.Parse(WordprocessingDocument)
    → ścieżka wsteczna: czyta bloki przez DocxBlockReader i zwraca LegalDocument
      BEZPOŚREDNIO, z pominięciem klasyfikacji dokumentu i koperty ParseResult
```

### 5.1b Warstwa odczytu formatu (`Ingest/`)

Każdy adapter formatu (`IDocumentBlockReader`) zamienia dokument źródłowy na listę `DocumentBlock` —
format-agnostyczny odpowiednik akapitu Word, wspólny dla DOCX/PDF/TXT:

| Właściwość `DocumentBlock` | Opis |
|---|---|
| `Text` | Tekst bloku (kanał indeksu górnego `[x]`, tabulatory `\t`; **nie** trymowany/sanityzowany) |
| `StyleId` | Identyfikator stylu Word — tylko DOCX; `null` dla PDF/TXT |
| `Layout` | Opcjonalne metadane układu (`BlockLayoutInfo` — wcięcia, pogrubienie, kursywa) — z DOCX/PDF |
| `Source` | Położenie w źródle (`BlockSourceLocation`) — diagnostyka |
| `Role` | `BlockRole` (`Body`, `FootnoteText`, …) — bloki przypisów są filtrowane przez orkiestrator |
| `IsEmpty` | `true` gdy `Text` jest pusty/białe znaki |

`PdfBlockReader` (PdfPig) składa bloki z geometrii linii tekstu (`PdfLineExtractor`, `BlockAssembler`,
`PageArtifactFilter`) — bez stylów Word, więc klasyfikacja akapitu opiera się tam głównie na
warstwie syntaktycznej (regex) i układzie (wcięcia).

### 5.2 Rdzeń budowy modelu (`LegalDocumentParser.ParseBlocks`, `internal`)

Niezależny od formatu źródłowego — działa wyłącznie na `IReadOnlyList<DocumentBlock>`:

```
ParseBlocks(blocks)
    │
    ├── Tworzy LegalDocument z domyślną hierarchią systematyzującą
    ├── Tworzy ParsingContext (stan parsowania)
    ├── Tworzy ParserOrchestrator() — domyślny ParagraphClassifier
    ├── Iteruje po blocks (pomijając elementy null)
    │   └── ParserOrchestrator.ProcessBlock(block, context)
    └── ParserOrchestrator.Finalize(context)
          ├── flush bufora nowelizacji, jeśli dokument kończy się w jej trakcie
          └── context.Metadata.ApplyTo(document) — zapis Title/ActDate ze strefy tytułowej
```

`ParserOrchestrator.ProcessParagraph(Paragraph, context)` pozostaje jako cienki adapter
OpenXml → `DocumentBlock` (wsteczna kompatybilność testów i dotychczasowych wywołujących);
deleguje do `ProcessBlock`.

### 5.3 Orkiestrator — `ParserOrchestrator.ProcessBlock`

Jednoprzebiegowy pipeline ze stanem. Dla każdego bloku:

```
1. Jeśli block.IsEmpty → pomiń
2. Jeśli block.Role == FootnoteText → pomiń (przypisy nie są treścią jednostek redakcyjnych;
   klasyfikator dokumentu czyta je osobno z pełnej listy bloków)
3. Sanityzacja tekstu (block.Text.Sanitize().Trim()) i odczyt StyleId (block.StyleId)
4. Budowanie NumberingHint (oczekiwana numeracja na bieżącym poziomie)
5. Klasyfikacja (ParagraphClassifier.Classify) → ClassificationResult (Kind, Confidence,
   IsAmendmentContent, StyleType, Penalties)
6. HandleAmendmentFlow (metoda prywatna orkiestratora):
   ├── AmendmentStateManager.UpdateState — aktualizuje InsideAmendment/trigger
   ├── jeśli właśnie wyszliśmy z nowelizacji → Flush()
   ├── ShouldExitForNewParentLawTrigger — wykrywa nowy trigger ustawy matki w trakcie
   │   zbierania (z uwzględnieniem QuoteBalanceTracker dla nowelizacji bez stylów)
   └── jeśli IsAmendmentContent lub InsideAmendment → Collect() i STOP (blok skonsumowany)
7. W przeciwnym razie: StructureProcessor.Process(context, classification, text, styleId, layout)
   ├── jednostki systematyzacyjne (Part/Book/Title/Division/Chapter/Subchapter) → SystematizingUnitBuilder
   ├── Article  → ArticleBuilder.Build()
   ├── Unknown  → wnioskowanie z tekstu / zbieranie metadanych strefy tytułowej / diagnostyka
   ├── Paragraph → ParagraphBuilder.Build()
   ├── Point    → PointBuilder.Build()
   ├── Letter   → LetterBuilder.Build()
   ├── Tiret    → TiretBuilder.Build() (głębokość ze stylu 2TIR/3TIR lub z wcięcia layoutu)
   └── WrapUp   → AttachWrapUpCommonPart (dla CZ_WSP_*)
8. Jeśli StructureProcessor zwrócił true → AmendmentStateManager.DetectTrigger (szuka
   "otrzymuje brzmienie:", "dodaje się", "uchyla się")
```

Aktualizacja referencji strukturalnej (`LegalReferenceService`), wykrywanie celów nowelizacji
i parsowanie publikatorów (`JournalReferenceService`, tylko dla artykułów) odbywają się
wewnątrz `StructureProcessor.Process`, nie jako osobne kroki orkiestratora (patrz 6.1).

### 5.4 Kontekst parsowania (`ParsingContext`)

Stan mutowalny przechowujący bieżącą pozycję w drzewie:

| Pole | Opis |
|---|---|
| `Document` | Aktualny `LegalDocument` (korzeń) |
| `Subchapter` | Bieżący oddział (kontener artykułów) |
| `CurrentPart/CurrentBook/CurrentTitle/CurrentDivision/CurrentChapter` | Bieżąca ścieżka jednostek systematyzujących, aktualizowana przez `SystematizingUnitBuilder` |
| `PendingHeadingUnit` | Jednostka systematyzacyjna oczekująca na tytuł (drugi wiersz wzorca dwuwierszowego, § 60 ZTP) |
| `CurrentArticle` | Ostatnio przetworzony artykuł |
| `CurrentParagraph` | Bieżący ustęp |
| `CurrentPoint` | Bieżący punkt |
| `CurrentLetter` | Bieżąca litera |
| `TiretStack` | Stos tiretów — `List<DtoTiret>` wspierający zagnieżdżone tirety (1TIR/2TIR/3TIR); `CurrentTiret` jako property `TiretStack[^1]` |
| `OpenTiretIndents` | Lista równoległa do `TiretStack` — wcięcia lewe (twips) tiretów otwartych na stosie, zasila wnioskowanie głębokości z układu (§ 58 ZTP) gdy styl 2TIR/3TIR nie rozstrzyga |
| `Metadata` | `DocumentMetadataCollector` — zbiera rodzaj/datę/przedmiot aktu ze strefy tytułowej (§ 16-19 ZTP) |
| `InsideAmendment` | Flaga: czy jesteśmy wewnątrz treści nowelizacji |
| `AmendmentTriggerDetected` | Flaga: czy wykryto zwrot "otrzymuje brzmienie:" |
| `AmendmentCollector` | Bufor akapitów nowelizacji (z wbudowanym `QuoteBalanceTracker` dla nowelizacji bez stylów) |
| `AmendmentOwner` | Encja-właściciel bieżącej nowelizacji |
| `ReferenceService` | Serwis referencji strukturalnych |
| `CurrentStructuralReference` | Bieżąca pozycja (art/ust/pkt/lit/tir) — aktualizowana przez `StructureProcessor` |
| `DetectedAmendmentTargets` | Słownik: `Guid` → `StructuralAmendmentReference` — cele nowelizacji detektowane przez `StructureProcessor` |

---

## 6. Role poszczególnych klas

### 6.1 Warstwa parsowania (`Services/Parsing/`)

| Klasa | Rola |
|---|---|
| **`ParserOrchestrator`** | Orkiestrator — jednoprzebiegowy pipeline sterujący całym procesem parsowania. Konstruktor: `(IParagraphClassifier? classifier = null)` z DI. Metoda `ProcessBlock(DocumentBlock, ParsingContext)` (format-agnostyczna); `ProcessParagraph(Paragraph, context)` to cienki adapter OpenXml → `ProcessBlock`. Koordynuje klasyfikację, zarządzanie nowelizacjami i budowanie struktury. |
| **`StructureProcessor`** (`internal sealed`) | Przetwarzanie struktury — buduje encje na podstawie `ClassificationResult`. Obsługuje mapowanie `Kind` na typ buildera (w tym jednostki systematyzacyjne przez `SystematizingUnitBuilder`), tworzenie implicit encji, obsługę `WrapUp`, wnioskowanie `Unknown` (tekst / metadane strefy tytułowej), aktualizację `CurrentStructuralReference` i wykrywanie celów nowelizacji (`DetectAmendmentTargets`). |
| **`SystematizingUnitBuilder`** (w `Builders/`) | Buduje jednostki systematyzacyjne (Część→Księga→Tytuł→Dział→Rozdział→Oddział, § 60-62 ZTP) i utrzymuje bieżącą ścieżkę w `ParsingContext`. |
| **`DocumentMetadataCollector`** | Zbiera metadane aktu (rodzaj, organ, data, przedmiot) ze strefy tytułowej (§ 16-19, § 102, § 120 ZTP); karmiony akapitami `Unknown` sprzed pierwszego artykułu; `ApplyTo(document)` zapisuje `Title`/`ActDate` przy finalizacji. |
| **`AmendmentStateManager`** | Zarządzanie cyklem życia nowelizacji — `UpdateState()`, `Collect()`, `Flush()`, `DetectTrigger()`. Wyodrębniony z orkiestratora dla lepszej separacji concerns. |
| **`QuoteBalanceTracker`** | Bilans cudzysłowów dla nowelizacji bez stylów `Z/*` (§ 94 ZTP) — wyznacza koniec cytowanej treści przez parzystość cudzysłowów, gdy brak sygnału stylowego. |
| **`AmendmentCommandParser`** | Rozpoznaje rodzaj komendy nowelizacyjnej z treści (`AmendmentCommandKind`: Change/Add/Repeal/ReplaceWords) — źródło niższego priorytetu niż mapa stylów. |
| **`ParsingContext`** | Stan parsowania — przechowuje bieżącą pozycję w drzewie (w tym jednostki systematyzacyjne i metadane aktu). Mutowany przez orkiestrator/`StructureProcessor`. |
| **`ParsingFactories`** | Fabryki statyczne — tworzenie encji, parsowanie numerów, usuwanie prefiksów numeracyjnych, podział tekstu na zdania (`SplitIntoSentences`). |
| **`AmendmentCollector`** | Bufor nowelizacji — zbiera akapity treści nowelizacji od momentu wejścia (trigger) do momentu powrotu do stylu ustawy matki. |
| **`AmendmentFinalizer`** | Finalizator nowelizacji — wykrywa typ operacji (Modification/Insertion/Repeal), tworzy obiekt `Amendment`, łączy z JournalInfo, przypisuje do encji-właściciela. |
| **`ValidationReporter`** | Reporter diagnostyczny — statyczne metody do rejestrowania ostrzeżeń o konfliktach styl/treść i brakujących stylach na encjach DTO. |

### 6.1b Warstwa klasyfikacji akapitów (`Services/Classify/`)

Wyodrębniona warstwa zajmująca się klasyfikacją akapitów i oceną pewności klasyfikacji.
Klasyfikator jest **jednoprzebiegowy** (`ParagraphClassifier`, `sealed class`) — nie ma osobnych klas
warstwowych ani katalogu `Classification/`; "warstwy" (styl/regex/numeracja/konflikt) to etapy
jednej metody `Classify`, nie osobne obiekty.

| Klasa | Rola |
|---|---|
| **`IParagraphClassifier`** | Interfejs klasyfikatora — `Classify(ClassificationInput) → ClassificationResult`. Umożliwia DI i testowanie. |
| **`ParagraphClassifier`** | Jedyna implementacja klasyfikatora — łączy sygnały: styl OpenXml + regex + NumberingHint, obsługuje rozwiązywanie konfliktów. |
| **`ClassificationInput`** | Record wejściowy klasyfikatora: `(Text: string, StyleId: string?, NumberingHint: NumberingHint?)`. |
| **`ClassificationResult`** | Record wyniku klasyfikacji: `Kind` (enum), `Confidence` (1–100), `IsAmendmentContent: bool`, `StyleType: string?`, `Penalties: IReadOnlyList<ClassificationPenalty>`. |
| **`ParagraphKind`** | Enum typów akapitów (14 wartości): jednostki redakcyjne — `Article`, `Paragraph`, `Point`, `Letter`, `Tiret`, `WrapUp`; jednostki systematyzacyjne (§ 60-62 ZTP) — `PartUnit`, `BookUnit`, `TitleUnit`, `DivisionUnit`, `ChapterUnit`, `SubchapterUnit`, `UnitHeading`; oraz `Unknown`. |
| **`ClassificationPenalty`** | Model kary: `(Reason: string, Value: int)` — obniża Confidence. |
| **`ConfidencePenaltyConfig`** | Statyczne konfiguracje kar: `StyleAbsentPenalty`, `SyntaxAbsentPenalty`, `StyleSyntaxConflictPenalty`, `NumberingBreakPenalty`. |
| **`NumberingHint`** | Model wskazówki numeracji — `ExpectedKind`, `ExpectedNumber?`, `IsContinuous(actual)`, `GetNextLetterValue()`. Obliczany przez orkiestrator (`BuildNumberingHint`) przed klasyfikacją; kara `NumberingBreakPenalty` stosowana wewnątrz `ParagraphClassifier.BuildResult`, gdy rozpoznany `Kind == ExpectedKind`, ale numer nie jest ciągły. |
| **`IConflictResolver`** | Interfejs rozwiązywania konfliktów styl↔treść. |
| **`DefaultConflictResolver`** | Domyślna implementacja: syntaktyka (regex) wygrywa nad stylem. |

### 6.1c Warstwa klasyfikacji dokumentu (`Services/Classify/Document/`)

Rozpoznaje **rodzaj całego dokumentu** (nie akapitu) wg ZTP, zanim (opcjonalnie) zbudowany zostanie
model strukturalny. Wywoływana przez `LegalDocumentParser.Parse(blocks, options, format)` — patrz 5.1.

| Klasa | Rola |
|---|---|
| **`IDocumentClassifier`** | Interfejs — `Classify(IReadOnlyList<DocumentBlock>) → DocumentClassificationResult`. Nie ocenia normatywności ani nie decyduje o parsowaniu — wyłącznie raportuje. |
| **`DocumentClassifier`** | Implementacja dwufazowa: (A) strefa tytułowa — pierwsze 25 niepustych bloków (nagłówek rodzaju aktu, organ, data, przedmiot); (B) statystyka korpusu (formuła kompetencyjna, dominacja jednostki podstawowej Art./§, wejście w życie, markery TJ, komendy nowelizacyjne). Punktacja rozdziela sygnały różnicujące typ od bonusu „aktowości"; wymaga silnego sygnału strukturalnego (`hasBackbone`) i wyniku ≥ progu (40), inaczej `IsLegalAct = false`. |
| **`ZtpPatterns`** | Skompilowane wzorce regex sygnałów dokumentu (nagłówki rodzajów aktu, formuła TJ, formuła kompetencyjna, wejście w życie, komendy nowelizacyjne, publikator woj., markery TJ, organ JST…). |
| **`DocumentClassificationResult`** (ModelDto) | `ActType: LegalActType?`, `IsLegalAct`, `IsConsolidatedText`, `IsAmending`, `Confidence` (1–100), `Signals: IReadOnlyList<DocumentSignal>`, `Justification`. |
| **`DocumentSignal`** / **`DocumentSignalKind`** (ModelDto) | Pojedynczy dowód klasyfikacji: rodzaj sygnału, wynik, indeks bloku, dopasowany fragment, opis. |

`ParseResult`/`ParseOptions`/`ParsePolicy` (w `WordParserCore`, poza `Services/`) spinają tę warstwę
z rdzeniem budowy modelu — patrz 5.1.

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

Poza wzorcem `IEntityBuilder<TInput, TResult>` (jednostki redakcyjne) w tym samym katalogu żyje:

| Builder | Rola |
|---|---|
| `SystematizingUnitBuilder` | Buduje jednostki systematyzacyjne (Część/Księga/Tytuł/Dział/Rozdział/Oddział) i utrzymuje bieżącą ścieżkę w `ParsingContext` (`Enter(ctx, kind, number)`) — patrz 6.1 |

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

Wzorce obsługują opcjonalny prefiks cytatu (`„`, `"`, `"`) dla treści nowelizacji. Jednostki
systematyzacyjne (§ 60-62 ZTP: Część/Księga/Tytuł/Dział/Rozdział/Oddział) mają własne wzorce
(`PartUnitPattern`…`SubchapterUnitPattern`), sprawdzane przed powyższymi — patrz 6.1b/6.1c.
WrapUp **nie** ma osobnego wzorca regex — rozpoznawany jest po stylu (`CZ_WSP_*`), a tekst
(początek półpauzą/dywizem + spacja) jedynie potwierdza lub obniża pewność (`IsWrapUpByText`).

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

`NumberingHint` weryfikuje spójność numeracji (przeprowadzane jako kara), **wyłącznie gdy
rozpoznany `Kind` jest równy `hint.ExpectedKind`** (inaczej hint po prostu nie ma zastosowania —
np. hint dla poziomu "punkt", a bieżący akapit rozpoznano jako "artykuł"):
- Oczekiwany numer (`ExpectedNumber`): jeśli sparsowany numer nie jest oczekiwanym następnikiem
  (`IsContinuous(actual)` zwraca `false`), kara `NumberingBreakPenalty`
- Metody: `IsContinuous(actual)`, `GetNextLetterValue()` (inkrementacja liter w stylu arkusza)

#### Warstwa 4 — Rozstrzyganie konfliktów

Gdy styl i treść się nie zgadzają:
- Używa `IConflictResolver` (domyślnie `DefaultConflictResolver`)
- **Reguła**: syntaktyka (regex) wygrywa nad stylem
- Konflikt generuje `ClassificationPenalty` z `StyleSyntaxConflictPenalty` i `ValidationMessage` na encji

### System kar (Confidence Penalties)

Każda kara obniża `Confidence`:

| Kara | Wartość domyślna | Warunki |
|---|---|---|
| `StyleAbsentPenalty` | 10 | Brak StyleId/stylu rozpoznanego — użyto fallback regex |
| `SyntaxAbsentPenalty` | 15 | Brak dopasowania regex — typ ustalony wyłącznie ze stylu |
| `StyleSyntaxConflictPenalty` | 25 | Konflikt: styl mówi X, treść mówi Y |
| `NumberingBreakPenalty` | 10 | Numeracja nieciągła (zła sekwencja), tylko gdy `Kind == ExpectedKind` |

**Wynik**: `Confidence = Math.Clamp(100 - sum(penalties), 1, 100)`. Gdy brak obu sygnałów (styl i
regex), `Kind = Unknown` i `Confidence = 1` wprost (nie przez odjęcie kar).

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

### 8.4 Nowelizacje bez stylów `Z/*` (dokumenty bezstylowe / PDF / TXT)

Gdy dokument nie niesie stylów Word (np. wejście PDF/TXT), granica nowelizacji jest wyznaczana
inaczej niż powrotem do stylu `ART/UST/PKT/LIT/TIR`:

- **`QuoteBalanceTracker`** — bilansuje cudzysłowy cytowanej treści (§ 94 ZTP: nowe brzmienie
  ujęte w „…"); uzbraja się, gdy pierwszy zbierany blok jest bezstylowy i zaczyna się cudzysłowem
  otwierającym „; koniec cytatu (`ClosureReached`) kończy zbieranie nowelizacji zamiast zmiany stylu.
  Trigger nowelizacyjny wykryty wewnątrz otwartego cytatu jest traktowany jako nowelizacja
  zagnieżdżona (`ZZ`) — cytat trwa (`MarkNestedTrigger`).
- **`AmendmentCommandParser`** — rozpoznaje rodzaj komendy nowelizacyjnej (`AmendmentCommandKind`:
  `Change`/`Add`/`Repeal`/`ReplaceWords`) z samej treści triggera, gdy mapa stylów nie rozstrzyga.
  Źródło niższego priorytetu niż styl — styl zawsze dominuje, gdy jest dostępny.

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

- Oczekiwany typ (`ExpectedKind`): kara stosowana wyłącznie gdy rozpoznany `Kind == ExpectedKind`
  (hint dla innego poziomu hierarchii nie ma zastosowania)
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

Domyślna ścieżka CLI czyta plik read-only, bez kopii. Tylko tryb legacy `--docx` tworzy kopię
z sygnaturą czasową: `nazwa-pliku_YYYYMMDD_HHmmss.ext` (zachowane dla wstecznej kompatybilności —
historycznie parser modyfikował dokument).

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
├── PublisherType.cs                 # Enum typu publikatora
├── LegalActType.cs                  # Enum rodzaju aktu (11 wartości, w tym AmendingStatute/Announcement/Resolution/ExecutiveOrder/LocalLegalAct)
├── DocumentClassificationResult.cs  # Wynik klasyfikacji dokumentu (rodzaj aktu, pewność, sygnały)
├── DocumentSignal.cs                # Pojedynczy dowód klasyfikacji dokumentu
├── DocumentSignalKind.cs            # Enum rodzajów sygnałów klasyfikacji dokumentu
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
├── LegalDocumentParser.cs           # PUNKT WEJŚCIA — Parse(Stream/filePath) → ParseResult; Parse(WordprocessingDocument) [Obsolete]
├── ParseResult.cs                   # Koperta wyniku: Classification, Document?, SourceFormat, BlockCount
├── ParseOptions.cs                  # Opcje: Policy (ParsePolicy), ForcedFormat
├── LoggerConfig.cs                  # Konfiguracja Serilog
├── Exceptions/
│   ├── ParsingException.cs          # Wyjątek bazowy parsowania
│   └── UnsupportedDocumentFormatException.cs  # Nierozpoznany format wejścia
├── Helpers/
│   ├── ParagraphExtensions.cs       # Bezpieczne StyleId() z null-check; GetFullText() z obsługą superscriptu
│   ├── StringExtensions.cs          # Sanitize(), ExtractDate(), ExtractOrdinal()
│   ├── StyleLibraryMapper.cs        # Mapa styli dokumentów prawnych
│   ├── AmendmentStyleDecoder.cs     # Dekoder styli nowelizacyjnych
│   ├── EnumExtensions.cs            # ToDescription() dla enumów
│   └── SpreadsheetHelper.cs         # Helper do tworzenia komórek XLSX
├── Ingest/                          # WARSTWA ODCZYTU FORMATU (DOCX/PDF/TXT → DocumentBlock)
│   ├── SourceFormat.cs              # Enum: Docx/Pdf/PlainText/Unknown
│   ├── SourceFormatDetector.cs      # Detekcja formatu po sygnaturze (nie po rozszerzeniu)
│   ├── DocumentBlockReaderFactory.cs  # Format → IDocumentBlockReader
│   ├── IDocumentBlockReader.cs      # Kontrakt adaptera formatu
│   ├── DocumentBlock.cs             # Format-agnostyczny odpowiednik akapitu (Text/StyleId/Layout/Source/Role)
│   ├── BlockRole.cs                 # Enum: Body/FootnoteText/...
│   ├── BlockLayoutInfo.cs           # Metadane układu (wcięcia, pogrubienie, kursywa)
│   ├── BlockSourceLocation.cs       # Położenie bloku w źródle (diagnostyka)
│   ├── BlockAlignment.cs / BlockAssembler.cs  # Składanie bloków z linii (PDF)
│   ├── DocxBlockReader.cs           # Adapter DOCX (OpenXml Paragraph → DocumentBlock)
│   ├── PlainTextBlockReader.cs      # Adapter TXT
│   ├── TextLine.cs / TextNormalizer.cs  # Pomocnicze dla adaptera TXT
│   └── Pdf/
│       ├── PdfBlockReader.cs        # Adapter PDF (PdfPig) — warstwa tekstowa
│       ├── PdfLineExtractor.cs      # Ekstrakcja linii z geometrii PDF
│       ├── PdfTextLine.cs           # Linia tekstu PDF z pozycją
│       └── PageArtifactFilter.cs    # Filtrowanie artefaktów strony (nagłówki/stopki/numery stron)
└── Services/
    ├── EntityNumberService.cs       # Parsowanie/formatowanie numerów encji
    ├── LegalReferenceService.cs     # Parsowanie odwołań strukturalnych
    ├── JournalReferenceService.cs   # Parsowanie publikatorów (Dz.U.)
    ├── Classify/                    # WARSTWA KLASYFIKACJI AKAPITÓW (jednoprzebiegowa)
    │   ├── IParagraphClassifier.cs  # Interfejs klasyfikatora
    │   ├── ParagraphClassifier.cs   # Jedyna implementacja klasyfikatora
    │   ├── ClassificationInput.cs   # Record wejściowy klasyfikatora
    │   ├── ClassificationResult.cs  # Record wyniku klasyfikacji
    │   ├── ParagraphKind.cs         # Enum (14 wartości): jednostki redakcyjne + systematyzacyjne + WrapUp/Unknown
    │   ├── ClassificationPenalty.cs # Model kary
    │   ├── ConfidencePenaltyConfig.cs  # Konfiguracja kar: Style/Syntax/StyleSyntaxConflict/NumberingBreak
    │   ├── NumberingHint.cs         # Logika ciągłości numeracji
    │   ├── IConflictResolver.cs     # Interfejs rozwiązywania konfliktów
    │   ├── DefaultConflictResolver.cs  # Domyślna implementacja: syntaktyka wygrywa
    │   └── Document/                # WARSTWA KLASYFIKACJI DOKUMENTU (rodzaj aktu wg ZTP)
    │       ├── IDocumentClassifier.cs   # Interfejs klasyfikatora dokumentu
    │       ├── DocumentClassifier.cs    # Implementacja (strefa tytułowa + statystyka korpusu)
    │       └── ZtpPatterns.cs           # Wzorce regex sygnałów dokumentu
    ├── Parsing/
    │   ├── ParserOrchestrator.cs    # Orkiestrator parsowania (pipeline) z DI konstruktorem; ProcessBlock/ProcessParagraph
    │   ├── StructureProcessor.cs    # Przetwarzanie struktury — budowanie encji z ClassificationResult (internal sealed)
    │   ├── AmendmentStateManager.cs # Zarządzanie cyklem życia nowelizacji (UpdateState, Collect, Flush, DetectTrigger)
    │   ├── QuoteBalanceTracker.cs   # Bilans cudzysłowów dla nowelizacji bez stylów (§ 94 ZTP)
    │   ├── AmendmentCommandParser.cs  # Rozpoznanie rodzaju komendy nowelizacyjnej z treści
    │   ├── DocumentMetadataCollector.cs  # Zbiera rodzaj/datę/przedmiot aktu ze strefy tytułowej
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
    │       ├── AmendmentBuilder.cs  # Builder treści nowelizacji
    │       └── SystematizingUnitBuilder.cs  # Builder jednostek systematyzacyjnych (§ 60-62 ZTP)
    └── Converters/
        └── [nieaktywne — kod zakomentowany] Konwertery do XML/XLSX (przyszłościowo)
```

---

## 12. Podsumowanie przepływu danych

```
  Stream (DOCX / PDF z warstwą tekstową / TXT) + fileNameHint?
       │
       ▼
  LegalDocumentParser.Parse(stream, fileNameHint, options)
       ├── SourceFormatDetector.Detect() — sniffing sygnatur (lub options.ForcedFormat)
       ├── DocumentBlockReaderFactory.Create(format) → IDocumentBlockReader
       ├── reader.ReadBlocks(stream) → IReadOnlyList<DocumentBlock>
       │
       ├── DocumentClassifier.Classify(blocks) → DocumentClassificationResult
       │     (rodzaj aktu wg ZTP: Statute/Regulation/Announcement/Resolution/
       │      ExecutiveOrder/AmendingStatute/LocalLegalAct..., IsLegalAct, Confidence, Signals)
       │
       ├── options.Policy decyduje, czy budować model:
       │     ParseWhenLegalAct (domyślna) → tylko gdy IsLegalAct
       │     AlwaysParse                  → zawsze
       │     ClassifyOnly                 → nigdy (Document = null)
       │
       ├── [jeśli budowa modelu] ParseBlocks(blocks):
       │     │
       │     ├── Iteracja po DocumentBlock[]
       │     │     │
       │     │     ▼
       │     │   ParserOrchestrator.ProcessBlock(block, context)
       │     │     ├── block.IsEmpty / block.Role == FootnoteText → pomiń
       │     │     ├── StringExtensions.Sanitize() — normalizacja tekstu
       │     │     ├── BuildNumberingHint() — oczekiwana numeracja na bieżącym poziomie
       │     │     ├── ParagraphClassifier.Classify() → ClassificationResult
       │     │     │   ├── styl (StyleId, gdy DOCX) → styleKind
       │     │     │   ├── regex (syntaktyka, w tym jednostki systematyzacyjne) → syntacticKind
       │     │     │   ├── IConflictResolver — rozwiązanie konfliktu styl↔regex
       │     │     │   ├── NumberingHint — kara przy nieciągłości (gdy Kind == ExpectedKind)
       │     │     │   └── Rezultat: Kind, Confidence, IsAmendmentContent, StyleType, Penalties[]
       │     │     │
       │     │     ├── HandleAmendmentFlow() — zarządzanie nowelizacją
       │     │     │   ├── AmendmentStateManager.UpdateState() / Flush() przy wyjściu
       │     │     │   ├── ShouldExitForNewParentLawTrigger() (+ QuoteBalanceTracker dla nowelizacji bez stylów)
       │     │     │   └── Collect() i STOP, jeśli IsAmendmentContent / InsideAmendment
       │     │     │
       │     │     ├── StructureProcessor.Process() — budowanie struktury (jeśli nie STOP)
       │     │     │   ├── jednostki systematyzacyjne → SystematizingUnitBuilder.Enter()
       │     │     │   ├── *Builder.Build() → encja DTO (Article/Paragraph/Point/Letter/Tiret/WrapUp)
       │     │     │   ├── Unknown → wnioskowanie z tekstu / DocumentMetadataCollector.Observe() / diagnostyka
       │     │     │   ├── Kaskadowe tworzenie implicit encji
       │     │     │   ├── Obsługa CommonPart (intro/wrapUp)
       │     │     │   ├── UpdateStructuralReference() + DetectAmendmentTargets()
       │     │     │   ├── ValidationReporter.AddClassificationWarning() — diagnostyka
       │     │     │   └── JournalReferenceService.ParseJournalReferences() — publikatory (tylko Article)
       │     │     │
       │     │     └── AmendmentStateManager.DetectTrigger() — jeśli encja zbudowana
       │     │
       │     └── ParserOrchestrator.Finalize()
       │           ├── flush ostatniego bufora nowelizacji (jeśli aktywny)
       │           └── context.Metadata.ApplyTo(document) — Title/ActDate ze strefy tytułowej
       │
       ▼
  ParseResult { Classification, Document?, SourceFormat, BlockCount }
       │
       └── Document (LegalDocument, gdy zbudowany)
             ├── Classification — kopia DocumentClassificationResult
             ├── Articles[] → Paragraphs[] → Points[] → Letters[] → Tirets[]
             ├── CommonParts[] (intro/wrapUp)
             ├── ValidationMessages (diagnostyka klasyfikacji)
             ├── Journals (publikatory z Dz.U.)
             └── Amendments (nowelizacje z operacjami)
                   │
                   ▼
             [Przyszłościowo] Converters → XML / XLSX
```
