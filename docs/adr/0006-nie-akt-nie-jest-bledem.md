# ADR-0006: Dokument niebędący aktem prawnym to wynik z raportem, nie wyjątek

- Status: Accepted
- Data: 2026-07-20 (commit 1350300)

## Kontekst

Po wpięciu `DocumentClassifier` w `LegalDocumentParser.Parse` trzeba było rozstrzygnąć, co się
dzieje, gdy klasyfikacja uzna wejście za nie-akt. Klasyfikacja jest heurystyką — myli się w obie
strony, a jej pewność jest wartością ciągłą (1–100), nie boolem.

## Decyzja

`Parse` zwraca kopertę `ParseResult { Classification, Document?, SourceFormat, BlockCount }`.
Nie-akt nie przerywa działania: `Classification` opisuje wynik (rodzaj, pewność, sygnały z dowodami),
a o zbudowaniu modelu decyduje `ParseOptions.Policy` — `ParseWhenLegalAct` (domyślnie),
`AlwaysParse`, `ClassifyOnly`. CLI wystawia to jako `--force` (bez niego kod wyjścia 2), Web jako
przycisk „Parsuj mimo wszystko".

## Odrzucone alternatywy

**Wyjątek na nie-akcie.** Odrzucone: odebrałby wywołującemu decyzję, która do niego należy —
przy pewności 45/100 sensowna odpowiedź brzmi „prawdopodobnie nie, ale spróbuj", a wyjątek tego nie
wyraża. Wymuszałby też sterowanie przepływem przez `try/catch` w CLI i Web.

**Zwracanie samego `LegalDocument` z pustą treścią.** Odrzucone: nieodróżnialne od aktu bez
rozpoznanej struktury; ginie informacja, czy problem jest w klasyfikacji, czy w parsowaniu.

## Konsekwencje

`Parse(string)` zmieniło typ zwracany z `LegalDocument` na `ParseResult` — źródłowy breaking change
naprawiony w tym samym commicie (jedyne call site'y to CLI i Web).
`Parse(WordprocessingDocument)` dostało `[Obsolete]` bez `error: true`.

Każdy nowy konsument musi świadomie sięgnąć po `Result.Document` i obsłużyć `null` — to celowe
tarcie, wymuszające decyzję zamiast jej pominięcia. Wynik klasyfikacji jest kopiowany do
`LegalDocument.Classification`, ale nie zmienia `Type` (ADR-0007).

## Weryfikacja

- `ParseFacadeTests.NonAct_DefaultPolicy_SkipsModelButReportsClassification` — nie-akt zwraca wynik
  z raportem i `Document == null`, bez wyjątku.
- `ParseFacadeTests.NonAct_AlwaysParse_BuildsModel` — wywołujący może przełamać decyzję polityką.
