# Analiza flow parsowania — WordParserCore

## Cel
Dokumentacja pełnego przebiegu parsowania od punktu wejścia (`LegalDocumentParser.Parse`)
do zbudowania hierarchii encji w modelu obiektowym.

---

## Krok 1 — Punkt wejścia: `LegalDocumentParser.Parse`

**Plik**: `WordParserCore/LegalDocumentParser.cs`

```
LegalDocumentParser.Parse(filePath)
  → otwiera DOCX przez WordprocessingDocument.Open()
  → deleguje do Parse(WordprocessingDocument)

LegalDocumentParser.Parse(WordprocessingDocument)
  1. Waliduje MainDocumentPart (rzuca ParsingException jeśli null)
  2. Tworzy pusty LegalDocument z domyślną strukturą
     (Part → Book → Title → Division → Chapter → Subchapter)
  3. Pobiera domyślny Subchapter (GetDefaultSubchapter)
  4. Tworzy: ParsingContext(document, subchapter)
  5. Tworzy: ParserOrchestrator() — używa domyślnego ParagraphClassifier
  6. Iteruje po wszystkich akapitach (Descendants<Word.Paragraph>):
     → orchestrator.ProcessParagraph(paragraph, context)
  7. Po pętli: orchestrator.Finalize(context)
  8. Zwraca LegalDocument
```

---

## Krok 2 — Przetwarzanie akapitu: `ParserOrchestrator.ProcessParagraph`

**Plik**: `WordParserCore/Services/Parsing/ParserOrchestrator.cs`

Dla każdego `Paragraph` (OpenXml) sekwencja kroków:

```
1. POBRANIE TEKSTU I SANITACJA
   - paragraph.GetFullText().Trim()
   - Jeśli pusty → return (pomiń akapit)
   - .Sanitize().Trim() (normalizacja białych znaków, en-dash itp.)
   - paragraph.StyleId() — bezpieczne pobranie StyleId (null-safe)

2. OBLICZENIE NUMBERING HINT (BuildNumberingHint)
   - Zwraca hint dla najgłębszego aktywnego poziomu (Letter → Point →
     Paragraph (jawny) → Article)
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
     i finalizacja poprzedniej nowelizacji)
   - Jeśli IsAmendmentContent lub InsideAmendment:
     → AmendmentStateManager.Collect() buforuje akapit
     → return true (akapit skonsumowany — STOP)

5. BUDOWANIE STRUKTURY (StructureProcessor.Process)
   - Dispatch na podstawie classification.Kind (włącznie z WrapUp)
   - Zwraca true jeśli akapit został skonsumowany

6. WYKRYCIE TRIGGERA NOWELIZACJI (tylko gdy structureProcessor zwróci true)
   - AmendmentStateManager.DetectTrigger(context, text)
   - Wzorce: RepealPattern ("uchyla się") → natychmiastowy Amendment Repeal
              ModificationPattern ("otrzymuje brzmienie:", "w brzmieniu:")
              → ustawia AmendmentTriggerDetected dla kolejnych akapitów
```

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
  - ArticlePattern:   ^"?Art\.?\s*\d+
  - ParagraphPattern: ^"?\d+[a-zA-Z]*\.\s+
  - PointPattern:     ^"?\d+[a-zA-Z]*\)\s*
  - LetterPattern:    ^"?[a-zA-Z]{1,5}\)\s*
  - TiretPattern:     ^-+\s+
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

Klasa wewnętrzna `internal sealed`. Dispatcher na `classification.Kind`:

```
Article    → ArticleBuilder.Build()
              Czyści CurrentPoint, CurrentLetter, TiretStack;
              ustawia CurrentArticle + CurrentParagraph (z ogona "Art. X")
              JournalReferenceService.ParseJournalReferences(article)

Unknown    → HandleUnknown() — inference-first:
              próba TryInferKindFromText i ponowne wywołanie Process,
              w razie braku wzorca dołącza ValidationMessage do najgłębszej encji

Paragraph  → ParagraphBuilder.Build()
              Czyści CurrentPoint, CurrentLetter, TiretStack

Point      → ParagraphBuilder.EnsureForPoint() + PointBuilder.Build()
              Przed dodaniem pierwszego Point → AttachIntroCommonPart(Paragraph)

Letter     → PointBuilder.EnsureForLetter() + LetterBuilder.Build()
              Przed pierwszą Letter → AttachIntroCommonPart(Point)
              Jeśli utworzono niejawny Point → ValidationMessage Warning

Tiret      → kaskada Ensure (Point, Letter)
              + AttachIntroCommonPart przed pierwszym tiretem
              + GetTiretDepth(styleId) z prefiksu stylu (TIR=1, 2TIR=2, 3TIR=3)
              + skracanie TiretStack do depth-1, wybór parentTiret
              + TiretBuilder.Build(... parentTiret)
              + jeśli parentTiret != null i jego Tirets.Count == 0
                → AttachIntroCommonPart(parentTiret)
              + dodanie do TiretStack

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

## Krok 5 — Buildery encji (wzorzec kaskadowy)

**Pliki**: `WordParserCore/Services/Parsing/Builders/`

### Hierarchia builderów (od najwyższego do najniższego):
```
ArticleBuilder → ParagraphBuilder → PointBuilder → LetterBuilder → TiretBuilder
```

### Mechanizm kaskadowy — kluczowa zasada:
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
  → Jeśli context.InsideAmendment LUB context.AmendmentCollector.IsCollecting:
    AmendmentStateManager.Flush(context)
      ├── AmendmentBuilder.Build(AmendmentBuildInput) → AmendmentContent
      ├── AmendmentFinalizer.Finalize(AmendmentFinalizerInput)
      │     → wykrywa AmendmentOperationType
      │     → łączy z JournalInfo / DetectedAmendmentTargets
      │     → przypisuje obiekt Amendment do encji-właściciela
      │       (Paragraph/Point/Letter/Tiret.Amendment przez IHasAmendments)
      └── collector.Reset(); context.AmendmentOwner = null;
    context.InsideAmendment = false;
```

---

## Pełny diagram sekwencji

```
Parse(filePath)
  ↓
  WordprocessingDocument.Open()
  ↓
  ParsingContext + ParserOrchestrator (+ ParagraphClassifier)
  ↓
  foreach Paragraph in document.Descendants<Paragraph>():
    ↓
    [1] GetFullText → Trim → Sanitize → StyleId
    ↓
    [2] BuildNumberingHint(context) → NumberingHint?
    ↓
    [3] ParagraphClassifier.Classify(ClassificationInput)
          → łączy: StyleType + regex MatchRegex + NumberingHint
          → IConflictResolver rozstrzyga konflikty (treść > styl)
          → ClassificationResult { Kind, Confidence, IsAmendmentContent,
                                    StyleType, Penalties }
    ↓
    [4] HandleAmendmentFlow():
          ├─ AmendmentStateManager.UpdateState()
          ├─ jeśli wyszliśmy z nowelizacji → Flush()
          ├─ ShouldExitForNewParentLawTrigger() → ewentualny Flush
          └─ jeśli IsAmendmentContent / InsideAmendment → Collect() + STOP
    ↓
    [5] StructureProcessor.Process(classification, text, styleId):
          ├─ Article   → ArticleBuilder.Build()
          ├─ Unknown   → HandleUnknown (inference-first lub diagnostyka)
          ├─ Paragraph → ParagraphBuilder.Build()
          ├─ Point     → [EnsureForPoint] + PointBuilder.Build()
          ├─ Letter    → [EnsureForLetter] + LetterBuilder.Build()
          ├─ Tiret     → [EnsureForLetter+Tiret] + TiretBuilder.Build()
          │              (TiretStack, parentTiret, depth z stylu)
          └─ WrapUp    → TryHandleWrapUp → AttachWrapUpCommonPart
          → UpdateStructuralReference + DetectAmendmentTargets
          → ValidationReporter.AddClassificationWarning
    ↓
    [6] DetectTrigger() — szuka RepealPattern / ModificationPattern
          ("uchyla się" → natychmiastowy Amendment Repeal;
           "otrzymuje brzmienie:" / "w brzmieniu:" → flaga triggera)
  ↓
  Finalize() → flush nowelizacji jeśli aktywna
  ↓
  return LegalDocument { Part > Book > Title > Division > Chapter > Subchapter > Articles }
```

---

## Klasy uczestniczące (podsumowanie)

| Klasa | Plik | Rola |
|---|---|---|
| `LegalDocumentParser` | `LegalDocumentParser.cs` | Publiczny punkt wejścia |
| `ParserOrchestrator` | `Services/Parsing/ParserOrchestrator.cs` | Główna pętla + koordynacja |
| `ParsingContext` | `Services/Parsing/ParsingContext.cs` | Mutowalny stan parsowania |
| `IParagraphClassifier` | `Services/Classify/IParagraphClassifier.cs` | Interfejs klasyfikatora (DI) |
| `ParagraphClassifier` | `Services/Classify/ParagraphClassifier.cs` | Klasyfikator (styl + regex + numeracja) |
| `ClassificationInput` | `Services/Classify/ClassificationInput.cs` | Wejście klasyfikatora |
| `ClassificationResult` | `Services/Classify/ClassificationResult.cs` | Wynik klasyfikacji |
| `ParagraphKind` | `Services/Classify/ParagraphKind.cs` | Enum typów akapitów |
| `ClassificationPenalty` | `Services/Classify/ClassificationPenalty.cs` | Model kary |
| `ConfidencePenaltyConfig` | `Services/Classify/ConfidencePenaltyConfig.cs` | Konfiguracja kar |
| `NumberingHint` | `Services/Classify/NumberingHint.cs` | Walidacja ciągłości numeracji |
| `IConflictResolver` | `Services/Classify/IConflictResolver.cs` | Interfejs rozstrzygania konfliktów |
| `DefaultConflictResolver` | `Services/Classify/DefaultConflictResolver.cs` | Domyślna reguła: treść > styl |
| `StructureProcessor` | `Services/Parsing/StructureProcessor.cs` | Dispatcher na buildery + WrapUp |
| `ArticleBuilder` | `Services/Parsing/Builders/ArticleBuilder.cs` | Buduje Article + Paragraph |
| `ParagraphBuilder` | `Services/Parsing/Builders/ParagraphBuilder.cs` | Buduje Paragraph |
| `PointBuilder` | `Services/Parsing/Builders/PointBuilder.cs` | Buduje Point |
| `LetterBuilder` | `Services/Parsing/Builders/LetterBuilder.cs` | Buduje Letter |
| `TiretBuilder` | `Services/Parsing/Builders/TiretBuilder.cs` | Buduje Tiret (z parentTiret dla zagnieżdżenia) |
| `AmendmentStateManager` | `Services/Parsing/AmendmentStateManager.cs` | UpdateState / Collect / Flush / DetectTrigger |
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
