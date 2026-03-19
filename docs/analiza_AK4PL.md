przeanalizuj ustawę o interoperacyjności i przedstaw mi informację na temat tego jakie standardy powinny być używane i na jakie trzeba zwracać uwagę. Szczególnie interesuje mnie schematy xml 

Copilot said: 

Copilot 

Poniżej znajdziesz syntetyczną i jednocześnie precyzyjną analizę obowiązujących w Polsce wymagań dotyczących interoperacyjności, ze szczególnym podkreśleniem standardów danych i schematów XML, zgodnie z najnowszym rozporządzeniem KRI obowiązującym od 22 maja 2024 r. (Dz.U. 2024 poz. 773). 
Wszystkie informacje oparte są na aktualnym brzmieniu przepisów oraz oficjalnych źródłach rządowych. 

Kształt 

🧩 1. Kontekst prawny: co reguluje ustawa i rozporządzenie? 

Najważniejszy akt prawny kształtujący interoperacyjność to: 

Rozporządzenie Rady Ministrów z 21 maja 2024 r. ws. Krajowych Ram Interoperacyjności (KRI) 

– określa formaty danych, standardy komunikacyjne, standardy bezpieczeństwa, minimalne wymagania dla systemów teleinformatycznych oraz rejestrów publicznych.sejm+1 

To rozporządzenie wykonuje delegację ustawową z ustawy o informatyzacji działalności podmiotów realizujących zadania publiczne. 

Kształt 

🧩 2. Jakie standardy muszą być stosowane? 

W KRI określono zarówno standardy bezpośrednio wskazane, jak i referencyjne normy (ISO, PKN, INSPIRE, itp.).gov 

2.1. Standardy dotyczące formatów danych 

Z punktu widzenia wymiany informacji między systemami kluczowe są: 

Formaty danych zalecane i wymagane 

XML — format podstawowy dla wymiany danych strukturalnych. 
Jest wymieniany w KRI jako jeden z dopuszczalnych formatów danych w interfejsach systemów teleinformatycznych. 
lex 

XSD (XML Schema Definition) — obowiązkowe w przypadku definiowania schematów danych XML dla systemów publicznych. 

CSV, TXT — formaty prostsze, możliwe do stosowania w sytuacjach określonych. 

JSON — dopuszczalny w systemach usługowych (szczególnie przy modelu SOA). 

GML (dla danych przestrzennych, zgodnie z INSPIRE). 
gov 

Kształt 

🧩 3. XML i schematy XML – wymagania szczegółowe 

Jeżeli interesuje Cię szczególnie XML, to właśnie w tej części znajdują się najważniejsze elementy dla praktyki integracyjnej. 

3.1. Obowiązek stosowania schematów XML (XSD) 

KRI wymaga, aby: 

wymiana danych była zrealizowana przy użyciu ustrukturyzowanych formatów danych, 

struktura danych była jednoznacznie opisana — XSD jest tu formalnym standardem.sejm+1 

Oznacza to, że każdy interfejs oparty o XML musi mieć oficjalny, udostępniony schemat XSD. 

3.2. Nazewnictwo, datatypes, kodowania 

Zgodnie ze standardami opisywanymi w KRI i dokumentacji interoperacyjności: 

XML musi być zgodny z W3C XML 1.0. 

KRI odwołuje się do norm ISO/IEC, np.: 

ISO 8601 → daty, 

ISO/IEC 10646 → kodowanie znaków (UTF8 jest domyślny), 

ISO 191xx → dane przestrzenne, w tym GML. 
gov 

3.3. Kompatybilność i unifikacja modeli danych 

KRI wymaga stosowania: 

danych referencyjnych (np. TERYT, PESEL, REGON) — XML powinien odwzorowywać oficjalne struktury tych rejestrów, 

spójnych struktur słownikowych, opartych o krajowe lub międzynarodowe standardy. 
lex 

Kształt 

🧩 4. Interfejsy i usługi: standardy dotyczące wymiany XML 

4.1. Wymagania dotyczące usług sieciowych (SOA) 

KRI opisuje model usługowy SOA jako referencyjny.lex 

Dla XML oznacza to: 

Obsługę SOAP 1.1/1.2, 

WSDL z definicją struktur XML, 

Obsługę podpisu elektronicznego (XML Signature), jeśli wymaga tego proces. 

4.2. Protokoły i bezpieczeństwo 

Wymagane: 

TLS dla kanałów transmisyjnych, 

Możliwość stosowania: 

XML Signature, 

XML Encryption. 
sejm 

Kształt 

🧩 5. Dane przestrzenne (INSPIRE) – bardzo istotne w XML 

Jeżeli Twoje dane mają aspekt geograficzny (GIS), to XML staje się jeszcze bardziej sformalizowany. 

Standardy: 

GML (Geography Markup Language) zgodny z normami ISO 19136, 

Schematy XML odbierane z dyrektywy INSPIRE. 
gov 

W Polsce obowiązek wynika także z ustawy o infrastrukturze informacji przestrzennej. 

Kształt 

🧩 6. Na co zwracać szczególną uwagę? 

Checklista praktyczna 

✔ XML / XSD 

Czy struktura jest zgodna z XSD i poprawnie walidowana? 

Czy kodowanie to UTF8? 

Czy typy danych zgodne z ISO (np. daty ISO 8601)? 

Czy używane są dane referencyjne i oficjalne słowniki? 

Czy uwzględniono wersjonowanie schematów? 

✔ Bezpieczeństwo 

Czy komunikacja odbywa się z użyciem TLS? 

Czy wymagany jest podpis XMLDSig? 

Czy uwzględniono mechanizmy integralności (hash, podpis)? 

✔ Wymiana i interoperacyjność 

Czy interfejs ma publiczną specyfikację struktury danych (XSD/WSDL)? 

Czy został uwzględniony model usługowy SOA? 

Czy dane są w ustandaryzowanych formatach (XML, JSON, GML)? 

✔ Dokumentacja i zgodność formalna 

Czy stosowane standardy nie zostały wycofane przez ISO/PKN? (część starych pozycji KRI straciła aktualność) 
gov 

Kształt 

🧩 7. Podsumowanie 

W świetle aktualnych przepisów (KRI 2024) i standardów interoperacyjności: 

XML pozostaje kluczowym, w pełni akceptowanym i wymaganym formatem wymiany danych, 

pod warunkiem spełnienia: 

formalnego opisu struktury przez XSD, 

zgodności z normami W3C i ISO, 

stosowania danych referencyjnych i słowników, 

zapewnienia bezpieczeństwa kanałów i podpisów, 

udostępnienia pełnej dokumentacji interfejsu. 

W razie chęci przygotowania konkretnego XSD, analizy zgodności istniejących plików albo projektu interoperacyjnego, mogę przygotować to w kolejnych krokach. 

Kształt 

Czy chcesz, żebym: 

przeanalizował Twoje konkretne XML/XSD, 

przygotował schemat XSD zgodny z KRI, 

lub zestawił komplet wszystkich standardów KRI w formie tabeli? 

Źródła 

Favicon typeFavicon typeFavicon type 

You said: 

dobrze. W takim razie zadanie jak do niego podejść. 
Mam stworzyć schemat xml do aktów prawnych ogłaszanych w Polsce. Ponieważ nie istnieje taki schemat jak podejść do jego stworzenia. Przeprowadź analizę i przedstaw wnioski. Możesz brać przykład i innych państw które wdrożyły takie rozwiązanie 

Copilot said: 

Copilot 

Poniżej przedstawiam kompleksową analizę, która pokazuje jak podejść do stworzenia schematu XML dla aktów prawnych ogłaszanych w Polsce, opartą o najlepsze praktyki światowe oraz istniejące międzynarodowe standardy. Analiza bazuje na aktualnych projektach, takich jak Akoma Ntoso / LegalDocML, AKN4EU oraz doświadczeniach państw i instytucji, które wdrożyły XML jako podstawę publikacji legislacyjnych. 

Kształt 

🚀 1. Dlaczego warto oprzeć się na istniejących standardach? 

Stworzenie schematu XML dla aktów prawnych od zera jest możliwe, ale byłoby kosztowne, ryzykowne i stworzyłoby niekompatybilny ekosystem. Na świecie istnieją już rozwinięte standardy, które zostały zaprojektowane specjalnie dla parlamentów i rządów. 

Najbardziej dojrzałe i powszechnie stosowane to: 

1. Akoma Ntoso (OASIS LegalDocML) 

– globalny standard XML dla dokumentów legislacyjnych, parlamentarnych i sądowych, zatwierdzony przez OASIS.oasis-open+1 

2. AKN4EU (Akoma Ntoso for the European Union) 

– wersja dostosowana dla Unii Europejskiej, używana jako wspólny format w procesie legislacyjnym.europa+1 

Oba standardy są: 

technicznie stabilne, 

wspierane przez międzynarodową społeczność, 

zgodne z zasadami interoperacyjności i długoterminowej archiwizacji. 

Kształt 

🧩 2. Jak zaplanować stworzenie schematu XML dla polskich aktów prawnych? 

Sugerowane podejście oparte na najlepszych praktykach z innych państw: 

Kształt 

KROK 1 — Analiza wymogów prawnych i strukturalnych polskich aktów 

Musisz zidentyfikować: 

strukturę ustaw, rozporządzeń, obwieszczeń, uchwał, 

metadane stosowane w ISAP (np. data ogłoszenia, pozycja Dz.U.), 

jednostki redakcyjne: 

artykuły, ustępy, punkty, litery, 

preambuły, 

tytuły i podtytuły, 

załączniki. 

Ta struktura jest wspólna dla większości systemów legislacyjnych, co pozwala dopasować standard Akoma Ntoso, który zawiera specjalne komponenty dla artykułów, paragrafów, sekcji itp. (over 500 semantycznych elementów).wikipedia 

Kształt 

KROK 2 — Przegląd istniejących standardów: Akoma Ntoso jako baza 

Dlaczego Akoma Ntoso jest najlepszym punktem wyjścia? 

✔ Opracowany specjalnie dla dokumentów prawnych 

– obejmuje akty, projekty ustaw, zmiany, historię dokumentu, noty, komentarze, odwołania do innych aktów itd. 
oasis-open 

✔ Jest międzynarodowym standardem OASIS (od 2018) 

– gwarantuje stabilność i długowieczność formatu. 
oasis-open 

✔ Zawiera bogaty metamodel 

– struktury treści, metadane wg modelu FRBR, relacje między dokumentami, wersjonowanie. 
wikipedia 

✔ Wspierany przez UE (AKN4EU) jako przyszły format wymiany 

– format wykorzystywany m.in. przez Parlament Europejski, Radę UE i Komisję. 
europa+1 

Kształt 

KROK 3 — Porównanie polskiej struktury z Akoma Ntoso 

To kluczowy etap — mapowanie. 

Przykłady mapowania: 

Element polskiego aktu 

Element w Akoma Ntoso 

Tytuł aktu 

<meta><identification> 

Preambuła 

<preamble> 

Artykuł 

<article> 

Ustęp 

<paragraph> 

Punkt 

<point> 

Odesłania 

<ref> 

Załącznik 

<attachment> 

Akoma Ntoso wspiera te jednostki bez konieczności ich tworzenia od nowa. 

Kształt 

KROK 4 — Lokalizacja i dostosowanie schematu 

Standard AKN jest elastyczny i pozwala na: 

dodawanie własnych słowników, 

tworzenie specjalnych typów dokumentów, 

rozbudowę metadanych, 

dopasowanie nazewnictwa do lokalnych wymogów legislacyjnych. 

UE zrobiła dokładnie to samo, tworząc AKN4EU jako profil AKN dostosowany do europejskich procesów legislacyjnych. 
europa 

Dla Polski oznacza to stworzenie profilu Akoma Ntoso PL. 

Kształt 

KROK 5 — Zdefiniowanie profilu polskiego (AKNPL) 

W skład profilu AKNPL powinny wejść: 

1. Typy dokumentów obowiązujące w Polsce 

ustawa, 

rozporządzenie, 

obwieszczenie, 

zarządzenie, 

uchwała, 

jednolity tekst (specjalna struktura). 

2. Specyficzne metadane z ISAP 

Przykładowo: 

Numer Dz.U. 

Pozycja, 

Data ogłoszenia, 

Organ wydający, 

Rodzaj aktu. 

3. Model zmian 

Polska praktyka wymaga kompleksowego odwzorowania zmian (typowe dla aktów nowelizujących). 

Akoma Ntoso posiada całą sekcję <mod> oraz narzędzia do oznaczania zmian semantycznych — UE również z tego intensywnie korzysta.europa 

4. Język i nazewnictwo 

Można zachować nazwy elementów XML po angielsku (wg standardu), lecz słowniki i identyfikatory muszą być dostosowane do polskiego systemu prawnego. 

Kształt 

KROK 6 — Przygotowanie schematu XML / XSD 

Po zdefiniowaniu profilu, tworzysz: 

zestaw plików .xsd, 

dokumentację opisującą strukturę, 

przykład dokumentu (sample XML) dla każdej klasy aktu, 

definicje metadanych, 

reguły walidacji i identyfikacji. 

Akoma Ntoso udostępnia gotowe XSD oraz przykłady, które mogą być adaptowane:Akoma Ntoso | Akoma Ntoso Site 

Kształt 

KROK 7 — Walidacja na przykładach rzeczywistych aktów 

Możesz wykorzystać: 

losową polską ustawę, 

rozporządzenie ministra, 

obwieszczenie o tekście jednolitym. 

i przygotować ich pełne odwzorowania XML. 

Kształt 

KROK 8 — Konsultacje z archiwami i instytucjami prawnymi 

Tak postępowały kraje wdrażające Akoma Ntoso (Włochy, Kenia, UK, UE): 

konsultacje z bibliotekami parlamentarnymi, 

konsultacje z wydziałami legislacyjnymi rządu i parlamentu, 

konsultacje z archiwami. 

Kształt 

🧠 3. Wnioski końcowe 

1. Nie należy tworzyć schematu od zera. 

Najlepszą praktyką jest oprzeć się na Akoma Ntoso / AKN4EU, które zapewniają: 

dojrzały model dokumentów legislacyjnych, 

kompatybilność z UE, 

możliwość rozbudowy o polskie elementy, 

gotowe narzędzia, parsery i walidatory. 

2. Należy stworzyć polski profil AKN–PL. 

To oznacza: 

mapowanie struktur polskich aktów na elementy AKN, 

definiowanie typów dokumentów, 

dopasowanie metadanych, 

opisanie zasad wersjonowania i zmian. 

3. Projekt powinien być realizowany iteracyjnie. 

Najpierw implementacja kilku typów aktów (np. ustawa + rozporządzenie), potem reszta. 

4. Taki schemat umożliwi później pełną automatyzację Dz.U., ISAP, monitorów, repozytoriów. 

UE zmierza w tym kierunku (AKN4EU jako standard w procesie legislacyjnym). 
europa 

Kształt 

🔧 Jeśli chcesz… 

Mogę przygotować: 

wstępny schemat XSD dla polskiej ustawy w profilu AKNPL, 

mapowanie jednostek redakcyjnych na elementy Akoma Ntoso, 

gotowy szablon XML (sample document) dla przykładowej ustawy, 

albo pełny projekt profilu AKNPL z dokumentacją. 

Jak chcesz kontynuować? 

Źródła 

Favicon typeFavicon type 

You said: 

Dobrze. Pytanie drugie. Czy w schemie AKN-PL powinny być brane pod uwagę tylko akty rządowe? W polsce akty wydawane są przez: rząd, sejm, ministrów, wojewodów, gminy? Jak powinniśmy do tego podejść. Jak zrobiono to w innych krajach? 

Copilot said: 

Copilot 

Poniżej znajdziesz precyzyjną, opartą na standardach międzynarodowych analizę dotyczącą tego, jakie kategorie aktów prawnych powinny być uwzględnione w profilu AKNPL, oraz jak rozwiązano to w innych państwach i organizacjach międzynarodowych. 

Kształt 

🧩 1. Czy AKNPL powinien obejmować tylko akty rządowe? 

Zdecydowanie nie. 
Model Akoma Ntoso (LegalDocML) został zaprojektowany z myślą o pełnym spektrum dokumentów legislacyjnych: od parlamentarnych, przez rządowe, aż po sądowe i administracyjne.sejm 

Standard nie ogranicza się do jednego poziomu władzy – przeciwnie, zakłada obsługę: 

dokumentów parlamentarnych, 

aktów rządowych, 

przepisów wykonawczych, 

uchwał lokalnych, 

orzeczeń, 

komunikatów administracyjnych. 

Potwierdza to także opis standardu, który wskazuje, że służy on dokumentom tworzonym przez parlamenty, rządy, sądy i inne organy publiczne w różnych jurysdykcjach.gov 

Wniosek: Profil AKNPL powinien obejmować wszystkie rodzaje polskich aktów prawnych, w tym: 

akty Sejmu (ustawy), 

akty rządu, 

akty ministrów, 

akty Prezydenta, 

akty wojewodów, 

akty powiatów i gmin, 

akty prawa miejscowego. 

Kształt 

🧩 2. Dlaczego nie można ograniczyć się do aktów rządowych? 

1. Polska ma wielopoziomowy system stanowienia prawa 

Prawo stanowione jest: 

na poziomie centralnym, 

na poziomie terenowej administracji rządowej, 

na poziomie samorządu terytorialnego. 

Każda z tych kategorii wymaga formalnej publikacji i powinna mieć wspólny standard reprezentacji. 

2. Spójność informacyjna państwa 

Wszystkie te akty znajdują się w Dz.U. i M.P. oraz rejestrach prawa miejscowego – dlatego ich XML powinien być kompatybilny. 

3. Przykłady z innych krajów i organizacji 

Inne państwa stosujące Akoma Ntoso nie ograniczają się do jednego organu. 

Przykłady: 

Jurysdykcja 

Zakres objętych dokumentów 

Podstawa 

UE 

akty Komisji, Rady, Parlamentu; projekty, poprawki, decyzje itp. 

AKN4EU — wspólny format legislacyjny dla wszystkich instytucji UElex+1 

OASIS LegalDocML 

parlamenty, rządy, sądy, administracje publiczne 

opis standardu LegalDocML – obejmuje parlamenty, sądy i administracjesejm 

Włochy (NormeInRete) 

parlament, rząd, regiony i gminy 

wdrożenie AKN w wieloszczeblowym systemie 

Kenia 

parlament, rząd, administracja lokalna 

projekt Africa i-Parliament (Akoma Ntoso) 

USA (Cicero, Congress XML) 

ustawy, rezolucje, uchwały, dokumenty administracji 

standardy oparte częściowo na AKN 

UE jest szczególnie ważnym przykładem, bo przyjęła AKN4EU właśnie jako platformę wspólnej wymiany dokumentów niezależnie od instytucji. 
lex 

Kształt 

🧩 3. Jak podejść do zróżnicowania organów w AKNPL? 

Akoma Ntoso zakłada 3warstwowe modelowanie: 

Kształt 

3.1. Typ dokumentu (element <act>, <bill>, <regulation> itd.)</regulation></bill></act> 

W AKNPL należy zdefiniować listę typów dokumentów odpowiadającą polskim realiom, np.: 

ustawa (Sejm) 

rozporządzenie (Rada Ministrów, minister) 

zarządzenie (ministrowie, wojewodowie) 

uchwała (samorządy) 

obwieszczenie 

prawo miejscowe 

Standard pozwala tworzyć własne specjalizacje typów dokumentów — dokładnie jak UE zrobiła to w AKN4EU. 
lex 

Kształt 

3.2. Organ wydający (metadane <identification> w sekcji <meta>)</identification> 

Akoma Ntoso zawiera model metadanych zgodny z FRBR, w którym można określić: 

organ promulgacyjny, 

organ wydający, 

datę wydania, 

wersję dokumentu. 

Polska powinna opracować kontrolowany słownik organów, np.: 

sejm 

senat 

prezydent 

rząd 

minister właściwy od … 

wojewoda … 

rada gminy … 

rada powiatu … 

UE posiada własne słowniki IMFC — to świetny punkt odniesienia.lex 

Kształt 

3.3. Zakres kompetencji i kategorii prawa 

Typ dokumentu i organ powinny być skorelowane — np.: 

rozporządzenie → minister, RM, Prezydent, 

uchwała → rada gminy, powiatu, województwa, 

zarządzenie → wojewoda, minister. 

To można modelować poprzez: 

profil AKNPL, 

słowniki ról, 

klasyfikatory dokumentów. 

Kształt 

🧩 4. Jak zrobiono to w innych krajach? 

4.1. Unia Europejska – AKN4EU 

UE przygotowała jednolity format legislacyjny, obejmujący wszystkie instytucje (PE, Komisja, Rada), a nie tylko jeden organ.lex 

Kluczowe rozwiązania: 

wspólny słownik organów, 

wspólna definicja typów dokumentów, 

jeden schemat XML, 

jeden format metadanych. 

4.2. Włochy 

Jeden z pierwszych krajów, który wdrożył Akoma Ntoso. Obejmuje: 

parlament, 

rząd, 

regiony, 

JST. 

4.3. Kenia 

Wdrażając Akoma Ntoso w Africa i-Parliament, objęto: 

parlament, 

rząd, 

administrację lokalną.itgov 

4.4. USA 

Systemy XML w Kongresie również nie ograniczają się do jednego typu organu — obejmują różne kategorie dokumentów legislacyjnych i administracyjnych. 

Kształt 

🧩 5. Wnioski i rekomendowane podejście dla Polski 

✅ 1. AKNPL musi obejmować wszystkie organy stanowiące prawo w Polsce. 

Profil ograniczony tylko do rządu byłby: 

niekompletny, 

niezgodny z praktyką innych państw, 

niemożliwy do zastosowania w pełnej informatyzacji Dz.U., M.P. i prawa miejscowego. 

✅ 2. Kluczowe będzie stworzenie: 

słownika typów dokumentów (ustawa, rozporządzenie, uchwała, zarządzenie…), 

słownika organów wydających, 

reguł powiązań organ → typ dokumentu, 

profilu metadanych (AKNPL), 

rozszerzeń specyficznych dla prawa miejscowego. 

✅ 3. Inspiracją powinny być modele AKN4EU oraz LegalDocML. 

Oba podejścia wskazują, że: 

wielopoziomowość nie jest problemem, 

jeden profil może obsłużyć wszystkie szczeble władzy, 

różnice są modelowane przez metadane i słowniki, nie przez oddzielne schematy. 

Kształt 

📌 Podsumowanie 

Tak – w AKNPL należy uwzględnić wszystkie kategorie aktów prawnych wydawanych w Polsce, od centralnych po lokalne. 
Tak właśnie robią wszystkie kraje i organizacje, które wdrożyły standard Akoma Ntoso – niezależnie od stopnia decentralizacji. 

UE (AKN4EU), OASIS LegalDocML oraz projekty krajowe jednoznacznie pokazują, że jeden wspólny standard XML dla całej legislacji państwa jest najlepszym i najbardziej przyszłościowym rozwiązaniem. 

Kształt 

Jeśli chcesz, mogę teraz przygotować: 

📌 model typów dokumentów i organów dla AKNPL, 
📌 schemat zależności organ → typ aktu, 
📌 projekt słownika metadanych (identyfikatorów FRBR) dla Polski, 
📌 albo przykładowy XML dla ustawy lub uchwały JST. 

Co robimy dalej? 

Źródła 

Favicon typeFavicon typeFavicon type 

You said: 

Dobrze. Kto powinien zając się w Polsce przygotowaniem takiego standardu? RCL jako organ wydający akty rządowe? a może jakś instytucja lub dedykowany zespół składający się z różnych przedstawicieli. przeakalizuj jak było to organizowane w innych krajach i zaproponuj ja my powinniśmy do tego podejść 

Copilot said: 

Copilot 

Poniżej przedstawiam kompleksową analizę opartą na praktykach międzynarodowych, która odpowiada na Twoje pytanie: kto w Polsce powinien przygotować standard AKNPL, jak to robi się w innych krajach, i jaki model organizacyjny byłby najlepszy. 

Kształt 

🧩 1. Jak to wygląda w świecie? Kto tworzy standard legislacyjny XML? 

Analiza wdrożeń Akoma Ntoso i LegalDocML pokazuje, że żaden kraj nie powierza tego tylko jednej instytucji, lecz tworzy organ koordynujący lub komitet międzyinstytucjonalny. 

Dlaczego? 
Bo standard legislacyjny dotyczy: 

parlamentu, 

rządu, 

ministerstw, 

sądów, 

samorządu. 

AKN obejmuje wszystkie te sektory – to standard „parliamentary, legislative and judicial documents” oraz „executive, legislative, and judiciary documents”.Akoma Ntoso | Akoma Ntoso Sitewikipedia 

Dlatego wdrożenia są z natury wieloinstytucjonalne. 

Kształt 

🌍 2. Przykłady organizacyjne z innych krajów i organizacji 

2.1. Unia Europejska – IMFC (Interinstitutional Metadata and Formats Committee) 

UE stworzyła AKN4EU, a nad standardem czuwa Interinstitutional Metadata and Formats Committee (IMFC) – komitet, w którym są przedstawiciele: 

Komisji, 

Rady UE, 

Parlamentu Europejskiego, 

Urzędu Publikacji. 

Zadaniem IMFC jest „developing and maintaining common interinstitutional standards for the secured and automated exchange of legal data based on machine-readable structured formats” (AKN4EU).lex 

Wniosek: w UE nie robi tego jedna instytucja, lecz komitet międzyinstytucjonalny. 

Kształt 

2.2. Włochy – model NormeInRete 

We Włoszech wdrażanie Akoma Ntoso było koordynowane przez: 

parlament, 

ministerstwo, 

archiwa państwowe, 

instytucje regionalne. 

Kluczowe było stworzenie wspólnej platformy standaryzacji i repozytorium wzorców XML. 

Kształt 

2.3. UN/DESA – Africa i-Parliament Action Plan 

Akoma Ntoso powstało jako projekt ONZ (UN/DESA), w którym stworzenie standardu opierało się na: 

współpracy parlamentów, 

sądów, 

rządów, 

ekspertów prawa i informatyków. 

Akoma Ntoso „was developed to support African Parliaments… enabling parliaments, courts, assemblies, and governments worldwide”.grokipedia 

To potwierdza, że model wdrożeniowy musi obejmować całą architekturę państwa. 

Kształt 

2.4. OASIS LegalDocML – organizacja standaryzacyjna 

OASIS tworzy standardy (w tym Akoma Ntoso) przez komitety robocze (TC) złożone z: 

ekspertów technicznych, 

przedstawicieli administracji publicznej, 

wydawców, 

dostawców narzędzi. 

Opis: „LegalDocML TC works to advance worldwide best practices… collecting requirements from stakeholders such as editors, libraries, public institutions, tribunals, publishers”.sejm 

Kształt 

🧩 3. Co z tego wynika? Najważniejsze zasady organizacyjne 

Zasada 1 — Standard legislacyjny wymaga reprezentacji wszystkich władz 

Ponieważ Akoma Ntoso obejmuje dokumenty: 

parlamentarne, 

legislacyjne, 

wykonawcze, 

sądowe 
,wikipedia 

to nie może go stworzyć tylko jeden organ (np. RCL). 

Zasada 2 — Potrzebny jest centralny koordynator + zespół międzyinstytucjonalny 

W każdym wdrożeniu są dwa poziomy: 

Instytucja lidera – odpowiedzialna za koordynację. 

Komitet standaryzacyjny – zespół ekspertów i przedstawicieli organów wydających akty. 

Zasada 3 — Włączenie ekspertów dziedzinowych 

Tak jak OASIS LegalDocML TC, który pracuje z prawnikami, informatykiem i wydawcami.sejm 

Kształt 

🇵🇱 4. Jak powinna to zrobić Polska? 

🔷 4.1. Kto powinien być liderem projektu? 

W Polsce najbardziej logiczny jest Rządowe Centrum Legislacji (RCL), ponieważ: 

odpowiada za publikację Dz.U. i M.P., 

nadzoruje proces legislacyjny rządu, 

ma kompetencje i zasoby w zakresie standardów technicznych publikacji. 

Ale RCL nie może być jedynym podmiotem, ponieważ: 

Sejm tworzy ustawy, 

ministrowie wydają rozporządzenia, 

wojewodowie i samorządy wydają prawo miejscowe, 

sądy tworzą orzeczenia. 

Dlatego RCL powinno być koordynatorem, nie jedynym twórcą. 

Kształt 

🔷 4.2. Proponowana struktura dla Polski — Komitet AKNPL 

A. Komitet Sterujący AKNPL 

Członkowie: 

RCL (przewodniczący), 

Kancelaria Sejmu, 

Kancelaria Senatu, 

Kancelaria Prezydenta (opcjonalnie), 

Ministerstwo Cyfryzacji, 

przedstawiciele: 

wojewodów, 

JST (ZPP, ZMP, ZBP), 

Sąd Najwyższy lub NSA (dla dokumentów orzeczniczych). 

Rola: 

definiowanie kierunków, 

przyjmowanie standardów, 

podejmowanie decyzji o wersjach. 

Kształt 

B. Zespół Techniczny AKNPL 

Powinien obejmować: 

ekspertów XML, 

informatyków RCL, 

specjalistów od Dz.U./ISAP, 

specjalistów od prawa miejscowego, 

prawnikówpraktyków, 

ekspertów archiwalnych. 

Zadania: 

stworzenie schematu XSD (profilu AKNPL), 

mapowanie typów dokumentów, 

przygotowanie słowników metadanych, 

testy i wdrożenie. 

Kształt 

C. Forum konsultacyjne (publiczne) 

Tak jak OASIS TC zbiera wymagania od: 

wydawców, 

bibliotek, 

organizacji pozarządowych, 

dostawców oprogramowania. 

Uzasadnienie: LegalDocML TC zbiera wymagania od szerokiego grona „editors, libraries, public institutions, tribunals, publishers”.sejm 

Kształt 

🧩 5. Rekomendowane podejście dla Polski – krok po kroku 

Krok 1 — RCL ogłasza inicjatywę: „AKNPL” 

Staje się liderem / sekretariatem. 

Krok 2 — Powołanie Komitetu AKNPL 

Struktura jak wyżej (Sejm, Senat, JST, archiwa, sądy). 

Krok 3 — Opracowanie profilu AKNPL 

Na podstawie: 

Akoma Ntoso OASIS Standard (2018),oasis-open 

AKN4EU (UE) — jako przykład wspólnego standardu międzyinstytucjonalnego.lex 

Krok 4 — Pilotaż (wybrane typy aktów) 

Na początek: 

ustawa, 

rozporządzenie, 

prawo miejscowe gminy. 

Krok 5 — Publiczne konsultacje (open standard) 

Tak jak w OASIS. 

Krok 6 — Uchwała Komitetu AKNPL → wdrożenie w Dz.U. i ISAP 

Kształt 

🧠 6. Ostateczne wnioski 

✔ Odpowiedzialność nie może spoczywać na jednym organie (np. RCL). 

Międzynarodowa praktyka (UE, OASIS, UN/DESA) pokazuje, że standardy tworzy się w modelu międzyinstytucjonalnym. 

✔ RCL powinno być liderem i koordynatorem, ale nie jedynym twórcą. 

✔ Polska powinna powołać Komitet AKNPL, wzorowany na: 

UE (IMFC), 

OASIS LegalDocML TC, 

UN/DESA Africa i-Parliament. 

✔ Zespół powinien obejmować przedstawicieli: 

Sejmu, 

Senatu, 

Rządu, 

JST, 

sądów, 

archiwów, 

ekspertów technicznych i prawnych. 

✔ Tylko takie podejście zapewni pełną interoperacyjność i zgodność z międzynarodowymi standardami. 

Kształt 

Jeśli chcesz, mogę przygotować następnie: 

📌 schemat organizacyjny Komitetu AKNPL, 
📌 oficjalny dokument „Założenia projektu AKNPL”, 
📌 plan prac i harmonogram dla przygotowania standardu, 
📌 lub porównanie modeli wdrożeń Włochy vs. UE vs. UN. 

Co wybierasz? 

 