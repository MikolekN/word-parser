# CLAUDE.md

Ten plik zawiera wskazówki dla Claude Code (claude.ai/code) dotyczące pracy z tym repozytorium.

## Opis projektu

**WordParser** to zestaw narzędzi na platformie .NET 10 do parsowania polskich dokumentów prawnych (aktów prawnych) z formatu DOCX do hierarchicznego modelu obiektowego, a następnie eksportu do XML/XLSX. Obsługuje hierarchiczne struktury dokumentów oraz nowelizacje.

## Polecenia

```bash
# Budowanie (tylko biblioteka + CLI — projekt API jest zawieszony, nie używaj build-all)
dotnet build WordParserCore/WordParserCore.csproj
dotnet build WordParser/WordParser.csproj
# Lub użyj zadania VS Code: "build-offline"

# Uruchomienie wszystkich testów
dotnet test WordParserCore.Tests/WordParserCore.Tests.csproj

# Uruchomienie pojedynczej klasy testowej
dotnet test WordParserCore.Tests/WordParserCore.Tests.csproj --filter "FullyQualifiedName~EIdTests"

# Build wydania + Docker (obraz WordParserWeb/Dockerfile, tag = skrócony hash commita gita + 'latest', push do rejestru; inkrementacja build.number jest ZAKOMENTOWANA i nieużywana)
./build.sh
```

## Architektura

### Graf zależności projektów

```
WordParser (CLI)          ──► WordParserCore ──► ModelDto
WordParserWeb (Web)       ──► WordParserCore ──► ModelDto
WordParserCore.Tests   ──► WordParserCore ──► ModelDto
WordParserApi (zawieszony) ──► WordParserCore ──► ModelDto
```

- `ModelDto` — czyste DTO, bez logiki biznesowej
- `WordParserCore` — cała logika silnika parsowania; zależy od `DocumentFormat.OpenXml`, `Serilog` i `PdfPig` 0.1.15 (adapter PDF: `WordParserCore/Ingest/Pdf/` — `PdfBlockReader`, `PdfLineExtractor`, `PageArtifactFilter`)
- `WordParser` — cienka nakładka CLI
- `WordParserWeb` — aktywna aplikacja webowa ASP.NET 10 (renderowanie HTML dokumentów)
- `WordParserApi` — zawieszony; nie rozwijaj tego projektu

### Hierarchia modelu dokumentu

**Jednostki redakcyjne** (struktura treści):
```
Article → Paragraph (Ustęp) → Point (Punkt) → Letter (Litera) → Tiret → Tiret (zagnieżdżony)
```
Uwaga: „DoubleTiret" nie jest osobną klasą — to zagnieżdżona lista `Tiret.Tirets: List<Tiret>` w tej samej klasie `Tiret`.

**Jednostki systematyzacyjne** (kontenery organizacyjne):
```
Part → Book → Title → Division → Chapter → Subchapter → [Articles]
```

**Jednostki niejawne (wirtualne)**: jeśli artykuł ma dokładnie jeden ustęp, ten ustęp jest oznaczony jako `IsImplicit = true`. Jednostki niejawne są pomijane w ścieżkach eId i w prezentacji. Przykładowy format eId: `art_5__ust_2__pkt_3__lit_a__tir_1` (podwójny podkreślnik między komponentami, pojedynczy podkreślnik między prefiksem a numerem).

### Potok parsowania (`WordParserCore/Services/Parsing/`)

Punkt wejścia: `LegalDocumentParser.Parse(...)` przyjmuje `Stream` lub ścieżkę pliku i zwraca kopertę `ParseResult` (`WordParserCore/ParseResult.cs`), nie bezpośrednio `LegalDocument` → wewnętrznie wywołuje `ParserOrchestrator`

Etapy potoku:
0. Detekcja formatu przez `SourceFormatDetector` (`WordParserCore/Ingest/SourceFormatDetector.cs`) i odczyt bloków przez `DocumentBlockReaderFactory`/`IDocumentBlockReader` (adaptery DOCX/PDF/TXT, katalog `WordParserCore/Ingest/`), następnie klasyfikacja CAŁEGO dokumentu (rodzaj aktu) przez `DocumentClassifier` (`WordParserCore/Services/Classify/Document/DocumentClassifier.cs`), sterowana `ParseOptions.Policy` (`WordParserCore/ParseOptions.cs`); dopiero potem budowa modelu przez `ParserOrchestrator`
1. `ParagraphClassifier` (w `Services/Classify/`) — klasyfikuje każdy akapit przy użyciu 3 warstw (patrz niżej)
2. `StructureProcessor` — buduje encje domenowe delegując do klas `*Builder` (`ArticleBuilder`, `ParagraphBuilder`, `PointBuilder`, `LetterBuilder`, `TiretBuilder`, `AmendmentBuilder`, `SystematizingUnitBuilder` — buduje jednostki systematyzacyjne Part/Book/Title/Division/Chapter/Subchapter) — wzorzec kaskadowy; buildery niższego poziomu zapewniają istnienie encji nadrzędnych
3. `AmendmentStateManager` / `AmendmentCollector` / `AmendmentFinalizer` — wykrywają wyzwalacze nowelizacji, buforują treść, finalizują obiekty `Amendment`
4. `NumberingHint` (w `Services/Classify/`) + `ParagraphClassifier` — walidują ciągłość numeracji podczas klasyfikacji; kara `NumberingBreakPenalty` obniża Confidence
5. Stan przechowywany jest w `ParsingContext` przez cały czas parsowania; `ValidationReporter` zbiera komunikaty walidacji

### Klasyfikacja warstwowa (kluczowa zasada)

Klasyfikacja akapitów musi być odporna na błędy. Zawsze stosuj wszystkie trzy warstwy; przy konflikcie preferuj treść/układ nad stylem:

1. **Strukturalna** — style akapitów Word (np. `ART`, `UST`, `Z/*` dla nowelizacji) jako silna wskazówka, nie pewnik. Zawsze sprawdzaj null dla `ParagraphProperties` przed dostępem do stylów.
2. **Syntaktyczna** — wzorce regex dla `Art.`, `§`, `ust.`, `pkt`, `lit.`, znaczników tiretu. Oceniaj treść niezależnie od stylu.
3. **Semantyczna** — spójność hierarchii. Wstawiaj jednostki niejawne, gdy brakuje poziomu.

Reguła decyzyjna: twardy wymóg dwóch zgodnych sygnałów dotyczy głównie rozpoznania Artykułu — sam styl bez sygnatury tekstowej daje `Unknown` (`ParagraphClassifier.cs:291-296`); pozostałe jednostki dopuszczają pojedynczy sygnał kosztem obniżonej Confidence (kary `StyleAbsentPenalty`/`SyntaxAbsentPenalty`). Gdy styl konfliktuje z treścią, preferuj treść (`DefaultConflictResolver`). Rejestruj decyzje naprawcze przez Serilog.

### System nowelizacji

- Słowa kluczowe wyzwalające: „otrzymuje brzmienie:", „dodaje się", „uchyla się"
- Style akapitów nowelizacji używają prefiksów `Z/*`, `ZZ*`, `Z_*` (dekodowane przez `AmendmentStyleDecoder`)
- Typy (`ModelDto/AmendmentOperationType.cs`): Modification, Insertion, Repeal — dotyczą udanej klasyfikacji; czwarta wartość Error oznacza błąd przetwarzania
- Wieloetapowy cykl życia: Wykrycie → Zbieranie (`AmendmentCollector`) → Finalizacja (`AmendmentFinalizer`)

## Kluczowe pliki

| Plik | Rola |
|---|---|
| `WordParserCore/LegalDocumentParser.cs` | Publiczny punkt wejścia |
| `WordParserCore/Ingest/SourceFormatDetector.cs` + `DocumentBlockReaderFactory` | Detekcja formatu źródłowego i odczyt bloków (DOCX/PDF/TXT) |
| `WordParserCore/Services/Classify/Document/DocumentClassifier.cs` | Klasyfikacja rodzaju aktu (całego dokumentu) |
| `WordParserCore/ParseResult.cs` i `WordParserCore/ParseOptions.cs` | Koperta wyniku parsowania + polityka parsowania |
| `WordParserCore/Services/Parsing/ParserOrchestrator.cs` | Główny potok |
| `WordParserCore/Services/Parsing/StructureProcessor.cs` | Buduje encje domenowe (deleguje do Builders) |
| `WordParserCore/Services/Parsing/ParsingContext.cs` | Mutowalny stan parsera |
| `WordParserCore/Services/Parsing/Builders/` | Buildery encji (wzorzec kaskadowy) |
| `WordParserCore/Services/Parsing/Builders/SystematizingUnitBuilder.cs` | Buduje jednostki systematyzacyjne (Part/Book/Title/Division/Chapter/Subchapter) |
| `WordParserCore/Services/Classify/ParagraphClassifier.cs` | Logika klasyfikacji (3-warstwowa) |
| `WordParserCore/Services/Classify/NumberingHint.cs` | Walidacja ciągłości numeracji |
| `WordParserCore/Helpers/ParagraphExtensions.cs` | Bezpieczne helpery OpenXml (używaj rozszerzenia `.StyleId()`) |
| `WordParserCore/Helpers/AmendmentStyleDecoder.cs` | Dekoduje style nowelizacji (`Z/*`, `ZZ*`, `Z_*`) |
| `ModelDto/BaseEntity.cs` | Abstrakcyjna baza dla wszystkich encji domenowych |
| `ModelDto/EntityNumber.cs` | Model numeru encji (NumericPart, LexicalPart, Superscript) |
| `WordParserCore.Tests/` | Testy xUnit; artefakty testowe w podkatalogu `Artifacts/` |

## Konwencje

- **Język**: kod (zmienne, metody, klasy) po **angielsku**; komentarze, teksty UI i komunikaty logów po **polsku**. Jest to celowe.
- **Bezpieczeństwo null w OpenXml**: zawsze sprawdzaj null dla `paragraph.ParagraphProperties` przed dostępem do stylów. Używaj metod rozszerzających z `ParagraphExtensions`: `.StyleId()` (bez argumentu) zwraca `string?` z identyfikatorem stylu, a `.StyleId("PREFIKS")` zwraca `bool?` informujące, czy styl zaczyna się od podanego prefiksu.
- **Wzorce regex**: deklaruj jako `private static readonly Regex`, prekompilowane. Wzorce muszą obsługiwać opcjonalny prefiks cudzysłowu dla treści nowelizacji.
- **Logowanie**: używaj Serilog (konfigurowanego przez `LoggerConfig.ConfigureLogger()`); minimalny poziom Warning. Logi trafiają do `logs/log.txt` i na konsolę.
- **Komunikaty commitów**: proponuj nazwy commitów po **angielsku** po każdej zmianie (zarówno małej jak i architektonicznej).
- **Dokumentacja i plany**: wersjonowany katalog `docs/` trzyma wyłącznie dokumenty opisujące **stan i uzasadnienia** — `architecture.md` (jak działa dziś), `adr/` (dlaczego tak — patrz niżej), `backlog.md` (znane luki i prace świadomie odroczone), `ztp-struktura-aktow.md` (kondensat ZTP). Plany przebudowy, analizy i raporty robocze powstają w `docs/internal/` (gitignorowane), nigdy w wersjonowanym `docs/`. Po wykonaniu planu wygaś go trójpodziałem: „dlaczego" i zasady twarde → ADR, niezrealizowane/odroczone → `backlog.md`, etapowanie i definicje ukończenia → usuń (historia jest w `git log`). Każde twierdzenie przepisywane z planu **zweryfikuj w kodzie** — plany opisują stan z dnia ich napisania i po drodze dryfują.
- **ADR (`docs/adr/`)**: jeden plik = jedna decyzja, format wg `docs/adr/0000-template.md`, wpis w indeksie `docs/adr/README.md`. ADR piszemy tylko dla decyzji, która **miała odrzuconą alternatywę**, jest **trudno odwracalna** albo jest **nieoczywista** (kod wygląda na przekomplikowany, dopóki nie znasz kontekstu); nie dla wyborów bez alternatywy ani dla konwencji kodu — te należą do tego pliku. Zaakceptowanego ADR **nie edytuje się**: zmiana zdania to nowy ADR ze statusem `Supersedes ADR-NNNN`, a stary dostaje `Superseded by ADR-NNNN`. `architecture.md` nie powtarza uzasadnień — trzyma jednozdaniowy skrót i link.
- **Walidacja**: dołączaj obiekty `ValidationMessage` (Info/Warning/Error/Critical) do encji DTO dla akapitów o niepewnej lub naprawionej klasyfikacji.

## Testy

- Projekt testowy: `WordParserCore.Tests`
- Framework: xUnit 2.9.3
- Dokumenty referencyjne DOCX leżą w lokalnym `DocRepo/` (niewersjonowany); `WordParserCore.Tests/Artifacts/` zawiera golden snapshoty oczekiwanych wyników (np. `doc001.snapshot.xml`) — oba katalogi niewersjonowane
- Klasy testowe: `EIdTests`, `AmendmentFinalizerTests`, `AmendmentBuilderTests`, `AmendmentCollectorTests`, `AmendmentStyleDecoderTests`, `ParagraphClassifierTests`, `NumberingHintTests`, `ParserOrchestratorAmendmentTests`, `ParserOrchestratorCommonPartTests`, `LegalReferenceServiceTests`, `JournalReferenceServiceTests`, `ContentStrippingTests`, `GetFullTextTests`, `SentenceSplittingTests`, `IntroCommonPartTests`, `ReferenceActTests`, `LegalDocumentSnapshotTests`, `DocumentClassifierTests`, `SourceFormatDetectorTests`, `DocxBlockReaderTests`, `PdfBlockReaderTests`, `PlainTextBlockReaderTests`, `SystematizingUnitTests`, `TiretDepthTests`, `AmendmentCommandParserTests`, `AmendmentTextualBoundaryTests`, `DocumentMetadataCollectorTests`, `EntityNumberServiceTests`, `LetterDesignationTests`, `ParagraphClassifierStylelessTests`, `ParseFacadeTests`, `ParserEquivalenceTests`

  Uwaga: `ParsingBuildersTests` to NAZWA PLIKU (`ParsingBuildersTests.cs`), nie klasa testowa — plik zawiera klasy `ArticleBuilderTests`, `ParagraphBuilderTests`, `PointBuilderTests`, `LetterBuilderTests`, `TiretBuilderTests` (filtr `--filter "FullyQualifiedName~ParsingBuildersTests"` zwraca 0 testów; filtruj po nazwie klasy, np. `ArticleBuilderTests`)
- Uruchomienie testów konkretnej klasy: `--filter "FullyQualifiedName~NazwaKlasy"`
