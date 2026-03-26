# Schemat JSON dla wykrytych odesłań prawnych

Specyfikacja formatu danych zwracanych przez zewnętrzne narzędzie wykrywające odesłania (powołania) w tekście aktów prawnych. Format jest przeznaczony do wymiany danych między narzędziem detekcji a WordParserem.

## Poziom główny

```json
{
  "documentId": "string",
  "sourceFile": "ustawa_2024_1234.docx",
  "extractedAt": "2026-03-23T14:30:00Z",
  "references": [ ... ]
}
```

| Pole | Typ | Wymagane | Opis |
|------|-----|----------|------|
| `documentId` | string | tak | Identyfikator dokumentu (dowolny, unikalny w ramach sesji) |
| `sourceFile` | string | tak | Nazwa pliku źródłowego |
| `extractedAt` | string (ISO 8601) | tak | Moment ekstrakcji |
| `references` | array of `Reference` | tak | Wykryte odesłania |

---

## Reference — obiekt odesłania

Każdy obiekt reprezentuje jedno odesłanie z jedną ścieżką docelową. Złożony fragment tekstu z wieloma celami na różnych poziomach hierarchii generuje wiele obiektów `Reference` ze wspólnym `sourceSpan`.

Wyjątek: gdy różnica dotyczy jednego poziomu (np. "pkt 1, 3 i 5"), używamy jednego obiektu z selektorem `values`.

```json
{
  "id": "ref_001",
  "type": "internal",
  "citationType": "implicit",
  "temporalAspect": "dynamic",

  "sourceSpan": {
    "entityEId": "art_5__ust_2",
    "textSegmentIndex": 0,
    "paragraphIndex": null,
    "charOffset": 45,
    "charLength": 28,
    "rawText": "art. 12 ust. 3 pkt 5"
  },

  "targetAct": null,
  "targetPath": [
    { "unitType": "article", "selector": { "value": "45" } },
    { "unitType": "paragraph", "selector": { "value": "3" } },
    { "unitType": "point", "selector": { "value": "5" } }
  ],

  "alias": null,
  "staticDate": null,
  "staticDateDirection": null
}
```

### Pola główne

| Pole | Typ | Wymagane | Opis |
|------|-----|----------|------|
| `id` | string | tak | Unikalny identyfikator odesłania w ramach dokumentu |
| `type` | enum | tak | `"internal"` / `"external"` |
| `citationType` | enum | tak | Typ powołania (patrz [CitationType](#citationtype)) |
| `temporalAspect` | enum | tak | Aspekt temporalny (patrz [TemporalAspect](#temporalaspect)) |
| `sourceSpan` | object | tak | Lokalizacja w dokumencie źródłowym |
| `targetAct` | object? | nie | Identyfikacja aktu docelowego; `null` = odesłanie wewnętrzne |
| `targetPath` | array | tak | Ścieżka do jednostki docelowej (może być pusta dla powołań całego aktu) |
| `alias` | object? | nie | Informacja o skróconym powołaniu lokalnym |
| `staticDate` | string? | nie | Data dla odesłań statycznych (ISO 8601, tylko data) |
| `staticDateDirection` | enum? | nie | `"asOf"` / `"before"` |

---

## SourceSpan — lokalizacja źródłowa

Podwójna lokalizacja: `entityEId` (jeśli narzędzie operuje na sparsowanym dokumencie) + `paragraphIndex` (fallback na surową pozycję).

```json
{
  "entityEId": "art_5__ust_2__pkt_3",
  "textSegmentIndex": 0,
  "paragraphIndex": null,
  "charOffset": 45,
  "charLength": 28,
  "rawText": "o którym mowa w art. 12 ust. 3 pkt 5"
}
```

| Pole | Typ | Wymagane | Opis |
|------|-----|----------|------|
| `entityEId` | string? | nie | eId jednostki redakcyjnej w modelu WordParser (format: `art_5__ust_2__pkt_3`) |
| `textSegmentIndex` | int? | nie | Indeks segmentu tekstu (zdania) w jednostce; mapuje na `TextSegment.Order` |
| `paragraphIndex` | int? | nie | Numer akapitu w surowym dokumencie (0-based); fallback gdy brak eId |
| `charOffset` | int | tak | Pozycja znaku od początku `ContentText` encji (gdy jest eId) lub od początku akapitu (gdy `paragraphIndex`) |
| `charLength` | int | tak | Długość fragmentu tekstu z odesłaniem |
| `rawText` | string | tak | Dosłowny fragment tekstu źródłowego |

**Reguła**: co najmniej jedno z `entityEId` lub `paragraphIndex` musi być podane.

---

## TargetAct — identyfikacja aktu docelowego

Null dla odesłań wewnętrznych. Dla zewnętrznych:

```json
{
  "actTitle": "ustawa z dnia 20 lipca 2000 r. o ogłaszaniu aktów normatywnych i niektórych innych aktów prawnych",
  "actType": "statute",
  "actDate": "2000-07-20",
  "eli": "https://eli.gov.pl/eli/DU/2019/1461/ogl",
  "euNumber": null,
  "journals": [
    {
      "type": "DU",
      "year": 2019,
      "positions": [1461],
      "number": null,
      "page": null,
      "isConsolidated": true,
      "sourceString": "t.j. Dz. U. z 2019 r. poz. 1461",
      "amendments": null
    }
  ]
}
```

| Pole | Typ | Wymagane | Opis |
|------|-----|----------|------|
| `actTitle` | string? | nie | Tytuł aktu prawnego (może być null dla powołań skróconych globalnych) |
| `actType` | enum | tak | Typ aktu (patrz [ActType](#acttype)) |
| `actDate` | string? | nie | Data aktu (ISO 8601, tylko data) |
| `eli` | string? | nie | URI ELI aktu docelowego, np. `"https://eli.gov.pl/eli/DU/2025/825"` (kompatybilność z polskim systemem ELI) |
| `euNumber` | string? | nie | Numer aktu UE, np. `"2016/679"`, `"3254/91"` |
| `journals` | array of `Journal` | nie | Publikatory; tablica obiektów, jeden per rok |

### Journal — publikator

Jeden obiekt per rok publikacji. Spójne z istniejącym `JournalInfo` w modelu WordParser.

```json
{
  "type": "DU",
  "year": 2025,
  "positions": [825, 1014, 1080],
  "number": null,
  "page": null,
  "isConsolidated": false,
  "sourceString": "Dz. U. z 2025 r. poz. 825, 1014 i 1080",
  "amendments": "z późn. zm."
}
```

| Pole | Typ | Wymagane | Opis |
|------|-----|----------|------|
| `type` | enum | tak | `"DU"` (Dz.U.) / `"DUrzUE"` (Dz.Urz.UE) |
| `year` | int | tak | Rok publikacji |
| `positions` | array of int | tak | Numery pozycji (Dz.U.) |
| `number` | int? | nie | Numer dziennika (starszy format PL: "Nr 119"; format UE: "L 2025/1106") |
| `page` | int? | nie | Numer strony (format UE: "str. 1") |
| `isConsolidated` | bool | tak | Czy tekst jednolity ("t.j.") |
| `sourceString` | string | tak | Dosłowny fragment tekstu z publikatorem |
| `amendments` | string? | nie | Np. `"z późn. zm."` jeśli występuje |

**Wieloletni publikator**: "Dz. U. z 2025 r. poz. 825, 1014 i 1080 oraz z 2026 r. poz. 26" → dwa obiekty `Journal`:
```json
"journals": [
  { "type": "DU", "year": 2025, "positions": [825, 1014, 1080], "isConsolidated": false, "sourceString": "Dz. U. z 2025 r. poz. 825, 1014 i 1080" },
  { "type": "DU", "year": 2026, "positions": [26], "isConsolidated": false, "sourceString": "z 2026 r. poz. 26" }
]
```

---

## TargetPath — ścieżka do jednostki docelowej

Tablica kroków hierarchicznych od najwyższego do najniższego poziomu. Pusta tablica oznacza powołanie całego aktu.

```json
[
  { "unitType": "article", "selector": { "value": "45" } },
  { "unitType": "paragraph", "selector": { "value": "3" } },
  { "unitType": "point", "selector": { "value": "5" } }
]
```

### TargetPathStep

| Pole | Typ | Wymagane | Opis |
|------|-----|----------|------|
| `unitType` | enum | tak | Typ jednostki (patrz [UnitType](#unittype)) |
| `selector` | object | tak | Selektor wartości (patrz [Selector](#selector)) |

### Selector

Trzy wzajemnie wykluczające się warianty. Dokładnie jeden zestaw pól musi być podany.

**Wariant 1 — pojedyncza wartość:**
```json
{ "value": "5" }
```

**Wariant 2 — zakres:**
```json
{ "rangeFrom": "12", "rangeTo": "15" }
```

**Wariant 3 — lista:**
```json
{ "values": ["12", "15", "18"] }
```

| Pole | Typ | Opis |
|------|-----|------|
| `value` | string | Pojedyncza wartość |
| `rangeFrom` | string | Początek zakresu (włącznie) |
| `rangeTo` | string | Koniec zakresu (włącznie) |
| `values` | array of string | Lista wartości |

**Reguły wartości:**
- Zawsze stringi (nie liczby) — unifikuje numery (`"5"`), litery (`"a"`), indeksy (`"5a"`), superscripty (`"5a1"`)
- Słowa porządkowe normalizowane do numerów: "pierwsze" → `"1"`, "drugi" → `"2"`, "trzecie" → `"3"`
- Dotyczy zwłaszcza zdań, akapitów i tiretów, które w tekście prawnym zapisywane są słownie

---

## Alias — skrócone powołania lokalne

Dla odesłań korzystających ze skrótów wprowadzonych w danym akcie.

```json
{
  "aliasType": "zwanaDalej",
  "aliasName": "ustawą",
  "definingReferenceId": "ref_003"
}
```

| Pole | Typ | Wymagane | Opis |
|------|-----|----------|------|
| `aliasType` | enum | tak | Typ skrótu (patrz [AliasType](#aliastype)) |
| `aliasName` | string | tak | Tekst aliasu (np. `"ustawą"`, `"rozporządzeniem SAFE"`) |
| `definingReferenceId` | string? | nie | `id` referencji z pełnym powołaniem, które definiuje ten alias |

---

## Enumeracje

### ReferenceType

| Wartość | Opis |
|---------|------|
| `internal` | Odesłanie do przepisów tego samego aktu |
| `external` | Odesłanie do przepisów innego aktu |

### CitationType

| Wartość | Opis | Przykład |
|---------|------|---------|
| `full` | Pełne powołanie z tytułem i publikatorem | "ustawy z dnia 20 lipca 2000 r. ... (Dz. U. z 2019 r. poz. 1461)" |
| `shortGlobal` | Skrót rozpoznawalny powszechnie | "Kodeksu cywilnego", "rozporządzenia nr 1896/2006" |
| `shortLocal` | Skrót wprowadzony w danym akcie | "ustawy, o której mowa w ust. 2", "ustawy zmienianej w art. 1" |
| `implicit` | Powołanie wewnętrzne bez wzmianki o innym akcie | "art. 45 ust. 3", "pkt 5" |

### TemporalAspect

| Wartość | Opis |
|---------|------|
| `dynamic` | Domyślny; odsyła do aktualnej wersji przepisu |
| `static` | Z dopiskiem "w brzmieniu obowiązującym w dniu/przed dniem..." |
| `implicitlyStatic` | W przepisach zmieniających/uchylających (bez jawnego markera) |

### StaticDateDirection

| Wartość | Opis | Przykład |
|---------|------|---------|
| `asOf` | Wersja obowiązująca w danym dniu | "w brzmieniu obowiązującym w dniu 21 sierpnia 2025 r." |
| `before` | Wersja obowiązująca przed danym dniem | "w brzmieniu obowiązującym przed dniem 1 września 2017 r." |

### ActType

| Wartość | Opis |
|---------|------|
| `statute` | Ustawa |
| `regulation` | Rozporządzenie (PL) |
| `code` | Kodeks |
| `directive` | Dyrektywa UE |
| `euRegulation` | Rozporządzenie UE |
| `other` | Inny akt (zarządzenie, uchwała itp.) |

### UnitType

#### Jednostki redakcyjne

| Wartość | Opis | Oznaczenie w tekście | Powołanie | eId prefix |
|---------|------|---------------------|-----------|------------|
| `article` | Artykuł (ustawa/akt UE) lub § jako główna jednostka (rozporządzenie) | `Art. 1.` / `§ 1.` | `art. 1` / `§ 1` | `art` |
| `paragraph` | Ustęp | `1.` | `ust. 1` | `ust` |
| `paragraphSection` | § jako podjednostka artykułu (w kodeksach) | `§ 1.` | `§ 1` | `par` |
| `point` | Punkt | `1)` | `pkt 1` | `pkt` |
| `letter` | Litera | `a)` | `lit. a` | `lit` |
| `tiret` | Tiret | `–` | `tiret pierwsze` | `tir` |
| `doubleTiret` | Podwójne tiret | `– –` | `podwójne tiret pierwsze` | `tir` |
| `tripleTiret` | Potrójne tiret | `– – –` | `potrójne tiret pierwsze` | `tir` |
| `quadrupleTiret` | Poczwórne tiret | `– – – –` | `poczwórne tiret pierwsze` | `tir` |
| `sentence` | Zdanie | (brak osobnego oznaczenia) | `zdanie pierwsze` | `zd` |
| `akapit` | Akapit (prawo UE) | (nowy wiersz) | `akapit pierwszy` | `ak` |

#### Jednostki systematyzacyjne

| Wartość | Opis | eId prefix |
|---------|------|------------|
| `part` | Część | `cz` |
| `book` | Księga | `ks` |
| `title` | Tytuł | `tyt` |
| `division` | Dział | `dz` |
| `chapter` | Rozdział | `rozd` |
| `subchapter` | Oddział | `oddz` |
| `attachment` | Załącznik | `zal` |

### AliasType

| Wartość | Opis | Wzorzec w tekście |
|---------|------|-------------------|
| `zwanaDalej` | Alias wprowadzony frazą "zwana dalej..." | `zwanej dalej "ustawą"` |
| `oKtorejMowa` | Odwołanie "o której mowa w..." | `ustawy, o której mowa w ust. 2 pkt 2` |
| `zmieniana` | Odwołanie "zmienianej/uchylanej w art. ..." | `ustawy zmienianej w art. 1` |

### JournalType

| Wartość | Opis |
|---------|------|
| `DU` | Dziennik Ustaw RP |
| `DUrzUE` | Dziennik Urzędowy Unii Europejskiej |

---

## Kompletne przykłady

### 1. Odesłanie wewnętrzne proste

Tekst: *o którym mowa w art. 45 ust. 3 pkt 5*

```json
{
  "id": "ref_001",
  "type": "internal",
  "citationType": "implicit",
  "temporalAspect": "dynamic",
  "sourceSpan": {
    "entityEId": "art_10__ust_1",
    "textSegmentIndex": 0,
    "paragraphIndex": null,
    "charOffset": 15,
    "charLength": 33,
    "rawText": "o którym mowa w art. 45 ust. 3 pkt 5"
  },
  "targetAct": null,
  "targetPath": [
    { "unitType": "article", "selector": { "value": "45" } },
    { "unitType": "paragraph", "selector": { "value": "3" } },
    { "unitType": "point", "selector": { "value": "5" } }
  ],
  "alias": null,
  "staticDate": null,
  "staticDateDirection": null
}
```

### 2. Odesłanie wewnętrzne z zakresem artykułów

Tekst: *art. 12-15*

```json
{
  "id": "ref_002",
  "type": "internal",
  "citationType": "implicit",
  "temporalAspect": "dynamic",
  "sourceSpan": {
    "entityEId": "art_20__ust_1",
    "textSegmentIndex": 0,
    "paragraphIndex": null,
    "charOffset": 5,
    "charLength": 10,
    "rawText": "art. 12-15"
  },
  "targetAct": null,
  "targetPath": [
    { "unitType": "article", "selector": { "rangeFrom": "12", "rangeTo": "15" } }
  ],
  "alias": null,
  "staticDate": null,
  "staticDateDirection": null
}
```

### 3. Odesłanie wewnętrzne z listą punktów

Tekst: *pkt 12 i 15*

```json
{
  "id": "ref_003",
  "type": "internal",
  "citationType": "implicit",
  "temporalAspect": "dynamic",
  "sourceSpan": {
    "entityEId": "art_5__ust_3",
    "textSegmentIndex": 0,
    "paragraphIndex": null,
    "charOffset": 20,
    "charLength": 11,
    "rawText": "pkt 12 i 15"
  },
  "targetAct": null,
  "targetPath": [
    { "unitType": "point", "selector": { "values": ["12", "15"] } }
  ],
  "alias": null,
  "staticDate": null,
  "staticDateDirection": null
}
```

### 4. Odesłanie zewnętrzne pełne

Tekst: *ustawy z dnia 20 lipca 2000 r. o ogłaszaniu aktów normatywnych i niektórych innych aktów prawnych (t.j. Dz. U. z 2019 r. poz. 1461)*

```json
{
  "id": "ref_004",
  "type": "external",
  "citationType": "full",
  "temporalAspect": "dynamic",
  "sourceSpan": {
    "entityEId": "art_5__ust_1",
    "textSegmentIndex": 0,
    "paragraphIndex": null,
    "charOffset": 20,
    "charLength": 102,
    "rawText": "ustawy z dnia 20 lipca 2000 r. o ogłaszaniu aktów normatywnych i niektórych innych aktów prawnych (t.j. Dz. U. z 2019 r. poz. 1461)"
  },
  "targetAct": {
    "actTitle": "ustawa z dnia 20 lipca 2000 r. o ogłaszaniu aktów normatywnych i niektórych innych aktów prawnych",
    "actType": "statute",
    "actDate": "2000-07-20",
    "eli": "https://eli.gov.pl/eli/DU/2019/1461/ogl",
    "euNumber": null,
    "journals": [
      {
        "type": "dzU",
        "year": 2019,
        "positions": [1461],
        "number": null,
        "page": null,
        "isConsolidated": true,
        "sourceString": "t.j. Dz. U. z 2019 r. poz. 1461",
        "amendments": null
      }
    ]
  },
  "targetPath": [],
  "alias": null,
  "staticDate": null,
  "staticDateDirection": null
}
```

### 5. Odesłanie zewnętrzne pełne do aktu UE

Tekst: *rozporządzenia Parlamentu Europejskiego i Rady (UE) 2016/679 z dnia 27 kwietnia 2016 r. w sprawie ochrony osób fizycznych... (Dz. U. UE. L. z 2016 r. Nr 119, str. 1 z późn. zm.)*

```json
{
  "id": "ref_005",
  "type": "external",
  "citationType": "full",
  "temporalAspect": "dynamic",
  "sourceSpan": {
    "entityEId": "art_1__ust_1",
    "textSegmentIndex": 0,
    "paragraphIndex": null,
    "charOffset": 15,
    "charLength": 180,
    "rawText": "rozporządzenia Parlamentu Europejskiego i Rady (UE) 2016/679 z dnia 27 kwietnia 2016 r. w sprawie ochrony osób fizycznych w związku z przetwarzaniem danych osobowych... (Dz. U. UE. L. z 2016 r. Nr 119, str. 1 z późn. zm.)"
  },
  "targetAct": {
    "actTitle": "rozporządzenie Parlamentu Europejskiego i Rady (UE) 2016/679 z dnia 27 kwietnia 2016 r. w sprawie ochrony osób fizycznych w związku z przetwarzaniem danych osobowych i w sprawie swobodnego przepływu takich danych oraz uchylenia dyrektywy 95/46/WE",
    "actType": "euRegulation",
    "actDate": "2016-04-27",
    "euNumber": "2016/679",
    "journals": [
      {
        "type": "DUrzUE",
        "year": 2016,
        "positions": [],
        "number": 119,
        "page": 1,
        "isConsolidated": false,
        "sourceString": "Dz. U. UE. L. z 2016 r. Nr 119, str. 1",
        "amendments": "z późn. zm."
      }
    ]
  },
  "targetPath": [],
  "alias": null,
  "staticDate": null,
  "staticDateDirection": null
}
```

### 6. Skrócone powołanie globalne — kodeks

Tekst: *art. 231 Kodeksu cywilnego*

```json
{
  "id": "ref_006",
  "type": "external",
  "citationType": "shortGlobal",
  "temporalAspect": "dynamic",
  "sourceSpan": {
    "entityEId": "art_8__ust_1",
    "textSegmentIndex": 0,
    "paragraphIndex": null,
    "charOffset": 25,
    "charLength": 26,
    "rawText": "art. 231 Kodeksu cywilnego"
  },
  "targetAct": {
    "actTitle": "Kodeks cywilny",
    "actType": "code",
    "actDate": null,
    "euNumber": null,
    "journals": []
  },
  "targetPath": [
    { "unitType": "article", "selector": { "value": "231" } }
  ],
  "alias": null,
  "staticDate": null,
  "staticDateDirection": null
}
```

### 7. Skrócone powołanie globalne — akt UE

Tekst: *rozporządzenia nr 1896/2006*

```json
{
  "id": "ref_007",
  "type": "external",
  "citationType": "shortGlobal",
  "temporalAspect": "dynamic",
  "sourceSpan": {
    "entityEId": "art_3__ust_2",
    "textSegmentIndex": 0,
    "paragraphIndex": null,
    "charOffset": 30,
    "charLength": 25,
    "rawText": "rozporządzenia nr 1896/2006"
  },
  "targetAct": {
    "actTitle": null,
    "actType": "euRegulation",
    "actDate": null,
    "euNumber": "1896/2006",
    "journals": []
  },
  "targetPath": [],
  "alias": null,
  "staticDate": null,
  "staticDateDirection": null
}
```

### 8. Kodeks — art. 1 § 1 (paragraf jako ustęp)

Tekst: *art. 1 § 1 Kodeksu karnego*

```json
{
  "id": "ref_008",
  "type": "external",
  "citationType": "shortGlobal",
  "temporalAspect": "dynamic",
  "sourceSpan": {
    "entityEId": "art_3__ust_1",
    "textSegmentIndex": 0,
    "paragraphIndex": null,
    "charOffset": 10,
    "charLength": 26,
    "rawText": "art. 1 § 1 Kodeksu karnego"
  },
  "targetAct": {
    "actTitle": "Kodeks karny",
    "actType": "code",
    "actDate": null,
    "euNumber": null,
    "journals": []
  },
  "targetPath": [
    { "unitType": "article", "selector": { "value": "1" } },
    { "unitType": "paragraphSection", "selector": { "value": "1" } }
  ],
  "alias": null,
  "staticDate": null,
  "staticDateDirection": null
}
```

### 9. Prawo UE — akapit

Tekst: *art. 3 ust. 1 akapit drugi rozporządzenia (EWG) nr 3254/91*

```json
{
  "id": "ref_009",
  "type": "external",
  "citationType": "shortGlobal",
  "temporalAspect": "dynamic",
  "sourceSpan": {
    "entityEId": "art_7__ust_2",
    "textSegmentIndex": 0,
    "paragraphIndex": null,
    "charOffset": 15,
    "charLength": 58,
    "rawText": "art. 3 ust. 1 akapit drugi rozporządzenia (EWG) nr 3254/91"
  },
  "targetAct": {
    "actTitle": null,
    "actType": "euRegulation",
    "actDate": null,
    "euNumber": "3254/91",
    "journals": []
  },
  "targetPath": [
    { "unitType": "article", "selector": { "value": "3" } },
    { "unitType": "paragraph", "selector": { "value": "1" } },
    { "unitType": "akapit", "selector": { "value": "2" } }
  ],
  "alias": null,
  "staticDate": null,
  "staticDateDirection": null
}
```

### 10. Zdanie

Tekst: *art. 5 ust. 2 zdanie drugie*

```json
{
  "id": "ref_010",
  "type": "internal",
  "citationType": "implicit",
  "temporalAspect": "dynamic",
  "sourceSpan": {
    "entityEId": "art_10__ust_3",
    "textSegmentIndex": 0,
    "paragraphIndex": null,
    "charOffset": 30,
    "charLength": 28,
    "rawText": "art. 5 ust. 2 zdanie drugie"
  },
  "targetAct": null,
  "targetPath": [
    { "unitType": "article", "selector": { "value": "5" } },
    { "unitType": "paragraph", "selector": { "value": "2" } },
    { "unitType": "sentence", "selector": { "value": "2" } }
  ],
  "alias": null,
  "staticDate": null,
  "staticDateDirection": null
}
```

### 11. Zagnieżdżone tirety

Tekst: *lit. a tiret trzecie podwójne tiret pierwsze, drugie i trzecie*

```json
{
  "id": "ref_011",
  "type": "internal",
  "citationType": "implicit",
  "temporalAspect": "dynamic",
  "sourceSpan": {
    "entityEId": "par_2",
    "textSegmentIndex": 0,
    "paragraphIndex": null,
    "charOffset": 0,
    "charLength": 62,
    "rawText": "lit. a tiret trzecie podwójne tiret pierwsze, drugie i trzecie"
  },
  "targetAct": null,
  "targetPath": [
    { "unitType": "letter", "selector": { "value": "a" } },
    { "unitType": "tiret", "selector": { "value": "3" } },
    { "unitType": "doubleTiret", "selector": { "values": ["1", "2", "3"] } }
  ],
  "alias": null,
  "staticDate": null,
  "staticDateDirection": null
}
```

### 12. Głęboko zagnieżdżone tirety (inny cel z tego samego fragmentu)

Tekst: *podwójne tiret czwarte potrójne tiret pierwsze* (kontynuacja przykładu 11)

```json
{
  "id": "ref_012",
  "type": "internal",
  "citationType": "implicit",
  "temporalAspect": "dynamic",
  "sourceSpan": {
    "entityEId": "par_2",
    "textSegmentIndex": 0,
    "paragraphIndex": null,
    "charOffset": 0,
    "charLength": 140,
    "rawText": "lit. a tiret trzecie podwójne tiret pierwsze, drugie i trzecie, podwójne tiret czwarte potrójne tiret pierwsze"
  },
  "targetAct": null,
  "targetPath": [
    { "unitType": "letter", "selector": { "value": "a" } },
    { "unitType": "tiret", "selector": { "value": "3" } },
    { "unitType": "doubleTiret", "selector": { "value": "4" } },
    { "unitType": "tripleTiret", "selector": { "value": "1" } }
  ],
  "alias": null,
  "staticDate": null,
  "staticDateDirection": null
}
```

### 13. Odesłanie zewnętrzne z aliasem "zwana dalej"

Tekst: *ustawy z dnia 11 marca 2022 r. o obronie Ojczyzny (Dz. U. z 2025 r. poz. 825, 1014 i 1080 oraz z 2026 r. poz. 26), zwanej dalej "ustawą"*

```json
{
  "id": "ref_013",
  "type": "external",
  "citationType": "full",
  "temporalAspect": "dynamic",
  "sourceSpan": {
    "entityEId": "art_1__ust_1",
    "textSegmentIndex": 0,
    "paragraphIndex": null,
    "charOffset": 20,
    "charLength": 140,
    "rawText": "ustawy z dnia 11 marca 2022 r. o obronie Ojczyzny (Dz. U. z 2025 r. poz. 825, 1014 i 1080 oraz z 2026 r. poz. 26), zwanej dalej \"ustawą\""
  },
  "targetAct": {
    "actTitle": "ustawa z dnia 11 marca 2022 r. o obronie Ojczyzny",
    "actType": "statute",
    "actDate": "2022-03-11",
    "euNumber": null,
    "journals": [
      { "type": "DU", "year": 2025, "positions": [825, 1014, 1080], "number": null, "page": null, "isConsolidated": false, "sourceString": "Dz. U. z 2025 r. poz. 825, 1014 i 1080", "amendments": null },
      { "type": "DU", "year": 2026, "positions": [26], "number": null, "page": null, "isConsolidated": false, "sourceString": "z 2026 r. poz. 26", "amendments": null }
    ]
  },
  "targetPath": [],
  "alias": {
    "aliasType": "zwanaDalej",
    "aliasName": "ustawą",
    "definingReferenceId": null
  },
  "staticDate": null,
  "staticDateDirection": null
}
```

### 14. Użycie aliasu "zwana dalej"

Tekst: *art. 2a ust. 4 pkt 2 ustawy* (gdzie "ustawy" odnosi się do aliasu z ref_013)

```json
{
  "id": "ref_014",
  "type": "external",
  "citationType": "shortLocal",
  "temporalAspect": "dynamic",
  "sourceSpan": {
    "entityEId": "art_5__ust_1",
    "textSegmentIndex": 0,
    "paragraphIndex": null,
    "charOffset": 40,
    "charLength": 30,
    "rawText": "art. 2a ust. 4 pkt 2 ustawy"
  },
  "targetAct": null,
  "targetPath": [
    { "unitType": "article", "selector": { "value": "2a" } },
    { "unitType": "paragraph", "selector": { "value": "4" } },
    { "unitType": "point", "selector": { "value": "2" } }
  ],
  "alias": {
    "aliasType": "zwanaDalej",
    "aliasName": "ustawy",
    "definingReferenceId": "ref_013"
  },
  "staticDate": null,
  "staticDateDirection": null
}
```

### 15. Alias "zmieniana w art."

Tekst: *art. 11a ust. 1 ustawy zmienianej w art. 1*

```json
{
  "id": "ref_015",
  "type": "external",
  "citationType": "shortLocal",
  "temporalAspect": "dynamic",
  "sourceSpan": {
    "entityEId": "art_2__ust_1",
    "textSegmentIndex": 0,
    "paragraphIndex": null,
    "charOffset": 40,
    "charLength": 43,
    "rawText": "art. 11a ust. 1 ustawy zmienianej w art. 1"
  },
  "targetAct": null,
  "targetPath": [
    { "unitType": "article", "selector": { "value": "11a" } },
    { "unitType": "paragraph", "selector": { "value": "1" } }
  ],
  "alias": {
    "aliasType": "zmieniana",
    "aliasName": "ustawy zmienianej w art. 1",
    "definingReferenceId": "ref_013"
  },
  "staticDate": null,
  "staticDateDirection": null
}
```

### 16. Odesłanie statyczne

Tekst: *ustawy z dnia 26 października 1995 r. o społecznych formach rozwoju mieszkalnictwa (Dz. U. z 2025 r. poz. 1273) w brzmieniu obowiązującym w dniu 21 sierpnia 2025 r.*

```json
{
  "id": "ref_016",
  "type": "external",
  "citationType": "full",
  "temporalAspect": "static",
  "sourceSpan": {
    "entityEId": "art_15__ust_1",
    "textSegmentIndex": 0,
    "paragraphIndex": null,
    "charOffset": 30,
    "charLength": 165,
    "rawText": "ustawy z dnia 26 października 1995 r. o społecznych formach rozwoju mieszkalnictwa (Dz. U. z 2025 r. poz. 1273) w brzmieniu obowiązującym w dniu 21 sierpnia 2025 r."
  },
  "targetAct": {
    "actTitle": "ustawa z dnia 26 października 1995 r. o społecznych formach rozwoju mieszkalnictwa",
    "actType": "statute",
    "actDate": "1995-10-26",
    "euNumber": null,
    "journals": [
      { "type": "DU", "year": 2025, "positions": [1273], "number": null, "page": null, "isConsolidated": false, "sourceString": "Dz. U. z 2025 r. poz. 1273", "amendments": null }
    ]
  },
  "targetPath": [],
  "alias": null,
  "staticDate": "2025-08-21",
  "staticDateDirection": "asOf"
}
```

### 17. Odesłanie statyczne — "przed dniem"

Tekst: *w brzmieniu obowiązującym przed dniem 1 września 2017 r.*

```json
{
  "id": "ref_017",
  "type": "external",
  "citationType": "full",
  "temporalAspect": "static",
  "sourceSpan": { "..." : "..." },
  "targetAct": { "..." : "..." },
  "targetPath": [
    { "unitType": "article", "selector": { "value": "5" } },
    { "unitType": "paragraph", "selector": { "value": "5g" } }
  ],
  "alias": null,
  "staticDate": "2017-09-01",
  "staticDateDirection": "before"
}
```

### 18. Jednostka systematyzacyjna

Tekst: *rozdział 3*

```json
{
  "id": "ref_018",
  "type": "internal",
  "citationType": "implicit",
  "temporalAspect": "dynamic",
  "sourceSpan": {
    "entityEId": "art_50__ust_1",
    "textSegmentIndex": 0,
    "paragraphIndex": null,
    "charOffset": 20,
    "charLength": 10,
    "rawText": "rozdział 3"
  },
  "targetAct": null,
  "targetPath": [
    { "unitType": "chapter", "selector": { "value": "3" } }
  ],
  "alias": null,
  "staticDate": null,
  "staticDateDirection": null
}
```

### 19. Załącznik

Tekst: *załącznik 1*

```json
{
  "id": "ref_019",
  "type": "internal",
  "citationType": "implicit",
  "temporalAspect": "dynamic",
  "sourceSpan": {
    "entityEId": "art_30__ust_2",
    "textSegmentIndex": 0,
    "paragraphIndex": null,
    "charOffset": 70,
    "charLength": 11,
    "rawText": "załącznik 1"
  },
  "targetAct": null,
  "targetPath": [
    { "unitType": "attachment", "selector": { "value": "1" } }
  ],
  "alias": null,
  "staticDate": null,
  "staticDateDirection": null
}
```

### 20. Lokalizacja surowa (bez eId)

Gdy narzędzie operuje na surowym tekście bez uprzedniego parsowania:

```json
{
  "id": "ref_020",
  "type": "internal",
  "citationType": "implicit",
  "temporalAspect": "dynamic",
  "sourceSpan": {
    "entityEId": null,
    "textSegmentIndex": null,
    "paragraphIndex": 42,
    "charOffset": 15,
    "charLength": 20,
    "rawText": "art. 5 ust. 3 pkt 2"
  },
  "targetAct": null,
  "targetPath": [
    { "unitType": "article", "selector": { "value": "5" } },
    { "unitType": "paragraph", "selector": { "value": "3" } },
    { "unitType": "point", "selector": { "value": "2" } }
  ],
  "alias": null,
  "staticDate": null,
  "staticDateDirection": null
}
```

---

## Mapowanie unitType → eId prefix

Tabela konwersji dla przyszłej integracji z systemem eId WordParsera:

| unitType | eId prefix | Istniejący w BaseEntity |
|----------|------------|------------------------|
| `article` | `art` | tak (`Article.EIdPrefix`) |
| `paragraph` | `ust` | tak (`Paragraph.EIdPrefix`) |
| `paragraphSection` | `par` | nie (nowy) |
| `point` | `pkt` | tak (`Point.EIdPrefix`) |
| `letter` | `lit` | tak (`Letter.EIdPrefix`) |
| `tiret` | `tir` | tak (`Tiret.EIdPrefix`) |
| `doubleTiret` | `tir` | tak (zagnieżdżony `Tiret`) |
| `tripleTiret` | `tir` | tak (zagnieżdżony `Tiret`) |
| `quadrupleTiret` | `tir` | tak (zagnieżdżony `Tiret`) |
| `sentence` | `zd` | nie (nowy) |
| `akapit` | `ak` | nie (nowy) |
| `part` | `cz` | tak (`Part.EIdPrefix`) |
| `book` | `ks` | tak (`Book.EIdPrefix`) |
| `title` | `tyt` | tak (`Title.EIdPrefix`) |
| `division` | `dz` | tak (`Division.EIdPrefix`) |
| `chapter` | `rozd` | tak (`Chapter.EIdPrefix`) |
| `subchapter` | `oddz` | tak (`Subchapter.EIdPrefix`) |
| `attachment` | `zal` | nie (nowy) |
