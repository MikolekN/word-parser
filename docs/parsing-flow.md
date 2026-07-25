# Analiza flow parsowania — WordParserCore

## Cel
Dokumentacja pełnego przebiegu parsowania od punktu wejścia (`LegalDocumentParser.Parse`)
do zbudowania hierarchii encji w modelu obiektowym.

---

## Krok 1 — Punkt wejścia: `LegalDocumentParser.Parse`

**Plik**: `WordParserCore/LegalDocumentParser.cs`

Kanoniczny punkt wejścia jest **uniwersalny** — przyjmuje DOCX/PDF (z warstwą tekstową)/TXT
i zwraca kopertę `ParseResult`, nie sam `LegalDocument`:

```
LegalDocumentParser.Parse(Stream stream, string? fileNameHint = null, ParseOptions? options = null)
  → ParseResult
  1. options ??= ParseOptions.Default
  2. Jeśli stream nie jest seekowalny → buforuje w MemoryStream i wywołuje się rekurencyjnie
  3. Detekcja formatu:
       options.ForcedFormat, jeśli podany i != SourceFormat.Unknown
       w przeciwnym razie SourceFormatDetector.Detect(stream, fileNameHint)
  4. DocumentBlockReaderFactory.Create(format) → IDocumentBlockReader
     (DocxBlockReader / PdfBlockReader / PlainTextBlockReader; nieobsługiwany format
     rzuca UnsupportedDocumentFormatException)
  5. reader.ReadBlocks(stream) → IReadOnlyList<DocumentBlock>
  6. Deleguje do prywatnej Parse(blocks, options, format)

LegalDocumentParser.Parse(string filePath, ParseOptions? options = null)
  → otwiera plik read-only (File.OpenRead) i deleguje do Parse(Stream, ...)

LegalDocumentParser.Parse(IReadOnlyList<DocumentBlock> blocks, ParseOptions? options = null)
  → wejście z gotowych bloków (np. własny adapter wywołującego); SourceFormat w kopercie = Unknown

private Parse(IReadOnlyList<DocumentBlock> blocks, ParseOptions options, SourceFormat format)
  → ParseResult (rdzeń koperty)
  1. classification = new DocumentClassifier().Classify(blocks)  → DocumentClassificationResult
  2. buildModel wg options.Policy (ParsePolicy):
       AlwaysParse        → zawsze true
       ClassifyOnly       → zawsze false
       ParseWhenLegalAct  → classification.IsLegalAct (domyślna)
  3. Jeśli buildModel: document = ParseBlocks(blocks); document.Classification = classification
  4. Zwraca ParseResult { Classification, Document, SourceFormat = format, BlockCount = blocks.Count }

[Obsolete] LegalDocumentParser.Parse(WordprocessingDocument wordDocument)
  → LegalDocument (bezpośrednio, bez klasyfikacji dokumentu i koperty ParseResult)
  → czyta bloki przez new DocxBlockReader().ReadBlocks(wordDocument) i deleguje do ParseBlocks
  → zachowana dla wstecznej kompatybilności; nowy kod powinien używać Parse(Stream, ...)

internal LegalDocumentParser.ParseBlocks(IReadOnlyList<DocumentBlock> blocks)
  → LegalDocument (rdzeń budowy modelu, niezależny od formatu i od klasyfikacji dokumentu)
  1. Tworzy pusty LegalDocument z domyślną strukturą
     (Part → Book → Title → Division → Chapter → Subchapter)
  2. Pobiera domyślny Subchapter (GetDefaultSubchapter)
  3. Tworzy: ParsingContext(document, subchapter)
  4. Tworzy: ParserOrchestrator() — używa domyślnego ParagraphClassifier
  5. Iteruje po blocks (pomija elementy null — parytet z DocumentClassifier.Normalize):
     → orchestrator.ProcessBlock(block, context)
  6. Po pętli: orchestrator.Finalize(context)
  7. Zwraca LegalDocument
```

`ParserOrchestrator.ProcessParagraph(Paragraph, context)` pozostaje jako cienki adapter
OpenXml → `DocumentBlock`, zachowany dla wstecznej kompatybilności testów i dotychczasowych
wywołujących; deleguje do `ProcessBlock`.

---

## Krok 2 — Przetwarzanie bloku: `ParserOrchestrator.ProcessBlock`

**Plik**: `WordParserCore/Services/Parsing/ParserOrchestrator.cs`

Metoda operuje na `DocumentBlock` — format-agnostycznej reprezentacji pośredniej (patrz Krok 1),
wspólnej dla DOCX/PDF/TXT. `ProcessParagraph(Paragraph, context)` jest cienkim adapterem
OpenXml → `DocumentBlock`, zachowanym dla wstecznej kompatybilności (testy, dotychczasowi
wywołujący) — deleguje wprost do `ProcessBlock`.

Dla każdego `DocumentBlock` sekwencja kroków:

```
0. FILTR WSTĘPNY
   - block.IsEmpty → return (pomiń blok)
   - block.Role == BlockRole.FootnoteText → return (przypisy NIE są treścią jednostek
     redakcyjnych — bez tego filtra przypis „1) ..." z dołu strony PDF zostałby
     sklasyfikowany tekstowo jako punkt 1) i przeinaczył treść aktu; klasyfikator
     dokumentu czyta przypisy osobno, z pełnej listy bloków)

1. POBRANIE TEKSTU I SANITACJA
   - text = block.Text.Sanitize().Trim() (normalizacja białych znaków, en-dash itp.)
   - styleId = block.StyleId (null dla PDF/TXT)

2. OBLICZENIE NUMBERING HINT (BuildNumberingHint)
   - Zwraca hint dla najgłębszego aktywnego poziomu (Letter → Point →
     Paragraph (jawny, nie-implicit) → Article)
   - Hint zawiera ExpectedKind + ExpectedNumber dla walidacji ciągłości

3. KLASYFIKACJA
   - _classifier.Classify(new ClassificationInput(text, styleId) { NumberingHint })
   - Zwraca ClassificationResult { Kind, Confidence, IsAmendmentContent,
     StyleType, Penalties }

4. OBSŁUGA NOWELIZACJI (HandleAmendmentFlow — metoda prywatna)
   - AmendmentStateManager.UpdateState() aktualizuje InsideAmendment / Trigger
   - Jeśli wyszliśmy z nowelizacji (wasInside && !nowInside) → Flush()
   - ShouldExitForNewParentLawTrigger() — sprawdza czy bieżący akapit
     to nowy element ustawy matki rozpoczynający się triggerem (wyjście
     i finalizacja poprzedniej nowelizacji); dla nowelizacji bez stylów sprawdza
     najpierw QuoteBalanceTracker (trigger wewnątrz otwartego cytatu = nowelizacja
     zagnieżdżona ZZ, cytat trwa — MarkNestedTrigger())
   - Jeśli IsAmendmentContent lub InsideAmendment:
     → AmendmentStateManager.Collect() buforuje akapit
     → jeśli QuoteBalanceTracker.ClosureReached (bilans cudzysłowów domknięty,
       § 94 ZTP) → Flush() i InsideAmendment = false
     → return true (blok skonsumowany — STOP)

5. BUDOWANIE STRUKTURY (StructureProcessor.Process(context, classification, text, styleId, block.Layout))
   - Dispatch na podstawie classification.Kind (włącznie z jednostkami
     systematyzacyjnymi i WrapUp — patrz Krok 4)
   - Zwraca true jeśli encja została zbudowana / blok skonsumowany

6. WYKRYCIE TRIGGERA NOWELIZACJI (tylko gdy StructureProcessor.Process zwróci true)
   - AmendmentStateManager.DetectTrigger(context, text)
   - Wzorce: RepealPattern ("uchyla się") → natychmiastowy Amendment Repeal
              ModificationPattern ("otrzymuje brzmienie:", "w brzmieniu:")
              → ustawia AmendmentTriggerDetected dla kolejnych akapitów
```

Aktualizacja referencji strukturalnej (`UpdateStructuralReference`), wykrywanie celów
nowelizacji (`DetectAmendmentTargets`) oraz parsowanie publikatorów (`JournalReferenceService`,
tylko dla artykułów) odbywają się **wewnątrz** `StructureProcessor.Process` — nie jako osobne
kroki orkiestratora (patrz Krok 4).

---

## Krok 3 — Klasyfikacja akapitu: `ParagraphClassifier`

**Plik**: `WordParserCore/Services/Classify/ParagraphClassifier.cs`

Klasyfikator jest monolityczną implementacją `IParagraphClassifier` łączącą
wiele sygnałów: styl Word, syntaktyka (regex) i ciągłość numeracji. Konflikty
między stylem a regexem rozstrzyga `IConflictResolver` (domyślnie
`DefaultConflictResolver` — treść wygrywa nad stylem).

### Sygnały wejściowe

```
Sygnał 1 — Styl Word (GetStyleType)
  - Sprawdza StyleLibraryMapper.TryGetStyleInfo dla styli nowelizacji
    i WrapUp (CZ_WSP_*)
  - Fallback: prefiksy "Z/", "ZZ", "Z_" → "AMENDMENT"
  - Prefiksy ART/UST/PKT/LIT/TIR/2TIR/3TIR → odpowiedni typ
  - MapStyleToKind: "ART"→Article, "UST"→Paragraph, "PKT"→Point,
    "LIT"→Letter, "TIR"→Tiret

Sygnał 2 — Syntaktyka (MatchRegex, static readonly compiled)
  - Jednostki systematyzacyjne (§ 60-62 ZTP) sprawdzane NAJPIERW (MatchSystematizingRegex) —
    Część/Księga/Tytuł/Dział wymagają numeru rzymskiego/słownego, Rozdział/Oddział — arabskiego;
    walidacja numeru rozstrzyga, więc np. „Część majątku…" nie jest brana za jednostkę
  - ArticlePattern:   ^"?Art\.?\s*\d+  (IgnoreCase)
  - ParagraphPattern: ^"?\d+[a-zA-Z]*\.\s+  (IgnoreCase)
  - PointPattern:     ^"?\d+[a-zA-Z]*\)\s*  (IgnoreCase)
  - LetterPattern:    ^"?[a-z]{1,2}\)\s*  — UWAGA: TYLKO małe litery, max 2 znaki
    (§ 56 ZTP), bez IgnoreCase — to wzorzec ROZPOZNAJĄCY. Wersalik „A)" lub token 3+
    znaków nie jest literą redakcyjną. Wydobycie numeru i obcięcie prefiksu (również dla
    liter sklasyfikowanych ze stylu LIT) używa szerszego LetterStripPattern/LetterNumberCapture:
    ^"?[a-zA-Z]{1,5}\)\s*  (IgnoreCase) — nie uczestniczy w rozpoznaniu
  - TiretPattern:     ^"?[-–]+\s+  (dywiz LUB półpauza)
  - Wszystkie wzorce obsługują opcjonalny prefiks cytatu („ " " ‟)
    dla treści nowelizacji (OptionalQuotePrefix)

Sygnał 3 — NumberingHint (ciągłość numeracji)
  - Jeśli rozpoznany Kind == hint.ExpectedKind, parsujemy numer z tekstu
    i sprawdzamy hint.IsContinuous(parsedNumber)
  - Niezgodność → kara NumberingBreakPenalty
```

### Drzewo decyzyjne (BuildResult)

```
1. WrapUp (priorytet) — styl == "WRAPUP" (CZ_WSP_*)
   → ParagraphKind.WrapUp; kary za brak półpauzy w tekście / brak stylu

2. Brak obu sygnałów (styleKind == null && syntacticKind == null)
   → Kind = Unknown, Confidence = 1

3. Oba sygnały zgodne (styleKind == syntacticKind)
   → Kind = syntacticKind, Confidence = 100

4. Konflikt (styleKind != syntacticKind, oba != null)
   → Kind = _conflictResolver.Resolve(...)
   → kara StyleSyntaxConflictPenalty

5. Tylko regex (styleKind == null)
   → Kind = syntacticKind, kara StyleAbsentPenalty

6. Tylko styl (syntacticKind == null)
   → Reguła: Article ZAWSZE wymaga sygnatury tekstowej
      - styleKind == Article → Kind = Unknown, Confidence = 1
      - inny → Kind = styleKind, kara SyntaxAbsentPenalty

7. Po wybraniu Kind: jeśli NumberingHint dostępne i niezgodne
   → dodatkowa kara NumberingBreakPenalty
```

### Wynik

`ClassificationResult { Kind, Confidence (1–100), IsAmendmentContent,
StyleType, Penalties: List<ClassificationPenalty> }`

Kary konfigurowane przez `ConfidencePenaltyConfig`:
`StyleAbsentPenalty`, `SyntaxAbsentPenalty`, `StyleSyntaxConflictPenalty`,
`NumberingBreakPenalty`.

---

## Krok 4 — Budowanie struktury: `StructureProcessor.Process`

**Plik**: `WordParserCore/Services/Parsing/StructureProcessor.cs`

Klasa wewnętrzna `internal sealed`. Sygnatura:
`Process(ParsingContext context, ClassificationResult classification, string text, string? sourceStyleId = null, BlockLayoutInfo? layout = null)`.

Przed dispatcherem na `classification.Kind` — dwa kroki wstępne:

```
0a. TYTUŁ JEDNOSTKI SYSTEMATYZACYJNEJ (wzorzec dwuwierszowy, § 60 ZTP)
    Jeśli context.PendingHeadingUnit != null:
      → wyzeruj PendingHeadingUnit
      → jeśli classification.Kind == Unknown i tekst wygląda na tytuł
        (IsHeadingCandidate: krótki, zaczyna się wielką literą, NIE kończy kropką)
        → ustaw pendingUnit.Heading = text, return true (blok skonsumowany)
      → w przeciwnym razie kontynuuj normalną obsługę bieżącego bloku

0b. JEDNOSTKI SYSTEMATYZACYJNE (§ 60-62 ZTP) — mogą wystąpić przed pierwszym artykułem
    Jeśli classification.Kind to PartUnit/BookUnit/TitleUnit/DivisionUnit/ChapterUnit/SubchapterUnit:
      → HandleSystematizingUnit(): parsuje numer (ParseSystematizingNumber) i wywołuje
        SystematizingUnitBuilder.Enter(context, kind, number) — buduje/przejmuje jednostkę,
        ustawia PendingHeadingUnit, czyści CurrentArticle/Paragraph/Point/Letter/TiretStack
      → return true (blok skonsumowany)
```

Dispatcher na `classification.Kind`:

```
Article    → ArticleBuilder.Build()
              context.Metadata.Seal() — pierwszy artykuł zamyka strefę tytułową
              Czyści CurrentPoint, CurrentLetter, TiretStack;
              ustawia CurrentArticle + CurrentParagraph (z ogona "Art. X")
              JournalReferenceService.ParseJournalReferences(article)

Unknown    → HandleUnknown(context, text, sourceStyleId, layout):
              1. jeśli CurrentArticle != null: TryInferKindFromText (Paragraph/Point/
                 Letter/Tiret wg wzorców tekstowych) → jeśli trafiono, rekonstruuje
                 ClassificationResult (Confidence=50) i wywołuje Process() ponownie
              2. jeśli CurrentArticle == null (strefa tytułowa): próbuje
                 context.Metadata.Observe(text) — zbiera rodzaj/datę/przedmiot aktu
                 (§ 16-19 ZTP); w przeciwnym razie loguje i pomija
              3. w razie braku wzorca i istniejącego artykułu — dołącza
                 ValidationMessage do najgłębszej aktywnej encji

Paragraph  → ParagraphBuilder.Build()
              Czyści CurrentPoint, CurrentLetter, TiretStack

Point      → ParagraphBuilder.EnsureForPoint() + PointBuilder.Build()
              Przed dodaniem pierwszego Point → AttachIntroCommonPart(Paragraph)

Letter     → PointBuilder.EnsureForLetter() + LetterBuilder.Build()
              Przed pierwszą Letter → AttachIntroCommonPart(Point)
              Jeśli utworzono niejawny Point → ValidationMessage Warning

Tiret      → kaskada Ensure (Point, Letter)
              + AttachIntroCommonPart przed pierwszym tiretem
              + GetTiretDepth(sourceStyleId, layout, context): priorytet (1) styl
                jawnie kodujący głębokość (2TIR=2, 3TIR=3, TIR=1); (2) wcięcie lewe
                bloku (layout.LeftIndentTwips) względem tiretów otwartych na stosie
                (§ 58 ZTP, InferTiretDepthFromIndent) — dla dokumentów bezstylowych
                z układem (PDF); (3) brak sygnału → poziom 1 (np. czysty TXT)
              + skracanie TiretStack do depth-1, wybór parentTiret
              + TiretBuilder.Build(... parentTiret)
              + jeśli parentTiret != null i jego Tirets.Count == 0
                → AttachIntroCommonPart(parentTiret)
              + dodanie do TiretStack (wraz z wcięciem — PushTiret)
              + diagnostyka: Info gdy głębokość ustalona z wcięcia (nie ze stylu),
                Warning gdy poziom > 1 sklasyfikowany, ale brak tiretu nadrzędnego
                (degradacja do poziomu 1)

WrapUp     → TryHandleWrapUp(): wykrywa styl CZ_WSP_PKT/LIT/TIR i wywołuje
              ParsingFactories.AttachWrapUpCommonPart(parent, text)
```

Po zbudowaniu każdej encji `IHasAmendments`:
- `UpdateStructuralReference(context, entity)` — aktualizuje
  `CurrentStructuralReference` (kaskadowe zerowanie podrzędnych poziomów)
- `DetectAmendmentTargets(context, entity)` — parsuje treść w
  poszukiwaniu odwołań ("w art. 5", "po ust. 2") i zapisuje do
  `context.DetectedAmendmentTargets[entity.Guid]`. Kontekst dziedziczony
  z encji nadrzędnej przez `FindParentAmendmentTargetReference`.

Encje są też anotowane diagnostycznie przez
`ValidationReporter.AddClassificationWarning(entity, classification, prefix)`.

---

## Krok 5 — Buildery encji (wzorzec kaskadowy) i jednostki systematyzacyjne

**Pliki**: `WordParserCore/Services/Parsing/Builders/`

### Hierarchia builderów jednostek redakcyjnych (od najwyższego do najniższego):
```
ArticleBuilder → ParagraphBuilder → PointBuilder → LetterBuilder → TiretBuilder
```

### `SystematizingUnitBuilder` (jednostki systematyzacyjne, § 60-62 ZTP)

Odrębny builder (nie wpisuje się w `IEntityBuilder<TInput, TResult>`) budujący
Część→Księga→Tytuł→Dział→Rozdział→Oddział i utrzymujący bieżącą ścieżkę w `ParsingContext`
(`CurrentPart`/`CurrentBook`/`CurrentTitle`/`CurrentDivision`/`CurrentChapter`/`Subchapter`):

```
Enter(ctx, kind, number):
  - Pierwsza JAWNA jednostka danego poziomu przejmuje istniejący węzeł NIEJAWNY
    (CanClaim: IsImplicit && brak artykułów w poddrzewie) — unika reparentowania
    wcześniejszych artykułów (zmiana eId)
  - Kolejna jednostka tego samego poziomu tworzy jednostkę-rodzeństwo z własnym
    świeżym łańcuchem niejawnym w dół do Oddziału
  - Ustawia ctx.PendingHeadingUnit = entered (na wypadek tytułu w kolejnym wierszu)
  - Czyści CurrentArticle/Paragraph/Point/Letter/TiretStack (nowa jednostka
    systematyzacyjna zaczyna świeże jednostki redakcyjne)

Ograniczenie modelu: RootPart jest pojedynczy — druga jawna „CZĘŚĆ" nie jest
reprezentowalna i jest pomijana z ostrzeżeniem (Log.Warning).
```

### Mechanizm kaskadowy jednostek redakcyjnych — kluczowa zasada:
Każdy builder niższego poziomu udostępnia metodę `EnsureFor*()`, która
tworzy **niejawną (implicit)** encję rodzica jeśli brakuje. Wywoływana
przez `StructureProcessor` przed `Build()` na poziomie dziecka.

### ArticleBuilder
```
Wejście: ArticleBuildInput(Subchapter, text)
1. Tworzy Article, parent=Subchapter
2. Parsuje numer: ParseArticleNumber(text) → EntityNumber
3. Wyciąga "ogon" (tekst za "Art. X") jako pierwszy Paragraph
4. Tworzy Paragraph (niejawny jeśli brak tekstu w ogonie)
5. Dodaje Article do Subchapter.Articles
6. Zwraca ArticleBuildResult(Article, Paragraph)
```

### ParagraphBuilder
```
Wejście: ParagraphBuildInput(Article, CurrentParagraph?, text)
- Jeśli CurrentParagraph jest niejawny i pusty:
  → aktualizuje go (realizuje jako jawny), zwraca go
- W przeciwnym razie:
  → tworzy nowy Paragraph, dodaje do Article.Paragraphs

EnsureForPoint(Article, CurrentParagraph?):
  → jeśli null lub wymaga utworzenia: zwraca pakiet z Paragraphem
    (niejawnym gdy potrzebny)
```

### PointBuilder
```
Wejście: PointBuildInput(Paragraph, Article, text)
1. Tworzy Point, parent=Paragraph
2. Parsuje numer: ParsePointNumber(text) → EntityNumber
3. Dodaje do Paragraph.Points

EnsureForLetter(Paragraph?, Article, CurrentPoint?):
  → jeśli null: tworzy niejawny Point (CreatedImplicit=true)
```

### LetterBuilder
```
Wejście: LetterBuildInput(Point, Paragraph?, Article, text)
1. Tworzy Letter, parent=Point
2. Parsuje numer: ParseLetterNumber(text) → EntityNumber (symbol: a, b, aa…)
3. Dodaje do Point.Letters

EnsureForTiret(Point, Paragraph?, Article, CurrentLetter?):
  → jeśli null: tworzy niejawną Letter (CreatedImplicit=true)
```

### TiretBuilder
```
Wejście: TiretBuildInput(Letter, Point?, Paragraph?, Article, text, index,
                        ParentTiret?)
1. Tworzy Tiret, parent=Letter lub parent=ParentTiret (zagnieżdżony)
2. Numer = EntityNumber { NumericPart=index } (sekwencyjny, nie parsowany)
3. Dodaje do Letter.Tirets (lub ParentTiret.Tirets dla zagnieżdżonych)
```

### Przykład kaskady dla tiretu bez wyraźnych rodziców:
```
Dokument: Art. 5. → – (tiret)

1. ArticleBuilder → Article + Paragraph(niejawny)
2. StructureProcessor (case Tiret):
   → pointBuilder.EnsureForLetter()  → Point(niejawny)
   → letterBuilder.EnsureForTiret()  → Letter(niejawna)
   → tiretBuilder.Build(letter, point, paragraph, article, text,
                        index=1, parentTiret=null)

Wynik eId: art_5__tir_1
(niejawne jednostki pomijane w eId)
```

---

## Krok 6 — Model danych encji

**Pliki**: `ModelDto/`

### BaseEntity (baza wszystkich encji)
```csharp
Guid: Guid
UnitType: UnitType
DisplayLabel: string
EIdPrefix: string
Number: EntityNumber? { NumericPart, LexicalPart, Superscript, Value }
ContentText: string
EffectiveDate: DateTime
ValidationMessages: List<ValidationMessage>

// Hierarchia
Parent: BaseEntity?           // bezpośredni rodzic
Article: Article?             // skrót do artykułu (bez iteracji po hierarchii)
Paragraph: Paragraph?         // skrót do ustępu
Point: Point?                 // skrót do punktu
Letter: Letter?               // skrót do litery
Tiret: Tiret?                 // skrót do tiretu

Id (virtual): string          // eId: "art_5__ust_2__pkt_3__lit_a__tir_1"
```

### Hierarchia kolekcji dzieci:
```
Article.Paragraphs: List<Paragraph>
Paragraph.Points:   List<Point>       + IsImplicit, TextSegments, CommonParts, Amendment
Point.Letters:      List<Letter>      + TextSegments, CommonParts, Amendment
Letter.Tirets:      List<Tiret>       + TextSegments, CommonParts, Amendment
Tiret.Tirets:       List<Tiret>       + TextSegments (zagnieżdżone tirety: 2TIR / 3TIR)
```

---

## Krok 7 — Finalizacja: `ParserOrchestrator.Finalize`

```
orchestrator.Finalize(context)
  1. Jeśli context.InsideAmendment LUB context.AmendmentCollector.IsCollecting:
    AmendmentStateManager.Flush(context)
      ├── AmendmentBuilder.Build(AmendmentBuildInput) → AmendmentContent
      ├── AmendmentFinalizer.Finalize(AmendmentFinalizerInput)
      │     → wykrywa AmendmentOperationType
      │     → łączy z JournalInfo / DetectedAmendmentTargets
      │     → przypisuje obiekt Amendment do encji-właściciela
      │       (Paragraph/Point/Letter/Tiret.Amendment przez IHasAmendments)
      └── collector.Reset(); context.AmendmentOwner = null;
    context.InsideAmendment = false;
  2. context.Metadata.ApplyTo(context.Document) — zapisuje Title/ActDate zebrane
     ze strefy tytułowej (DocumentMetadataCollector) do korzenia dokumentu
```

---

## Pełny diagram sekwencji

```
Parse(Stream, fileNameHint?, ParseOptions?)
  ↓
  strumień nieseekowalny? → buforuj w MemoryStream
  ↓
  SourceFormatDetector.Detect() (lub options.ForcedFormat) → SourceFormat
  ↓
  DocumentBlockReaderFactory.Create(format) → IDocumentBlockReader
  ↓
  reader.ReadBlocks(stream) → IReadOnlyList<DocumentBlock>
  ↓
  DocumentClassifier.Classify(blocks) → DocumentClassificationResult
        (strefa tytułowa: nagłówek rodzaju aktu, organ, data, przedmiot;
         korpus: formuła kompetencyjna, dominacja Art./§, wejście w życie,
         markery TJ, komendy nowelizacyjne → ActType?, IsLegalAct, Confidence, Signals)
  ↓
  options.Policy decyduje o budowie modelu (ParseWhenLegalAct/AlwaysParse/ClassifyOnly)
  ↓ (jeśli budowa)
  ParseBlocks(blocks):
    ParsingContext + ParserOrchestrator (+ ParagraphClassifier)
    ↓
    foreach DocumentBlock in blocks (pomijając null):
      ↓
      [0] block.IsEmpty / Role==FootnoteText → pomiń
      ↓
      [1] block.Text → Sanitize → Trim; StyleId = block.StyleId
      ↓
      [2] BuildNumberingHint(context) → NumberingHint?
      ↓
      [3] ParagraphClassifier.Classify(ClassificationInput)
            → jednostki systematyzacyjne sprawdzane najpierw w regexie
            → łączy: StyleType + regex MatchRegex + NumberingHint
            → IConflictResolver rozstrzyga konflikty (treść > styl)
            → ClassificationResult { Kind, Confidence, IsAmendmentContent,
                                      StyleType, Penalties }
      ↓
      [4] HandleAmendmentFlow():
            ├─ AmendmentStateManager.UpdateState()
            ├─ jeśli wyszliśmy z nowelizacji → Flush()
            ├─ ShouldExitForNewParentLawTrigger() (+ QuoteBalanceTracker
            │    dla nowelizacji bez stylów) → ewentualny Flush
            └─ jeśli IsAmendmentContent / InsideAmendment → Collect() + STOP
                  (+ Flush jeśli QuoteBalanceTracker.ClosureReached)
      ↓
      [5] StructureProcessor.Process(context, classification, text, styleId, layout):
            ├─ PendingHeadingUnit → tytuł jednostki systematyzacyjnej (§ 60)?
            ├─ jednostki systematyzacyjne → SystematizingUnitBuilder.Enter()
            ├─ Article   → ArticleBuilder.Build() (+ context.Metadata.Seal())
            ├─ Unknown   → wnioskowanie z tekstu / DocumentMetadataCollector.Observe() / diagnostyka
            ├─ Paragraph → ParagraphBuilder.Build()
            ├─ Point     → [EnsureForPoint] + PointBuilder.Build()
            ├─ Letter    → [EnsureForLetter] + LetterBuilder.Build()
            ├─ Tiret     → [EnsureForLetter+Tiret] + TiretBuilder.Build()
            │              (TiretStack, parentTiret, depth ze stylu lub z layout.LeftIndentTwips)
            └─ WrapUp    → TryHandleWrapUp → AttachWrapUpCommonPart
            → UpdateStructuralReference + DetectAmendmentTargets
            → JournalReferenceService.ParseJournalReferences (tylko Article)
            → ValidationReporter.AddClassificationWarning
      ↓
      [6] DetectTrigger() — szuka RepealPattern / ModificationPattern
            ("uchyla się" → natychmiastowy Amendment Repeal;
             "otrzymuje brzmienie:" / "w brzmieniu:" → flaga triggera)
    ↓
    Finalize():
      ├─ flush nowelizacji jeśli aktywna
      └─ context.Metadata.ApplyTo(document) — Title/ActDate ze strefy tytułowej
    ↓
    return LegalDocument { Part > Book > Title > Division > Chapter > Subchapter > Articles }
  ↓
  document.Classification = classification (jeśli zbudowano model)
  ↓
  return ParseResult { Classification, Document?, SourceFormat, BlockCount }
```

---

## Klasy uczestniczące (podsumowanie)

| Klasa | Plik | Rola |
|---|---|---|
| `LegalDocumentParser` | `LegalDocumentParser.cs` | Publiczny punkt wejścia — `Parse(Stream/filePath/blocks)` → `ParseResult`; `ParseBlocks` (internal, rdzeń); `Parse(WordprocessingDocument)` `[Obsolete]` |
| `ParseResult` | `ParseResult.cs` | Koperta wyniku: Classification, Document?, SourceFormat, BlockCount |
| `ParseOptions` / `ParsePolicy` | `ParseOptions.cs` | Opcje: Policy (ParseWhenLegalAct/AlwaysParse/ClassifyOnly), ForcedFormat |
| `SourceFormatDetector` | `Ingest/SourceFormatDetector.cs` | Detekcja formatu po sygnaturze (ZIP/OOXML, `%PDF-`, heurystyka BOM/tekst) |
| `DocumentBlockReaderFactory` | `Ingest/DocumentBlockReaderFactory.cs` | Format → `IDocumentBlockReader` |
| `IDocumentBlockReader` / `DocxBlockReader` / `PdfBlockReader` / `PlainTextBlockReader` | `Ingest/` | Adaptery formatu → `IReadOnlyList<DocumentBlock>` |
| `DocumentBlock` / `BlockRole` / `BlockLayoutInfo` | `Ingest/` | Format-agnostyczna reprezentacja pośrednia akapitu |
| `IDocumentClassifier` / `DocumentClassifier` | `Services/Classify/Document/` | Klasyfikacja rodzaju CAŁEGO dokumentu (ZTP), wywoływana przed budową modelu |
| `ZtpPatterns` | `Services/Classify/Document/ZtpPatterns.cs` | Wzorce regex sygnałów klasyfikacji dokumentu |
| `DocumentClassificationResult` / `DocumentSignal` | `ModelDto/` | Wynik klasyfikacji dokumentu (ActType, IsLegalAct, Confidence, Signals) |
| `ParserOrchestrator` | `Services/Parsing/ParserOrchestrator.cs` | Główna pętla + koordynacja — `ProcessBlock(DocumentBlock, context)`; `ProcessParagraph` = adapter OpenXml |
| `ParsingContext` | `Services/Parsing/ParsingContext.cs` | Mutowalny stan parsowania (w tym jednostki systematyzacyjne i metadane aktu) |
| `IParagraphClassifier` | `Services/Classify/IParagraphClassifier.cs` | Interfejs klasyfikatora akapitu (DI) |
| `ParagraphClassifier` | `Services/Classify/ParagraphClassifier.cs` | Jedyna implementacja — jednoprzebiegowa (styl + regex + numeracja) |
| `ClassificationInput` | `Services/Classify/ClassificationInput.cs` | Wejście klasyfikatora |
| `ClassificationResult` | `Services/Classify/ClassificationResult.cs` | Wynik klasyfikacji: Kind, Confidence, IsAmendmentContent, StyleType, Penalties |
| `ParagraphKind` | `Services/Classify/ParagraphKind.cs` | Enum typów akapitów (14 wartości: jednostki redakcyjne + systematyzacyjne + WrapUp/Unknown) |
| `ClassificationPenalty` | `Services/Classify/ClassificationPenalty.cs` | Model kary |
| `ConfidencePenaltyConfig` | `Services/Classify/ConfidencePenaltyConfig.cs` | Konfiguracja kar (Style/Syntax/StyleSyntaxConflict/NumberingBreak) |
| `NumberingHint` | `Services/Classify/NumberingHint.cs` | Walidacja ciągłości numeracji |
| `IConflictResolver` | `Services/Classify/IConflictResolver.cs` | Interfejs rozstrzygania konfliktów |
| `DefaultConflictResolver` | `Services/Classify/DefaultConflictResolver.cs` | Domyślna reguła: treść > styl |
| `StructureProcessor` | `Services/Parsing/StructureProcessor.cs` | Dispatcher na buildery + jednostki systematyzacyjne + WrapUp |
| `SystematizingUnitBuilder` | `Services/Parsing/Builders/SystematizingUnitBuilder.cs` | Buduje jednostki systematyzacyjne (§ 60-62 ZTP) |
| `DocumentMetadataCollector` | `Services/Parsing/DocumentMetadataCollector.cs` | Zbiera rodzaj/datę/przedmiot aktu ze strefy tytułowej |
| `ArticleBuilder` | `Services/Parsing/Builders/ArticleBuilder.cs` | Buduje Article + Paragraph |
| `ParagraphBuilder` | `Services/Parsing/Builders/ParagraphBuilder.cs` | Buduje Paragraph |
| `PointBuilder` | `Services/Parsing/Builders/PointBuilder.cs` | Buduje Point |
| `LetterBuilder` | `Services/Parsing/Builders/LetterBuilder.cs` | Buduje Letter |
| `TiretBuilder` | `Services/Parsing/Builders/TiretBuilder.cs` | Buduje Tiret (z parentTiret dla zagnieżdżenia) |
| `AmendmentStateManager` | `Services/Parsing/AmendmentStateManager.cs` | UpdateState / Collect / Flush / DetectTrigger |
| `QuoteBalanceTracker` | `Services/Parsing/QuoteBalanceTracker.cs` | Bilans cudzysłowów — koniec nowelizacji bez stylów (§ 94 ZTP) |
| `AmendmentCommandParser` | `Services/Parsing/AmendmentCommandParser.cs` | Rozpoznaje rodzaj komendy nowelizacyjnej z treści |
| `AmendmentCollector` | `Services/Parsing/AmendmentCollector.cs` | Buforuje treść nowelizacji |
| `AmendmentBuilder` | `Services/Parsing/Builders/AmendmentBuilder.cs` | Buduje AmendmentContent |
| `AmendmentFinalizer` | `Services/Parsing/AmendmentFinalizer.cs` | Materializuje Amendment i przypisuje do owner'a |
| `ValidationReporter` | `Services/Parsing/ValidationReporter.cs` | Diagnostyka klasyfikacji |
| `ParsingFactories` | `Services/Parsing/ParsingFactories.cs` | Parsowanie numerów, AttachIntro/WrapUp CommonPart |
| `LegalReferenceService` | `Services/LegalReferenceService.cs` | Wykrywanie celów nowelizacji |
| `JournalReferenceService` | `Services/JournalReferenceService.cs` | Parsowanie publikatorów (Dz.U.) |
| `BaseEntity` | `ModelDto/BaseEntity.cs` | Baza wszystkich encji domenowych |
| `EntityNumber` | `ModelDto/EntityNumber.cs` | Model numeru encji |
| `LegalDocument` | `ModelDto/LegalDocument.cs` | Korzeń dokumentu |
