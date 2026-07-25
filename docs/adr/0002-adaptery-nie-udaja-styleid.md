# ADR-0002: Adaptery PDF/TXT nie zgadują `styleId`

- Status: Accepted
- Data: 2026-07-16 (zasada przyjęta przy projektowaniu adapterów; utrwalona w commitach TXT i PDF)

## Kontekst

Cały istniejący potok umiał rozpoznawać strukturę po stylach szablonu RCL. Dokumenty PDF i TXT nie
niosą stylów. Najkrótsza droga do „działającego" parsowania takich wejść to zgadnięcie stylu z
treści (linia zaczyna się od `Art.` → wstaw `styleId = "ART"`) i puszczenie reszty potoku bez zmian.

## Decyzja

Adaptery ustawiają `DocumentBlock.StyleId = null` dla PDF i TXT. Nigdy nie syntetyzują nazw styli.
Rozpoznanie struktury w takim wejściu opiera się na warstwie syntaktycznej (regex) i układzie, a
brak stylu obniża pewność przez `StyleAbsentPenalty`.

## Odrzucone alternatywy

**Synteza `styleId` z treści w adapterze.** Odrzucone: zgadnięcie awansowałoby do rangi sygnału
stylowego, czyli najsilniejszego w klasyfikacji, i stałoby się nieodróżnialne od stylu prawdziwego
szablonu. Błąd zgadywania utrwalałby się jako pewność 100% zamiast obniżonej pewności z karą, a
diagnostyka (`ValidationMessage`, `Penalties[]`) przestałaby cokolwiek znaczyć. Klasyfikacja
straciłaby też jedyny sygnał odróżniający „dokument z szablonu RCL" od „dowolny tekst".

## Konsekwencje

Część zachowań jest dostępna wyłącznie przy stylach i nie ma odpowiednika w gałęzi bezstylowej:
część wspólna wyliczenia (`CZ_WSP_*`), `CommonPartOf` w nowelizacjach, głębokość tiretu przy braku
sygnału układu. Te luki są zarejestrowane w [backlog.md](../backlog.md) jako świadome, a nie jako
błędy do naprawienia przy okazji.

Konsekwencja dla przeglądów kodu: propozycja „ustawmy tu styl zastępczy, będzie prościej" jest
odrzucana z tego ADR, bez ponownej dyskusji.

## Weryfikacja

- `PlainTextBlockReaderTests.Read_PlainTextBlocks_HaveNoStyleId`
- `ArchitectureDecisionTests.Adr0002_PdfBlocks_NeverCarryStyleId`
