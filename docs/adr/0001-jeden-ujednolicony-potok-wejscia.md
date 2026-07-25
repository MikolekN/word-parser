# ADR-0001: Wszystkie formaty wejścia schodzą się do jednego potoku przez reprezentację pośrednią

- Status: Accepted
- Data: 2026-07-16 (commit b75a4f7)

## Kontekst

Parser przyjmował wyłącznie DOCX napisany na specjalistycznym szablonie RCL (style `ART`, `UST`,
`Z/*`). Doszły trzy nowe wejścia: DOCX bez szablonu, PDF z warstwą tekstową, TXT. Analiza wykazała,
że granica OpenXml kończy się w `ParserOrchestrator.ProcessParagraph` — dalej potok operuje na
`(string text, string? styleId)`, a `ModelDto` jest w 100% wolne od OpenXml.

## Decyzja

Wprowadzono reprezentację pośrednią `src/Saga.Core/Ingest/` (`DocumentBlock`, `BlockLayoutInfo`,
`BlockSourceLocation`). Każdy format ma adapter (`IDocumentBlockReader`) produkujący
`IReadOnlyList<DocumentBlock>`; dalej istnieje **jeden** potok budowy modelu. Styl Word jest w nim
jednym z sygnałów klasyfikacji, a nie warunkiem działania.

## Odrzucone alternatywy

**Osobny parser dla PDF/TXT obok istniejącego DOCX.** Kuszące, bo nie ruszałoby działającego kodu.
Odrzucone: reguły ZTP (oznaczenia jednostek, hierarchia, nowelizacje) musiałyby zostać
zaimplementowane po raz drugi i natychmiast zaczęłyby się rozjeżdżać, a testy regresji straciłyby
wspólny punkt odniesienia — nie dałoby się porównać, czy ten sam akt w DOCX i w TXT daje ten sam
model.

**Konwersja PDF/TXT → DOCX przed parsowaniem.** Odrzucone: wymaga generowania sztucznych stylów,
co jest osobno zakazane (ADR-0002), i dokłada format pośredni bez żadnej korzyści informacyjnej.

## Konsekwencje

`DocumentBlock` jest kontraktem wewnętrznym potoku i celowo **nie** trafia do `ModelDto` — inaczej
graf zależności otworzyłby się na szczegóły odczytu formatu, a DTO przestałoby być czystym wynikiem.

Adapter DOCX iteruje `Descendants<Word.Paragraph>()` tak samo jak wcześniejszy kod (parytet — łapie
też akapity w tabelach). `ProcessParagraph(Word.Paragraph, …)` został zachowany jako cienki adapter
nad `ProcessBlock`, bo kilkanaście klas testowych buduje wejście helperem
`CreateParagraph(text, styleId)`; usunięcie go wymusiłoby przepisanie testów dokładnie wtedy, gdy
stanowiły siatkę bezpieczeństwa przebudowy.

Wcięcia są przenoszone w twipach (DOCX natywnie, PDF przeliczany z pozycji X). Mapowanie
„wcięcie → poziom hierarchii" należy do warstwy klasyfikacji, nie do adaptera: adapter zgadujący
poziom ukryłby błąd przeliczenia w miejscu bez dostępu do kontekstu listy.

## Weryfikacja

- `ArchitectureDecisionTests.Adr0001_ModelDto_DoesNotDependOnParserOrIntermediateRepresentation` —
  pilnuje, że `ModelDto` nie sięga do parsera, IR ani OpenXml.
- `ParserEquivalenceTests` — pilnuje, że ten sam akt podany różnymi formatami daje równoważny model;
  to test sensu istnienia wspólnego potoku.

Samego zakazu „drugiego parsera" test nie obejmuje — pilnuje go rewizja zmian.
