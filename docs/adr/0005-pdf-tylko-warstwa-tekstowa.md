# ADR-0005: PDF przyjmowany wyłącznie z warstwą tekstową; skan odrzucany, OCR poza zakresem

- Status: Accepted
- Data: 2026-07-19 (commit e3de312)

## Kontekst

Akty prawne krążą również jako skany — PDF bez warstwy tekstowej albo z warstwą zepsutą (błędna
cmapa daje strumień `U+FFFD`). Taki plik technicznie otwiera się w bibliotece PDF i zwraca „tekst",
tylko że pusty lub bezwartościowy.

## Decyzja

`PdfBlockReader` wykrywa brak użytecznej warstwy tekstowej (progi: gęstość znaków na stronę, udział
stron pustych, udział `U+FFFD`) i rzuca `ScannedPdfException` z komunikatem po polsku. OCR nie jest
częścią projektu.

## Odrzucone alternatywy

**Wbudowanie OCR (Tesseract).** Odrzucone: jakość rozpoznania na dokumentach prawnych z indeksami
górnymi, tiretami i cytatami w cudzysłowach jest niewystarczająca, a błędy OCR wchodziłyby do modelu
nieodróżnialnie od treści prawdziwej — akt prawny z przekręconą liczbą jest gorszy niż brak aktu.
Dochodzi ciężar zależności (modele językowe, natywne binarki w obrazie Docker).

**Ciche zwracanie pustego modelu.** Odrzucone: wywołujący nie odróżniłby „dokument nie ma treści" od
„nie umiemy tego przeczytać", a użytkownik Web dostałby puste okno bez wyjaśnienia.

## Konsekwencje

Użytkownik ze skanem dostaje jawną odmowę z powodem, a nie model do wyrzucenia. Progi detekcji są
heurystyką — PDF hybrydowy (część stron ze warstwą, część bez) może przejść, dając model niepełny;
to znany kompromis, nie przeoczenie.

Gdyby OCR miał kiedyś wejść w zakres, właściwym miejscem jest osobny preprocesor przed adapterem
(PDF-skan → PDF z warstwą), nie modyfikacja `PdfBlockReader` — adapter ma zostać czytnikiem, nie
rozpoznawaczem obrazu.
