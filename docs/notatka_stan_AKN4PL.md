# Notatka służbowa — Stan projektu AKN4PL i rekomendacje dalszych kroków

**Data:** 2026-03-17

**Temat:** Ocena stanu prac nad formatem XML dla polskich aktów prawnych (AKN4PL) oraz propozycja dalszych działań

---

## I. Cel notatki

Niniejsza notatka stanowi podsumowanie aktualnego stanu technicznego projektu digitalizacji polskich aktów prawnych do ustrukturyzowanego formatu XML. Opracowanie obejmuje analizę istniejących schematów EAP (format RCL), stanu implementacji modelu domenowego (.NET / `ModelDto`) oraz ocenę ich zgodności z międzynarodowym standardem Akoma Ntoso (OASIS LegalDocML). Celem jest wskazanie luk oraz przedstawienie priorytytyzowanych rekomendacji dalszych kroków.

---

## II. Stan faktyczny

### 2.1. Własny format XML (EAP, format RCL)

W katalogu `docs/eap_schema/` znajdują się kompletne schematy i przykłady dokumentów autorstwa RCL (Rządowe Centrum Legislacji), opracowane w wersji `2018/10`:

**Pliki schematów (XSD):**

| Plik | Zawartość |
|---|---|
| `strict.xsd` | Typy główne: `UstawaTyp`, `KodeksTyp`, `RozporzadzenieTyp`, `ObwieszczenieTyp`; typy artykułu (`ArtykulUstawyTyp`, `ArtykulKodeksuTyp`), element `preambula` |
| `wspolne.xsd` | Typy bazowe: `JednostkaRedakcyjnaTyp`, `JednostkaRedakcyjnaNumerowanaTyp`, grupy elementów systematyzacyjnych (`jednostka-systematyzacyjna`), `MetadaneTyp`, typy zawartości tekstowej, wzorce `cytat-strukt` |
| `typografia.xsd` | Atrybuty typograficzne (marginesy, łamanie strony, nieprzenosic) |
| `mathml3*.xsd` | Schematy MathML3 — wzory matematyczne w treści |

**Przestrzenie nazw (namespace):**
- `http://www.rcl.gov.pl/2018/10/prawo#strict` — wariant ścisły (zgodny z ZTP)
- `http://www.rcl.gov.pl/2018/10/prawo#base` — wariant bazowy
- `http://www.rcl.gov.pl/2018/10/metadane` — metadane dokumentu
- `http://www.rcl.gov.pl/2018/10/eap/typografia` — atrybuty typograficzne

**Przykładowe dokumenty XML:**

- `Ustawa.xml` — Prawo restrukturyzacyjne; pełna hierarchia: `tyt → dzial → rozdzial → artykul → punkt → litera`; tabele zagnieżdżone w `cytat-strukt`, grafiki, odnośniki dolne
- `Rozporzadzenie.xml` — analogiczna struktura z `paragraf` (§) zamiast `artykul`
- `TJ ustawy.xml` — tekst jednolity w obwieszczeniu z zagnieżdżonymi nowelizacjami

**Kluczowa obserwacja:** Format EAP używa **polskich nazw elementów XML** (`artykul`, `ustep`, `punkt`, `litera`, `tiret`, `dzial`, `rozdzial`, `tyt`) i jest formatem własnym RCL — **nie jest** to implementacja Akoma Ntoso ani żaden znany profil AKN.

Metadane dokumentu w EAP obejmują: `Tytul`, `Rodzaj`, `DataWydania`, `DataOgloszenia`, `Dziennik`, `Pozycja`, `ELI`, `Instytucja`, `Organy/Organ`, `Sygnatura`.

### 2.2. Model domenowy (.NET / ModelDto)

Biblioteka `ModelDto` implementuje obiektowy model reprezentacji struktury aktu prawnego. Stan aktualny:

**Hierarchia redakcyjna (kompletna, 6 poziomów):**
```
Article → Paragraph (IsImplicit) → Point → Letter → Tiret → DoubleTiret
```

**Hierarchia systematyzacyjna (kompletna, 6 poziomów):**
```
Part → Book → Title → Division → Chapter → Subchapter → [Articles]
```

**Klasy modelu:**

| Klasa | Rola |
|---|---|
| `LegalDocument` | Wrapper całego aktu: `Type`, `Title`, `SourceJournal`, `RootPart`, `Articles` |
| `BaseEntity` | Baza abstrakcyjna: `Guid`, `UnitType`, `EntityNumber`, `ContentText` (string), `EffectiveDate`, `ValidationMessages`, generowanie `eId` (`art_5__ust_2__pkt_3`) |
| `EntityNumber` | Numer jednostki: `NumericPart`, `LexicalPart`, `Superscript` |
| `JournalInfo` | Publikator: `Year`, `Positions[]`, `SourceString` |
| `Amendment` + `AmendmentContent` | Nowelizacje: `OperationType` (Modification/Insertion/Repeal) |
| `CommonPart` | Część wspólna intro/wrapUp przy Paragraph/Point/Letter |
| `ValidationMessage` | Komunikaty diagnostyczne: Info/Warning/Error/Critical |
| `LegalActType` | Enum: Statute, Regulation, Code, Bill, Ordinance, RegulatoryImpactAssessment |

**Format eId** generowany przez `BaseEntity.Id`: `art_5__ust_2__pkt_3__lit_a__tir_1`
(podwójny podkreślnik jako separator poziomów, zgodnie z implementacją w `BaseEntity.cs`).

### 2.3. Ocena zgodności z Akoma Ntoso (OASIS LegalDocML)

| Aspekt | Stan |
|---|---|
| Nazwy elementów | EAP używa nazw polskich (`artykul`, `ustep`); AKN — angielskich (`article`, `paragraph`); ModelDto — angielskich klas; **brak wspólnego mianownika** |
| Hierarchia redakcyjna | Zbieżna strukturalnie (artykuł/ustęp/punkt/litera/tiret); różnice nazewnicze |
| Hierarchia systematyzacyjna | Zbieżna (część/księga/tytuł/dział/rozdział/oddział) — AKN obsługuje te poziomy przez `hcontainer`/`section` |
| Identyfikatory (eId) | Format `art_5__ust_2__pkt_3` jest zbliżony do AKN EIDS (`art_5-ust_2-pkt_3`), ale inny separator |
| Metadane (FRBR) | EAP ma własny schemat metadanych; brak modelu FRBR (FRBRuri, FRBRthis, FRBRdate, FRBRauthor) |
| Nowelizacje | EAP ma specjalne style `Z/*`, `ZZ*`; AKN ma element `<mod>`; ModelDto ma `Amendment` — różne podejścia |
| Tabele i grafiki | EAP obsługuje przez `tabela`, `grafika`; ModelDto przechowuje jako string w `ContentText` |
| Podpisy cyfrowe | EAP rezerwuje miejsce na `xmldsig#`; AKN podobnie; ModelDto — brak |
| Typy dokumentów | EAP: ustawa, kodeks, rozporządzenie, obwieszczenie; ModelDto: Statute/Regulation/Code/Bill/Ordinance/RegulatoryImpactAssessment; AKN: typy parametryczne przez `name` |

---

## III. Zidentyfikowane luki

### 3.1. Luki ModelDto względem formatu EAP i wymagań AKN

| Luka | Opis | Priorytet |
|---|---|---|
| **Brak struktury metadanych dokumentu** | EAP zawiera `MetadaneTyp` z `Tytul`, `Rodzaj`, `DataWydania`, `DataOgloszenia`, `Dziennik`, `Pozycja`, `ELI`, `Instytucja`, `Sygnatura`. `LegalDocument` ma tylko `Title` + `JournalInfo` (bez ELI, bez organu, bez daty ogłoszenia, bez sygnatury) | Wysoki |
| **`ContentText` jako surowy string** | Tabele, grafiki, wzory matematyczne są zredukowane do tekstu płaskiego. EAP modeluje je jako zagnieżdżone elementy XML. Komentarz TODO w `BaseEntity.cs` potwierdza tę świadomość | Wysoki |
| **`cytat-strukt` inline poza kontekstem nowelizacji** | `Amendment` + `AmendmentContent` poprawnie pokrywają `cytat-strukt` w kontekście nowelizacyjnym (pełna hierarchia artykuł/ustęp/punkt/litera/tiret, renderowanie w cudzysłowie). Luka dotyczy wyłącznie przypadku, gdy `cytat-strukt` pojawia się jako inline w zwykłym tekście artykułu (cytowanie innego aktu bez zmiany) — tam treść jest spłaszczana do `ContentText`; rozwiązanie wchodzi w zakres ogólnego problemu bogatej treści | Niski |
| **Brak klasy `Preamble`** | EAP i AKN mają element `preambula`/`preamble`; ModelDto nie ma encji preambuły | Średni |
| **Brak modelu odnośników (przypisów)** | EAP obsługuje `odnosnik` i `punkt-odnosnik`; ModelDto brak | Średni |
| **Brak identyfikatorów FRBR** | AKN wymaga `FRBRuri`, `FRBRthis`, `FRBRdate`, `FRBRauthor` w metadanych; EAP ma `ELI` jako namiastkę; ModelDto — brak | Średni |
| **Niepełny `LegalActType`** | Brak: `Announcement` (obwieszczenie), `Resolution` (uchwała), `Order` (zarządzenie), `LocalLaw` (prawo miejscowe), `JudicialDecision` | Niski |
| **Brak atrybutów typograficznych** | Pominięte celowo jako warstwa prezentacji — należy potwierdzić to założenie | Niski |

### 3.2. Luki EAP (format RCL) względem AKN

| Luka | Opis |
|---|---|
| **Brak FRBR** | Brak modelu FRBRuri/FRBRwork/FRBRexpression/FRBRmanifestation |
| **Brak elementu `<ref>`** | AKN posiada semantyczne odwołania (`<ref href="...">`) do innych aktów; EAP używa zwykłego tekstu |
| **Brak obsługi aktów samorządowych** | EAP pokrywa wyłącznie akty centralne (ustawa, rozporządzenie, obwieszczenie); brak uchwał gmin, zarządzeń wojewodów |
| **Wersjonowanie** | EAP nie modeluje historii wersji dokumentu; AKN posiada `<lifecycle>` i `<timeInterval>` |
| **Własne przestrzenie nazw** | EAP używa `http://www.rcl.gov.pl/...`; utrudnia interoperacyjność bez warstwy transformacji |

---

## IV. Plan wdrożenia

### Kontekst porównawczy — lekcje z innych wdrożeń

Analiza wdrożeń w innych państwach wskazuje na trójwarstwową architekturę, którą Polska powinna przyjąć jako model referencyjny:

| Warstwa | Cel | Przykład |
|---|---|---|
| **Identyfikacja** | Trwałe, czytelne maszynowo URI dla każdego aktu | Hiszpania (BOE/ELI od 2018) |
| **Treść strukturalna** | Krajowy profil XML dla struktury dokumentu | Niemcy (LegalDocML.de, 2020) |
| **Publikowanie** | Generowanie wszystkich formatów z jednego źródła XML | Hiszpania (BOE: XML → PDF/HTML/ePUB) |

Podejście warstwowe pozwala na niezależny postęp w każdej warstwie — warstwy nie blokują się wzajemnie i mogą być wdrażane równolegle przez różne zespoły. Wdrożenie ELI (warstwa identyfikacji) jest niezależne od decyzji EAP vs. AKN i może zostać rozpoczęte natychmiast.

---

### Krok 1 — Decyzja strategiczna: EAP vs. AKN4PL (poziom kierowniczy)

**Pytanie kluczowe:** Czy projekt dąży do:
- (A) **rozwinięcia formatu EAP** jako specyficznego dla Polski (z zachowaniem polskich nazw elementów i przestrzeni nazw RCL), czy
- (B) **stworzenia polskiego profilu Akoma Ntoso** (AKN4PL) zgodnego z OASIS LegalDocML — analogicznie do `LegalDocML.de` (Niemcy, 2020) i `LexML` (Brazylia)?

**Opcja A — rozwinięcie formatu EAP:**
- Istniejące schematy XSD i przykłady dokumentów stanowią gotowy punkt startowy; nie jest wymagana migracja
- Polskie nazwy elementów (`artykul`, `ustep`, `punkt`) są intuicyjne dla polskich prawników i legislatorów
- Pełna kontrola nad ewolucją standardu bez zależności od zewnętrznych organów standaryzacyjnych
- Wyzwanie: brak gotowej infrastruktury narzędziowej (parsery, walidatory, edytory obsługujące EAP); każde narzędzie musi być budowane od zera
- Wyzwanie: konieczność samodzielnego opracowania modelu metadanych, wersjonowania i odnośników między aktami
- Wyzwanie: EAP pokrywa wyłącznie akty centralne (ustawa, rozporządzenie, obwieszczenie); rozszerzenie o prawo miejscowe i orzeczenia sądowe wymaga zaprojektowania nowych typów dokumentów i słowników organów od podstaw

**Opcja B — polski profil Akoma Ntoso (AKN4PL):**
- Wyzwanie: dostępne narzędzia ekosystemu AKN nie działają w warunkach produkcyjnych (weryfikacja własna); jedyną biblioteką z dobrym rozpoznawaniem AKN jest `cobalt` (Python), który nie jest zgodny ze stosem technologicznym projektu (.NET); infrastruktura narzędziowa wymaga budowy od zera niezależnie od wybranej opcji
- Dojrzały model metadanych (FRBR), wersjonowania i odnośników semantycznych — do adaptacji, nie do tworzenia
- Wyzwanie: konieczność mapowania polskiej nomenklatury prawnej na angielskie nazwy elementów AKN; ryzyko niejednoznaczności (np. `paragraph` dla ustępu i paragrafu kodeksowego)
- Wyzwanie: dostosowanie schematu do specyfiki polskiej techniki prawodawczej (tiret, podwójny tiret, cyrkularne nowelizacje) może wymagać rozszerzeń niestandardowych
- Wyzwanie: zakres profilu obejmuje akty centralne, prawo miejscowe (uchwały gmin i powiatów, zarządzenia wojewodów) oraz orzeczenia sądowe — każda kategoria wymaga odrębnych typów dokumentów, słowników organów i reguł struktury; AKN dostarcza mechanizmy (typy parametryczne, `hcontainer`, słowniki ról), ale ich konfiguracja dla polskich realiów pozostaje pracą do wykonania
- Wyzwanie: uzależnienie od cyklu wydawniczego standardu OASIS; zmiany w AKN mogą wymuszać aktualizacje profilu

---

### Krok 2 — Warstwa identyfikacji: wdrożenie ELI (niezależne od Kroku 1)

**Model referencyjny: Hiszpania (BOE, od 2018)**

Hiszpański dziennik urzędowy przypisał każdemu aktowi prawnemu trwały adres URI oparty na standardzie ELI (European Legislation Identifier), np.:
```
https://boe.es/eli/es/l/2015/10/01/40/
```
Dzięki temu relacje między aktami (która ustawa zmienia którą) są zakodowane maszynowo w nagłówkach XML — bez konieczności analizowania tekstu przez człowieka. System prawny staje się grafem powiązań nawigowanym automatycznie.

**Zakres dla Polski:**
- Objąć systemem ELI: ISAP, Dziennik Ustaw, Monitor Polski oraz wszystkie wojewódzkie dzienniki urzędowe
- Zdefiniować schemat URI dla każdego typu aktu i organu (akty centralne, prawo miejscowe, orzeczenia)
- Uzupełnić `LegalDocument` w ModelDto o pole `ELI` (URI) — zmiana minimalna, niezablokowana przez decyzję z Kroku 1

**Uwaga:** ELI jest warunkiem wstępnym dla warstwy powiązań semantycznych, niezależnie od wybranego formatu XML.

---

### Krok 3 — Struktura organizacyjna: Komitet AKN4PL

Na wzór **IMFC** (Interinstitutional Metadata and Formats Committee) przy UE oraz **OASIS LegalDocML TC** należy powołać **Komitet AKN4PL** złożony z:

| Organ | Rola |
|---|---|
| **RCL** (przewodniczący/sekretariat) | Koordynacja, Dz.U., M.P., standard techniczny |
| Kancelaria Sejmu | Ustawy, projekty, dokumenty parlamentarne, ISAP, ELI |
| Kancelaria Senatu | Dokumenty senackie |
| Ministerstwo Cyfryzacji | KRI |
| Przedstawiciele JST (ZPP, ZMP) | Prawo miejscowe, uchwały gmin/powiatów |
| Archiwa Państwowe | Archiwizacja, długoterminowe przechowywanie |
| Eksperci techniczni | XML/XSD, parsery, narzędzia |
| Prawnicy-praktycy | Zgodność z zasadami techniki prawodawczej |

Komitet ustanawia **Zespół Techniczny** odpowiedzialny za schematy XSD, słowniki kontrolowane i narzędzia walidacji.

---

### Krok 4 — Warstwa treści: opracowanie profilu krajowego

Po podjęciu decyzji z Kroku 1 Zespół Techniczny opracowuje profil krajowy (EAP rozszerzony lub AKN4PL). W obu przypadkach zakres prac obejmuje:

1. **Mapowanie struktur** — tabela odpowiedniości między elementami EAP, AKN i ModelDto (szkic w sekcji III.1 niniejszej notatki)
2. **Typy dokumentów** — zdefiniowanie wszystkich klas aktów objętych profilem:
   - akty centralne: ustawa, kodeks, rozporządzenie, obwieszczenie, zarządzenie
   - prawo miejscowe: uchwała gminy/powiatu/województwa, zarządzenie wójta/starosty/marszałka
   - orzeczenia: wyroki, postanowienia (zakres do ustalenia przez Komitet)
3. **Słowniki organów** — kontrolowany rejestr organów wydających powiązany z ELI URI
4. **Model nowelizacji** — rozszerzenie pokrycia poza `Amendment`/`AmendmentContent` o cytaty strukturalne inline
5. **Uzupełnienie ModelDto** — według listy luk z sekcji III.1 (priorytetowo: metadane, `LegalActType`, `Preamble`)

---

### Krok 5 — Warstwa publikowania: jedno źródło, wiele formatów

**Model referencyjny: Hiszpania (BOE)**

Każdy opublikowany akt w BOE jest dostępny w formatach XML, PDF, HTML i ePUB — wszystkie generowane automatycznie z jednego bazowego pliku XML. Eliminuje to ryzyko rozbieżności między wersjami.

**Cel dla Polski:** XML jako jedyne źródło prawdy (`single source of truth`). Formaty wyjściowe (PDF do publikacji w Dz.U., HTML na stronie ISAP, ePUB) są renderowane z XML przez pipeline transformacji — nie redagowane oddzielnie.

**Wymagania techniczne:**
- Pipeline XSLT lub dedykowany renderer .NET (WordParserCore może pełnić rolę odwrotną: XML → DOCX → PDF dla RCL)
- Walidacja XML względem schematu jako brama przed publikacją
- Wersjonowanie dokumentów w repozytorium XML

---

### Krok 6 — Pilotaż

Kolejność typów aktów dla pilotażu:

| Etap | Typ aktu | Uzasadnienie |
|---|---|---|
| 1 | Ustawa | Najczęstszy typ; najlepiej pokryty przez parser i EAP |
| 2 | Rozporządzenie | Różni się użyciem `§`; drugi co do częstości |
| 3 | Obwieszczenie (tekst jednolity) | Weryfikacja obsługi zagnieżdżonych nowelizacji |
| 4 | Uchwała gminy | Pierwszy akt z zakresu prawa miejscowego |
| 5 | Wyrok sądowy | Weryfikacja zakresu profilu poza aktami normatywnymi |

Dla każdego etapu: przykładowy dokument XML zgodny z profilem, walidacja względem schematu, roundtrip test (DOCX → XML → format wyjściowy).

---

### Krok 7 — Konsultacje i standaryzacja

- Konsultacje wewnętrzne w ramach Komitetu AKN4PL przed publikacją profilu
- Publiczne konsultacje z wydawcami prawa, bibliotekami, systemami LegalTech
- Zgłoszenie profilu do rejestracji w OASIS LegalDocML TC lub jako Polska Norma (PKN)

---

## V. Podsumowanie

Projekt WordParser dysponuje **solidnymi fundamentami**:
- kompletnym modelem domenowym (`ModelDto`) z pełną hierarchią redakcyjną i systematyzacyjną,
- działającym parserem DOCX (`WordParserCore`) z obsługą nowelizacji,
- wzorcowym formatem XML (EAP/RCL) z przykładami i schematami XSD.

Analiza porównawcza (Hiszpania, Niemcy, Brazylia) wskazuje na trójwarstwowy model wdrożenia: **identyfikacja (ELI) → treść strukturalna (profil XML) → publikowanie z jednego źródła**. Warstwy są od siebie niezależne — wdrożenie ELI może rozpocząć się natychmiast, bez oczekiwania na rozstrzygnięcie kwestii formatu.

**Decyzja blokująca:** wybór między rozwinięciem EAP a opracowaniem profilu AKN4PL (Krok 1) warunkuje kierunek prac Zespołu Technicznego i powinna zostać podjęta w pierwszej kolejności.

**Rekomendacja priorytetyzacji zakresu:** Z uwagi na ograniczenia czasowe pierwsza wersja profilu powinna obejmować wyłącznie **ustawy i rozporządzenia** — typy najlepiej udokumentowane, pokryte przez istniejący parser i schematy EAP. Pozostałe rodzaje dokumentów (prawo miejscowe, orzeczenia, obwieszczenia) powinny być implementowane równolegle i niezależnie, bez presji czasowej. Warunek: profil od początku musi być zaprojektowany z punktami rozszerzenia (`extensibility points`) umożliwiającymi dodanie nowych typów dokumentów i słowników organów bez modyfikacji rdzenia schematu.

**Pozostałe kluczowe wyzwania (niezależne od decyzji formatowej):**
- `ContentText` jako surowy string blokuje pełną wierność treści (tabele, grafiki, wzory)
- Niekompletność metadanych w `LegalDocument` (brak ELI URI, organu wydającego, daty ogłoszenia)
- Brak modelu dla prawa miejscowego i orzeczeń w obu formatach (EAP i ModelDto)
