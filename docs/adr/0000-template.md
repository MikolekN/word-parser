# ADR-NNNN: <decyzja w jednym zdaniu, w formie orzekającej>

- Status: Proposed | Accepted | Superseded by ADR-NNNN
- Data: RRRR-MM-DD

## Kontekst

Co wymagało rozstrzygnięcia i jakie ograniczenia obowiązywały. Fakty, nie narracja — czytelnik ma
zrozumieć sytuację bez znajomości rozmowy, w której decyzja zapadła.

## Decyzja

Co postanowiono. Krótko i jednoznacznie.

## Odrzucone alternatywy

Co jeszcze rozważano i **dlaczego odpadło**. Najważniejsza sekcja — bez niej ADR jest tylko opisem
stanu, a ten należy do architecture.md.

## Konsekwencje

Co z tego wynika: koszty, ograniczenia, rzeczy które stały się niemożliwe, miejsca w kodzie
utrzymywane specjalnie po to. Także to, co trzeba pilnować przy zmianach.

## Weryfikacja

Nazwy testów, które **spadną**, jeśli decyzja zostanie odwrócona — dzięki temu jej cofnięcie wymaga
świadomego usunięcia asercji, widocznego w diffie. Strażników pisze się w
`WordParserCore.Tests/ArchitectureDecisionTests.cs`, chyba że istnieje już test w naturalnym miejscu.
Jeśli decyzji nie da się objąć testem, napisz to wprost i wskaż, co ją pilnuje.
