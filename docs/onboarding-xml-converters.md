# 🎯 Onboarding: Rozwój konwerterów do XML — WordParser

## Czym jest projekt WordParser?

**WordParser** to narzędzie .NET 10 do automatycznego parsowania polskich aktów prawnych (ustaw, rozporządzeń) z formatu DOCX i eksportu do XML/XLSX. Projekt rozkłada złożoną, hierarchiczną strukturę dokumentów prawnych na obiektowy model danych.

---

## 📦 Trzy projekty — ich role i zależności

```
┌─────────────────────────────────────────────────────────────┐
│  WordParser (CLI)                                           │ ← Punkt wejścia dla użytkownika
│  Zawiera: Program.cs, główne polecenia                      │
│  📍 GitHub: https://github.com/kujawiak/word-parser         │
└──────────────────┬──────────────────────────────────────────┘
                   │ zależy od
                   ▼
┌─────────────────────────────────────────────────────────────┐
│  WordParserCore (biblioteka główna)                         │ ← Logika biznesowa
│  Zawiera: parsowanie, konwersje, klasyfikacja,             │
│  konwertery XML                                             │
│  📍 GitHub: https://github.com/kujawiak/word-parser-core    │
└──────────────────┬──────────────────────────────────────────┘
                   │ zależy od
                   ▼
┌─────────────────────────────────────────────────────────────┐
│  ModelDto (puro DTO — bez logiki)                           │ ← Model danych
│  Zawiera: ArticleDto, ParagraphDto,                         │
│  wszystkie klasy domenowe                                   │
│  📍 GitHub: https://github.com/kujawiak/legal-act-dto-model │
└─────────────────────────────────────────────────────────────┘
```

**Reguła:** zmiany w ModelDto mogą wpłynąć na całą aplikację; zmiany w WordParserCore mogą wpłynąć na WordParser.

---

## 🎯 Twoja rola — Konwertery do XML

Pracujesz w: **WordParserCore/Services/Converters/**

Istniejące konwertery to tylko makiety. Niektóre zawierają jeszcze w komentarzach stary kod z pierwszej wersji parsera.
Możesz posłużyć się nimi jako przykład, albo zaproponować zupełnie inne rozwiązanie:

- `ArticleXmlConverter` ← Artykuł → XML
- `ParagraphXmlConverter` ← Ustęp → XML
- `PointXmlConverter` ← Punkt → XML
- `LetterXmlConverter` ← Litera → XML
- `TiretXmlConverter` ← Tiret → XML

**Zadanie:** pełny rozwój tych klas — transformacja DTO → strukturę XML zgodną z wymaganiami projektu (najprawdopodobniej będzie to Akoma w jakiejś odsłonie).

---

## 🏗️ Hierarchia modelu dokumentu

Dokumenty mają **dwie warstwy struktur**:

### 1️⃣ **Jednostki redakcyjne** (treść artykułu)
```
Article (Artykuł)
  ↓
Paragraph (Ustęp / ust.)
  ↓
Point (Punkt / pkt)
  ↓
Letter (Litera / lit.)
  ↓
Tiret (Łącznik)
  ↓
DoubleTiret (Podwójny łącznik)
```

### 2️⃣ **Jednostki systematyzacyjne** (organizacyjne)
```
Part (Część) → Book (Książka) → Title (Tytuł) → Division (Dział)
  → Chapter (Rozdział) → Subchapter (Podrozdział) → [Artykuły]
```

**Ważne:** Jeśli artykuł ma **dokładnie jeden ustęp**, ten ustęp jest oznaczony jako `IsImplicit = true` i jest pomijany w ścieżkach identyfikacyjnych (eId).

---

## 🔑 Kluczowe koncepcje dla konwerterów

### eId — elektroniczny identyfikator
```
Przykład: art_5__ust_2__pkt_3__lit_a__tir_1
          (podwójny podkreślnik między komponentami)
```
eId buduje się na podstawie numerów elementów i pomija jednostki niejawne.

### Relacje w ModelDto

Każda klasa DTO ma:
- **Liczby** (`EntityNumber`) — numer jednostki
- **Tekst** — treść/opis
- **Kolekcje** — elementy podrzędne (lista Paragrafów w Artykule, etc.)
- **Metadata** — `IsImplicit`, `ValidationMessages`

Wszystkie klasy dziedziczą z `BaseEntity` w ModelDto.

---

## 📝 Konwencje w projekcie

| Element | Język | Przykład |
|---------|-------|---------|
| Kod (zmienne, metody, klasy) | **Angielski** | `ConvertToXml()`, `articleNumber` |
| Komentarze, logi, UI | **Polski** | `// Konwertuje artykuł na XML` |
| Commit messages | **Angielski** | `Add ArticleXmlConverter implementation` |

---

## 🚀 Setup i pierwsze kroki

### ⚙️ Połączenie projektów

Trzy projekty znajdują się w oddzielnych repozytoriach, ale musisz je połączyć w jedną Solution:

1. Sklonuj wszystkie trzy repozytoria do wspólnego katalogu
2. Ręcznie utwórz plik `.sln` (Solution) w głównym katalogu, który będzie referencjonować wszystkie trzy projekty, albo użyj do tego IDE, np. VS Code
3. Dodaj referencje między projektami:
   - WordParser → WordParserCore
   - WordParserCore → ModelDto

Przykład struktury:
```
workspace/
  ├── word-parser/
  ├── word-parser-core/
  ├── legal-act-dto-model/
  └── WordParser.sln (ręcznie utworzony)
```

### 📚 Gdzie szukać informacji

1. **Przejrzyj** [ModelDto](https://github.com/kujawiak/legal-act-dto-model) — typy DTO które będziesz konwertować
2. **Sprawdź** [WordParserCore/Services/Converters/](https://github.com/kujawiak/word-parser-core/tree/master/Services/Converters) — makiety konwerterów
3. **Uruchom build:** `dotnet build WordParserCore/WordParserCore.csproj`
