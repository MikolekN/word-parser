# ZTP — kondensat dla maszynowego parsowania aktów prawnych

> Źródło: „Zasady techniki prawodawczej" — tekst jednolity Dz. U. z 2026 r. poz. 300 ([ZTP-2026-300.md](ZTP-2026-300.md)).
> Zakres: wyłącznie reguły istotne dla parsera/klasyfikatora (struktura, oznaczenia, formuły tekstowe). Pominięto dyrektywy czysto merytoryczne (jak *redagować* prawo). Każda reguła ma odesłanie do § źródła.

## 1. Rodzaje aktów i ich identyfikacja

| Rodzaj | Nagłówek tytułu | Jednostka podstawowa | Formuła otwierająca tekst | Cechy dodatkowe |
|---|---|---|---|---|
| **Ustawa** | `USTAWA` (§ 16) | artykuł `Art. N.` (§ 54) | — (od razu przepisy) | przedmiot: „o …" / „Kodeks/Prawo/Ordynacja …" / „Przepisy wprowadzające …" (§ 19) |
| **Ustawa zmieniająca** | `USTAWA` | artykuł | „W ustawie … wprowadza się następujące zmiany:" (§ 85) | przedmiot: „o zmianie ustawy …", „ustawa zmieniająca ustawę …", „… oraz niektórych innych ustaw" (§ 96) |
| **Ustawa wprowadzająca** | `USTAWA` | artykuł | — | przedmiot: „Przepisy wprowadzające …" (§ 19 pkt 2, § 47–50) |
| **Kodeks** | `USTAWA` | artykuł, ale **ustępy oznaczane `§`** (§ 55 ust. 3) | — | dodatkowe jednostki systematyzacyjne: księgi, części (§ 60 ust. 3) |
| **Rozporządzenie** | `ROZPORZĄDZENIE` + organ WIELKIMI (§ 120 ust. 1, 4) | paragraf `§ N.` (§ 124 ust. 1) | „Na podstawie art. … ustawy … zarządza się, co następuje:" (§ 121 ust. 3) | przedmiot zawsze „w sprawie …" (§ 120 ust. 6); zmieniające: „zmieniające rozporządzenie w sprawie …" (§ 129 ust. 2) |
| **Uchwała / zarządzenie** | „Uchwała nr …" / „Zarządzenie nr …" (numer opcjonalny; § 138a ust. 2) | paragraf (przez odesłanie do działu V — § 141) | tekst zaczyna się od wskazania podstawy prawnej (§ 139) | akty wewnętrzne nazywają się WYŁĄCZNIE „uchwała" albo „zarządzenie" (§ 140); zmieniające: „zmieniająca uchwałę w sprawie …" (§ 138a ust. 3) |
| **Akt prawa miejscowego** | jak uchwała/zarządzenie | jak wyżej (§ 143) | jak wyżej | publikator: `Dz. Urz. Woj. …` (§ 162 ust. 2 pkt 10) |
| **Obwieszczenie (tekst jednolity)** | `OBWIESZCZENIE` + organ WIELKIMI (§ 102 ust. 1–2) | — (TJ jest załącznikiem; § 101) | „Na podstawie art. 16 ust. 1 [ustawa] / ust. 3 [inny akt] ustawy z dnia 20 lipca 2000 r. o ogłaszaniu aktów normatywnych … ogłasza się w załączniku …" (§ 104) | przedmiot: „w sprawie ogłoszenia jednolitego tekstu …" (§ 102 ust. 4) |
| **Obwieszczenie o sprostowaniu błędu** | `Obwieszczenie … o sprostowaniu błędu` (§ 112 ust. 2) | — | „… obwieszcza się, że w … zamiast wyrazu „…" powinien być wyraz „…"" (§ 112 ust. 3) | podstawa: art. 17 ustawy o ogłaszaniu (§ 112 ust. 3) |

**Budowa tytułu — oddzielne wiersze** (§ 16, § 102 ust. 1, § 120 ust. 1, § 138a ust. 1):
1. oznaczenie rodzaju aktu (ustawa: samo `USTAWA`; pozostałe: rodzaj + organ) — wersaliki są wprost nakazane tylko dla rozporządzenia (§ 120 ust. 4) i obwieszczenia (§ 102 ust. 2); zapis `USTAWA` wielkimi literami to utrwalona konwencja Dz. U., nie norma § 16,
2. data: `z dnia D <miesiąc słownie> RRRR r.` — dzień cyframi arabskimi, miesiąc SŁOWNIE, rok cyframi + „r." (§ 17),
3. określenie przedmiotu.

**Tytuł aktu zmieniającego — czego w nim NIE MA** (sygnał klasyfikacyjny): przy tytule aktu zmienianego NIE podaje się daty aktu zmienianego ani oznaczeń dzienników urzędowych (§ 96 ust. 3 dla ustawy; § 129 ust. 2 dla rozporządzenia — pomija się też organ i datę; § 138a ust. 3 dla uchwały/zarządzenia — pomija się też numer). Obecność daty/publikatora w nagłówku przy tytule zmienianego aktu wskazuje na błąd ekstrakcji albo zwykłe odesłanie w treści.

**Odnośniki do tytułu** (kolejność gdy współwystępują — § 19c): (1) notyfikacja techniczna, (2) wdrożenie UE: „niniejsza ustawa wdraża … (tytuł aktu)" (§ 19a), (3) tytuły ustaw zmienianych/uchylanych (§ 19b, § 96 ust. 4).

## 2. Jednostki redakcyjne — oznaczenia, interpunkcja, powołania (§ 54–59)

| Jednostka | Oznaczenie w tekście | Terminator elementu | Powołanie | Uwagi |
|---|---|---|---|---|
| Artykuł | `Art. N.` — cyfry arabskie Z kropką; numeracja ciągła w całej ustawie | — | `art. N` (bez kropki po numerze) | § 57 ust. 1 |
| Paragraf (rozporządzenie) | `§ N.` | — | `§ N` | § 124 |
| Ustęp | `N.` — cyfry arabskie z kropką, BEZ nawiasu; ciągłość w obrębie artykułu | — | `ust. N` | § 57 ust. 2; w kodeksie ustęp = `§ N.` (§ 55 ust. 3) |
| Punkt | `N)` — cyfry arabskie z nawiasem z prawej; ciągłość w obrębie artykułu/ustępu | `;` (ostatni `.`); przy części wspólnej: każdy punkt Z WYJĄTKIEM OSTATNIEGO `,`, a `.` po części wspólnej | `pkt N` (bez nawiasu) | § 57 ust. 3 |
| Litera | `x)` — małe litery ŁACIŃSKIE, **bez** ą ć ę ł ń ó ś ż ź; ciągłość alfabetyczna w obrębie punktu | `,` (ostatnia `;` albo `.`; przy części wspólnej — znak po części wspólnej) | `lit. x` | § 57 ust. 4; po wyczerpaniu: `za)`, `zb)` … `zz)`, `zza)`, `zzb)` … (§ 57 ust. 5) |
| Tiret | myślnik na początku wiersza | `,` (ostatnie `,`/`;`/`.`; przy części wspólnej — po niej) | słownie: `tiret pierwsze/drugie…` | § 57 ust. 6 |
| Podwójne tiret | myślnik (głębszy poziom) | jak tiret | `podwójne tiret pierwsze…` | § 57 ust. 7; wprowadzane w obrębie tiret (§ 56 ust. 4) |
| Zdanie | — | — | `art. N ust. M zdanie drugie` (numer porządkowy zdania) | § 59 ust. 2 |

**Hierarchia zagnieżdżenia**: art./§ → ust. → pkt → lit. → tiret → podwójne tiret (§ 56, § 124 ust. 2).

**Kolejność powołań, bez przecinków między jednostkami**: `art. … ust. … pkt … lit. … tiret … podwójne tiret …` (§ 59 ust. 1).

**Wyliczenia i część wspólna (§ 56)**: w obrębie artykułu (ustępu) z wyliczeniem wyróżnia się *wprowadzenie do wyliczenia* (kończy się dwukropkiem) oraz punkty; wyliczenie MOŻE kończyć się *częścią wspólną* odnoszącą się do wszystkich punktów. Po części wspólnej NIE dodaje się kolejnej samodzielnej myśli (nowa myśl = nowy ustęp). Analogicznie dla liter/tiret/podwójnych tiret.

**Układ graficzny (§ 58)** — sygnały segmentacji:
- każda jednostka redakcyjna od NOWEGO WIERSZA, oznaczenie w tym samym wierszu co treść;
- artykuły i ustępy zaczynają się od akapitu (wcięcie pierwszego wiersza);
- punkty, litery, tirety i podwójne tirety zaczynają się na wysokości początku wprowadzenia do wyliczenia (wcięcie niesie poziom hierarchii).

**Numery jednostek dodawanych nowelizacją (§ 89)**:
- do numeru dodaje się małą literę łacińską (bez polskich znaków), z ciągłością alfabetyczną: `art. 5a`, `ust. 2a`, `pkt 3a`, `lit. ba`;
- po wyczerpaniu liter: dwu- i wieloliterowo `Xa…Xz, Xza…Xzz, Xzza…` (§ 89 ust. 3);
- dodanie MIĘDZY jednostkami już literowanymi: „po art. Xa dodaje się art. Xaa (następnie Xab…Xaz, Xaza…Xazz)" (§ 89 ust. 4) → wzorzec sufiksu musi dopuszczać wielokrotne litery także w środku ciągu, nie tylko po wyczerpaniu alfabetu;
- **indeks górny** dopuszczalny, gdy akt już go stosuje (§ 89 ust. 5); fragment oznaczenia w indeksie górnym zapisuje się w NAWIASACH KWADRATOWYCH: `x[y]` (§ 89 ust. 6) — zbieżne z kanałem `[x]` emitowanym przez `ParagraphExtensions.GetFullText`.

## 3. Jednostki systematyzacyjne (§ 60–62, § 124a)

Hierarchia (ustawa): artykuły → **rozdziały** → **działy** → **tytuły**; opcjonalnie **oddziały** poniżej rozdziału. Tylko w kodeksach: tytuły → **księgi** → **części** (§ 60). Rozporządzenie: paragrafy → rozdziały → działy; oddziały wyjątkowo (§ 124a).

- Jednostki wyższego stopnia wolno użyć tylko, gdy użyto niższych (§ 61) — sanity-check hierarchii.
- **Numeracja**: rozdziały i oddziały — cyfry ARABSKIE; część/księga/tytuł/dział — cyfry RZYMSKIE (§ 62 ust. 1).
- **Wzorzec dwuwierszowy** (§ 62 ust. 2): wiersz 1 = oznaczenie („część"/„księga"/„tytuł"/„dział"/„rozdział"/„oddział" + liczba porządkowa), wiersz 2 = tytuł jednostki od nowego wiersza, rozpoczęty wielką literą.
- Wydzielone przepisy ogólne oznacza się nazwą „Przepisy ogólne" (§ 20 ust. 2); grupa przepisów zmieniających: „Zmiany w przepisach" (§ 97 ust. 1).
- Załączniki: wprowadzane w części artykułowej; przepis wprowadzający określa zakres treści załącznika (§ 15a).

## 4. Kolejność elementów aktu (segmentacja semantyczna)

**Ustawa (§ 14–15)**: tytuł → przepisy merytoryczne (ogólne, szczegółowe) → przepisy zmieniające → przepisy epizodyczne → przepisy przejściowe i dostosowujące → przepisy końcowe (uchylające → o utracie mocy obowiązującej → o wejściu w życie; § 38).

**Przepisy szczegółowe — kolejność wewnętrzna (§ 24)**: prawo materialne → przepisy ustrojowe → proceduralne → o administracyjnych karach pieniężnych i karne.

**Ustawa wprowadzająca (§ 48)**: wejście w życie ustawy „głównej" → zmieniające → uchylające → epizodyczne/przejściowe/dostosowujące → wejście w życie ustawy wprowadzającej.

**Rozporządzenie**: tytuł → podstawa prawna („Na podstawie art. …") → przepisy; przepis ogólny może zaczynać się „Rozporządzenie określa …" (§ 125 ust. 1 pkt 1).

## 5. Katalog formuł rozpoznawczych (frazy-sygnatury)

### 5.1 Podstawa prawna i formuła kompetencyjna
- `Na podstawie art. … ustawy … (tytuł + publikator) zarządza się, co następuje:` — rozporządzenie (§ 121 ust. 3);
- uchwała/zarządzenie: tekst rozpoczyna wskazanie przepisu podstawy (§ 139); w praktyce formuły „uchwala się, co następuje:" / „postanawia się, co następuje:" (ZTP nie narzuca czasownika dla uchwał — sygnał pomocniczy, nie normatywny).

### 5.2 Wejście w życie — zamknięty katalog brzmień (§ 45 ust. 1)
- „Ustawa wchodzi w życie po upływie 14 dni od dnia ogłoszenia";
- „… po upływie … (dni, tygodni, miesięcy, lat) od dnia ogłoszenia";
- „… z dniem następującym po dniu ogłoszenia";
- „… pierwszego dnia miesiąca następującego po miesiącu ogłoszenia" / „… dnia … miesiąca następującego po miesiącu ogłoszenia";
- „… pierwszego dnia miesiąca następującego po upływie … od dnia ogłoszenia";
- „… z dniem … (data kalendarzowa)";
- „… z dniem ogłoszenia";
- „… wchodzi w życie …, z wyjątkiem art. …, który wchodzi w życie …";
- „… w terminie określonym w ustawie … (ustawa wprowadzająca)".
Liczby: cyframi arabskimi albo słownie; dzień i miesiąc z wariantu „pierwszego dnia miesiąca…" — słownie (§ 45 ust. 2). Moc wsteczna: „…, z mocą od dnia …" (§ 51). Utrata mocy: „Ustawa obowiązuje do dnia …" / „Przepisy art. … tracą moc …" (§ 52 ust. 3).

### 5.3 Przepisy uchylające i przejściowe (wzory brzmień)
- „Traci moc ustawa … (tytuł)." (§ 40 ust. 1); wyjątkowo: „Tracą moc wszelkie dotychczasowe przepisy dotyczące spraw uregulowanych w ustawie; w szczególności tracą moc …" (§ 39 ust. 2);
- „W ustawie … (tytuł) uchyla się …" (§ 41 ust. 2);
- „W sprawach … stosuje się art. … ustawy …[, w brzmieniu dotychczasowym]." (§ 31); „nie dłużej niż …" (§ 31 ust. 2);
- „Dotychczasowe przepisy wykonawcze wydane na podstawie art. … zachowują moc / tracą moc [z dniem wejścia w życie przepisów wykonawczych wydanych na podstawie …, jednak nie później niż …]" (§ 33).

### 5.4 Upoważnienia (§ 63–74)
- obligatoryjne: „… (organ) określi, w drodze rozporządzenia, …"; fakultatywne: „… może określić, w drodze rozporządzenia, …" (§ 68 ust. 3);
- warunkowe: „W razie wystąpienia …, … określi, w drodze rozporządzenia, …" (§ 69);
- współuczestniczenie: „… w porozumieniu z …", „… po zasięgnięciu opinii …", „… na wniosek …" (§ 74).

### 5.5 Definicje, skróty, odesłania (§ 146–160)
- „Użyte w ustawie określenie … oznacza …" / „W rozumieniu ustawy określenie … oznacza …" / „Ilekroć w ustawie jest mowa o …, należy przez to rozumieć …" (§ 151, § 148);
- skrót: „… (pełne określenie), zwane dalej „…"" (§ 154 ust. 3); wydzielony fragment „Objaśnienia określeń ustawowych" (§ 150 ust. 3);
- wyliczenie przykładowe: „w szczególności" (§ 153 ust. 3);
- odesłanie przedmiotowe: „Do … stosuje się odpowiednio przepisy o …" (§ 156 ust. 4); odesłanie statyczne: „… w brzmieniu z dnia …" (§ 160);
- odesłania uzupełniające/odmienne: „w sprawach … w zakresie nieuregulowanym niniejszą ustawą stosuje się przepisy ustawy …" / „ustawy nie stosuje się do … w zakresie uregulowanym ustawą …" (§ 22 ust. 3).

### 5.6 Granice swobody (§ 155 ust. 4)
„… nieprzekraczające …" / „… nie więcej niż …" / „… nie niższej niż …" / „nie mniej niż …".

### 5.7 Przepisy karne i o administracyjnych karach pieniężnych (§ 75–81a) — sygnatury typu przepisu
- adresat powszechny normy: przepis otwiera wyraz `Kto` (§ 144 ust. 1); określenia adresata nie poprzedza się wyrazem „każdy" (§ 144 ust. 5) — „Kto …" to typowy początek przepisu karnego;
- odesłania karne: „Kto wbrew przepisom art. …" (§ 75 ust. 2); „Tej samej karze podlega, kto …" (§ 78 ust. 2);
- sankcje: „… podlega karze … albo karze … [albo obu tym karom łącznie]" / „… podlega karze … i karze …" (§ 79);
- tryb: „Orzekanie w sprawach o czyny, o których mowa w art. …, następuje w trybie przepisów Kodeksu postępowania …" (§ 81);
- kary administracyjne: „administracyjnej kary pieniężnej nie nakłada się" / „nie podlega administracyjnej karze pieniężnej" (§ 81a ust. 4);
- **sygnał klasyfikacyjny**: rozporządzenie NIE może zawierać przepisów karnych ani o administracyjnych karach pieniężnych, ani odesłań do nich (§ 117) — ich obecność przemawia przeciw kwalifikacji jako rozporządzenie.

## 6. Nowelizacje (Dział II, § 82–97) — kluczowe dla parsera

**Zakres stosowania**: Dział II stosuje się ODPOWIEDNIO do rozporządzeń (§ 132), uchwał i zarządzeń (§ 141) oraz aktów prawa miejscowego (§ 143). Jednostką bazową komend jest wtedy paragraf, więc formuły przyjmują postać: „W rozporządzeniu … wprowadza się następujące zmiany:", „w § X …", „po § X dodaje się § Xa w brzmieniu: …", „§ X otrzymuje brzmienie: …", „uchyla się § X". Analogicznie wejście w życie: „Rozporządzenie/Uchwała/Zarządzenie wchodzi w życie …" (§ 45 odpowiednio). Parser komend nowelizacyjnych MUSI obsługiwać oba warianty tokenów (`art.` i `§`) oraz oba rzeczowniki („W ustawie" / „W rozporządzeniu" / „W uchwale" / „W zarządzeniu").

### 6.1 Komendy nowelizacyjne (triggery)
- otwarcie serii zmian: `W ustawie … (tytuł) wprowadza się następujące zmiany: …` (§ 85 ust. 2);
- pojedyncze zmiany: `W ustawie … uchyla się art. …` / `W ustawie … po art. X dodaje się art. Xa w brzmieniu: …` (§ 85 ust. 3); `W ustawie … art. X otrzymuje brzmienie: …` / `w art. X … otrzymuje brzmienie: …` (§ 85 ust. 4–5);
- dodawanie: `po art. X dodaje się art. Xa w brzmieniu: …` (§ 89 ust. 1); `w art. … po ust. X (pkt X, lit. X) dodaje się ust. Xa (pkt Xa, lit. Xa) w brzmieniu: …`; na końcu jednostki: `w art. … w ust. … dodaje się pkt … (lit. …) w brzmieniu: …` — przed oznaczeniem każdej kolejnej jednostki przyimek „w" (§ 89 ust. 2); na początku jednostki systematyzacyjnej: `w (oznaczenie jednostki) dodaje się art. … w brzmieniu: …` (§ 89a);
- zmiany drobne bez przytaczania pełnego brzmienia (§ 87 ust. 3): `wyrazy „…" zastępuje się wyrazami „…"`; `po wyrazach „…" dodaje się wyrazy „…"`; `skreśla się wyrazy „…"`;
- zmiana rozproszona (§ 88 ust. 1): `skreśla się użyte w art. …, w różnej liczbie i różnym przypadku, wyrazy „…"` / `użyte w art. …, w różnej liczbie i różnym przypadku, wyrazy „…" zastępuje się użytymi w odpowiedniej liczbie i odpowiednim przypadku wyrazami „…"`;
- wzór/załącznik: nowe brzmienie całego wzoru albo wyodrębnionej części (§ 87a).

**Rozróżnienie „uchyla się" vs „skreśla się"**: jednostki redakcyjne się *uchyla*, wyrazy/liczby/znaki się *skreśla* (§ 85 ust. 3 vs § 87 ust. 3 pkt 3).

### 6.2 Struktura ustawy zmieniającej (§ 94–95) — koduje głębokość zmiany
- wszystkie zmiany jednej ustawy w JEDNYM artykule (§ 94 ust. 1); zmiana kilku ustaw = osobny artykuł na każdą (§ 95);
- każdy nowelizowany artykuł → oddzielny **punkt**; zmiany w jednostkach niższego stopnia w obrębie artykułu → oddzielna **litera** na jednostkę tego samego stopnia; kolejny niższy stopień → **tiret**, dalej **podwójne tiret** (§ 94 ust. 2);
- zmiany kolejno następujących po sobie jednostek można ująć w jeden punkt/literę/tiret (§ 94 ust. 3);
- **wniosek dla parsera**: poziom wyliczenia, w którym stoi komenda, odpowiada instrumentowi zmiany (odpowiedniki stylów `Z/`, `Z_LIT/`, `Z_TIR/`, `Z_2TIR/`).

### 6.3 Treść nowego brzmienia
- przytaczana w CUDZYSŁOWIE po dwukropku komendy („…"; typograficznie `„` U+201E … `”` U+201D); zamknięcie cytatu + interpunkcja komendy (`;` między zmianami, `.` na końcu artykułu zmian);
- zmieniany przepis przytacza się w PEŁNYM nowym brzmieniu (§ 87 ust. 1); można poprzestać na zmienianych jednostkach niższych lub zdaniach (§ 87 ust. 2);
- nie nowelizuje się przepisów zmieniających (§ 91 ust. 1; wyjątek: w vacatio legis — § 91 ust. 2) ani aktu wykonawczego o odroczonej utracie mocy (§ 34);
- nowelizacja dorozumiana zakazana (§ 86) — zmiana zawsze przez wyraźną komendę.

## 7. Tekst jednolity i obwieszczenia (Dział III)

- TJ jest ZAŁĄCZNIKIEM do obwieszczenia (§ 101); tytuł obwieszczenia: „w sprawie ogłoszenia jednolitego tekstu …" (§ 102 ust. 4); treść wg wzoru z § 104 (w tym pkt 2: „tekst jednolity nie obejmuje: art./§ …, które stanowią: „…"" — cytaty przepisów pominiętych!).
- **Część wstępna obwieszczenia (§ 103)**: przed załącznikiem wymienia się (1) akty, które wprowadziły zmiany do tekstu pierwotnego/ostatniego TJ, (2) orzeczenia TK stwierdzające niezgodność przepisów, (3) obwieszczenia o sprostowaniu błędów; ponadto zamieszcza się treść przepisów zmieniających oraz przepisów o wejściu w życie i epizodycznych/przejściowych/dostosowujących aktów zmieniających, jeżeli mają związek ze zmianami (§ 103 ust. 2); w obwieszczeniu o TJ rozporządzenia — także odnośniki o wdrożeniu prawa UE i notyfikacji (§ 103 ust. 3). To duży, rozpoznawalny blok cytatów przepisów PRZED właściwym tekstem aktu.
- **Termin ogłoszenia TJ** — fraza-sygnatura w ustawie zmieniającej/wprowadzającej: „Ogłoszenie tekstu jednolitego ustawy … nastąpi w terminie … miesięcy (dni) od dnia ogłoszenia niniejszej ustawy" (§ 98).
- **Numeracja TJ**: zachowuje się numerację tekstu pierwotnego + dodaną przez nowele; **NIE wprowadza się ciągłości numeracji** (§ 106 pkt 1) → walidator ciągłości numeracji MUSI tolerować luki w TJ.
- **Markery w miejscu jednostek**: `(uchylony)` w odpowiednim rodzaju (§ 106 pkt 2); `(utracił moc)` — orzeczenie TK (§ 106b ust. 2); `(uznany za nieważny)` — akty prawa miejscowego (§ 106b ust. 4).
- **Odnośniki** (przypisy dolne) do jednostek — wzory brzmień (§ 106 pkt 2–5): „Przez art. … (uchylenie)", „W brzmieniu ustalonym przez art. …", „Ze zmianą wprowadzoną przez art. …", „Dodany przez art. …" — zawsze z tytułem ustawy zmieniającej, publikatorem i datami wejścia w życie.
- Zmiany przyszłe (jeszcze nieobowiązujące) w TJ: jednostka w przyszłym brzmieniu (lub oznaczenie + „(uchylony)") POGRUBIONĄ czcionką (§ 106a ust. 4) + odnośnik „W tym brzmieniu obowiązuje do wejścia w życie zmiany, o której mowa w odnośniku …" (§ 106a ust. 2) albo — dla przyszłego uchylenia — „Obowiązuje do wejścia w życie zmiany, o której mowa w odnośniku …" (§ 106a ust. 3).
- Sprostowania w TJ: odnośniki „W brzmieniu ustalonym przez obwieszczenie …" / „Ze zmianą wprowadzoną przez obwieszczenie …" (§ 106c). Akt, do którego odsyła TJ, a który utracił moc — tytuł kursywą + odnośnik „Ustawa utraciła moc na podstawie art. …" (§ 108a ust. 1); uchylony przepis, do którego następuje odesłanie — kursywą + „Uchylony przez art. …" (§ 108a ust. 2).
- **Organy/instytucje w TJ (§ 108b)**: nazwa zlikwidowanego/zniesionego organu — kursywą + odnośnik „Zlikwidowany/zniesiony przez art. … (tytuł ustawy, publikator …)"; przekształconego — kursywą dotychczasowa nazwa + odnośnik „Obecnie … (aktualna nazwa), na podstawie art. … (…)".
- **Zmiany formalne w TJ bez odnośnika (§ 107)** — walidator diff TJ↔tekst pierwotny musi je tolerować: poprawa ortografii, oczywistych omyłek pisarskich (błędny/przestawiony/opuszczony/powtórzony znak), aktualizacja publikatora powołanego aktu (wg § 158), aktualizacja przedmiotu powołanego aktu (ta jedna JEST omawiana odnośnikiem — § 107 ust. 3), dostosowanie zapisu do § 57 ust. 3–4 i § 89 ust. 6.
- **Sprostowanie błędu — drugi wariant (§ 112 ust. 3a)**: przy pominięciu jednostki/załącznika: „… obwieszcza się, że w … po … (oznaczenie jednostki albo załącznika) powinien być … o następującej treści „…"". Jedno obwieszczenie może prostować wiele błędów, także w różnych aktach (§ 112 ust. 4).
- **Odnośniki graficznie (§ 163)**: cyfry arabskie z nawiasem z prawej strony, umieszczone w INDEKSIE GÓRNYM po wyrażeniu (`tekst¹⁾`); kilka odnośników rozdziela się przecinkami. W kanale tekstowym parsera: `[N)]`.

## 8. Publikatory i odesłania (§ 158, § 161a, § 162)

**Skróty dzienników (§ 162 ust. 2)**: `Dz. U.` (Dziennik Ustaw), `M.P.` (Monitor Polski), `Dz. Urz. UE`, `Dz. Urz. UE Polskie wydanie specjalne`, `Dz. Urz. WE`, `M.S.G.` (Monitor Sądowy i Gospodarczy), `Dz. Urz. Min. …`, `Dz. Urz. <skrót urzędu>`, `Dz. Urz. Woj. …`.

**Format przytoczenia publikatora (§ 158 ust. 7)**: `(Dz. U. poz. … i …, z … r. poz. … oraz z … r. poz. …)`; rocznik podawany, gdy inny niż rok aktu albo gdy TJ: `(Dz. U. z … r. poz. …)`; przy >5 zmianach: `…, z późn. zm.` + odnośnik wymieniający wszystkie zmiany (§ 158 ust. 5–6).

**Zasady przytaczania**: pełny tytuł aktu + publikator tylko przy PIERWSZYM odesłaniu; kolejne odesłania — sam tytuł (§ 158 ust. 2). W rozporządzeniu pełny publikator ustawy tylko w podstawie prawnej (§ 123).

**Akty UE (§ 161a)**: pełny tytuł + `(Dz. Urz. UE <seria> <numer> z dd.mm.rrrr[, str. …])`; nowelizowane: `z późn. zm.`; sprzed 1.05.2004: dodatkowo `Dz. Urz. UE Polskie wydanie specjalne, rozdz. …, t. …, str. …`.

**Odesłania wewnętrzne — format powołań**: patrz § 59 (sekcja 2). Odesłanie dynamiczne = domyślne (§ 159); statyczne z „w brzmieniu z dnia …" (§ 160).

## 9. Wskazówki klasyfikacyjne — rozstrzyganie rodzaju aktu

1. **Ustawa vs rozporządzenie**: nagłówek (`USTAWA` vs `ROZPORZĄDZENIE` + organ); jednostka podstawowa (`Art.` vs `§`); rozporządzenie MUSI mieć formułę „Na podstawie art. … zarządza się, co następuje:" (§ 121), ustawa jej nie ma; przedmiot „w sprawie …" tylko w rozporządzeniu (§ 120 ust. 6).
2. **Kodeks**: `USTAWA` + przedmiot „Kodeks …" (§ 19 pkt 2) + ustępy oznaczane `§` (§ 55 ust. 3) + ewentualnie księgi/części (§ 60 ust. 3).
3. **Akt zmieniający**: tytuł wg § 96/§ 129 ust. 2/§ 138a ust. 3 + komendy z sekcji 6.1; pojedynczy przepis zmieniający może też wystąpić w ustawie „zwykłej" (§ 83 pkt 2).
4. **Tekst jednolity**: nagłówek `OBWIESZCZENIE` + „w sprawie ogłoszenia jednolitego tekstu" + formuła § 104 + markery `(uchylony)`/`(utracił moc)` + gęste odnośniki `N)` w indeksie górnym + numeracja z lukami i jednostkami literowymi (5a, 5b). UWAGA: załącznik TJ zawiera pełny akt z własnym nagłówkiem (`USTAWA`/`ROZPORZĄDZENIE`) — pierwszy nagłówek dokumentu rozstrzyga.
5. **Uchwała/zarządzenie**: nagłówek „Uchwała/Zarządzenie [nr]" (§ 138a) + start od podstawy prawnej (§ 139); nazwa rodzaju wyłącznie „uchwała"/„zarządzenie" (§ 140).
6. **Akt prawa miejscowego**: forma uchwały/zarządzenia + organ JST (rada gminy/powiatu, sejmik, wojewoda…) + `Dz. Urz. Woj.`.
7. **Sygnały negatywne (nie-akt)**: brak nagłówka rodzaju i daty wg § 16–17; brak jednostek redakcyjnych wg § 57; tekst narracyjny; apele/postulaty/uzasadnienia (w akcie zakazane — § 11); brak formuły wejścia w życie — sygnał POMOCNICZY: ustawa zamieszcza przepis o wejściu w życie, CHYBA ŻE termin określa ustawa wprowadzająca (§ 43), więc sam brak nie dyskwalifikuje (por. sekcja 10).

## 10. Pułapki i przypadki brzegowe dla parsera

- **Litera vs wieloliterowe oznaczenia**: `za)`, `zzb)` to legalne litery (§ 57 ust. 5) — wzorzec liter musi dopuszczać 1–n małych liter łacińskich (bez polskich znaków).
- **Tiret vs część wspólna**: oba zaczynają się myślnikiem od nowego wiersza; różnicuje kontekst (tiret = element wyliczenia w obrębie litery, kończy się przecinkiem; część wspólna zamyka wyliczenie i kończy się kropką/średnikiem — § 56, § 57).
- **Numeracja z wstawkami**: następnik `n` → `n+1` LUB `n` → `na` (§ 89); w TJ dodatkowo luki po uchyleniach (§ 106 pkt 1).
- **Indeks górny**: `x[y]` w zapisie znormalizowanym (§ 89 ust. 6); odnośniki przypisów też są w indeksie górnym (§ 163) — nie mylić z oznaczeniem jednostki.
- **Cudzysłowy zagnieżdżone**: treść nowelizacji w „…" może zawierać własne cytaty i własne komendy nowelizacyjne (nowelizacja przepisu zmieniającego — dopuszczalna w vacatio legis, § 91 ust. 2); bilans cudzysłowów konieczny do delimitacji.
- **„W ustawie … wprowadza się następujące zmiany:"** może wystąpić zarówno w ustawie zmieniającej (całość), jak i jako pojedynczy przepis zmieniający w zwykłej ustawie (§ 83, § 97) — nie przesądza rodzaju aktu.
- **Data w tytule**: miesiąc zawsze słownie (§ 17) — data z miesiącem cyfrowym w „tytule" to silny sygnał nie-aktu lub uszkodzonej ekstrakcji.
- **Przepis o wejściu w życie może być nieobecny** w ustawie „głównej", gdy termin określa ustawa wprowadzająca (§ 43) — brak § 45 nie dyskwalifikuje samodzielnie.
- **Rozporządzenie wydaje się na podstawie JEDNEGO upoważnienia** (§ 119a) — w podstawie prawnej jeden przepis art. (ew. kilka ustępów, § 121 ust. 2).
