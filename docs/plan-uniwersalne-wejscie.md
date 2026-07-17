# Plan przebudowy: uniwersalne wejście parsera (DOCX bez szablonu, PDF, TXT) + klasyfikacja aktu wg ZTP

> **Status: W REALIZACJI** — Etapy 0–3 ukończone. Etap 0 (2026-07-16): siatka bezpieczeństwa — snapshot doc001, testy charakteryzujące gałąź bezstylową, CLI `--dump`; plus naprawa zastanej czerwonej bazy (4 testy en-dash). Etap 1 (2026-07-16): reprezentacja pośrednia `Ingest/` + przepięcie DOCX. Etap 2 (2026-07-17): ujednolicenie kanału indeksu górnego `^`→`[x]` — snapshot doc001 bez diffu (dokument referencyjny nie zawiera jednostek z indeksem). Etap 3 (2026-07-17): `DocxBlockReader.ExtractLayout` wypełnia `BlockLayoutInfo` (wcięcia twips, wyrównanie, pogrubienie/kursywa/rozmiar jako dominanta ważona znakami) — addytywne, nikt jeszcze nie czyta layoutu, snapshot bez diffu; `DocxBlockReaderTests`. Plan opracowany 2026-07-16, skorygowany po adwersaryjnej weryfikacji kondensatu ZTP.
> Podstawa merytoryczna: „Zasady techniki prawodawczej" — tekst jednolity Dz. U. z 2026 r. poz. 300 ([docs/ZTP-2026-300.md](ZTP-2026-300.md)); kondensat reguł dla parsera: [docs/ztp-struktura-aktow.md](ztp-struktura-aktow.md).

## Kontekst

Parser zakłada dziś na wejściu DOCX napisany na specjalistycznym szablonie (style ART, UST, Z/* itd.). Cel: przyjmować dowolny dokument tekstowy — DOCX bez szablonu, PDF (warstwa tekstowa), TXT — oraz klasyfikować na podstawie ZTP, czy dokument jest normatywnym aktem prawnym i jakiego rodzaju.

Analiza wykazała, że przebudowa jest **niskiego ryzyka architektonicznego**: granica OpenXml kończy się w `ParserOrchestrator.ProcessParagraph` (potok konsumuje `(string text, string? styleId)`), a ModelDto jest w 100% wolne od OpenXml. Główna praca to: (1) reprezentacja pośrednia + adaptery wejścia, (2) klasyfikator dokumentu wg ZTP (nie istnieje — od zera), (3) domknięcie luk klasyfikacji bezstylowej (jednostki systematyzacyjne, WrapUp, głębokość tiretu, nowelizacje bez stylów Z/*).

## Rozstrzygnięcia (potwierdzone 2026-07-16)

1. **PDF**: wyłącznie warstwa tekstowa; skany (OCR) poza zakresem — odrzucane z czytelnym komunikatem.
2. **Nie-akt**: parser zwraca raport klasyfikacji (rodzaj/nie-akt, pewność, sygnały); decyzja „czy parsować" należy do wywołującego.
3. **JEDEN ujednolicony potok**: wspólna reprezentacja pośrednia; styl Word = jeden z sygnałów. Pełna regresja na istniejących testach.
4. **Rodzaje aktów**: ustawa (też zmieniająca), rozporządzenie, obwieszczenie/tekst jednolity, uchwała/zarządzenie, akty prawa miejscowego.

## Wyciąg reguł ZTP kluczowych dla parsera tekstowego

- **Oznaczenia jednostek (§ 54–59)**: `Art. N.` (arab. + kropka; w kodeksach ustępy jako `§`); ustęp `N.` bez nawiasu; punkt `N)` kończony `;`; litera `x)` małe łacińskie BEZ polskich znaków, kończona `,`, po wyczerpaniu `za)`, `zb)`…; tiret = myślnik od nowego wiersza kończony `,`. Jednostki dodawane: `5a` (§ 89); indeks górny w notacji `x[y]` (§ 89 ust. 6) — zbieżne z kanałem `[x]` z `GetFullText`.
- **Układ (§ 58)**: każda jednostka od nowego wiersza; art./ust. od akapitu (wcięcie 1. wiersza); pkt/lit./tiret na wysokości wprowadzenia do wyliczenia → wcięcie niesie hierarchię.
- **Jednostki systematyzacyjne (§ 60–62)**: CZĘŚĆ/KSIĘGA/TYTUŁ/DZIAŁ + cyfra RZYMSKA; Rozdział/Oddział + ARABSKA; tytuł jednostki od nowego wiersza wielką literą (wzorzec dwuwierszowy).
- **Tytuły aktów**: ustawa (§ 16–19: „USTAWA" / „z dnia D miesiąca RRRR r." / „o …"|„Kodeks/Prawo/Ordynacja"|„Przepisy wprowadzające"); zmieniająca (§ 96: „o zmianie ustawy…", „…oraz niektórych innych ustaw"); rozporządzenie (§ 120: „ROZPORZĄDZENIE" + organ WIELKIMI + „w sprawie…", tekst od „Na podstawie art. … zarządza się, co następuje:"); uchwała/zarządzenie (§ 138a–139: „Uchwała nr…", od podstawy prawnej); obwieszczenie/TJ (§ 102–106: „w sprawie ogłoszenia jednolitego tekstu…", markery „(uchylony)"/„(utracił moc)"/„(uznany za nieważny)", odnośniki `N)` w indeksie górnym § 163).
- **Nowelizacje (§ 82–97)**: „W ustawie … wprowadza się następujące zmiany:"; „otrzymuje brzmienie:", „po art. X dodaje się art. Xa w brzmieniu:", „uchyla się", „wyrazy „…" zastępuje się wyrazami „…""; treść nowego brzmienia W CUDZYSŁOWIE „…"; struktura zmian = zagnieżdżenie pkt→lit→tiret→podw. tiret (§ 94) → instrument odtwarzalny z poziomu zagnieżdżenia. Dział II stosuje się ODPOWIEDNIO do rozporządzeń (§ 132), uchwał/zarządzeń (§ 141) i aktów prawa miejscowego (§ 143) — komendy występują też w wariancie „W rozporządzeniu/uchwale/zarządzeniu …" oraz z `§` jako jednostką bazową („po § X dodaje się § Xa w brzmieniu:").
- **Wejście w życie (§ 45)**: zamknięty katalog „… wchodzi w życie …". **Publikatory (§ 162)**: Dz. U., M.P., Dz. Urz. UE/WE/Min./Woj.

## Inwentaryzacja sprzężenia kodu z DOCX/szablonem (stan na 2026-07-16, zweryfikowana)

- **Szew**: `ProcessParagraph` ([ParserOrchestrator.cs:34](../WordParserCore/Services/Parsing/ParserOrchestrator.cs#L34)) → `paragraph.GetFullText()` + `paragraph.StyleId()`, dalej stringi. Pliki z OpenXml: `LegalDocumentParser.cs`, `ParserOrchestrator.cs` (tylko sygnatura), `Helpers/ParagraphExtensions.cs` (ekstrakcja: superscript→`[x]`, SymbolChar F02D→`–`, FootnoteReference, Tab), `Helpers/SpreadsheetHelper.cs` (martwy), `WordParser/Program.cs`, 3 pliki testów.
- **Bez stylu działa**: ścieżka regex-only (Confidence 90) dla Art/Ust/Pkt/Lit/Tiret. Kary: StyleAbsent −10, SyntaxAbsent −15, Conflict −25 (`DefaultConflictResolver` = mock, treść zawsze wygrywa), NumberingBreak −10.
- **Bez stylu NIE działa**: treść nowelizacji (`IsAmendmentContent` tylko ze stylu Z/*), WrapUp (`IsWrapUpByText` niewpięte w gałąź bezstylową — „– tekst" błędnie→Tiret), jednostki systematyzacyjne (zero regexów, brak w `ParagraphKind`), głębokość tiretu (tylko styleId 2TIR/3TIR), metadane aktu (TYTUŁ_AKTU/DATA_AKTU/OZN_RODZ_AKTU→Unknown). Klasyfikator nie czyta ŻADNYCH sygnałów układu (wcięcia/wyrównanie/pogrubienie).
- **Nowelizacje**: triggery tekstowe (`dodaje się`, `uchyla się`, `otrzymuje brzmienie:` — `AmendmentFinalizer.cs:38-48`) działają bez stylu (wejście/trwanie/wyjście — potwierdzone testami); semantyka szczegółowa (instrument/target/ParentContext/CommonPartOf) TYLKO z mapy `StyleLibraryMapper.AmendmentStyleInfoMap` (~240 wpisów). Cudzysłowy nieużywane do delimitacji.
- **Detekcja typu dokumentu NIE ISTNIEJE**: `LegalActType` nigdy nieustawiany (`LegalDocument.Type` domyślnie Statute); nie-akt → pusty `LegalDocument` bez sygnału.
- **Bug**: kanał superscriptu rozspójniony — `GetFullText` emituje `[x]`, `EntityNumberService.Parse` rozumie tylko `^` → `EntityNumber.Superscript` nigdy się nie wypełnia.
- **Siatka testowa słaba**: `ReferenceActTests` ma 2 asercje punktowe; `Artifacts/doc001.dto.xml` nieużywany przez żaden kod (martwy golden). 17 klas testowych buduje `Word.Paragraph` helperem `CreateParagraph(text, styleId)` → sygnatura `ProcessParagraph` musi przetrwać jako adapter.
- Eksporty XML/XLSX = martwe zaślepki; działa tylko HTML/JSON w WordParserWeb (czysto na ModelDto).

---

# REKOMENDOWANA ARCHITEKTURA

## 1. Reprezentacja pośrednia (IR) — `WordParserCore/Ingest/`

Nowy namespace `WordParserCore.Ingest` (IR to wewnętrzny kontrakt potoku, nie model wyjściowy — nie do ModelDto; graf zależności bez zmian):

- **`DocumentBlock`** (record): `Text` (surowy `GetFullText`-parytet: kanał `[x]`, taby `\t`; BEZ Trim/Sanitize — te zostają w `ProcessBlock`), `StyleId?` (null dla PDF/TXT), `BlockLayoutInfo? Layout`, `BlockSourceLocation Source`, `BlockRole Role` (Body|FootnoteText), `IsEmpty`.
- **`BlockLayoutInfo`**: `LeftIndentTwips?`, `FirstLineIndentTwips?`, `HangingIndentTwips?`, `Alignment?`, `IsBold?`, `IsItalic?`, `FontSizeHalfPoints?` (twips jako jednostka kanoniczna; mapowanie twips→poziom hierarchii to zadanie klasyfikatora, nie IR). `IsItalic` jest konieczne dla tekstów jednolitych: kursywa oznacza w TJ akty/przepisy, które utraciły moc (§ 108a), oraz zlikwidowane/przekształcone organy i instytucje (§ 108b); `IsBold` — przyszłe brzmienia (§ 106a ust. 4).
- **`BlockSourceLocation`**: `BlockIndex`, `PageNumber?` (PDF), `LineNumber?` — do diagnostyki i sygnałów klasyfikacji.
- **`IDocumentBlockReader`**: `SourceFormat Format`, `IReadOnlyList<DocumentBlock> ReadBlocks(Stream)` (lista zmaterializowana — klasyfikator dokumentu i parser czytają ten sam odczyt).
- **`SourceFormatDetector`**: sniffing sygnatur (`%PDF-`→Pdf, `PK\x03\x04`→Docx, brak NUL + dekodowalny tekst→PlainText; rozszerzenie tylko rozstrzyga remisy) + `DocumentBlockReaderFactory`.
- Nowe wyjątki: `UnsupportedDocumentFormatException`, `ScannedPdfException` (dziedziczą po `ParsingException`, komunikaty po polsku).

## 2. Adaptery

- **`DocxBlockReader`**: ta sama iteracja `Descendants<Word.Paragraph>()` co dziś (parytet — łapie też akapity w tabelach!); `internal static ToBlock(Paragraph, int)` = jedyny most OpenXml→IR, reużywany przez zachowany `ProcessParagraph`. Layout z `ParagraphProperties.Indentation`/`Justification` + dominanta bold/italic z run-properties.
- **`PlainTextBlockReader`**: BOM/UTF-8 ścisłe, fallback CP1250 (wymaga pakietu `System.Text.Encoding.CodePages` + `Encoding.RegisterProvider`); linia→`TextLine`→wspólny `BlockAssembler`.
- **`PdfBlockReader`** (`Ingest/Pdf/`), biblioteka **PdfPig (Apache-2.0)** — iText wykluczony (AGPL; RCL = instytucja rządowa z usługą sieciową). Algorytm:
  1. detekcja skanu: <~20 znaków/stronę lub >50% stron pustych, lub >5% znaków U+FFFD (zepsuta cmapa) → `ScannedPdfException`;
  2. litery→słowa (`NearestNeighbourWordExtractor`)→linie po baseline (uwaga: oś Y PdfPig rośnie do góry — sortować malejąco);
  3. superscript→`[x]` (rozmiar ≤0,75× dominanty linii + baseline podniesiony >0,2×) — łapie też odnośniki § 163;
  4. filtr artefaktów stron: pozycyjno-powtórzeniowy (górne/dolne 8% strony, powtórzenie na ≥60% stron przy podobnym Y) + whitelist regexowa (`^Dziennik Ustaw$`, `^–\s*\d+\s*–$`, `^Poz\.\s*\d+$`, `^Kancelaria Sejmu`, `^©`);
  5. strefa przypisów (mniejszy font ≤0,85× na dole strony + `^\d+\)\s`) → `Role=FootnoteText`, emitowane na końcu (parser pomija po `Role==Body`, klasyfikator dokumentu czyta z nich sygnały TJ);
  6. `BlockAssembler` (wspólny z TXT): nowy blok gdy pusta linia / marker jednostki (NADzbiór wzorców klasyfikatora — tylko segmentacja, nie klasyfikacja) / skok wcięcia >0,5 em / tie-breaker interpunkcyjny (`;`/`,`/`:` + marker; nigdy sama kropka); dehyfenacja TYLKO `-` (U+002D) na końcu linii + mała litera na początku następnej (`–`/`−` na początku linii = kandydat tiret, nigdy nie skleja w górę);
  7. wcięcia z pozycji X → twips (lewy margines = moda minimalnych X korpusu).

## 3. Przebudowa szwu i koperta wyniku

- **`ParserOrchestrator.ProcessBlock(DocumentBlock, ParsingContext)`** = przeniesione ciało `ProcessParagraph`; `ProcessParagraph(Word.Paragraph, …)` zostaje jako cienki adapter — 17 klas testowych działa bez zmian.
- **`ClassificationInput`** + addytywne `Layout`/`Source`/`ListContext` (init-only, zero wpływu na istniejące testy).
- **`ParseResult`** (WordParserCore, root): `{ DocumentClassificationResult Classification; LegalDocument? Document; SourceFormat SourceFormat; int BlockCount }`. **`ParseOptions`**: `Policy ∈ {ParseWhenLegalAct (default), AlwaysParse, ClassifyOnly}` — decyzja „parsuj mimo wszystko" żyje tu.
- **`LegalDocumentParser`**: nowe kanoniczne `ParseResult Parse(Stream, string? fileNameHint = null, ParseOptions? = null)` i `ParseResult Parse(IReadOnlyList<DocumentBlock>, ParseOptions?)`; do Etapu 10 stare `Parse(string)`/`Parse(WordprocessingDocument)` → `LegalDocument` pozostają fasadami bez zmiany kontraktu; w Etapie 10 `Parse(string)` przechodzi na `ParseResult` (source-break naprawiany w tym samym PR — call site'y tylko CLI/Web), `Parse(WordprocessingDocument)` dostaje `[Obsolete]` (nigdy `error: true`).
- **CLI**: `WordParser <plik> [--format docx|pdf|txt] [--force]`; `--docx` zachowany jako alias (z legacy backupem pliku; nowe ścieżki bez backupu — parser czyta read-only); raport klasyfikacji na konsolę; nie-akt bez `--force` → exit code 2; catch nowych wyjątków.
- **Web**: `accept=".docx,.pdf,.txt"`, parsowanie ze strumienia (bez pliku temp), `RequestSizeLimit`, obsługa `ScannedPdfException`/`UnsupportedDocumentFormatException`/`OpenXmlPackageException`, banner raportu klasyfikacji + przycisk „Parsuj mimo wszystko" (`force=true`).

## 4. DocumentClassifier — klasyfikacja dokumentu wg ZTP

**Typy wyniku w ModelDto** (dane wyjściowe, wyłącznie prymitywy — bez referencji do Ingest):

```csharp
public sealed class DocumentClassificationResult
{
    public LegalActType? ActType { get; init; }          // null gdy nie-akt
    public bool IsNormativeAct { get; init; }
    public bool IsConsolidatedText { get; init; }        // obwieszczenie + TJ (§ 102-106)
    public bool IsAmending { get; init; }                // akt zmieniający (§ 96)
    public int Confidence { get; init; }                 // 1–100, spójne ze skalą ClassificationResult
    public IReadOnlyList<DocumentSignal> Signals { get; init; } = [];
    public string Justification { get; init; } = string.Empty; // zdanie po polsku, do logów/UI
}
public sealed class DocumentSignal
{
    public DocumentSignalKind Kind { get; init; }
    public int Score { get; init; }                      // wkład punktowy (może być ujemny)
    public LegalActType? SupportsType { get; init; }
    public int BlockIndex { get; init; } = -1;
    public string MatchedText { get; init; } = string.Empty; // max 120 znaków dowodu
    public string Description { get; init; } = string.Empty; // po polsku
}
```

`DocumentSignalKind` (enum): `ActKindHeader, IssuingOrganHeader, ActDateLine, ActSubjectLine, AmendingTitle, ConsolidatedTextTitle, LegalBasisFormula, EnactmentFormula, ConsolidatedTextFormula, DominantUnitArticle, DominantUnitSection, NumberingContinuity, EntryIntoForce, JournalCitation, RepealedMarker, AmendmentCommands, PenalProvisions, LocalGovernmentOrgan, VoivodeshipJournal, WordStyleHint, AmbiguousType, NoTextLayer, InsufficientSignals`.

Rozszerzenie **`LegalActType`** (na końcu enum — wartości istniejących bez zmian): `AmendingStatute, Announcement, Resolution, ExecutiveOrder, LocalLegalAct` + uzupełnienie switchy w `LegalActTypeExtensions` (`GetMainUnitLabel`: nowe → `"§"` poza AmendingStatute→`"art."`; `ToFriendlyString` po polsku). Addytywnie: `LegalDocument.Classification`, `LegalDocument.ActDate` (DateOnly?).

**Serwis** `WordParserCore/Services/Classify/Document/`: `IDocumentClassifier.Classify(IReadOnlyList<DocumentBlock>)`; wzorce współdzielone w `internal static class ZtpPatterns` (używane też przez `DocumentMetadataCollector`). Dwufazowo: **(A)** strefa tytułowa = pierwsze 25 niepustych bloków; **(B)** statystyka całego korpusu.

**Punktacja** (akumulacja per typ-kandydat; każdy przyznany punkt = `DocumentSignal` z dowodem):

| # | Sygnał | Punkty → typ |
|---|---|---|
| S01–S04 | nagłówek rodzaju aktu (USTAWA / ROZPORZĄDZENIE / OBWIESZCZENIE / Uchwała nr / Zarządzenie nr) w strefie tytułowej | +35 → odpowiedni typ (+5 gdy organ w linii/następnym bloku) |
| S05 | data „z dnia D <miesiąc słownie> RRRR r." (§ 17) | +10 → lider tytułowy |
| S06 | przedmiot („o …" / „w sprawie …" / „Kodeks\|Prawo\|Ordynacja" / „Przepisy wprowadzające") | +5 (Kodeks itd. +10 → Statute) |
| S07 | tytuł zmieniający (§ 96) | +15; `IsAmending`, Statute→AmendingStatute |
| S08 | „w sprawie ogłoszenia jednolitego tekstu" | +25 → Announcement; `IsConsolidatedText` |
| S10 | „Na podstawie art. …" + „zarządza się\|uchwala się\|postanawia się, co następuje:" | +20 (czasownik różnicuje: zarządza→Regulation/ExecutiveOrder, uchwala/postanawia→Resolution); sama podstawa bez formuły +10 |
| S11 | formuła obwieszczenia TJ (art. 16 ustawy o ogłaszaniu aktów normatywnych) | +30 → Announcement |
| S12 | dominacja jednostki podstawowej: ≥3 bloki i ≥80% jednego wzorca (`^Art\.` vs `^§`) | +20 |
| S13 | ciągłość numeracji jednostki podstawowej od PIERWSZEGO napotkanego numeru (TJ nie zaczynają od 1!), ≤10% przerw | +10 |
| S14 | formuła wejścia w życie § 45 | +10 ogólny, +10 typ z podmiotu zdania |
| S15 | markery „(uchylony)"/„(utracił moc)" ≥2 | +10 → Announcement |
| S16 | „wprowadza się następujące zmiany:" lub ≥2 triggery modyfikacji (wzorzec obejmuje warianty „W ustawie/rozporządzeniu/uchwale/zarządzeniu" — § 132/141/143) | +15; ustawia `IsAmending`; przy liderze Statute → AmendingStatute (pozostałe typy zachowują swój typ, tylko flaga) |
| S17 | `Dz. Urz. Woj.` +20 / organ JST w strefie tytułowej +25 | → LocalLegalAct |
| S18 | styl Word (`OZN_RODZ_AKTU` +10, `DATA_AKTU`/`TYTUŁ_AKTU` +5) | styl = jeden z sygnałów |
| S19 | sygnał UJEMNY: sygnatury przepisów karnych / kar administracyjnych („podlega karze …", „administracyjnej karze pieniężnej") | −15 → Regulation (rozporządzenie nie może zawierać przepisów karnych ani o karach administracyjnych — § 117) |

**Progi decyzyjne**: <3 bloki lub śr. długość <10 znaków → nie-akt z sygnałem `NoTextLayer` (Confidence 95); `winner < 40` → **NIE-AKT** (`ActType=null`, `Confidence = Clamp(90−winner, 40, 90)`, sygnał `InsufficientSignals`); `winner ≥ 40` → akt, `Confidence = winner`; przewaga nad drugim <15 → −10 + sygnał `AmbiguousType`. Interpretacja dla wywołującego: ≥75 wysoka, 40–74 średnia (parsować z raportem), <40 nie parsować.

**Pułapka obwieszczenia**: załącznik TJ zawiera linię „USTAWA" — sygnały tytułowe liczone tylko z pierwszych 25 bloków, pierwszy nagłówek wygrywa (drugi ignorowany z sygnałem informacyjnym). Dodatkowy sygnał TJ: gęstość odnośników `[N)]` (kanał `[x]`) ≥3 → +5 Announcement.

## 5. Rozpoznawanie struktury bez stylów (domknięcie luk)

- **(a) Jednostki systematyzacyjne**: nowe wartości `ParagraphKind` (na końcu: `PartUnit, BookUnit, TitleUnit, DivisionUnit, ChapterUnit, SubchapterUnit, UnitHeading, ActKindHeader, ActDateLine, ActSubjectLine`); regexy (Załącznik A) sprawdzane w `MatchRegex` PRZED ArticlePattern. Wzorzec dwuwierszowy: `ParsingContext.PendingHeadingTarget` — następny niepasujący akapit od wielkiej litery = tytuł jednostki; brak → `ValidationMessage(Warning, „Jednostka systematyzacyjna bez tytułu (§ 60 ZTP)")`. Nowy `SystematizingUnitBuilder` (Builders/) + `RomanNumeralConverter` (Helpers/); `ParsingContext.Subchapter` dostaje internal setter; pierwsza jawna jednostka przejmuje istniejący niejawny węzeł (`IsImplicit=false`) — drzewo golden doc001 bez jednostek systematyzacyjnych identyczne. Mapowania stylów `CZKSIGA…`/`TYTDZOZN…`/`ROZDZODDZOZN…`/`UNIT_PRZEDM`/`OZNRODZAKTU…`/`DATAAKTU…`/`TYTUAKTU…`/`NIEARTTEKST…` w `GetStyleType`.
- **(b) Metadane aktu**: `DocumentMetadataCollector` (maszyna stanów Kind→Organ/Date→Subject), karmiony z `HandleUnknown` w `StructureProcessor`; wypełnia `LegalDocument.Title`/`ActDate`; wzorce z `ZtpPatterns`.
- **(c) WrapUp bez stylu**: `ClassificationInput.ListContext` (`ListContextHint`: otwarte listy pkt/lit/tir, `PreviousEndsWithColon`, `PreviousLastChar`, `OpenTiretDepth`); decyzja gdy `TiretPattern`/`IsWrapUpByText` pasuje i brak stylu: (1) poprzednik kończy się `:` → Tiret; (2) otwarty tiret + tekst kończy się `,` → Tiret; (3) otwarta lista + poprzedni element kończył się `,`, `;` ALBO był BEZ interpunkcji końcowej (ostatni punkt/tiret przed częścią wspólną nie ma terminatora — § 57 ust. 3 i 6; brak terminatora to silny predyktor części wspólnej) + bieżący kończy `,`, `;` lub `.` (część wspólna po tiretach może kończyć się przecinkiem — § 57 ust. 6) + zaczyna się małą literą → **WrapUp** z karą `ContextOverridePenalty` (Confidence 100−20−10=70); kolejność sprawdzania: warunek (2) przed (3), więc przy otwartym tirecie tekst zakończony `,` pozostaje Tiretem; (4) sygnał wcięcia gdy Layout dostępny; (5) domyślnie Tiret (zachowanie dzisiejsze). `TiretPattern` rozszerzony o półpauzę: `^[-–]+\s+`. `TryGetWrapUpTarget` z fallbackiem kontekstowym (stos tiretów→Letter→Point) + `ValidationMessage(Info)`.
- **(d) Głębokość tiretu**: `GetTiretDepth(styleId, layout, context)` — priorytet: styl 2TIR/3TIR → wcięcie (`ParsingContext.TiretIndentByDepth`, tolerancja ±120 twips, cap 3) → heurystyka kontekstowa (poprzedni tiret kończy `:` → głębokość+1) + `ValidationMessage(Info)`.
- **(e) Nowelizacje bez Z/***: `QuoteBalanceTracker` (bilans „ U+201E vs " U+201D/U+0022; `Arm()` gdy pierwszy zbierany blok zaczyna się od „; `Armed && Depth==0 && ClosedAtEnd` → flush nowelizacji) — tłumi też fałszywe triggery wewnątrz cytatów; `AmendmentCommandParser` (Załącznik B) → `AmendmentTargetKind` z tokenu jednostki, **instrument z poziomu właściciela triggera** (§ 94: owner Point→`Z/`, Letter→`Z_LIT/`, Tiret gł.1→`Z_TIR/`, gł.2→`Z_2TIR/`); wynik jako źródło o niższym priorytecie niż mapa stylów w `AmendmentFinalizer`. Nieosiągalne bez stylów (flagowane `ValidationMessage(Warning)`): zagnieżdżone `ZZ/` (trigger przy Depth>0 — tylko flaga, bez budowy zagnieżdżonego Amendment), `CommonPartOf`, głębokość tiretu w cytowanej treści. Komendy obsługiwane w obu wariantach jednostki bazowej (`art.` i `§`) oraz z rzeczownikami „W ustawie/rozporządzeniu/uchwale/zarządzeniu" (§ 132/141/143) — `UnitToken` w Załączniku B już zawiera `§`.
- **(f) Fix superscriptu**: `EntityNumberService.Parse` — regex `^(?<base>.*?)(?:\^(?<sup>\w+)|\[(?<sup>\w+)\])\s*\.?\s*$` (oba kanały; dwie grupy o tej samej nazwie w alternatywie są legalne w .NET); `FormatToString` emituje `[x]` (zgodnie z TODO w kodzie i § 89 ust. 6 ZTP); wzorce numerów klasyfikatora rozszerzone o `(?:\[\d+\])?`. `NumberingHint` bez zmian (ten sam NumericPart).
- Nowe pola `ConfidencePenaltyConfig` (init-only, domyślne nie zmieniają arytmetyki): `ContextOverridePenalty=20`, `IndentMismatchPenalty=10`, `IndentMatchBonus=5`, `MissingUnitHeadingPenalty=15`, `QuoteImbalancePenalty=15`. Premie raportowane jako `ClassificationPenalty` z ujemnym Value (doprecyzować XML-doc).

---

# ETAPOWANIE IMPLEMENTACJI

Każdy etap = osobny PR. **Bramka każdego PR**: `dotnet test` zielone + snapshot doc001 bez diffu (chyba że PR jawnie deklaruje i dokumentuje diff — dozwolone tylko w Etapach 2, 6–8 dla testów charakteryzujących, nigdy dla golden ścieżki stylowej). **(R)**=czysty refaktor, **(A)**=addytywne, **(Z)**=zmiana zachowania (wyłącznie gałąź bezstylowa; styl zawsze dominuje).

| Etap | Typ | Zakres | Definicja ukończenia |
|---|---|---|---|
| 0 | A | **Siatka bezpieczeństwa**: `LegalDocumentSnapshotTests` (kanoniczna serializacja DTO z doc001.docx, regeneracja przez `UPDATE_SNAPSHOTS=1`; NAJPIERW diff istniejącego `Artifacts/doc001.dto.xml` z bieżącym wynikiem — golden jest martwy i podejrzany); `ParagraphClassifierStylelessTests` utrwalające OBECNE zachowanie bezstylowe (w tym błędne „– tekst"→Tiret, z komentarzem); test determinizmu (2 przebiegi → identyczna serializacja); CLI `--dump <plik.xml>` | 17 klas zielone bez modyfikacji; snapshot stabilny; `--dump` działa |
| 1 | R | **IR + przepięcie DOCX**: typy `Ingest/`, `DocxBlockReader.ToBlock`, `ProcessBlock`; `ProcessParagraph` = adapter | ZERO zmian w plikach testów; snapshot identyczny |
| 2 | Z | **Fix superscript** `^`→`[x]` (mały, izolowany) | możliwy świadomy diff snapshotu — rewizja ręczna |
| 3 | A | **LayoutHints z DOCX** (nikt ich jeszcze nie czyta) | snapshot identyczny; `DocxBlockReaderTests` |
| 4 | A | **DocumentClassifier** (typy ModelDto, ZtpPatterns, punktacja; NIEwpięty w Parse) | `DocumentClassifierTests` zielone; snapshot identyczny |
| 5 | A | **Adapter TXT + rama ekwiwalencji**: `PlainTextBlockReader`, wspólna normalizacja (NBSP, CRLF, cudzysłowy, U+00AD); `ParserEquivalenceTests` z lukami jako `Skip` (= jawny backlog etapów 6–8) | prosty akt w TXT ≡ wariant DOCX-szablon |
| 6 | Z | **WrapUp (6a) + jednostki systematyzacyjne (6b)**: wpięcie `IsWrapUpByText` w gałąź bezstylową; nowe `ParagraphKind` + audyt WSZYSTKICH switchy (test refleksyjny `Enum.GetValues`); `SystematizingUnitBuilder` | snapshot identyczny (ścieżka stylowa nietknięta); skipy ekwiwalencji odblokowane |
| 7 | Z | **Głębokość tiretu z wcięcia + metadane + zaostrzenie LetterPattern** (regex-only: małe litery bez polskich znaków + kontekst wyliczenia) | snapshot identyczny; testy charakteryzujące zaktualizowane jawnie |
| 8 | Z | **Nowelizacje bez stylu** (najwyższe ryzyko): QuoteBalanceTracker + AmendmentCommandParser | ekwiwalencja aktu zmieniającego DOCX-szablon ≡ TXT |
| 9 | A | **Adapter PDF** (PdfPig) | `PdfTextExtractorTests`; ekwiwalencja PDF ≡ TXT |
| 10 | A/Z | **Integracja**: ParseResult/ParseOptions/routing/detektor wpięty w Parse (do tego czasu stub `IsLegalAct=true`); CLI/Web UX; `[Obsolete]` | pełny scenariusz e2e |

## Strategia testów

- **Korpus ekwiwalencji** `DocRepo/equivalence/`: ten sam akt w 4 wariantach — `eq001-ustawa.{templ.docx, plain.docx, pdf, txt}` (tytuł, DZIAŁ+Rozdział, art./ust./pkt/lit./tiret w tym 5a i indeks górny, wprowadzenie do wyliczenia + część wspólna, wejście w życie), `eq002-nowela.*` (zmiany + zagnieżdżenie w cudzysłowie), `eq003-rozporzadzenie.*` (jednostka §). `ParserEquivalenceTests` porównuje modele przez `ModelEquivalenceComparer` (struktura + numery + treść; ignoruje pola proweniencyjne) oraz surowe bloki (spójność kanału tekstowego).
- **Fixtures klasyfikatora** `WordParserCore.Tests/Artifacts/classifier/`: pozytywy każdego rodzaju + negatywy: list urzędowy, artykuł prasowy CYTUJĄCY ustawę (test odporności), umowa z „§ 1" i wyliczeniami (najtrudniejszy negatyw), plik pusty. Przypadki graniczne: **rozporządzenie zmieniające** (oczekiwane: Regulation + `IsAmending=true`, komendy z `§`) oraz **obwieszczenie o sprostowaniu błędu** (pasuje do nagłówka OBWIESZCZENIE, ale bez sygnałów TJ — nie może dostać `IsConsolidatedText`).
- **PDF edge-case'y**: powtarzalny nagłówek usuwany; numer strony nie wpada do tekstu; dehyfenacja na łamaniu wiersza i strony; jednostka przecięta granicą stron scala się; przypis dolny „1) …" ≠ punkt; skan → `ScannedPdfException` po polsku; PDF z hasłem → czytelny błąd.
- **Property-testy numeracji**: `NumberingContinuityPropertyTests` — generator `MemberData` + `Random` ze stałym seedem (NIE wprowadzać FsCheck); łańcuchy następników (n→n+1, n→na→nb, z→aa, wstawki § 89, w tym wieloliterowe w środku ciągu: Xa→Xaa→Xab — § 89 ust. 4) bez kary, z luką — kara.
- **Smoke-test na samym ZTP**: konwersja `docs/ZTP-2026-300.md`→TXT → oczekiwane „rozporządzenie, wysoka pewność", jednostki § sparsowane.

## Ryzyka i mitygacje (pełna lista)

- **R1** — testy nowelizacji z `styleId=null` zmienią wynik po Etapach 6–8 → testy charakteryzujące z Etapu 0 + tabela „przypadek → było → jest → uzasadnienie ZTP" w opisie PR; zmiany tylko w gałęzi bezstylowej.
- **R2** — TiretPattern vs WrapUp: prawdziwe tirety pisane półpauzą (częste mimo ZTP) mogą wpaść w WrapUp → koniunkcja warunków kontekstowych + kara Confidence zamiast twardej decyzji; domyślnie Tiret; przypadki graniczne w korpusie eq001.
- **R3** — LetterPattern (`^[a-zA-Z]{1,5}\)`) łapie inicjały/„A)"/„Uwaga)" w nie-aktach → Etap 7 zaostrza tylko gałąź regex-only; klasyfikator dokumentu (Etap 4) odsiewa nie-akty zanim parser zacznie zgadywać.
- **R4** — fałszywe triggery nowelizacyjne w cytatach („otrzymuje brzmienie:" wewnątrz „…") → licznik głębokości cudzysłowów (Etap 8); do tego czasu TXT z nowelą jawnie na Skip. Uwaga: część wstępna obwieszczenia TJ (§ 103–104) zawiera CYTATY przepisów zmieniających i pominiętych — śledzenie cudzysłowów obowiązuje także przy parsowaniu obwieszczeń, nie tylko nowelizacji.
- **R5** — zmiana sygnatury orkiestratora rozbiłaby 17 klas testowych → adapter `ProcessParagraph→ProcessBlock` zostaje na stałe; helper `CreateParagraph` nietykany.
- **R6** — golden słaby, `doc001.dto.xml` może być nieaktualny → Etap 0 najpierw diffuje istniejący artefakt z bieżącym wynikiem; rozbieżność = osobna decyzja przed startem.
- **R7** — rozszerzenie `ParagraphKind` po cichu wpadnie w `default:` istniejących switchy → audyt grep + test refleksyjny po `Enum.GetValues` (każda wartość ma jawną obsługę lub jawny fallback).
- **R8** — fix `EntityNumberService` zmieni numerację jednostek z indeksem górnym → osobny PR (Etap 2), ręczna rewizja diffu snapshotu, testy obu formatów w okresie przejściowym.
- **R9** — rozjazd kanału tekstowego między adapterami (DOCX daje `–` z SymbolChar i `[x]`; TXT/PDF muszą dawać identycznie) → wspólna warstwa normalizacji (Etap 5) + `GetFullTextTests` jako specyfikacja kanału.
- **R10** — Web łapie tylko `IOException`; nowe wyjątki dałyby 500 → Etap 10 rozszerza obsługę; do tego czasu nowe wyjątki rzucane tylko z nowych wejść.
- **R11** — artefakty stron PDF między „pkt 3" a „pkt 4" → kara NumberingBreak → czyszczenie artefaktów PRZED klasyfikacją (Etap 9) + property-testy na blokach z fixtures.
- **R12** — CLI śmieci timestampowanymi kopiami w DocRepo → backup tylko dla legacy `--docx`; testy kopiują do temp; fixtures ekwiwalencji w podkatalogu.
- **Zasada twarda**: NIE „udawać" styleId w adapterach PDF/TXT (kusząca droga na skróty — zabetonuje błędy klasyfikacji).

## Weryfikacja end-to-end

```bash
dotnet test WordParserCore.Tests/WordParserCore.Tests.csproj          # po każdym etapie
# diff modelu przed/po (od Etapu 0, przez --dump):
dotnet run --project WordParser -- --docx DocRepo/doc001.docx --dump doc001.after.xml
diff -u doc001.before.xml doc001.after.xml                            # oczekiwany pusty
# smoke TXT (po Etapie 5): raport klasyfikacji + parsowanie aktu w TXT
# smoke PDF (po Etapie 9): eq001-ustawa.pdf → model ekwiwalentny z wariantem DOCX
# Web (po Etapie 10): upload .docx/.pdf/.txt; nie-akt → raport zamiast 500; skan → czytelny komunikat
```

## Kluczowe pliki

**Modyfikowane**: `WordParserCore/LegalDocumentParser.cs`, `WordParserCore/Services/Parsing/{ParserOrchestrator, StructureProcessor, ParsingContext, AmendmentStateManager, AmendmentFinalizer}.cs`, `WordParserCore/Services/Classify/{ParagraphClassifier, ClassificationInput, ParagraphKind, ConfidencePenaltyConfig}.cs`, `WordParserCore/Services/EntityNumberService.cs`, `ModelDto/{LegalActType, LegalDocument}.cs`, `WordParser/Program.cs`, `WordParserWeb/Program.cs` + `Renderers/HtmlDocumentRenderer.cs`, `WordParserCore/WordParserCore.csproj` (PdfPig, System.Text.Encoding.CodePages).

**Nowe**: `WordParserCore/Ingest/{DocumentBlock, BlockLayoutInfo, BlockSourceLocation, SourceFormat, SourceFormatDetector, IDocumentBlockReader, DocumentBlockReaderFactory, DocxBlockReader, PlainTextBlockReader, TextLine, BlockAssembler}.cs`, `WordParserCore/Ingest/Pdf/{PdfBlockReader, PdfTextLine, PdfLineExtractor, PageArtifactFilter, SuperscriptDetector}.cs`, `WordParserCore/{ParseResult, ParseOptions}.cs`, `WordParserCore/Services/Classify/Document/{IDocumentClassifier, DocumentClassifier, ZtpPatterns}.cs`, `WordParserCore/Services/Parsing/{DocumentMetadataCollector, QuoteBalanceTracker, AmendmentCommandParser}.cs`, `WordParserCore/Services/Parsing/Builders/SystematizingUnitBuilder.cs`, `WordParserCore/Helpers/RomanNumeralConverter.cs`, `ModelDto/{DocumentClassificationResult, DocumentSignalKind}.cs`, `WordParserCore/Exceptions/{UnsupportedDocumentFormatException, ScannedPdfException}.cs` + klasy testowe z tabeli etapów.

---

# Załącznik A — wzorce regex klasyfikacji dokumentu i jednostek (pełne brzmienia, do `ZtpPatterns`)

Konwencja: `private static readonly Regex`, `RegexOptions.Compiled`; celowo **bez** `IgnoreCase` tam, gdzie wersaliki są sygnałem ZTP.

```csharp
// === STREFA TYTUŁOWA (§ 16-19, § 96, § 102, § 120, § 138a) — dopasowanie do całej linii po Trim() ===

// § 16: samodzielny wiersz "USTAWA" (tolerancja rozstrzelenia "U S T A W A");
// wersaliki to utrwalona konwencja Dz.U., nie norma § 16 (wprost nakazane tylko w § 102 ust. 2 i § 120 ust. 4)
private static readonly Regex StatuteHeaderPattern = new(@"^U\s*S\s*T\s*A\s*W\s*A$", RegexOptions.Compiled);

// § 120: "ROZPORZĄDZENIE" + opcjonalnie organ WIELKIMI w tej samej linii
private static readonly Regex RegulationHeaderPattern = new(
    @"^ROZPORZĄDZENIE(?:\s+(?<organ>[A-ZĄĆĘŁŃÓŚŹŻ][A-ZĄĆĘŁŃÓŚŹŻ\s\-–,\.]+))?$", RegexOptions.Compiled);

// organ w osobnej linii
private static readonly Regex IssuingOrganLinePattern = new(
    @"^(?:MINISTRA?|PREZESA\s+RADY\s+MINISTRÓW|RADY\s+MINISTRÓW|PREZYDENTA\s+RZECZYPOSPOLITEJ\s+POLSKIEJ|KRAJOWEJ\s+RADY|MARSZAŁKA\s+SEJMU)[A-ZĄĆĘŁŃÓŚŹŻ\s\-–,\.]*$",
    RegexOptions.Compiled);

// § 102: obwieszczenie + tytuł TJ
private static readonly Regex AnnouncementHeaderPattern = new(
    @"^OBWIESZCZENIE\b[A-ZĄĆĘŁŃÓŚŹŻ\s\-–,\.]*$", RegexOptions.Compiled);
private static readonly Regex ConsolidatedTextTitlePattern = new(
    @"w\s+sprawie\s+ogłoszenia\s+jednolitego\s+tekstu", RegexOptions.Compiled | RegexOptions.IgnoreCase);

// § 138a: uchwała / zarządzenie (nr opcjonalny)
private static readonly Regex ResolutionHeaderPattern = new(
    @"^(?:UCHWAŁA|Uchwała)(?:\s+(?:NR|Nr|nr)\s*(?<no>[\w/\-\.]+))?\b", RegexOptions.Compiled);
private static readonly Regex OrderHeaderPattern = new(
    @"^(?:ZARZĄDZENIE|Zarządzenie)(?:\s+(?:NR|Nr|nr)\s*(?<no>[\w/\-\.]+))?\b", RegexOptions.Compiled);

// § 17: data aktu (miesiąc słownie)
private static readonly Regex ActDateLinePattern = new(
    @"^z\s+dnia\s+(?<day>\d{1,2})\s+(?<month>stycznia|lutego|marca|kwietnia|maja|czerwca|lipca|sierpnia|września|października|listopada|grudnia)\s+(?<year>\d{4})\s*r\.$",
    RegexOptions.Compiled | RegexOptions.IgnoreCase);

// § 18-19 / § 120 ust. 6: przedmiot aktu
private static readonly Regex ActSubjectPattern = new(
    @"^(?:o\s+\p{Ll}.+|w\s+sprawie\s+.+|(?:Kodeks|Prawo|Ordynacja)\b.*|Przepisy\s+wprowadzające\b.+)$",
    RegexOptions.Compiled);

// § 96: tytuł zmieniający
private static readonly Regex AmendingTitlePattern = new(
    @"o\s+zmianie\s+ustaw(?:y|)\b|oraz\s+niektórych\s+innych\s+ustaw|zmieniając[ea]\s+(?:rozporządzenie|uchwałę|zarządzenie)\s+w\s+sprawie",
    RegexOptions.Compiled | RegexOptions.IgnoreCase);

// === KORPUS (§ 121, § 106, § 45, § 162, § 82-85) ===

private static readonly Regex LegalBasisPattern = new(
    @"^Na\s+podstawie\s+art\.\s*\d+\w*", RegexOptions.Compiled | RegexOptions.IgnoreCase);
private static readonly Regex EnactmentFormulaPattern = new(
    @"(?<verb>zarządza|uchwala|postanawia)\s+się,?\s+co\s+następuje\s*:", RegexOptions.Compiled | RegexOptions.IgnoreCase);

// § 102/§ 104: formuła obwieszczenia TJ
private static readonly Regex ConsolidatedTextFormulaPattern = new(
    @"Na\s+podstawie\s+art\.\s*16\s+ust\.\s*[13]\s+ustawy\s+z\s+dnia\s+20\s+lipca\s+2000\s+r\.\s+o\s+ogłaszaniu\s+aktów\s+normatywnych.*?ogłasza\s+się\s+w\s+załączniku",
    RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

// jednostka podstawowa: Art. vs §
private static readonly Regex ArticleUnitStatPattern = new(@"^Art\.\s*\d+", RegexOptions.Compiled);
private static readonly Regex SectionUnitStatPattern = new(@"^§\s*\d+\w*\.\s+", RegexOptions.Compiled);

// § 45: wejście w życie (podmiot zdania różnicuje typ)
private static readonly Regex EntryIntoForcePattern = new(
    @"^(?<subject>Ustawa|Rozporządzenie|Uchwała|Zarządzenie|Niniejsz[ae]\s+(?:ustawa|rozporządzenie|uchwała|zarządzenie))\s+wchodzi\s+w\s+życie\s+(?:po\s+upływie|z\s+dniem|pierwszego\s+dnia|w\s+terminie)",
    RegexOptions.Compiled | RegexOptions.IgnoreCase);

// § 162: publikatory
private static readonly Regex JournalCitationPattern = new(
    @"(?:Dz\.\s*U\.|M\.\s*P\.)\s*(?:z\s*\d{4}\s*r\.)?\s*(?:Nr\s*\d+[,\s]*)?poz\.\s*\d+", RegexOptions.Compiled);
private static readonly Regex VoivodeshipJournalPattern = new(@"Dz\.\s*Urz\.\s*Woj\.", RegexOptions.Compiled);

// § 106/§ 106a-b: markery TJ (warianty rodzajowe)
private static readonly Regex RepealedMarkerPattern = new(
    @"\(\s*(?:uchylon[yae]|utracił[a]?\s+moc|uznan[yae]\s+za\s+nieważn[yae])\s*\)",
    RegexOptions.Compiled | RegexOptions.IgnoreCase);

// § 82-85 (stosowane odpowiednio przez § 132/§ 141/§ 143): wprowadzenie zmian — wszystkie rodzaje aktów
private static readonly Regex AmendmentIntroPattern = new(
    @"[Ww]\s+(?:ustawie|rozporządzeniu|uchwale|zarządzeniu)\s+.*?wprowadza\s+się\s+następujące\s+zmiany\s*:",
    RegexOptions.Compiled | RegexOptions.Singleline);

// organy JST → akt prawa miejscowego
private static readonly Regex LocalGovernmentOrganPattern = new(
    @"\b(?:RAD[AY]\s+(?:GMINY|MIASTA|MIEJSKIEJ?|POWIATU)|SEJMIK(?:U)?\s+WOJEWÓDZTWA|WOJEWOD[AY]|BURMISTRZ|WÓJT|PREZYDENT\s+MIASTA|STAROST[AY])\b",
    RegexOptions.Compiled | RegexOptions.IgnoreCase);

// odnośniki TJ w kanale [x]
private static readonly Regex FootnoteRefPattern = new(@"\[\d+\)?\]", RegexOptions.Compiled);

// === JEDNOSTKI SYSTEMATYZACYJNE (§ 60-62) — do ParagraphClassifier, sprawdzane PRZED ArticlePattern ===

internal static readonly Regex PartUnitPattern = new(
    @"^CZĘŚĆ\s+(?<num>[IVXLCDM]+[a-z]{0,3}|OGÓLNA|SZCZEGÓLNA|WOJSKOWA|PIERWSZA|DRUGA|TRZECIA|CZWARTA|PIĄTA|SZÓSTA|SIÓDMA|ÓSMA)$", RegexOptions.Compiled);
internal static readonly Regex BookUnitPattern = new(
    @"^KSIĘGA\s+(?<num>[IVXLCDM]+[a-z]{0,3}|PIERWSZA|DRUGA|TRZECIA|CZWARTA|PIĄTA|SZÓSTA|SIÓDMA|ÓSMA)$", RegexOptions.Compiled);
internal static readonly Regex TitleUnitPattern = new(@"^TYTUŁ\s+(?<num>[IVXLCDM]+[a-z]{0,3})$", RegexOptions.Compiled);
internal static readonly Regex DivisionUnitPattern = new(@"^DZIAŁ\s+(?<num>[IVXLCDM]+[a-z]{0,3})$", RegexOptions.Compiled); // "DZIAŁ IVa" (§ 89)
internal static readonly Regex ChapterUnitPattern = new(
    @"^(?:ROZDZIAŁ|Rozdział)\s+(?<num>\d+[a-z]{0,3}|[IVXLCDM]+[a-z]{0,3})$", RegexOptions.Compiled); // rzymskie w starszych aktach → ValidationMessage(Info)
internal static readonly Regex SubchapterUnitPattern = new(@"^(?:ODDZIAŁ|Oddział)\s+(?<num>\d+[a-z]{0,3})$", RegexOptions.Compiled);
```

# Załącznik B — wzorce `AmendmentCommandParser` (odtwarzanie semantyki nowelizacji z tekstu)

```csharp
internal sealed record AmendmentCommand(
    AmendmentOperationType Operation,
    string? TargetUnitToken,   // "art."|"§"|"ust."|"pkt"|"lit."|"tiret"
    string? TargetNumber,
    IReadOnlyList<(string Unit, string Number)> ParentPath,  // segmenty "w art. 5 w ust. 2"
    string? AnchorNumber);     // "po art. 5a dodaje się art. 5b" → anchor 5a

private const string UnitToken = @"(?<unit>art\.|§|ust\.|pkt|lit\.|tiret)";

private static readonly Regex ParentSegmentPattern = new(
    @"[Ww]\s+(?<unit>art\.|§|ust\.|pkt|lit\.)\s*(?<num>\d+[a-z]*(?:\[\d+\])?|[a-z]{1,5})", RegexOptions.Compiled);
private static readonly Regex ChangeCommandPattern = new(
    $@"{UnitToken}\s*(?<num>\d+[a-z]*(?:\[\d+\])?|[a-z]{{1,5}})?\s+otrzymuj[eą]\s+brzmienie\s*:",
    RegexOptions.Compiled | RegexOptions.IgnoreCase);
private static readonly Regex AddCommandPattern = new(
    $@"[Pp]o\s+{UnitToken}\s*(?<anchor>\d+[a-z]*|[a-z]{{1,5}})\s+dodaje\s+się\s+(?<newUnit>art\.|§|ust\.|pkt|lit\.|tiret)\s*(?<newNum>\d+[a-z]*(?:\s*[-–i,]\s*\d*[a-z]*)*)\s+w\s+brzmieniu\s*:",
    RegexOptions.Compiled | RegexOptions.IgnoreCase);
private static readonly Regex RepealCommandPattern = new(
    $@"uchyla\s+się\s+{UnitToken}\s*(?<nums>[\d\w,\si–\-]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
private static readonly Regex ReplaceWordsPattern = new(
    @"(?:użyte\s+w\s+.+?\s+)?wyraz(?:y|u|ów)?\s+[„“].+?[”"]\s+zastępuje\s+się\s+wyraz(?:ami|em)\s+[„“].+?[”"]",
    RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

// Delimitacja treści (QuoteBalanceTracker): bilans „ (U+201E) vs ” (U+201D) / " (U+0022);
// zamknięcie bloku treści: koniec cytatu + interpunkcja komendy (";" między zmianami, "." na końcu, "," przed częścią wspólną)
private static readonly Regex QuoteClosePattern = new(@"[""”]\s*[;,.]?\s*$", RegexOptions.Compiled);
```

Mapowania: `TargetUnitToken` → `AmendmentTargetKind` (art./§→Article, ust.→Paragraph, pkt→Point, lit.→Letter, tiret→Tiret); instrument z zagnieżdżenia § 94 = funkcja właściciela triggera (Point→`Z/`, Letter→`Z_LIT/`, Tiret gł.1→`Z_TIR/`, gł.2→`Z_2TIR/`); `ParentPath` z segmentów „w …" zasila `ParentContext`. Wynik wpinany w `AmendmentFinalizer.Finalize` jako źródło o niższym priorytecie niż dekodowanie stylów.
