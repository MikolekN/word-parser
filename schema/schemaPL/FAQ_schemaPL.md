
# Dlaczego stosowane jest `xs:enumeration value="" zamiast minOccurs="0"`

Odpowiedź leży w praktyce redakcyjnej — niektóre edytory XML i systemy CMS generują element z pustą zawartością zamiast go pominąć. Zastosowana konstrukcja zapewnia, że dokument przejdzie walidację, zamiast odrzucać go z błędem technicznym mimo semantycznej poprawności.

# Dlaczego używać `xs:whiteSpace value="preserve"`?

Użycie powyższego sprawia, że wszystkie białe znaki w wartości elementu są przekazywane bez żadnej modyfikacji:

```
xml
<Uwagi>Treść pierwszego wiersza
treść drugiego wiersza
    wcięty wiersz trzeci</Uwagi>
```
Aplikacja odczyta dokładnie taki łańcuch — ze znakami \n i spacjami w oryginalnych pozycjach.
Ważny niuans — `xs:string` już to robi domyślnie

Trzy możliwe wartości fasety `xs:whiteSpace`:
- preserve: wszystkie białe znaki zachowane bez zmian; domyślne dla: xs:string
- replace: każdy \t, \n, \r zastąpiony spacją; domyślne dla: xs:normalizedString
- collapse: ciągi spacji zwinięte do jednej, przycięte z brzegów; domyślne dla: xs:token, xs:date, ...
Oznacza to, że xs:string domyślnie zachowuje łamania linii — i deklaracja xs:whiteSpace value="preserve" w restrykcji bazującej na xs:string jest technicznie redundantna. To po co ją pisać? Właśnie dlatego, że jest redundantna — ale intencjonalnie.
Pełni funkcję:
- dokumentacyjną — czytelnik schematu od razu widzi, że zachowanie spacji jest celowe, a nie przeoczone 
- ochronną — jeśli ktoś zmieni typ bazowy w przyszłości (np. na xs:normalizedString), faseta preserve zablokuje utratę białych znaków 
- kontraktową — jawna deklaracja jest częścią kontraktu schematu z aplikacją

# Co to jest gYear?

xs:gYear to rok w sensie kalendarzowym. Prefiks g pochodzi od Gregorian (kalendarz gregoriański), ale xs:gYear oznacza po prostu rok jako wartość częściową daty — liczbę całkowitą reprezentującą rok w formacie YYYY, np. 2024. Użycie xs:gYear zamiast xs:integer to świadoma decyzja, która mówi: „ta wartość jest rokiem w sensie czasowym, nie etykietą numeryczną" — co jest dokładnie prawdą w kontekście roku wydania dziennika urzędowego.

# Po co jest sekcja Typografia?

Sekcja typografia rozwiązuje bardzo konkretny problem — oddzielenie struktury prawnej dokumentu od jego wyglądu podczas druku.
Na czym polega problem
Akt prawny w postaci XML opisuje strukturę (artykuł, ustęp, punkt) — ale system skład/drukarnia i tak musi wiedzieć, jak to wydrukować. Bez sekcji typografii redaktor musiałby albo ufać domyślnym ustawieniom drukarni, albo wpisywać wartości formatowania jako zwykły tekst, bez walidacji.
Sekcja typografia dostarcza walidowanych atrybutów CSS-podobnych bezpośrednio na elementach XML aktu. Walidator sprawdzi, że margin-top="12pt" jest poprawne, ale margin-top="duże" — już nie.
Jakie atrybuty są dostępne
Atrybuty dzielą się na trzy grupy:

Odstępy i wcięcia — wartości w punktach typograficznych (pt):
- margin-top, margin-bottom, margin-left — marginesy akapitu
- line-height — interlinia
- text-indent — wcięcie pierwszego wiersza (może być ujemne, np. −12pt dla wysięku)

Łamanie stron i wierszy — wartości true/false:
- znastepnym — ten akapit musi być w tym samym bloku co następny (np. nagłówek nie może wisieć na końcu strony)
- wierszerazem — wszystkie wiersze akapitu muszą być na jednej stronie
- nowastrona — wymuś nową stronę przed tym akapitem
- bezprzenoszenia — zakaz dzielenia wyrazów w tym akapicie

Wyrównanie i tabulatory:
- text-align — left, right, center, justify
- tabulatory — lista pozycji tabulatora z typem (np. lewy=36pt,kropki=120pt,prawy=400pt)

Przykład: fragment artykułu z adnotacjami typograficznymi:
```aiignore
<art id="art1">
  <num>Art. 1.</num>
  <akapit margin-top="6pt" margin-bottom="3pt" znastepnym="true"/>
  <p>
    Ustawa określa zasady funkcjonowania krajowego systemu
    cyberbezpieczeństwa.
  </p>

  <ust id="art1ust1">
    <num>1.</num>
    <akapit text-indent="-12pt" margin-left="24pt" line-height="14pt"/>
    <p>Krajowy system cyberbezpieczeństwa obejmuje:</p>

    <pkt id="art1ust1pkt1">
      <num>1)</num>
      <akapit text-indent="-18pt" margin-left="36pt"
              bezprzenoszenia="true"/>
      <p>operatorów usług kluczowych;</p>
    </pkt>

    <pkt id="art1ust1pkt2">
      <num>2)</num>
      <akapit text-indent="-18pt" margin-left="36pt"/>
      <p>dostawców usług cyfrowych.</p>
    </pkt>
  </ust>
</art>
```
Atrybut `znastepnym="true"` na numerze artykułu gwarantuje, że Art. 1. nigdy nie pojawi się samotnie na końcu strony — system składu musi umieścić go razem z przynajmniej pierwszym wierszem treści. Wartości `text-indent="-12pt"` przy `margin-left="24pt"` to klasyczny wzorzec wysiękowego numerowania — numer 1. wychodzi 12 pt w lewo poza lewy margines bloku tekstu, co odpowiada standardowemu układowi Dziennika Ustaw.

# Czy atrybuty typograficzne są obowiązkowe w wersji xml aktu?
Nie — wszystkie atrybuty typograficzne są opcjonalne, więc zunifikowana schema nie wymusza typografii na nikim.
Oznacza to, że zunifikowana schema de facto zachowuje elastyczność — system zewnętrzny może walidować dokument bez żadnych atrybutów typograficznych i walidacja przejdzie pomyślnie.
## Praktyczny skutek
```xml
<!-- Oba artykuły są poprawnie zapisane względem zunifikowanej schemy: -->
<!-- Wersja bez typografii -->
<artykul GUID="art-001">
  <nr>1</nr>
  <p>Ustawa wchodzi w życie...</p>
</artykul>
<!-- Wersja z typografią -->
<artykul GUID="art-001" text-align="justify" margin-left="10mm">
  <nr>1</nr>
  <p wyrownanie="justify">Ustawa wchodzi w życie...</p>
</artykul>
```
Zatem zarzut o zanieczyszczeniu treści typografią traci na sile — skoro atrybuty są opcjonalne. Systemy zewnętrzne mogą po prostu ignorować atrybuty typograficzne, które ich nie dotyczą, a dokumenty bez typografii walidują się bez problemu.

# Po co jest grupa atrybutów `typoSpecjalne`? #

`typoSpecjalne` to uproszczony sposób przypisania pełnego zestawu atrybutów typograficznych bezpośrednio do elementu, który sam jest akapitem — bez potrzeby dodawania pośredniego podelemetu <akapit>.
## Czym różni się `typoSpecjalne` od `formatowanieAkapituPelne`

Na pierwszy rzut oka obie grupy zawierają identyczny zestaw atrybutów typograficznych. Różnica jest subtelna, ale istotna:


|  | `formatowanieAkapituPelne` | `typoSpecjalne` |
| :-- | :-- | :-- |
| Forma | **Grupa elementu** — opakowuje `<akapit>` | **Grupa atrybutów** — stosowana bezpośrednio na elemencie |
| Użycie | `<akapit margin-top="6pt"/>` jako **podelement** | `<p margin-top="6pt">` — atrybuty **na samym elemencie** |
| `form=` | brak (qualified) | `unqualified` na każdym atrybucie |

## Kiedy `typoSpecjalne` ma zastosowanie

Grupa `typoSpecjalne` jest przeznaczona dla elementów, które **same w sobie są akapitem** i nie mogą (lub nie powinny) posiadać dodatkowego pod-elementu `<akapit>`. Przykład typowy to nagłówki jednostek systematyzacyjnych:

```xml
<!-- Z typoSpecjalne — atrybuty bezpośrednio na elemencie nagłówka -->
<naglowek-rozdzialu
    margin-top="12pt"
    margin-bottom="6pt"
    text-align="center"
    wierszerazem="true">
  Rozdział 1
  Przepisy ogólne
</naglowek-rozdzialu>
```

Gdyby użyto wzorca z `<akapit>`, wyglądałoby to tak:

```xml
<!-- Wzorzec z formatowanieAkapituPelne — tutaj nieelegancki -->
<naglowek-rozdzialu>
  <akapit margin-top="12pt" text-align="center"/>
  Rozdział 1
  Przepisy ogólne
</naglowek-rozdzialu>
```

Dla nagłówka, który **jest** pojedynczym akapitem, wstawianie podelementy `<akapit>` tylko po to, żeby nieść atrybuty, jest sztucznie skomplikowane.

# Czym jest `<xs:element name="typografia">` ?

To element **wstawiany bezpośrednio w treść aktu** — w środek akapitu, między innymi elementami takimi jak `<b>`, `<i>`, `<sup>`, `<pole>`. Widzisz go w grupie `zawartosc-tekstowa-bez-cytatu` razem z wyróżnieniami typograficznymi .
Nie jest to więc deklaracja ustawień na zewnątrz tekstu, lecz **znacznik osadzony wewnątrz zdania**.

## Do czego służą jego dwa atrybuty

`nieprzenosic` — zakaz przenoszenia tekstu do następnej linii **w tym konkretnym miejscu**. Odpowiada funkcji nierozdzielnej spacji lub twardego łącznika — np. między liczbą a jednostką miary, żeby `12` i `pt` nie rozeszły się do różnych wierszy:

```xml
<p>
  Wejście w życie następuje po upływie
  14<typografia nieprzenosic="true"/>dni
  od dnia ogłoszenia.
</p>
```
`ściągnięcie` — nakaz złączenia dwóch sąsiednich słów bez spacji (ligatura prezentacyjna). Używane gdy dwa odrębne elementy XML muszą wyglądać w druku jak jeden ciągły ciąg znaków bez przerwy:
```xml
<p>
  na podstawie art.<typografia sciagniecie="true"/><b>3</b>
  ustawy
</p>
```
Bez `sciagniecie` między `art.` a pogrubionym `3` mogłaby pojawić się spacja wynikająca z granic elementów XML.
## Dlaczego to element, a nie atrybut

Ponieważ `<akapit>` i grupy atrybutów sterują formatowaniem **całego akapitu**. Element `<typografia>` natomiast działa **punktowo** — w jednym miejscu w środku zdania, między konkretnym słowem a następnym. Nie ma sensu opisywać go jako atrybut akapitu, bo dotyczy konkretnego miejsca w ciągu tekstu, nie całego bloku. Można go rozumieć jako odpowiednik niewidocznych znaków sterujących (takich jak `&nbsp;` czy twarda spacja w edytorach tekstu) — tyle że tu mamy do czynienia z walidowanym, semantycznym elementem XML.

# Co robi `<xs:group name="e-podpisy">` ?
Grupa `e-podpisy` to miejsce na uwierzytelnienie elektroniczne dokumentu XML zgodne ze standardem W3C XML Digital Signature (XMLDSig).
## Czym się różni od grupy `<podpisy>`?
`<podpisy>` to drukowana pieczęć na końcu dokumentu — wskazanie kto wydał akt i z kim w porozumieniu. To element redakcyjny/typograficzny.

`<e-podpisy>` to miejsce na rzeczywisty podpis elektroniczny organu, weryfikowalny kryptograficznie.

## Jak to działa technicznie

Standard XMLDSig (`http://www.w3.org/2000/09/xmldsig#`) definiuje element `<Signature>`, który zawiera kryptograficzny skrót całego dokumentu. Po podpisaniu dokumentu wygląda to tak:
```xml
<akt>
  <metadane>…</metadane>
  <tresc>…artykuły…</tresc>

  <!-- podpisy ludzkie (widoczne w druku) -->
  <podpisy>
    <organ-wydajacy>PREZES RADY MINISTRÓW</organ-wydajacy>
  </podpisy>

  <!-- pieczęć elektroniczna (niewidoczna w druku) -->
  <Signature xmlns="http://www.w3.org/2000/09/xmldsig#">
    <SignedInfo>
      <DigestValue>ABC123xyz…</DigestValue>
    </SignedInfo>
    <SignatureValue>XYZ987…</SignatureValue>
  </Signature>
</akt>
```
Jeśli ktokolwiek zmieni choćby jeden znak w treści aktu po podpisaniu — wartość `DigestValue` przestaje się zgadzać i podpis jest nieważny.
## Co oznacza `processContents="skip"`
To kluczowa decyzja projektowa. Schemat mówi walidatorowi: `„wiem, że tu będzie coś z namespace XMLDSig, ale nie sprawdzaj tego — po prostu przepuść"`. Powody:
- Struktura elementu `<Signature>` jest bardzo złożona i zmienia się między wersjami XMLDSig
- Walidacja kryptograficzna to zadanie dla biblioteki bezpieczeństwa, nie dla parsera XSD
- `processContents="lax"` lub `"strict"` wymagałoby dołączenia całego schematu XMLDSig

## Czy stoi w sprzeczności z `PodpisTyp` z metadanych?
Nie — to dwie różne warstwy, które się uzupełniają:

|  | `PodpisTyp` w `schemaPL.xsd`                   | `e-podpisy` (XMLDSig)                             |
| :-- |:-----------------------------------------------|:--------------------------------------------------|
| Gdzie | W metadanych, wewnątrz `<Organ>`               | Na końcu całego dokumentu                         |
| Co zawiera | Numer seryjny certyfikatu, osoba, data, zakres | Kryptograficzny skrót dokumentu                   |
| Kto czyta | System metadanych, redaktor, ISAP              | Oprogramowanie kryptograficzne (np. Adobe, ePUAP) |
| Cel | Opis kto i kiedy podpisał (dla człowieka)      | Dowód że dokument nie był zmieniany (dla maszyny) |
`PodpisTyp` odpowiada na pytania „kto podpisał i kiedy?", a `e-podpisy` odpowiada na pytanie *„czy dokument jest autentyczny?"*. Razem tworzą kompletną obsługę podpisu elektronicznego w polskim akcie prawnym.

# Jak wpisywać indeksy górne przy oznaczaniu jednostek redakcyjnych?
Należy używać znacznika `<sup></sup>`.

# Jakie zastosowanie ma `<xs:element name="pole">`?
`<pole>`** to „puste miejsce do wypełnienia" w szablonach aktów prawnych — jak pola w formularzu Worda.
Zamiast pisać ręcznie daty/kwoty/nazwy w każdym akcie, wstawiasz *placeholder*:

```
Ustawa wchodzi w życie <pole nazwa="data_wejscia">[tu data]</pole>.
```
**Edytor XML**:
1. Pokazuje `[tu data]` (domyślnie z `@data`).
2. **Auto-wypełnia** z metadanych (`@metadane="mdDataWejscia"`).
3. Eksport PDF: „Ustawa wchodzi w życie 01.05.2026."
## Kiedy jest stosowany?
**Tylko w szablonach/redakcji**, a nie w finalnych Dz.U.):
- Szablony ustaw: Data wejścia, kwoty kar, nazwy organów.
- Nowelizacje: „Art. X brzmi: [nowy tekst]".
- Metadane-linked: `@metadane` pobiera z np. `<Metadane/DataOgloszenia>`.
## Przykład użycia
```xml
<!-- Szablon -->
<p>
  Podpisano w Warszawie, dnia 
  <pole nazwa="data_podpisu" kod="DP" metadane="mdDataWydania" data="23 marca 2026 r.">
    [dd mmmm yyyy r.]
  </pole>
</p>
```
Po edycji metadanych wyświetlany będzie „23 marca 2026 r." (pobrany z metadanych). Sam PDF będzie prezentował czysty tekst bez `<pole>`.

# Dlaczego w cudzysłowie zamieszczony jest integer? 
`<attributeGroup name="cytat">` to zestaw atrybutów opisujący sposób wizualizacji cytatu innego aktu prawnego w treści nowelizacji lub obwieszczenia.
Używany jest na elementach `<cytat-strukt>` i elementach cytowanych w `cytat-wspolne` — wszędzie, gdzie przytaczamy fragment obcego aktu.
```xml
<cytat-strukt cudzyslowy="1">
  <artykul>
    <nr>5</nr>
    <p>Treść cytowanego przepisu...</p>
  </artykul>
</cytat-strukt>
```
## Dlaczego `xs:integer` (0–3)?
Ponieważ `@cudzyslowy` nie jest prostą flagą tak/nie, lecz koduje pozycję cudzysłowów co jest standardem typograficznym

| Wartość | Znaczenie           | Przykład |
| :-- |:--------------------| :-- |
| `0` | **Bez cudzysłowów** | Cytat bez oznaczeń |
| `1` | **Otwierający** „   | „Art. 5 ust. 1 brzmi: |
| `2` | **Zamykający** "    | treść cytatu"; |
| `3` | **Oba** „ "         | „cały cytat" |

# Jakie jest zastosowanie <xs:group name="cytat-wspolne">?

`<xs:group name="cytat-wspolne">` to zestaw elementów dozwolonych wewnątrz cytatu strukturalnego** — czyli fragmentu innego aktu prawnego przytaczanego w treści nowelizacji lub obwieszczenia. 
Grupa to `<xs:choice>` — dowolny z tych elementów może być zamieszczony wielokrotnie. Uwaga: tiret niższych stopni (potrójny, poczwórny itd.) zostały celowo wykluczone z cytowania.

## Kiedy ma zastosowanie?
Grupa ta jest stosowana **wewnątrz cytatu strukturalnego** — gdy jeden akt prawny przytacza treść innego:
```xml
<!-- Nowelizacja -->
<artykul>
  <nr>1</nr>
  <p>W ustawie z dnia 5 stycznia 2011 r. wprowadza się następujące zmiany:</p>
  <cytat-strukt cudzyslowy="3">
    <!-- Tu właśnie dozwolone są elementy z cytat-wspolne -->
    <czesc-wspolna-punkt>który działa w granicach prawa.</czesc-wspolna-punkt>
    <grafika href="zalacznik1.png"/>
    <odnosnik-cyt>...</odnosnik-cyt>
  </cytat-strukt>
</artykul>
```
## Różnica względem `kontynuacja-akapitu`
| Cecha | `cytat-wspolne`             | `kontynuacja-akapitu` |
| :-- |:----------------------------| :-- |
| **Kontekst** | Wewnątrz cytatu obcego aktu | Po akapicie własnego aktu |
| **Odnośniki** | `<odnosnik-cyt>` (cytowane) | Brak odnośników cytowanych |
| `<specjalny>` | Tak                         | Tak |
| `<czesc-wspolna-*>` | Tak (bez niższych tiret)    | Nie 
## Dlaczego części wspólne dla `artykuł`/`ustęp`/`paragraf` są nieobecne w `cytat-wspolne`?
Jednostki te zawierają ciągły tekst. Na potrzeby tej schemy, w odstępstwie od Zasad Techniki Prawodawczej i ze względów technicznych, przyjęto, że same w sobie nie posiadają części wspólnej. `cytat-wspolne` obsługuje natomiast elementy „końcówkowe" — to co pojawia się po wyliczeniu wewnątrz cytatu, a nie całe jednostki strukturalne.

# W schemie występują: `<xs:element name="odnosnik">`, `<xs:element name="punkt-odnosnik">` oraz `<xs:element name="litera-odnosnik">`. Jakie są wzajemne relacje między tymi elementami?
Relacja między tymi elementami to hierarchia strukturalna — `punkt-odnosnik` i `litera-odnosnik` są dziećmi `odnosnik`, a nie samodzielnymi odnośnikami.
## Hierarchia elementów
```
<odnosnik>              ← RODZIC: niezależny odnośnik (ma id-odnosnika)
  <p>Tekst główny</p>
  <punkt-odnosnik>      ← DZIECKO: punkt wewnątrz odnośnika
    <nr>1</nr>
    <p>Tekst punktu</p>
    <litera-odnosnik>   ← WNUK: litera wewnątrz punktu
      <nr>a</nr>
      <p>Tekst litery</p>
      <tiret-odnosnik>  ← PRAWNUK
        ...
      </tiret-odnosnik>
    </litera-odnosnik>
  </punkt-odnosnik>
</odnosnik>
```
## Dlaczego tylko `odnosnik` ma `id-odnosnika`
Ponieważ adresować (tworzyć odwołania) można wyłącznie cały odnośnik, nie jego wewnętrzne części.
```xml
<!-- To ma sens — link do całego odnośnika -->
<odsylacz-do-odnosnika id-odnosnika="ref1"/>
<!-- To NIE istnieje — nie linkujesz do punktu wewnątrz odnośnika -->
<odsylacz-do-odnosnika id-odnosnika="ref1-pkt1"/> 
```
`punkt-odnosnik` i `litera-odnosnik` to jednostki redakcyjne wewnętrzne — identyfikuje je ich pozycja (numer `<nr>`) w obrębie rodzica, nie globalny identyfikator.
## Czym się różnią typy bazowe

| Element | Typ bazowy | Co dodaje |
| :-- | :-- | :-- |
| `<odnosnik>` | `JednostkaRedakcyjnaTyp` + `tresc-odnosnika` | `@id-odnosnika`, `@symbol`, `@super` |
| `<punkt-odnosnik>` | `JednostkaRedakcyjnaNumerowanaProstaTyp` | `@ELI` (wzorzec `pkt[1-9]`) + opcjonalne litery |
| `<litera-odnosnik>` | `JednostkaRedakcyjnaNumerowanaProstaTyp` | `@ELI` (wzorzec `lit[a-z]`) + opcjonalne tirety |

# Jakie znaczenie i gdzie zastosowanie ma '<xs:element name="odsylacz-do-odnosnika">'?

`<xs:element name="odsylacz-do-odnosnika">` to po prostu miejsce do „kliknięcia w przypis" — wskazanie w tekście, że w tym miejscu jest odniesienie do wcześniej zdefiniowanego `<odnosnik>`.

Przykład:
```xml
<artykul>
  <nr>5</nr>
  <p>
    Organ wydaje decyzję
    <odsylacz-do-odnosnika id-odnosnika="ref1" symbol="1"/>
    w terminie 30 dni.
  </p>
</artykul>

<!-- ...gdzieś dalej lub na końcu aktu... -->
<odnosnik id-odnosnika="ref1" symbol="1">
  <p>Decyzja, o której mowa w art. 5...</p>
</odnosnik>
```
## Kluczowe cechy
| Cecha | Wartość |
| :-- | :-- |
| **Treść własna** | Brak — to pusty element |
| **Wymagany atrybut** | `@id-odnosnika` — musi pasować do istniejącego `<odnosnik>` |
| **Może wystąpić** | Wielokrotnie dla tego samego odnośnika |
| **Gdzie** | W `zawartosc-tekstowa-bez-cytatu` — czyli wewnątrz akapitu |

# Jak w elemencie `podstawa-prawna` zamieścić `pole`, które automatycznie pobiera metadane?
Przykładowy zapis:
```xml
<podstawa-prawna>
  Na podstawie 
  <pole nazwa="pp_jednostka"
        kod="PPJ"
        metadane="md:Relacje/md:PodstawaPrawnaJednostka/md:Relacja/md:Lokalizacja"
        data="art. 14 ust. 4">
    art. 14 ust. 4
  </pole>
  ustawy z dnia 5 lipca 2018 r. o krajowym systemie cyberbezpieczeństwa (Dz.U. z 2018 r. poz. 1560
  <pole nazwa="pp_eli"
        kod="PPE"
        metadane="md:Relacje/md:PodstawaPrawnaJednostka/md:Relacja/md:ELI"
        data="https://eli.gov.pl/eli/DU/2018/1560/ogl"></pole>)
  zarządza się, co następuje:
</podstawa-prawna>
```
Pomimo tego, że `<pole>` dla ELI ma pustą treść — jest niewidoczne w druku - system bądź edytor odczytuje URI z atrybutu `data` jako maszynowe powiązanie z bazą danych.

# Jakie jest znaczenie i zastosowanie `<xs:element name="fragment" type="JednostkaTekstowaTyp"/>`?

`<fragment>` pełni dwie odrębne role, zależnie od kontekstu wystąpienia.

| Grupa | Rola `<fragment>` |
| :-- | :-- |
| `cytat-wspolne` | Fragment tekstu **wewnątrz cytatu strukturalnego** |
| `kontynuacja-akapitu` | Fragment tekstu **w kontynuacji akapitu** (obok `sankcja-karna`, `slubowanie`, tabel, grafik) |

Inaczej: to niepodzielna jednostka tekstowa bez własnej numeracji, używana, gdy trzeba zacytować lub osadzić fragment przepisu, który nie jest pełną jednostką redakcyjną (nie jest artykułem, ustępem, punktem, literą). 
## Przykłady użycia
1. Wewnątrz cytatu — gdy nowelizacja dotyczy tylko kawałka zdania:
```xml
<cytat-strukt>
  <fragment GUID="frag-001">
    z wyjątkiem przypadków określonych w ust. 3
  </fragment>
</cytat-strukt>
```
2. W kontynuacji akapitu — gdy po wyliczeniu następuje domknięcie tekstowe niebędące częścią wspólną:
```xml
<p>Minister może:</p>
<punkt>...</punkt>
<punkt>...</punkt>
<fragment GUID="frag-002">
  – po zasięgnięciu opinii Rady.
</fragment>
```
`<fragment>` w `kontynuacja-akapitu` pojawia się obok takich elementów jak tabela, grafika, wzór matematyczny czy `<slubowanie>`. To sugeruje, że jego rola w kontynuacji to obsługa wtrąceń tekstowych między elementami blokującymi — np. tekstu między dwiema tabelami albo dopowiedzenia po grafice.
## Różnica względem podobnych elementów
| Element | Kiedy                                                        |
| :-- |:-------------------------------------------------------------|
| `<fragment>` | Tekst bez struktury, bez numeru, w cytacie lub kontynuacji   |
| `<fragment-odnosnik>` | Analogicznie, ale wewnątrz odnośnika (`tresc-odnosnika`)     |
| `<czesc-wspolna-punkt>` | Zamknięcie po wyliczeniu punktów — strukturalnie powiązane |
`<fragment>` to więc „worek" na tekst, który musi być w XML-u, ale nie pasuje do żadnej nazwanej jednostki redakcyjnej. 
Podsumowując: `<fragment>` to tekst, który nie jest akapitem `<p>`, nie jest jednostką redakcyjną i nie jest częścią wspólną wyliczenia.

# Jakie jest znaczenie i zastosowanie ma `<xs:element name="specjalny">`?
`<specjalny>` to fragment tekstu z niestandardowym formatowaniem — zwykły tekst inline, który redaktor chce sformatować inaczej niż otaczający go akapit.
To dokładnie to samo co `<b>`, `<i>`, `<sup>` — element inline wewnątrz akapitu — ale zamiast jednego konkretnego stylu (`bold`, `italic`) daje pełną swobodę typograficzną przez atrybuty `typoSpecjalne`.
`<specjalny>` niesie atrybutGroup `typoSpecjalne`, która pozwala ustawić wszystkie parametry typograficzne naraz:
```
margin-left, margin-right, margin-top, margin-bottom,
line-height, text-indent, znastepnym, wierszerazem,
nowastrona, bezprzenoszenia, text-align, tabulatory
```
To znacznie więcej niż `typoBezKwalifikacjiPelne` na zwykłym akapicie — stąd nazwa „specjalny".
Pojawia się w dwóch miejscach:
- w **`zawartosc-tekstowa-bez-cytatu`** — czyli wewnątrz każdego `<p>`, jako element inline
- w **`cytat-wspolne`** i **`rozszerzenie-kontynuacji`** — czyli w cytatach i kontynuacjach akapitu
- w **komórkach tabeli** (`<td>`) — jako jeden z dozwolonych elementów blokowych
## Przykłady użycia
1. Tekst ze szczególnym wcięciem wewnątrz przepisu:**
```xml
<p>Minister ogłasza wyniki w formie:
  <specjalny margin-left="20pt" text-align="center">
    OBWIESZCZENIA
  </specjalny>
  publikowanego w Dzienniku Urzędowym.
</p>
```
2. Fragment z wymuszonym łamaniem strony w załączniku:**
```xml
<p>Stawki określa poniższa tabela:
  <specjalny nowastrona="true" text-align="justify">
    (patrz tabela na następnej stronie)
  </specjalny>
</p>
```
3. W komórce tabeli — tekst z niestandardowym wyrównaniem:**
```xml
<td>
  <specjalny text-align="right" margin-right="5pt">
    1 234,56 zł
  </specjalny>
</td>
```
`<specjalny>` jest "wyjściem awaryjnym" — gdy żaden z gotowych elementów inline nie pasuje do potrzebnego formatowania, a redaktor musi ręcznie zdefiniować styl fragmentu tekstu.

# Jak są przechowywane grafiki i wzory matematyczne zamieszczone w treści aktu?

Stworzona schema zakłada dla każdego z tych elementów ma inną strategię przechowywania.
## Grafika rastrowa (`<xs:element name="grafika">`)
Przechowywana jako osobny plik zewnętrzny — `<grafika>` zawiera tylko referencję:
```xml
<grafika href="zalacznik1/rys01.png"
         width="120mm"
         height="80mm"
         alt="Schemat organizacyjny"
         skrot="[base64 thumbnails...]"/>
```
- `@href` — ścieżka do zewnętrznego pliku PNG/JPG
- `@skrot` — miniatura w base64 wbudowana w XML (podgląd w edytorze, niewidoczna w druku)
Plik graficzny znajduje się obok XML-a, np. w osobnym folderze.
## Grafika wektorowa (`<xs:element name="grafika-wektorowa">`)
Tu dopuszczalne są dwa rozwiązania: SVG może być osadzone w pliku lub zamieszczone zewnętrznie:
```xml
<!-- Wariant 1: SVG wbudowane bezpośrednio w XML -->
<grafika-wektorowa width="100mm" height="60mm">
  <svg xmlns="http://www.w3.org/2000/svg">
    <rect x="10" y="10" width="80" height="40"/>
    <text x="50" y="35">§ 1</text>
  </svg>
</grafika-wektorowa>

<!-- Wariant 2: referencja do zewnętrznego pliku SVG -->
<grafika-wektorowa href="zalacznik1/mapa.svg"
                   width="150mm"
                   skrot="[base64...]"/>
```
## Wzór matematyczny (`<xs:element name="wzor-mat">`)
Wzór MathML jest zawsze wbudowany bezpośrednio w XML:
```xml
<wzor-mat grafika="wzory/wzor01.png" skrot="[base64...]">
  <mml:math>
    <mml:mfrac>
      <mml:mi>a</mml:mi>
      <mml:mi>b</mml:mi>
    </mml:mfrac>
    <mml:mo>=</mml:mo>
    <mml:msup>
      <mml:mi>c</mml:mi>
      <mml:mn>2</mml:mn>
    </mml:msup>
  </mml:math>
  <opis>
    <objasnienie>gdzie <symbol-txt>a</symbol-txt> oznacza...</objasnienie>
  </opis>
</wzor-mat>
```

- MathML `<mml:math>` — wzór **bezpośrednio w XML**
- `@grafika` — opcjonalna referencja do obrazka PNG jako fallback
- `@skrot` — miniatura base64 dla edytora


## Zestawienie strategii

| Element | Treść główna | Gdzie | Miniatura/fallback |
| :-- | :-- | :-- | :-- |
| `<grafika>` | Plik PNG/JPG | **Osobny plik** (`@href`) | `@skrot` w base64 w XML |
| `<grafika-wektorowa>` | SVG | **W XML** lub osobny plik | `@skrot` w base64 |
| `<wzor-mat>` | MathML | **W XML** | `@grafika` zewnętrzny PNG |

## Praktyczny wniosek

Pełny akt prawny to zatem **paczka plików**: jeden XML z treścią + folder z plikami graficznymi rastrowych. Wzory matematyczne i grafiki wektorowe są samowystarczalne — XML zawiera je w całości.

# Element `<hiperlacze>` — znaczenie i zastosowanie

Element `hiperlacze` modeluje hiperłącze w treści aktu prawnego — odpowiednik HTML-owego `<a href="...">`, ale dostosowany do potrzeb dokumentów prawnych.
## Analiza atrybutów
| Atrybut | Typ XSD | Rola                                                                                                                      |
| :-- | :-- |:--------------------------------------------------------------------------------------------------------------------------|
| `href` | `xs:anyURI` | Docelowy adres URI — może być URL-em HTTP, odnośnikiem do innego aktu (np. `isap://...`), lub identyfikatorem wewnętrznym |
| `etykieta` | `xs:string` | Tekst wyświetlany użytkownikowi jako opis łącza                                                                           |
| `nazwa` | `xs:string` | Formalna nazwa dokumentu lub zasobu, do którego prowadzi łącze — np. „ustawa z dnia 23 kwietnia 1964 r. – Kodeks cywilny" |
| `skrot` | `xs:base64Binary` | Zakodowany skrót (hash) zasobu docelowego — służy do weryfikacji integralności lub identyfikacji wersji dokumentu|

## Charakterystyczne cechy tej definicji

Anonimowy `xs:complexType` bez zawartości tekstowej. Element `hiperlacze` ma typ zdefiniowany inline (nie przez `type="..."`) i zawiera wyłącznie atrybuty. Oznacza to, że element jest pusty: nie niesie treści tekstowej, jedynie metadane łącza.
To celowy wybór projektowy — w dokumentach prawnych hiperłącze jest zazwyczaj elementem osadzonym w treści akapitu, a jego czytelna etykieta pochodzi z atrybutu `etykieta`.
## Jak jest osadzony w strukturze
Element `hiperlacze` jest używany wewnątrz typów zawartości mieszanej (mixed content), takich jak `ZawartoscTekstowaTyp`. Typowy kontekst użycia wygląda tak:
```xml
<p>Zgodnie z przepisami
  <hiperlacze
    href="https://isap.sejm.gov.pl/isap.nsf/DocDetails.xsp?id=WDU19640160093"
    nazwa="Kodeks cywilny"
    etykieta="art. 6 k.c."
  />
stosuje się domniemanie...</p>
```
## Potencjalny problem: `skrot` jako `xs:base64Binary`
Atrybut `skrot` o typie `xs:base64Binary` w atrybucie (nie w elemencie) jest rzadkim, ale poprawnym użyciem w XSD. Wartość musi być zakodowana w Base64, co w praktyce oznacza, że jest to raczej pole przeznaczone do wypełniania przez systemy informatyczne.

# Do czego służą grupy `<xs:group name="od-punktu/litery/...">` itd.`?
Grupy te to grupy modelu zawartości opisujące hierarchię jednostek redakcyjnych polskiego aktu prawnego od danego poziomu i poniższych.
## Do czego służą te grupy
Każda grupa `od-X` definiuje zbiór elementów dozwolonych począwszy od poziomu X i poniżej. Dzięki temu model zawartości jest modularny:
- `od-punktu` — dopuszcza `punkt` (i zagnieżdżone w nim litery, tirety itd.)
- `od-litery` — dopuszcza `litera` (i zagnieżdżone tirety, podpunkty)
Analogicznie dla niższych poziomów
## Dlaczego taka konstrukcja, a nie bezpośrednie zagnieżdżenie?
Wynika to z dwóch potrzeb:
1. Elastyczność wejścia w hierarchię — `ustęp` może zaczynać się bezpośrednio od `punktu` albo od `litery` (gdy punkty są pominięte), bez konieczności pisania osobnych typów dla każdego wariantu. Zamiast tego `ustęp` odwołuje się do grupy `od-punktu`, a `punkt` do `od-litery` itd. Jest to przydatne, gdy prawodawca popełnił błąd w redagowaniu aktu prawnego, a mimo wszystko należy ów akt odwzorować w pliku xml. 
2. Unikanie cyklicznych zależności typów — bezpośrednie zagnieżdżenie `xs:element` wewnątrz definicji własnego przodka prowadziłoby do cyklicznych referencji. Grupy pośrednie pozwalają tę cykliczność kontrolować zgodnie z modelem XSD 1.0.[^1]
## Przykładowe użycie:
```xml
<!-- Ustęp może zawierać treść akapitową LUB sekwencję zaczynającą się od punktu -->
<xs:complexType name="UstepTyp">
  <xs:choice>
    <xs:group ref="tresc-akapitowa"/>
    <xs:group ref="od-punktu"/>
  </xs:choice>
</xs:complexType>
```
W ten sposób jeden typ `UstepTyp` obsługuje oba warianty redakcyjne bez duplikowania definicji.

# Jaki jest cel zamieszczenia grupy `rozszerzenie-kontynuacji`?
Grupa ta pełni rolę punktu rozszerzenia (extension point) w modelu zawartości; jest to mechanizm pozwalający wstawić treść specjalną w dowolnym miejscu ciągłości tekstu, bez łamania struktury głównego modelu.
## Mechanizm działania
```xml
<xs:group name="rozszerzenie-kontynuacji">
  <xs:sequence>
    <xs:element ref="specjalny" minOccurs="0" maxOccurs="unbounded"/>
  </xs:sequence>
</xs:group>
```
gdzie:
- `minOccurs="0"` — element `<specjalny>` jest całkowicie opcjonalny
- `maxOccurs="unbounded"` — może wystąpić dowolną liczbę razy
- `xs:sequence` w grupie jednoelementowej — technicznie można by użyć `xs:all`, ale `xs:sequence` jest zgodny z XML Schema 1.0 i nie narzuca kolejności przy jednym elemencie.

Element `<specjalny>` to kontener na treść nieregularną — typowo tabele, formuły matematyczne, ilustracje, ramki boczne lub inne obiekty, które przerywają ciągły bieg tekstu prawnego, ale są dopuszczalne w każdym miejscu, gdzie tekst może się kontynuować.

## Przykładowe użycie w modelu zawartości
Grupa jest dołączana za pomocą `xs:group ref` do typów opisujących akapity lub jednostki redakcyjne:
```xml
<xs:complexType name="AkapitTyp" mixed="true">
  <xs:sequence>
    <xs:group ref="fragmenty-tekstu" minOccurs="0" maxOccurs="unbounded"/>
    <xs:group ref="rozszerzenie-kontynuacji"/>
    <!-- ↑ po każdym fragmencie tekstu może pojawić się
           zero lub więcej elementów <specjalny> -->
  </xs:sequence>
</xs:complexType>
```
### Przykład instancji XML
```xml
<p>Wysokość opłaty wynosi:
  <specjalny>
    <tabela>
      <wiersz><komorka>Kategoria A</komorka><komorka>100 zł</komorka></wiersz>
      <wiersz><komorka>Kategoria B</komorka><komorka>100 zł</komorka></wiersz>
    </tabela>
  </specjalny>
  Opłata nie obejmuje kosztów postępowania.
</p>
```
## Dlaczego stworzono osobną grupę, a nie element inline?
Wydzielenie do grupy daje trzy korzyści:
1. Wielokrotne użycie — jeden `ref="rozszerzenie-kontynuacji"` zamiast kopiowania deklaracji do każdego typu
2. Wersjonowanie — zmiana definicji `specjalny` automatycznie propaguje się wszędzie
3. Czytelność — model zawartości każdego typu pozostaje zwięzły; rozszerzenie jest czytelnie wyodrębnione jako opcjonalny dodatek

# Jakie ma zastosowanie `<xs:group name="formatowanieAkapitu">`?
Grupa `formatowanieAkapitu` pełni rolę pośrednika (wrapper/indirection layer) — jej jedynym zadaniem jest opakowanie referencji do grupy `formatowanieAkapituCzesciowe`.
## Po co taki pośrednik?
Wzorzec ten jest klasycznym punktem rozszerzalności w plikach .xsd. Jeżeli w przyszłości trzeba było wprowadzić inny wariant formatowania (np. `formatowanieAkapituPelne`), wystarczy zmienić `ref=` wewnątrz `formatowanieAkapitu` — wszystkie typy, które ją konsumują, automatycznie „dostaną" nową wersję.

# Jakie znaczenie mają `xs:unique` i `xs:keyref` w elemencie - akcie prawnym (np. ustawa)?

To mechanizm spójności odesłań wewnątrz dokumentu XML — odpowiednik klucza głównego i klucza obcego ze świata baz danych.
## `xs:unique name="UstGUID"` — unikalność GUID-ów

```xml
<xs:unique name="UstGUID">
    <xs:selector xpath=".//*"/>
    <xs:field xpath="@GUID"/>
</xs:unique>
```
Ten fragment mówi walidatorowi:

> „W całym dokumencie (`.//*` = wszystkie elementy, na dowolnej głębokości) żadne dwa elementy nie mogą mieć tej samej wartości atrybutu `@GUID`."

Atrybut `@GUID` nie musi wystąpić w każdym elemencie — ale jeśli już się pojawi, jego wartość musi być unikalna w skali dokumentu.
## `xs:keyref name="UstRefGUID"` — integralność odesłań
```xml
<xs:keyref name="UstRefGUID" refer="UstGUID">
    <xs:selector xpath=".//*"/>
    <xs:field xpath="@RefGUID"/>
</xs:keyref>
```
Ten fragment mówi walidatorowi:
> „Każda wartość atrybutu `@RefGUID` (na dowolnym elemencie w dokumencie) musi wskazywać na istniejący `@GUID` zadeklarowany powyżej."

Jeśli jakiś element ma `RefGUID="abc-123"`, to gdzieś w tym samym dokumencie musi istnieć element z `GUID="abc-123"`. Nie może być „wiszącego" odnośnika.
## Jak to działa w praktyce — przykład
```xml
<!-- Element z GUID — "oryginalny" -->
<artykul GUID="a1b2-c3d4">
  <tytul>Art. 1</tytul>
</artykul>

<!-- Element odwołujący się do niego -->
<odnosnik RefGUID="a1b2-c3d4"/>   <!-- ✅ OK — GUID istnieje -->

<!-- Element z błędnym odwołaniem -->
<odnosnik RefGUID="xyz-9999"/>    <!-- ❌ BŁĄD — brak GUID o tej wartości -->
```
## Gdzie to jest zadeklarowane w schemie?
Ten blok jest zazwyczaj umieszczony **na elemencie głównym** (root) dokumentu — np. `<xs:element name="rozporzadzenie">` — żeby zasięg unikalności i sprawdzania obejmował cały dokument.
## Po co jest zamieszcznay w aktach prawnych?
W aktach prawnych `@GUID` identyfikuje konkretny fragment tekstu (artykuł, ustęp, punkt), a `@RefGUID` służy do tworzenia odesłań wewnętrznych. Ten mechanizm gwarantuje, że żadne odesłanie w dokumencie nie prowadzi do nieistniejącego miejsca.

# Dlaczego przyjęto nazwę elementów UstGUID i UstRefGUID zamiast użyć po prostu GUID i RefGUID?
Nazewnictwo `UstGUID` / `UstRefGUID` (i analogicznie dla innych rodzajów aktów prawnych) zamiast prostego `GUID` / `RefGUID` wynika z kilku nakładających się powodów projektowych:
## Unikanie kolizji w XSD
W pliku XSD (schemie) wszystkie deklaracje globalne `xs:element` i `xs:attribute` współdzielą jedną "płaską" przestrzeń nazw. Nazwa `GUID` jest skrajnie generyczna — gdyby schema była importowana do innego dokumentu XSD (innej schemy) lub rozszerzana o moduły dla innych typów dokumentów (np. umów, orzeczeń), każdy z nich mógłby potrzebować własnego identyfikatora GUID. Prefiks `Ust` jednoznacznie przypisuje element do domeny aktów prawnych i wskazuje rodzaj aktu. Jest więc znacznikiem semantycznym.
- `UstGUID` — unikalny identyfikator konkretnego aktu prawnego
- `UstRefGUID` — odesłanie do GUID innego aktu z rodzaju ustawa 

Gdyby schemat obsługiwał również np. załączniki z własnymi GUIDami, można by wprowadzić `ZalGUID` bez kolizji.
## Odporność na przyszłe rozszerzenia
Proste `GUID` byłoby sierotą pojęciową — nie wiadomo, czego jest GUID. `UstGUID` jest samodokumentujące: nawet bez czytania adnotacji `xs:documentation` od razu wiadomo, że identyfikuje akt prawny - ustawę - a nie np. wersję redakcyjną czy instancję publikacji.

# Dlaczego w schemie stworzono typ złożony "PunktOTJTyp" oraz element "punkt-OTJ", ale nie stworzono elementu "podpunkt-OJT" mimo że jest "PodpunktOTJTyp"?

To klasyczny wzorzec projektowy w XSD, który ma konkretne uzasadnienie. Oto najprawdopodobniejsza przyczyna tej asymetrii:

## Globalny element vs. lokalny element w XSD

`punkt-OTJ` jest zadeklarowany globalnie (na poziomie `xs:schema`), ponieważ musi być bezpośrednio referencjonowany — albo z modelu `ObwieszczenieTyp`, albo przez mechanizm `substitutionGroup`. Globalna deklaracja jest konieczna zawsze, gdy element ma być użyty przez `ref=` w innym typie.

`PodpunktOTJTyp` natomiast istnieje jako nazwany typ, ale element `podpunkt-OTJ` jest zadeklarowany **lokalnie** — wewnątrz `xs:sequence` w definicji `PunktOTJTyp`.

## Skąd taka decyzja projektowa?

- `podpunkt-OTJ` zawsze i wyłącznie jest dzieckiem `punkt-OTJ` — nigdy nie pojawia się samodzielnie w `ObwieszczenieTyp`
- Nie ma potrzeby referencjonowania go przez `ref=` z zewnątrz
- Nie uczestniczy w żadnej `substitutionGroup`
- Lokalność chroni przestrzeń nazw globalnej przed zaśmieceniem elementami, które mają tylko jeden kontekst użycia

## Typ nazwany mimo lokalnego elementu

`PodpunktOTJTyp` jest jednak typem nazwanym (nie anonimowym), co jest celowe — dzięki temu można go dokumentować, ewentualnie reużyć w innym miejscu lub rozszerzyć przez `xs:extension`, nawet jeśli sam element jest lokalny.
Użycie nazwanego `PodpunktOTJTyp` zamiast anonimowego bloku to świadomy wybór czytelności i potencjalnej rozszerzalności, niewymagający jednak globalnej deklaracji elementu.

# Dlaczego użyto odmiennej przestrzeni nazw (namespace) w `<xs:complexType name="PodpunktOTJTyp">`?
To celowy wybór projektowy; wyjaśnienie wymaga spojrzenia na trzy warstwy: 

## Czym jest `cytat-strukt` w tym kontekście?

`PodpunktOTJTyp` to typ używany w tekście jednolitym (TJ) — a podpunkt TJ może cytować fragment innego aktu prawnego, np. fragment rozporządzenia czy ustawy wbudowany strukturalnie w treść punktu. Ten zacytowany fragment ma własną strukturę redakcyjną: artykuły, paragrafy, ustępy — czyli elementy z przestrzeni nazw.

## Dlaczego użyto `xs:any` z odrębną przestrzenią nazw (namespace)?

Dzięki temu walidator otrzymuje komunikat: „wewnątrz `cytat-strukt` mogą pojawić się dowolne elementy z innej przestrzeni nazw — bez konieczności ich deklarowania w bieżącej schemie".

## Dlaczego użyto `processContents="skip"`?
`skip` jest tu wyborem, który akceptuje "na ślepo" wprowadzone wartości, niezależnie od tego, czy walidator będzie miał dostęp do schematu pozwalającego je zwalidować. W ten sposób `cytat-strukt` zawsze przejdzie walidację.

## Wzorzec projektowy

Jest to klasyczny wzorzec foreign content slot w schemie XSD — celowo "wycina się dziurę" dla zawartości, której nie chce się definiować ani walidować lokalnie, ale która musi być dozwolona strukturalnie.

# W pliku schemaPL.xsd w `<xs:element name="preambula">` użyto `<xs:group ref="tresc-akapitowa-TJ"/>` a nie `<xs:group ref="tresc-akapitowa-ustawy"/>`. Dlaczego?

`preambula` jest globalnym elementem, który używany jest w różnych miejscach: np. zarówno w `UstawaTyp` jak i `TJUstawyTyp`. Ponieważ XSD wymaga jednej definicji elementu globalnego, należało wybrać jeden wariant pasujący do obu typów. `tresc-akapitowa-TJ` powoduje, że preambuła działa poprawnie zarówno w  TJ ustawy czy kodeksu jak i w pierwotnym tekście ustawy czy kodeksu. Wariant dla TJ jest pozwala na większą elastyczność względem wariantu dla ustawy. Jest to więc celowe uproszczenie, aby nie tworzyć dwóch osobnych elementów (`preambula-ustawy` i `preambula-TJ`).

# Jaka jest różnica między elementami `"h"` oraz `"hiperlacze"`?

To dwa zupełnie różne podejścia do reprezentacji hiperłącza — różniące się modelem użycia i przeznaczeniem.

## Kluczowa różnica modelu

| Cecha | `<h>` | `<hiperlacze>` |
| :-- | :-- | :-- |
| Model | `type="xs:anyURI"` — **treść elementu** jest adresem | Atrybuty — adres w `@href`, treść brak |
| Użycie w dokumencie | `<h>https://sejm.gov.pl</h>` | `<hiperlacze href="https://sejm.gov.pl" nazwa="Sejm"/>` |
| Gdzie działa | W środku tekstu (inline) | Jako samodzielny element |
| Etykieta dla czytelnika | Sam adres URI jest tym, co widzi czytelnik | Oddzielna `@etykieta` i `@nazwa` |
| Skrót (hash) | Brak | `@skrot` (base64) — weryfikacja integralności |

## Przeznaczenie w praktyce
`<h>` jest elementem **inline** — należy do grupy `zawartosc-tekstowa-bez-cytatu`, więc można go wstawić w środek zdania, tak jak `<b>` czy `<i>`:
```xml
<p>Szczegóły na stronie <h>https://sejm.gov.pl</h> Sejmu.</p>
```
`<hiperlacze>` to element **blokowy** — samodzielny, z rozbudowanymi metadanymi. Służy do reprezentowania hiperłącza jako obiektu, np. w spisie załączników elektronicznych, gdzie ważna jest nie tylko treść URL, ale też ludzka nazwa, etykieta wyświetlana i skrót weryfikujący:
```xml
<hiperlacze href="https://sejm.gov.pl/akt.pdf"
            nazwa="Ustawa z dnia..."
            etykieta="Pobierz PDF"
            skrot="dGVzdA=="/>
```
`<h>` nie ma atrybutów — więc adres URI jest jednocześnie treścią i etykietą, to uproszczony model, który nie obsługuje przypadku, gdy wyświetlany tekst linku różni się od samego URL. `<hiperlacze>` rozwiązuje ten problem.

# Jaka jest różnica między typami `CytatWTekscieTyp`, `CytatBlokTyp` a `CytatStruktPustyTyp`?

`CytatWTekscieTyp` — jest najprostszą formą cytatu; używany jest głównie w nowelizacjach i opakowuje fragment tekstu podlegający zmianie (`funkcja="zastap-tekst"`, `"uchyl-tekst"`) lub wprowadzany (`funkcja="nowy-tekst"`) przez nowelizację tekstową. Para nowGUID ↔ ref-nowGUID spina logicznie stary i nowy tekst.

`CytatStruktPustyTyp` - to cytat strukturalny bez walidowanej zawartości, używany w kontekstach generycznych (patojednostki), gdzie typ cytowanych jednostek nie jest z góry znany. Pozwala na zamieszczenie bardziej rozbudowanego tekstu, niż ograniczony do jednego akapitu (`<p>`) `CytatWTekscieTyp`. Jego rozszerzeniem jest `CytatStruktPelnyTyp`, z walidowaną zawartością jednostek redakcyjnych aktu.

`CytatBlokTyp` - cytat blokowy niestrukturalny, opakowuje element blokowy (tabelę, grafikę, wzór mat.) podlegający zastąpieniu lub wprowadzany nowelizacją. Stosowany gdy zmieniany/dodawany fragment to element blokowy, ale NIE jest jednostką redakcyjną aktu (do tego służy cytat-strukt).

## Dlaczego `CytatWTekscieTyp` nie może mieć kilka `<p>`?

Wiele `<p>` oznacza, że zmieniany fragment to co najmniej jeden pełny akapit, czyli de facto zmiana na poziomie blokowym lub strukturalnym. Inline z założenia opakowuje fragment wewnątrz jednego zdania/akapitu.

## Schemat wyboru właściwego cytatu.

Czy zmiana dotyczy fragmentu tekstu wewnątrz zdania?

    ├─ TAK  →  `CytatWTekscieTyp`          ← jeden <tekst>, bez <p>
    └─ NIE — czy dotyczy jednostki redakcyjnej aktu (art., ust., pkt)?
                ├─ TAK  →  `CytatStruktPustyTyp`/`CytatStruktPelnyTyp`
                └─ NIE — czy dotyczy elementu blokowego (tabela, grafika…)?
                            └─ TAK  →  `CytatBlokTyp`

# Zdanie jako jednostka redakcyjna - jaki ma cel?

**Zdanie** to najmniejsza samodzielna jednostka redakcyjna tekstu prawnego — pojedyncze zdanie w sensie gramatycznym, kończące się kropką (lub innym znakiem kończącym). W strukturze XML dokumentu prawnego zdanie jest **atomem tekstu**: mniejszych jednostek redakcyjnych już nie ma. W obrębie zdania mogą jednak funkcjonować wyliczenia w postaci punktów, liter i niższych jednostek.
Zdania występują w akapitach `<p>`.

Zdania reprezentowane są przez dwa typy: `ZdanieTyp` i `ZdanieRefTyp`. Służą one do wskazywania, że mamy do czynienia z jednym zdaniem, mimo że rozciągniętym na kilka odrębnych `<p>`. `ZdanieTyp` może występować samodzielnie; `ZdanieRefTyp` tylko w powiązaniu ze `ZdanieTyp`.

**Przykład:**

```
<artykul ELI="art_1" GUID="{C4F4C1A2-5E6B-7890-ABCD-EF1234567890}">
      <nr>1</nr>
      <ustep ELI="art_1__ust_1" GUID="{B3F4C1D3-5E6B-7890-ABCD-EF1234567890}">
        <nr>1</nr>
        <p><zdanie nr="1" zdGUID="{A3F2C1D4-5E6B-7890-ABCD-EF1234567890}" fragment="1">Zastaw rejestrowy może być ustanowiony w celu zabezpieczenia wierzytelności:</zdanie></p>
        <punkt ELI="art_1__ust_1__pkt_1" GUID="{A1F2C3D2-5E6B-7890-ABCD-EF1234567890}">
          <nr>1</nr>
          <p><zdanie-ref ref-nr="1" ref-zdGUID="{A3F2C1D4-5E6B-7890-ABCD-EF1234567890}" fragment="2">Skarbu Państwa i innej państwowej osoby prawnej,</zdanie-ref></p>
        </punkt>
        <punkt ELI="art_1__ust_1__pkt_2">
          <nr>2</nr>
          <p><zdanie-ref ref-nr="1" ref-zdGUID="{A3F2C1D4-5E6B-7890-ABCD-EF1234567890}" fragment="3">gminy, związku międzygminnego (związku komunalnego) i innej komunalnej osoby prawnej,</zdanie-ref></p>
        </punkt>
        <punkt ELI="art_1__ust_1__pkt_3">
          <nr>3</nr>
          <p><zdanie-ref ref-nr="1" ref-zdGUID="{A3F2C1D4-5E6B-7890-ABCD-EF1234567890}" fragment="4">banku krajowego,</zdanie-ref></p>
        </punkt>       
      </ustep>
      <ustep ELI="art_1__ust_2">
        <nr>2</nr>
        <p><zdanie nr="1" zdGUID="{A2F3C2D5-0E1B-7890-ABCD-EF1234567890}" fragment="1">W sprawach nie uregulowanych w niniejszej ustawie do zastawu rejestrowego stosuje się przepisy Kodeksu cywilnego.</zdanie></p>
      </ustep>
    </artykul>
```

## Atrybuty zdania

### `nr` / `ref-nr`

**Numer porządkowy** zdania w obrębie danego akapitu `<p>`.

- Numeracja zaczyna się od `1` i rośnie kolejno: `1`, `2`, `3`…
- Numery są **lokalne** — każdy ustęp (ustawy/rozporządzenia) czy paragraf (kodeksu)  zaczyna numerowanie od nowa
- Pozwala jednoznacznie adresować konkretne zdanie w ramach przepisu, np. „art. 5 ust. 2 zdanie 3"

**Przykład:**

```xml
<p>
  <zdanie nr="1" zdGUID="..." fragment="...">Zastaw wygasa z chwilą zapłaty.</zdanie>
  <zdanie nr="2" zdGUID="..." fragment="...">Zastawnik obowiązany jest niezwłocznie złożyć wniosek o wykreślenie.</zdanie>
</p>
```
W `ZdanieRefTyp` odpowiada mu atrybut `ref-nr`, który wskazuje, którego zdania kontynuacją jest dane `<zdanie-ref>` w skali ustępu/paragrafu.

### `zdGUID`/`ref-zdGUID`

**Globalnie unikalny identyfikator** (GUID) zdania, w formacie:

```
{XXXXXXXX-XXXX-XXXX-XXXX-XXXXXXXXXXXX}
```

- Jest **unikalny w całym zbiorze dokumentów** — żadne dwa zdania w całej bazie aktów prawnych nie mogą mieć tego samego `zdGUID`
- **Nigdy się nie zmienia** — nawet jeśli treść zdania zostanie znowelizowana, `zdGUID` oryginalnego zdania pozostaje, a nowe zdanie otrzymuje nowy GUID
- Służy do **precyzyjnego cytowania i śledzenia zmian legislacyjnych** — system może wskazać dokładnie, które zdanie zostało zmienione, dodane lub uchylone

W `ZdanieRefTyp` odpowiada mu atrybut `ref-zdGUID`, który wskazuje, którego zdania kontynuacją jest dane `<zdanie-ref>` w skali globalnej.

### `fragment`

Wskazuje, który to fragment konkretnego zdania. `<zdanie>` będzie miało zawsze `fragment=1`, podczas gdy `zdanie-ref=...` będzie miało kolejne liczby naturalne (2, 3 etc.).