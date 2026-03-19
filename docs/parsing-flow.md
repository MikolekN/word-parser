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
  5. Tworzy: ParserOrchestrator()
  6. Iteruje po wszystkich akapitach dokumentu:
     → orchestrator.ProcessParagraph(paragraph, context)
  7. Po pętli: orchestrator.Finalize(context)
  8. Zwraca LegalDocument
```

---

## Krok 2 — Przetwarzanie akapitu: `ParserOrchestrator.ProcessParagraph`

**Plik**: `WordParserCore/Services/Parsing/ParserOrchestrator.cs`

Dla każdego `Paragraph` (OpenXml) sekwencja kroków:

```
1. SANITACJA
   - GetFullText(paragraph) → Trim → Sanitize
   - Jeśli pusty po sanitacji → return (pomiń akapit)

2. KLASYFIKACJA
   - Classify(text, styleId, context)
   - Jeśli klasyfikator to LayeredParagraphClassifier:
     przekazuje context.CurrentArticleTexts (historia artykułu dla AI)
   - Zwraca ClassificationResult { Kind, StyleType, IsAmendmentContent, Confidence, ... }

3. AKTUALIZACJA KONTEKSTU ARTYKUŁU
   - Jeśli Kind == Article → wyczyść CurrentArticleTexts
   - Jeśli nie nowelizacja → dodaj tekst do CurrentArticleTexts

4. OBSŁUGA NOWELIZACJI (HandleAmendmentFlow)
   - AmendmentStateManager analizuje czy jesteśmy wewnątrz nowelizacji
   - Jeśli IsAmendmentContent=true → AmendmentCollector buforuje akapit
   - Jeśli zwróci true → STOP (akapit pochłonięty przez nowelizację)

5. OBSŁUGA WRAPUP (TryHandleWrapUp)
   - StructureProcessor sprawdza czy to "zamknięcie" jednostki
   - Jeśli true → STOP

6. BUDOWANIE STRUKTURY (StructureProcessor.Process)
   - Dispatcher na podstawie Kind:
     Article   → ArticleBuilder.Build()
     Paragraph → ParagraphBuilder.Build()
     Point     → PointBuilder.Build()   (+ EnsureForLetter jeśli brak Paragraph)
     Letter    → LetterBuilder.Build()  (+ EnsureForPoint, EnsureForLetter)
     Tiret     → TiretBuilder.Build()   (+ kaskada Ensure)
   - Jeśli encja zbudowana → DetectTrigger(context, text)
     Szuka słów kluczowych: "otrzymuje brzmienie:", "dodaje się", "uchyla się"
```

---

## Krok 3 — Klasyfikacja: warstwy

**Pliki**: `WordParserCore/Services/Parsing/Classification/`

### Legacy: `ParagraphClassifier`
Monolityczny, regex + hybrid (styl + tekst). Wciąż używany przez orchestrator jako fallback.

### Nowy: `LayeredParagraphClassifier` implementuje `IParagraphClassifier`

Orchestruje 3 warstwy w kolejności:

```
Warstwa 1 — StyleClassificationLayer
  - Analizuje styleId (styl Word akapitu)
  - Mapuje: ART→Article, UST→Paragraph, PKT→Point, LIT→Letter, TIR→Tiret
  - Prefiksy Z/*, ZZ*, Z_* → IsAmendmentContent=true
  - Confidence=85 (normalny styl) / 95 (nowelizacja)
  - Zwraca null jeśli brak rozpoznanego stylu

Warstwa 2 — SyntacticClassificationLayer
  - Analizuje tekst regex-ami (compiled, static readonly):
    Art\.?\s*\d+     → Article
    \d+[a-z]*\.\s+  → Paragraph
    \d+[a-z]*\)\s*  → Point
    [a-z]{1,5}\)\s* → Letter
    \u2013+\s+       → Tiret (znak –)
  - Wszystkie wzorce obsługują opcjonalny prefiks " (treść nowelizacji)
  - Confidence=90 przy dopasowaniu
  - Zwraca null jeśli brak dopasowania

Warstwa 3 — SemanticClassificationLayer (SĘDZIA)
  - Rozstrzyga konflikty między Style i Syntactic
  - REGUŁA: treść wygrywa nad stylem
  - Jeśli Style=ART ale brak sygnatury tekstowej → Kind=Unknown
    (artykuł ZAWSZE wymaga sygnatury tekstowej)
  - Confidence=95 (zgodne) / 70 (konflikt lub tylko styl)
  - Generuje DiagnosticMessage przy konflikcie

Warstwa 4 (opcjonalna) — AiClassificationLayer
  - Aktywowana przez AiTriggerMode: Disabled/Always/OnUnknown/OnConflict/OnLowConfidence
  - Deleguje do IExternalClassificationProvider
  - Kontekst: ArticleContext = lista tekstów akapitów bieżącego artykułu
```

**Wynik**: `ClassificationResult { Kind, StyleType, IsAmendmentContent, UsedFallback, StyleTextConflict, Confidence, LayerResults }`

---

## Krok 4 — Buildery encji (wzorzec kaskadowy)

**Pliki**: `WordParserCore/Services/Parsing/Builders/`

### Hierarchia builderów (od najwyższego do najniższego):
```
ArticleBuilder → ParagraphBuilder → PointBuilder → LetterBuilder → TiretBuilder
```

### Mechanizm kaskadowy — kluczowa zasada:
Builder **niższego** poziomu jest odpowiedzialny za zapewnienie istnienia encji nadrzędnych.
Wywołuje metody `EnsureFor*()` które tworzą **niejawne (implicit) encje** gdy brakuje rodziców.

### ArticleBuilder
```
Wejście: (Subchapter, text)
1. Tworzy Article, parent=Subchapter
2. Parsuje numer: ParseArticleNumber(text) → EntityNumber
3. Wyciąga "ogon" (tekst za "Art. X") jako pierwszy Paragraph
4. Tworzy Paragraph (niejawny jeśli brak tekstu w ogonie)
5. Dodaje Article do Subchapter.Articles
6. Zwraca (Article, Paragraph)
```

### ParagraphBuilder
```
Wejście: (Article, CurrentParagraph?, text)
- Jeśli CurrentParagraph jest niejawny i pusty:
  → aktualizuje go (realizuje jako jawny), zwraca go
- W przeciwnym razie:
  → tworzy nowy Paragraph, dodaje do Article.Paragraphs

EnsureForPoint(Article, CurrentParagraph?):
  → jeśli null: tworzy niejawny Paragraph (IsImplicit=true)
```

### PointBuilder
```
Wejście: (Paragraph, Article, text)
1. Tworzy Point, parent=Paragraph
2. Parsuje numer: ParsePointNumber(text) → EntityNumber
3. Dodaje do Paragraph.Points

EnsureForLetter(Paragraph?, Article, CurrentPoint?):
  → jeśli null: tworzy niejawny Point
```

### LetterBuilder
```
Wejście: (Point, Paragraph?, Article, text)
1. Tworzy Letter, parent=Point
2. Parsuje numer: ParseLetterNumber(text) → EntityNumber (symbol: a, b, aa...)
3. Dodaje do Point.Letters

EnsureForTiret(Point, Paragraph?, Article, CurrentLetter?):
  → jeśli null: tworzy niejawną Letter
```

### TiretBuilder
```
Wejście: (Letter, Point?, Paragraph?, Article, text, index)
1. Tworzy Tiret, parent=Letter
2. Numer = EntityNumber { NumericPart=index } (sekwencyjny, nie parsowany)
3. Dodaje do Letter.Tirets
```

### Przykład kaskady dla tiretu bez wyraźnych rodziców:
```
Dokument: Art. 5. → – (tiret)

1. ArticleBuilder → Article + Paragraph(niejawny)
2. TiretBuilder potrzebuje Letter (i Point):
   → pointBuilder.EnsureForLetter()  → Point(niejawny)
   → letterBuilder.EnsureForTiret()  → Letter(niejawna)
   → tiretBuilder.Build(letter, point, paragraph, article, text, index=1)

Wynik eId: art_5__tir_1
(niejawne jednostki pomijane w eId)
```

---

## Krok 5 — Model danych encji

**Pliki**: `ModelDto/`

### BaseEntity (baza wszystkich encji)
```csharp
Guid, UnitType, DisplayLabel, EIdPrefix
Number: EntityNumber { NumericPart, LexicalPart, Superscript, Value }
ContentText: string
Parent: BaseEntity?           // bezpośredni rodzic
Article: Article?             // skrót do artykułu (bez iteracji po hierarchii)
Paragraph: Paragraph?         // skrót do ustępu
Point: Point?                 // skrót do punktu
Letter: Letter?               // skrót do litery
ValidationMessages: List<ValidationMessage>
Id (virtual): string          // eId: "art_5__ust_2__pkt_3__lit_a__tir_1"
```

### Hierarchia kolekcji dzieci:
```
Article.Paragraphs: List<Paragraph>
Paragraph.Points:   List<Point>       + IsImplicit, TextSegments, CommonParts, Amendment
Point.Letters:      List<Letter>      + TextSegments, CommonParts, Amendment
Letter.Tirets:      List<Tiret>       + TextSegments, CommonParts, Amendment
Tiret.Tirets:       List<Tiret>       + TextSegments (podwójne tirety)
```

---

## Krok 6 — Finalizacja: `Finalize`

```
orchestrator.Finalize(context)
  → Jeśli dokument kończy się wewnątrz nowelizacji:
    AmendmentStateManager.Flush(context)
    → AmendmentFinalizer materializuje obiekt Amendment
    → Przypisuje go do encji-właściciela (Paragraph/Point/Letter/Tiret.Amendment)
```

---

## Pełny diagram sekwencji

```
Parse(filePath)
  ↓
  WordprocessingDocument.Open()
  ↓
  ParsingContext + ParserOrchestrator
  ↓
  foreach Paragraph in document:
    ↓
    [1] Sanitacja tekstu
    ↓
    [2] Classify → ClassificationResult (Kind, StyleType, IsAmendmentContent)
          └─ StyleLayer → SyntacticLayer → SemanticLayer [→ AiLayer]
    ↓
    [3] IsAmendmentContent? → AmendmentCollector.Collect() → NEXT
    ↓
    [4] WrapUp? → finalizuj jednostkę → NEXT
    ↓
    [5] StructureProcessor.Process(Kind)
          ├─ Article   → ArticleBuilder.Build()
          ├─ Paragraph → ParagraphBuilder.Build()
          ├─ Point     → [Ensure Paragraph] → PointBuilder.Build()
          ├─ Letter    → [Ensure Point+Paragraph] → LetterBuilder.Build()
          └─ Tiret     → [Ensure Letter+Point+Paragraph] → TiretBuilder.Build()
    ↓
    [6] DetectTrigger() → szuka "otrzymuje brzmienie:", "dodaje się", "uchyla się"
  ↓
  Finalize() → flush nowelizacji
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
| `ParagraphClassifier` | `Services/Parsing/ParagraphClassifier.cs` | Legacy klasyfikator |
| `LayeredParagraphClassifier` | `Services/Parsing/Classification/LayeredParagraphClassifier.cs` | Nowy klasyfikator warstwowy |
| `StyleClassificationLayer` | `Classification/Layers/StyleClassificationLayer.cs` | Warstwa stylu Word |
| `SyntacticClassificationLayer` | `Classification/Layers/SyntacticClassificationLayer.cs` | Warstwa regex |
| `SemanticClassificationLayer` | `Classification/Layers/SemanticClassificationLayer.cs` | Sędzia konfliktów |
| `AiClassificationLayer` | `Classification/Layers/AiClassificationLayer.cs` | Warstwa AI (opcjonalna) |
| `ClassificationResult` | `Services/Parsing/ClassificationResult.cs` | Wynik klasyfikacji |
| `StructureProcessor` | `Services/Parsing/StructureProcessor.cs` | Dispatcher na buildery |
| `ArticleBuilder` | `Services/Parsing/Builders/ArticleBuilder.cs` | Buduje Article + Paragraph |
| `ParagraphBuilder` | `Services/Parsing/Builders/ParagraphBuilder.cs` | Buduje Paragraph |
| `PointBuilder` | `Services/Parsing/Builders/PointBuilder.cs` | Buduje Point |
| `LetterBuilder` | `Services/Parsing/Builders/LetterBuilder.cs` | Buduje Letter |
| `TiretBuilder` | `Services/Parsing/Builders/TiretBuilder.cs` | Buduje Tiret |
| `AmendmentStateManager` | `Services/Parsing/AmendmentStateManager.cs` | Zarządza stanem nowelizacji |
| `AmendmentCollector` | `Services/Parsing/AmendmentCollector.cs` | Buforuje treść nowelizacji |
| `AmendmentFinalizer` | `Services/Parsing/AmendmentFinalizer.cs` | Materializuje obiekt Amendment |
| `BaseEntity` | `ModelDto/BaseEntity.cs` | Baza wszystkich encji domenowych |
| `EntityNumber` | `ModelDto/EntityNumber.cs` | Model numeru encji |
| `LegalDocument` | `ModelDto/LegalDocument.cs` | Korzeń dokumentu |
