namespace Saga.Model
{
    /// <summary>
    /// Rodzaj sygnału klasyfikacyjnego dokumentu — dowód użyty przez DocumentClassifier
    /// do rozpoznania rodzaju aktu prawnego wg Zasad techniki prawodawczej (ZTP).
    /// Każdy sygnał niesie punktację i (opcjonalnie) wspierany typ aktu.
    /// </summary>
    public enum DocumentSignalKind
    {
        /// <summary>Za mało bloków tekstu, by klasyfikować (np. skan bez warstwy tekstowej).</summary>
        NoTextLayer,

        /// <summary>Nagłówek rodzaju aktu w strefie tytułowej („USTAWA", „ROZPORZĄDZENIE"…; § 16/102/120/138a).</summary>
        ActKindHeader,

        /// <summary>Drugi nagłówek rodzaju aktu zignorowany (pułapka załącznika TJ; § 102).</summary>
        IgnoredSecondaryHeader,

        /// <summary>Data aktu „z dnia D miesiąca RRRR r." (§ 17).</summary>
        ActDate,

        /// <summary>Przedmiot aktu („o …", „w sprawie …", „Kodeks/Prawo/Ordynacja"; § 18-19/120).</summary>
        ActSubject,

        /// <summary>Tytuł zmieniający „o zmianie ustawy…" (§ 96) → akt nowelizujący.</summary>
        AmendingTitle,

        /// <summary>„w sprawie ogłoszenia jednolitego tekstu" (§ 102) → tekst jednolity.</summary>
        ConsolidatedTextTitle,

        /// <summary>Formuła obwieszczenia TJ z art. 16 ustawy o ogłaszaniu aktów normatywnych (§ 104).</summary>
        ConsolidatedTextFormula,

        /// <summary>Formuła kompetencyjna „…zarządza/uchwala/postanawia się, co następuje:" (§ 121).</summary>
        EnactmentFormula,

        /// <summary>Podstawa prawna „Na podstawie art. …" (akty wykonawcze; § 121).</summary>
        LegalBasis,

        /// <summary>Dominacja jednostki podstawowej: „Art." (ustawa) vs „§" (rozporządzenie/uchwała).</summary>
        BaseUnitDominance,

        /// <summary>Ciągłość numeracji jednostki podstawowej od pierwszego numeru (TJ nie zaczynają od 1).</summary>
        NumberingContinuity,

        /// <summary>Formuła wejścia w życie z zamkniętego katalogu § 45.</summary>
        EntryIntoForce,

        /// <summary>Markery „(uchylony)"/„(utracił moc)" (§ 106) — typowe dla tekstu jednolitego.</summary>
        RepealedMarkers,

        /// <summary>Komendy nowelizacyjne „…wprowadza się następujące zmiany:" (§ 82-85).</summary>
        AmendmentCommands,

        /// <summary>Publikator wojewódzki „Dz. Urz. Woj." (§ 162) → akt prawa miejscowego.</summary>
        VoivodeshipJournal,

        /// <summary>Organ JST (rada gminy, sejmik, wójt, burmistrz…) → akt prawa miejscowego.</summary>
        LocalGovernmentOrgan,

        /// <summary>Organ wydający „Marszałek Sejmu" (§ 102) → obwieszczenie / tekst jednolity ustawy.</summary>
        MarshalOfSejmIssuer,

        /// <summary>Gęstość odnośników w indeksie górnym [N)] (§ 163) — typowa dla tekstu jednolitego.</summary>
        FootnoteDensity,

        /// <summary>Styl Word rozpoznający metadane aktu (OZN_RODZ_AKTU/DATA_AKTU/TYTUŁ_AKTU) — jeden z sygnałów.</summary>
        WordStyleHint,

        /// <summary>Przewaga zwycięzcy nad drugim typem poniżej marginesu — wynik niejednoznaczny.</summary>
        AmbiguousType,
    }
}
