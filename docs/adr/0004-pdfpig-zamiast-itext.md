# ADR-0004: PdfPig (Apache-2.0) do odczytu warstwy tekstowej PDF

- Status: Accepted
- Data: 2026-07-19 (commit e3de312)

## Kontekst

Adapter PDF potrzebował biblioteki dającej dostęp do pozycji znaków i słów (nie samego tekstu),
bo hierarchia jednostek redakcyjnych jest w PDF kodowana wcięciem (§ 58 ZTP), a odnośniki i indeksy
górne — rozmiarem i przesunięciem baseline. Saga.Web wystawia usługę sieciową w instytucji
publicznej (RCL).

## Decyzja

`PdfPig`, licencja Apache-2.0.

## Odrzucone alternatywy

**iText.** Funkcjonalnie mocniejszy, ale licencja AGPL: udostępnianie usługi sieciowej korzystającej
z biblioteki AGPL rozciąga wymóg udostępnienia kodu źródłowego na całość usługi. Dla usługi
prowadzonej przez RCL to warunek nieakceptowalny, a licencja komercyjna iText nie była rozważana
jako proporcjonalna do zakresu użycia (odczyt warstwy tekstowej).

**Wyciąganie tekstu narzędziem zewnętrznym (`pdftotext`).** Odrzucone: zależność od binarki poza
.NET komplikuje obraz Docker i deployment, a tryb `-layout` daje tekst z utraconą informacją
o pozycjach — czyli traci dokładnie to, po co sięgamy do PDF.

## Konsekwencje

Pakiet NuGet nazywa się **`PdfPig`**. Uwaga praktyczna: w rejestrze istnieje też
„UglyToad.PdfPig" — to obcy fork, nie ta biblioteka; pomyłka przy `dotnet add package` jest łatwa.

Wersja przypięta w `Saga.Core.csproj` (0.1.15). Oś Y w PdfPig rośnie do góry, więc składanie
linii wymaga sortowania malejąco po baseline — nieoczywistość opłacona w `PdfLineExtractor`.

Brak OCR jest osobną decyzją: ADR-0005.

## Weryfikacja

- `ArchitectureDecisionTests.Adr0004_NoAgplPdfLibraryReferenced` — żaden projekt nie referuje
  biblioteki iText/iTextSharp.
