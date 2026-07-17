# CorpusValidator

Narzędzie deweloperskie (poza `WordParser.sln`) do **empirycznej walidacji `DocumentClassifier`**
na oznakowanym korpusie DOCX aktów ogłoszonych.

Ground truth pochodzi z nazwy folderu kategorii w układzie:

```
<root>/<rok>/<KATEGORIA>/*.docx
KATEGORIA ∈ { USTAWY, ROZPORZADZENIA, OBWIESZCZENIA }
```

Dla każdego pliku: `DocxBlockReader.ReadBlocks` → `DocumentClassifier.Classify`,
a następnie porównanie predykowanego `LegalActType` z etykietą folderu.

## Uruchomienie

```bash
# domyślny korpus: "DocRepo/Akty ogłoszone (szczotki)" (gitignore'owany, lokalny)
dotnet run --project tools/CorpusValidator

# własny korpus i ścieżka raportu
dotnet run --project tools/CorpusValidator -- "<root-korpusu>" raport.txt
```

Wypisuje na konsolę i zapisuje do plików:
- `corpus-report.txt` — dokładność wg kategorii, macierz pomyłek, flagi
  `IsConsolidatedText`/`IsAmending`, przykłady błędnych klasyfikacji,
- `corpus-report.csv` — wiersz na plik (rok, kategoria, predykcja, pewność, flagi, liczba bloków)
  do dalszej analizy.

## Uwagi

- Korpus (`DocRepo/`) jest **gitignore'owany** — nie jest wersjonowany; narzędzie tylko go czyta.
- Projekt celowo **nie należy do `WordParser.sln`** ani do `build-all` — to narzędzie QA/diagnostyki,
  nie część produktu.
