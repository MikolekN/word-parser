# Algorytm klasyfikacji paragrafów

Dokument opisuje krok po kroku, jak każdy akapit dokumentu DOCX jest klasyfikowany
do jednego z typów jednostki redakcyjnej. Klasyfikacja jest centralnym etapem potoku
parsowania — jej wynik decyduje o tym, do jakiej encji domenowej zostanie zabudowany
dany akapit.

---

## 3. Typy danych

### ParagraphKind — możliwe wyniki klasyfikacji

```csharp
public enum ParagraphKind
{
    Article,    // Art. 5 — artykuł
    Paragraph,  // 2. — ustęp
    Point,      // 3) — punkt
    Letter,     // a) — litera
    Tiret,      // – — tiret (en-dash)
    Unknown     // brak rozpoznania
}
```

### ClassificationResult — wynik końcowy

| Pole | Typ | Znaczenie |
|------|-----|-----------|
| `Kind` | `ParagraphKind` | Rozpoznany typ (Unknown gdy brak) |
| `StyleType` | `string?` | Zdekodowany typ stylu (`"ART"`, `"UST"`, `"PKT"`, `"LIT"`, `"TIR"`, `"AMENDMENT"`, `null`) |
| `IsAmendmentContent` | `bool` | Akapit to treść nowelizacji |
| `UsedFallback` | `bool` | Klasyfikacja oparta na tekście (nie stylu) |
| `StyleTextConflict` | `bool` | Styl i tekst wskazują na różne typy |
| `Confidence` | `int?` | Pewność 0–100 (tylko tryb warstwowy) |
| `LayerResults` | `IReadOnlyList<LayerClassificationResult>?` | Wyniki poszczególnych warstw (tylko tryb warstwowy) |

### LayerClassificationResult — wynik pojedynczej warstwy

| Pole | Typ | Znaczenie |
|------|-----|-----------|
| `LayerName` | `string` | `"Style"`, `"Syntactic"`, `"Semantic"` lub `"AI"` |
| `Kind` | `ParagraphKind?` | Werdykt warstwy (`null` = brak werdyktu) |
| `StyleType` | `string?` | Typ stylu (tylko warstwa Style) |
| `IsAmendmentContent` | `bool` | Flaga nowelizacji |
| `Confidence` | `int` | Pewność warstwy 0–100 |
| `DiagnosticMessage` | `string?` | Opis konfliktu lub decyzji naprawczej |

---

## 4. Algorytm warstwowy — krok po kroku

### Schemat ogólny

```
ClassificationInput { Text, StyleId, ArticleContext }
        │
        ▼
┌─────────────────────────┐
│   Warstwa 1: Styl       │──► LayerClassificationResult | null
└─────────────────────────┘
        │  PrecedingLayerResults = [wynik warstwy 1]
        ▼
┌─────────────────────────┐
│   Warstwa 2: Syntaktyka │──► LayerClassificationResult | null
└─────────────────────────┘
        │  PrecedingLayerResults = [wynik 1, wynik 2]
        ▼
┌─────────────────────────┐
│   Warstwa 3: Semantyka  │──► LayerClassificationResult | null
└─────────────────────────┘
        │
        ▼
  BuildFinalResult(layerResults, strategy)
        │
        ├─ ShouldInvokeAi()? ──TAK──►┌─────────────────────┐
        │                             │  Warstwa 4: AI      │
        │                             └────────┬────────────┘
        │                                      │
        │                             BuildFinalResult() [ponownie]
        │
        ▼
ClassificationResult (wynik końcowy)
```

Każda warstwa otrzymuje wejście wzbogacone o wyniki wszystkich poprzednich warstw
(`PrecedingLayerResults`). Warstwa może zwrócić `null` — oznacza to brak werdyktu,
a nie błąd.

---

### Warstwa 1: Styl (`StyleClassificationLayer`)

**Cel:** Dekodowanie identyfikatora stylu Word na typ jednostki.

**Logika `GetStyleType(styleId)`:**

```
1. styleId null lub pusty           → null (brak stylu)
2. StyleLibraryMapper.TryGetStyleInfo() → IsAmendment==true → "AMENDMENT"
3. Prefiksy nowelizacji:
     Z/…   (zmiana artykułem/punktem)
     ZZ…   (zmiana zmiany)
     Z_…   (zmiana literą, tiretem, podwójnym tiretem)
                                    → "AMENDMENT"
4. Prefix ART                       → "ART"
5. Prefix UST                       → "UST"
6. Prefix PKT                       → "PKT"
7. Prefix LIT                       → "LIT"
8. Prefix TIR                       → "TIR"
9. Brak dopasowania                 → null
```

**Mapowanie na `ParagraphKind` i pewność:**

| styleType | Kind | Confidence |
|-----------|------|-----------|
| `"AMENDMENT"` | — | 95 (flaga `IsAmendmentContent=true`) |
| `"ART"` | `Article` | 85 |
| `"UST"` | `Paragraph` | 85 |
| `"PKT"` | `Point` | 85 |
| `"LIT"` | `Letter` | 85 |
| `"TIR"` | `Tiret` | 85 |
| `null` / nieznany | — | zwraca `null` |

---

### Warstwa 2: Syntaktyka (`SyntacticClassificationLayer`)

**Cel:** Rozpoznanie typu jednostki na podstawie wzorca tekstu, niezależnie od stylu.

**Wzorce regex** (skompilowane statycznie w `ParagraphClassifier`):

| Wzorzec | Wyrażenie | Przykłady |
|---------|-----------|-----------|
| `ArticlePattern` | `^(?:["„""]\s*)?Art\.?\s*\d+` | `Art. 5`, `Art 12`, `„Art. 3` |
| `ParagraphPattern` | `^(?:["„""]\s*)?\d+[a-zA-Z]*\.\s+` | `1. Tekst`, `2a. Tekst` |
| `PointPattern` | `^(?:["„""]\s*)?\d+[a-zA-Z]*\)\s*` | `1)`, `2a)`, `„3)` |
| `LetterPattern` | `^(?:["„""]\s*)?[a-zA-Z]{1,5}\)\s*` | `a)`, `ab)`, `„b)` |
| `TiretPattern` | `^\u2013+\s+` | `– treść`, `–– treść` |

Prefiks `(?:["„""]\s*)?` obsługuje polskie cudzysłowy otwierające (`"`, `„`, `"`, `"`)
na początku akapitów nowelizacyjnych, które cytują jednostkę z ustawy matki.

**Wynik:**
- Dopasowanie → `Kind=<typ>`, `Confidence=90`
- Brak dopasowania → `null` (warstwa nie ma werdyktu)

---

### Warstwa 3: Semantyka (`SemanticClassificationLayer`)

**Cel:** Rozstrzyganie konfliktów między warstwą Styl a Syntaktyka oraz aplikacja
reguł domenowych specyficznych dla polskich aktów prawnych.

**Algorytm (kolejność priorytetów):**

```
Krok 1: Warunek wstępny
  Brak poprzednich wyników → zwróć null

Krok 2: Priorytet nowelizacji
  Warstwa Style zgłosiła IsAmendmentContent?
  TAK → { IsAmendmentContent=true, Confidence=95 }

Krok 3: Treść wygrywa nad stylem (Content > Style)
  Warstwa Syntactic ma werdykt (Kind != null)?
  TAK →
    styleKind == syntacticKind → Confidence=95  (zgodność)
    styleKind != syntacticKind → Confidence=70  (konflikt; DiagnosticMessage="Konflikt: styl=X, tekst=Y; przyjęto tekst")
    Wynik: Kind = syntacticKind

Krok 4: Tylko styl (brak sygnatury tekstowej)
  Warstwa Style ma werdykt ORAZ Kind != Article?
  TAK → { Kind=styleKind, Confidence=70 }
  ⚠️  UWAGA: Kind=Article wymaga sygnatury tekstowej.
             Styl ART sam w sobie nie wystarczy → null (Unknown)

Krok 5: Brak werdyktu → zwróć null
```

**Kluczowa zasada:** Artykuł (Kind=Article) musi być potwierdzony sygnaturą tekstową
`Art. X`. Sam styl `ART` jest niewystarczający. Dla pozostałych typów (Paragraph, Point,
Letter, Tiret) styl może służyć jako fallback gdy tekst nie zawiera wzorca.

---

### Budowanie wyniku końcowego (`BuildFinalResult`)

Po zebraniu wyników ze wszystkich trzech warstw orchestrator klasyfikatora wywołuje
`BuildFinalResult` z wybraną strategią rozstrzygania konfliktu.

#### Krok 1: Sprawdzenie nowelizacji (najwyższy priorytet)

```
Którakolwiek warstwa zgłosiła IsAmendmentContent?
  TAK → { IsAmendmentContent=true, StyleType=..., Confidence=95 }
        (kończy algorytm, nie sprawdza dalej)
```

#### Krok 2: Wybór finalnej warstwy — strategia `ContentOverStyle` (domyślna)

```
Czy warstwa Semantic ma werdykt (Kind != null)?
  TAK → użyj jej wyniku
  NIE → użyj ostatniej warstwy innej niż "Style" z werdyktem
        (Warstwa Style jest sygnałem wspierającym, nigdy nie decyduje samodzielnie)
  ŻADNA → Kind=Unknown, zachowaj StyleType ze Style
```

#### Krok 2 (alternatywna): Strategia `WeightedMajority`

```
Dla każdego wyniku z Kind:
    score[Kind] += Confidence × Weight

Wagi domyślne:
    Style     = 2
    Syntactic = 3
    Semantic  = 2

Zwycięzca: Kind z najwyższą sumą ważoną
```

#### Krok 3: Obliczenie flag `UsedFallback` i `StyleTextConflict`

```
StyleTextConflict = Style ma Kind ORAZ Syntactic ma Kind ORAZ Style.Kind != Syntactic.Kind

UsedFallback:
  Dla Article: Syntactic miał werdykt AND (Style nie miał Kind=Article)
  Dla pozostałych: Syntactic brał udział (hasSyntacticKind=true)
```

#### Krok 4: Budowanie `ClassificationResult`

```
{ Kind, StyleType, UsedFallback, StyleTextConflict, Confidence }
```

---

### Warstwa 4: AI (`AiClassificationLayer`) — opcjonalna

Warstwa uruchamiana warunkowo po `BuildFinalResult`, jeśli skonfigurowano
`IExternalClassificationProvider`.

**Warunki aktywacji (`AiTriggerMode`):**

| Tryb | Warunek |
|------|---------|
| `Disabled` | Nigdy (domyślny) |
| `Always` | Zawsze |
| `OnUnknown` | `Kind == Unknown` po 3 warstwach |
| `OnConflict` | `Style.Kind != Syntactic.Kind` |
| `OnLowConfidence` | `Confidence < AiConfidenceThreshold` (domyślnie 60) |

**Wejście i wyjście:**

```csharp
// Wejście przekazywane do dostawcy AI:
ExternalClassificationRequest
{
    Text                  // tekst akapitu
    StyleId               // identyfikator stylu
    ArticleContext        // teksty bieżącego artykułu (z ParsingContext)
    PrecedingLayerResults // wyniki warstw 1-3
}

// Oczekiwana odpowiedź:
ExternalClassificationResponse
{
    Kind        // ParagraphKind
    Confidence  // 0–100
    Reasoning   // opcjonalne wyjaśnienie (→ DiagnosticMessage)
}
```

Jeśli AI zwróci wynik, `BuildFinalResult` jest wywoływane **ponownie** z rozszerzonym
zestawem `layerResults` (dołączony wynik AI). Wynik AI traktowany jest jak czwarta
warstwa w wybranej strategii.

---

## 5. Pełny diagram przepływu `ProcessParagraph`

```
Paragraph (OpenXml)
        │
        ├─ GetFullText() ──────────────────────────────────► rawText
        ├─ Sanitize() ─────────────────────────────────────► text
        └─ StyleId() ──────────────────────────────────────► styleId
        │
        ▼
ClassificationInput { text, styleId, CurrentArticleTexts }
        │
        ▼
LayeredParagraphClassifier.Classify()
   ├─ Style:     Kind? Confidence?
   ├─ Syntactic: Kind? Confidence?
   ├─ Semantic:  Kind? Confidence?  (rozstrzyga konflikt)
   └─ [AI]:      Kind? Confidence?  (jeśli warunek AiTriggerMode)
        │
        ▼
ClassificationResult { Kind, StyleType, Confidence, ... }
        │
        ├─► UpdateArticleContext()
        │      Kind==Article → context.CurrentArticleTexts.Clear()
        │      !IsAmendmentContent → context.CurrentArticleTexts.Add(text)
        │
        ├─► HandleAmendmentFlow()
        │      AmendmentStateManager.UpdateState()
        │      wasInsideAmendment && !InsideAmendment → Flush()
        │      IsAmendmentContent || InsideAmendment  → Collect() + return
        │
        ├─► StructureProcessor.TryHandleWrapUp()   (podpisanie encji)
        │
        └─► StructureProcessor.Process()           (budowanie encji)
               └─► AmendmentManager.DetectTrigger() (słowa kluczowe)
```

---

## 6. Tabela przypadków granicznych

| Styl Word | Tekst | Wynik Kind | Confidence | Flagi | Uwagi |
|-----------|-------|-----------|-----------|-------|-------|
| `ART` | `Art. 5 ...` | `Article` | 95 | — | Zgodność styl+tekst |
| `ART` | `1. Treść` | `Article` | 70 | `StyleTextConflict=true` | Tekst wygrywa |
| `ART` | `(brak wzorca)` | `Unknown` | — | — | Artykuł wymaga sygnatury |
| `null` | `Art. 5 ...` | `Article` | 90 | `UsedFallback=true` | Brak stylu, tekst decyduje |
| `UST` | `1. Treść` | `Paragraph` | 95 | — | Zgodność |
| `UST` | `(brak wzorca)` | `Paragraph` | 70 | — | Styl jako fallback |
| `Z/ART` | dowolny | — | 95 | `IsAmendmentContent=true` | Nowelizacja, priorytet |
| `null` | `(brak wzorca)` | `Unknown` | — | — | Brak sygnałów |
| `PKT` | `Art. 5 ...` | `Article` | 70 | `StyleTextConflict=true` | Tekst wygrywa |

---

## 7. Konfiguracja klasyfikatora

`ClassifierConfiguration` umożliwia dostosowanie zachowania `LayeredParagraphClassifier`:

| Parametr | Typ | Wartość domyślna | Opis |
|----------|-----|-----------------|------|
| `Layers` | `IReadOnlyList<LayerConfiguration>` | Style(2), Syntactic(3), Semantic(2) | Warstwy i ich wagi |
| `AiTriggerMode` | `AiTriggerMode` | `Disabled` | Kiedy uruchamiać AI |
| `AiConfidenceThreshold` | `int` | `60` | Próg pewności dla `OnLowConfidence` |
| `ConflictResolution` | `ConflictResolutionStrategy` | `ContentOverStyle` | Strategia rozstrzygania konfliktu |

Konfiguracja przekazywana przez konstruktor:

```csharp
var config = new ClassifierConfiguration
{
    AiTriggerMode          = AiTriggerMode.OnLowConfidence,
    AiConfidenceThreshold  = 70,
    ConflictResolution     = ConflictResolutionStrategy.WeightedMajority,
};
var classifier = new LayeredParagraphClassifier(config, myAiProvider);
var orchestrator = new ParserOrchestrator(classifier);
```

---

## 8. Kluczowe pliki

| Plik | Rola |
|------|------|
| `WordParserCore/Services/Parsing/ParserOrchestrator.cs` | Wywołanie klasyfikatora, integracja z potokiem |
| `WordParserCore/Services/Parsing/Classification/LayeredParagraphClassifier.cs` | Orchestrator warstwowy, `BuildFinalResult` |
| `WordParserCore/Services/Parsing/Classification/Layers/StyleClassificationLayer.cs` | Warstwa 1: styl Word |
| `WordParserCore/Services/Parsing/Classification/Layers/SyntacticClassificationLayer.cs` | Warstwa 2: wzorce regex |
| `WordParserCore/Services/Parsing/Classification/Layers/SemanticClassificationLayer.cs` | Warstwa 3: rozstrzyganie konfliktów |
| `WordParserCore/Services/Parsing/Classification/Layers/AiClassificationLayer.cs` | Warstwa 4: integracja AI |
| `WordParserCore/Services/Parsing/ParagraphClassifier.cs` | Wzorce regex, `GetStyleType`, tryb legacy |
| `WordParserCore/Services/Parsing/ParagraphKind.cs` | Enum typów jednostek |
| `WordParserCore/Services/Parsing/ClassificationResult.cs` | Model wyniku klasyfikacji |
| `WordParserCore/Services/Parsing/Classification/ClassificationInput.cs` | Model wejścia do klasyfikatora |
| `WordParserCore/Services/Parsing/Classification/LayerClassificationResult.cs` | Wynik pojedynczej warstwy |
| `WordParserCore/Services/Parsing/Classification/ClassifierConfiguration.cs` | Konfiguracja klasyfikatora |
| `WordParserCore/Services/Parsing/Classification/IExternalClassificationProvider.cs` | Interfejs dostawcy AI |
| `WordParserCore/Helpers/ParagraphExtensions.cs` | Helpery OpenXml (`GetFullText`, `StyleId`) |
