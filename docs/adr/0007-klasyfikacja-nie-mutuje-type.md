# ADR-0007: Rozpoznany rodzaj aktu nie nadpisuje `LegalDocument.Type`

- Status: Accepted
- Data: 2026-07-20 (commit 1350300)

## Kontekst

`LegalDocument.Type` (`LegalActType`) istniał przed klasyfikatorem i nigdy nie był ustawiany —
zostawał na domyślnej wartości `Statute`. Po wpięciu `DocumentClassifier` pojawiła się oczywista
pokusa: skoro znamy rodzaj aktu, wpiszmy go w `Type`.

## Decyzja

Wynik klasyfikacji jest zapisywany wyłącznie w `LegalDocument.Classification`
([LegalDocumentParser.cs:85](../../WordParserCore/LegalDocumentParser.cs#L85)). `Type` pozostaje pod
kontrolą wywołującego i nie jest ruszany przez potok.

## Odrzucone alternatywy

**Ustawianie `Type` z `Classification.ActType`.** Odrzucone z dwóch powodów. Po pierwsze, `Type`
sterują zachowania prezentacji i etykiet (`LegalActTypeExtensions.GetMainUnitLabel` decyduje, czy
jednostką podstawową jest `art.` czy `§`), więc wynik heurystyki zmieniłby renderowanie dokumentu
bez wiedzy konsumenta modelu. Po drugie, mieszałoby to dwa różne pojęcia: `Type` jest deklaracją
(„tak ten dokument traktujemy"), a `Classification` obserwacją z pewnością i dowodami — obserwacja
o pewności 45/100 nie powinna udawać deklaracji.

**Ustawianie `Type` tylko przy wysokiej pewności (≥75).** Odrzucone: próg ukryty w potoku daje
zachowanie nieprzewidywalne dla konsumenta — ten sam kod dla dwóch podobnych dokumentów raz
nadpisuje `Type`, raz nie, bez widocznej przyczyny.

## Konsekwencje

Konsument, który chce działać na rozpoznanym rodzaju, sięga po `Classification.ActType` i sam
decyduje, przy jakiej pewności mu to wystarcza. Rozbieżność między `Type` a `Classification.ActType`
jest dopuszczalna i nie jest błędem — to dwa różne stwierdzenia o dokumencie.

Gdyby kiedyś powstał wymóg „model ma znać swój rodzaj", właściwą drogą jest jawna metoda po stronie
wywołującego (np. `ApplyClassification()`), a nie cicha mutacja w `Parse`.
