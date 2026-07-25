# Decyzje architektoniczne (ADR)

Jeden plik = jedna decyzja. Zapis jest **niezmienny**: zaakceptowanego ADR nie edytujemy poza
dopisaniem statusu. Zmiana zdania = nowy ADR ze statusem `Supersedes ADR-NNNN`, a stary dostaje
`Superseded by ADR-NNNN`. Dzięki temu widać nie tylko stan, ale i drogę do niego.

## Kiedy pisać ADR

Tylko wtedy, gdy decyzja spełnia co najmniej jeden warunek:

- **miała odrzuconą alternatywę** — ktoś kiedyś zapyta „dlaczego nie prościej / nie tą biblioteką",
- **jest trudno odwracalna** — wycofanie oznacza przebudowę, nie zmianę jednej linii,
- **jest nieoczywista** — kod wygląda na skomplikowany bez powodu, dopóki nie znasz kontekstu.

Nie piszemy ADR dla wyborów bez alternatywy („używamy xUnit") ani dla konwencji kodu — te są
w [CLAUDE.md](../../CLAUDE.md). Jak coś działa dziś: [architecture.md](../architecture.md).
Czego nie ma i dlaczego odłożone: [backlog.md](../backlog.md).

## Indeks

| ADR | Decyzja | Status | Data |
|---|---|---|---|
| [0001](0001-jeden-ujednolicony-potok-wejscia.md) | Wszystkie formaty schodzą się do `DocumentBlock[]`; jeden potok, nie ścieżka per format | Accepted | 2026-07-16 |
| [0002](0002-adaptery-nie-udaja-styleid.md) | Adaptery PDF/TXT nie zgadują `styleId` — brak stylu zostaje brakiem | Accepted | 2026-07-16 |
| [0003](0003-backbone-warunkiem-uznania-za-akt.md) | Bez silnego sygnału strukturalnego dokument nie jest aktem, choćby zebrał punkty | Accepted | 2026-07-17 |
| [0004](0004-pdfpig-zamiast-itext.md) | PdfPig (Apache-2.0) do odczytu PDF; iText odrzucony z powodu AGPL | Accepted | 2026-07-19 |
| [0005](0005-pdf-tylko-warstwa-tekstowa.md) | PDF wyłącznie z warstwą tekstową; skan odrzucany wyjątkiem, OCR poza zakresem | Accepted | 2026-07-19 |
| [0006](0006-nie-akt-nie-jest-bledem.md) | Nie-akt to wynik z raportem, nie wyjątek; decyzja „czy parsować" u wywołującego | Accepted | 2026-07-20 |
| [0007](0007-klasyfikacja-nie-mutuje-type.md) | Rozpoznany rodzaj aktu ląduje w `Classification`, nigdy w `LegalDocument.Type` | Accepted | 2026-07-20 |

Nowy ADR: skopiuj [0000-template.md](0000-template.md), nadaj kolejny numer, dopisz wiersz wyżej.
