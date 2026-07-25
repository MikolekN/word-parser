# ADR-0003: Silny sygnał strukturalny jest warunkiem koniecznym uznania dokumentu za akt prawny

- Status: Accepted
- Data: 2026-07-17 (commity 3ce7c3e, ae75ec0)

## Kontekst

`DocumentClassifier` sumuje punkty z sygnałów ZTP (nagłówek rodzaju aktu, data, przedmiot, formuła
kompetencyjna, dominacja jednostki podstawowej, wejście w życie…) i porównuje wynik lidera z progiem.
Testy na negatywach pokazały, że dokumenty niebędące aktami potrafią przekroczyć próg samą sumą
sygnałów słabych: umowa albo statut wewnętrzny ma jednostki `§`, datę i frazę „w sprawie …".

## Decyzja

Wprowadzono warunek konieczny `hasBackbone`
([DocumentClassifier.cs:49](../../WordParserCore/Services/Classify/Document/DocumentClassifier.cs#L49)).
Dokument bez co najmniej jednego silnego sygnału strukturalnego — nagłówka rodzaju aktu, formuły
kompetencyjnej, komend nowelizacyjnych albo formuły wejścia w życie — jest klasyfikowany jako
nie-akt niezależnie od liczby punktów.

## Odrzucone alternatywy

**Podniesienie progu punktowego.** Odrzucone: próg wysoki na tyle, by odsiać umowę z paragrafami,
odsiewał też prawdziwe akty krótkie i nietypowe (np. akt zmieniający bez rozbudowanego tytułu).
Punkty są przy tym niewspółmierne — dziesięć słabych sygnałów nie znaczy tyle co jeden nagłówek.

**Wagi ujemne dla sygnatur nie-aktów.** Odrzucone jako krucha zabawa w wyprzedzanie: lista rzeczy,
które nie są aktem, jest nieskończona, a lista form, w których akt się ogłasza, jest domknięta
przez ZTP. Prościej wymagać obecności formy niż wyliczać nieobecności.

## Konsekwencje

Akty w formach nieprzewidzianych w katalogu sygnałów strukturalnych będą odrzucane jako nie-akty —
to świadomy koszt. Wywołujący ma wyjście: `ParseOptions.Policy = AlwaysParse` (ADR-0006).

Dodanie nowego rodzaju aktu do klasyfikatora wymaga dołożenia jego sygnału **do zbioru
backbone'owego**, nie tylko do punktacji; inaczej nowy rodzaj nigdy nie przejdzie klasyfikacji.
