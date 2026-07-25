# WordParser

Zestaw narzędzi .NET 10 do parsowania polskich dokumentów prawnych z formatu DOCX do hierarchicznego modelu obiektowego oraz eksportu do XML.

## Projekty

### ModelDto
Czyste DTO bez logiki biznesowej. Definiuje hierarchię encji dokumentu prawnego: jednostki redakcyjne (`Article`, `Paragraph`, `Point`, `Letter`, `Tiret`) oraz jednostki systematyzacyjne (`Part`, `Book`, `Title`, `Division`, `Chapter`, `Subchapter`). Zawiera też modele nowelizacji (`Amendment`) i komunikatów walidacji.

### WordParserCore
Silnik parsowania. Przetwarza pliki DOCX (via OpenXml) na model obiektowy z `ModelDto`. Kluczowe komponenty: klasyfikator dokumentu (`DocumentClassifier`, rozpoznaje, czy plik jest aktem prawnym i jakiego rodzaju), wielowarstwowy klasyfikator akapitów (`ParagraphClassifier`), buildery encji (wzorzec kaskadowy), obsługa nowelizacji (`AmendmentCollector`, `AmendmentFinalizer`) oraz konwertery do XML.

### WordParserCore.Tests
Testy jednostkowe i integracyjne (xUnit). Pokrywa klasyfikację akapitów, klasyfikację dokumentu, wykrywanie formatu (DOCX/PDF/TXT), generowanie eId, parsowanie nowelizacji, numerowanie encji i konwersję do XML.

### WordParser
Cienka nakładka CLI. Przyjmuje ścieżkę do pliku DOCX/PDF/TXT (flaga `--format docx|pdf|txt` albo automatyczna detekcja przez sygnaturę pliku) i wywołuje `WordParserCore`. Bez flagi `--dump` CLI tylko drukuje model na konsolę; `--dump <plik.xml>` zapisuje kanoniczny model do XML.

### WordParserWeb
Interfejs webowy. Przyjmuje upload wieloformatowy DOCX/PDF/TXT (limit 64MB). Gdy dokument nie jest aktem prawnym — wyświetla widok raportu klasyfikacji z opcją „parsuj mimo wszystko"; gdy jest aktem — renderuje stronę HTML z nawigacją po strukturze aktu prawnego.

## Szybki start

```bash
# Budowanie
dotnet build src/Saga.Core/WordParserCore.csproj
dotnet build src/Saga.Cli/WordParser.csproj

# Testy
dotnet test tests/Saga.Core.Tests/WordParserCore.Tests.csproj

# Uruchomienie CLI
dotnet run --project WordParser -- <ścieżka-do-pliku.docx>

# Uruchomienie CLI z zapisem wyniku do XML
dotnet run --project WordParser -- plik.docx --dump wynik.xml

# Parsowanie plików innych niż DOCX (PDF/TXT)
dotnet run --project WordParser -- plik.pdf --format pdf
```
