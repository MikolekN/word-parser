namespace Saga.Model
{
    /// <summary>
    /// Typ aktu prawnego - określa formę legislacyjną i konwencje nazewnictwa jednostek.
    /// 
    /// Trzy główne typy aktów różnią się:
    /// - Konwencją nazw jednostek redakcyjnych
    /// - Użyciem znaków specjalnych (§)
    /// - Strukturą hierarchii
    /// </summary>
    public enum LegalActType
    {
        /// <summary>
        /// Ustawa (forma legislacyjna)
        /// - Artykuły oznaczane "Art."
        /// - Ustępy bez przedrostka, tylko numer kolejny
        /// Przykład: Art. 5 ust. 2 pkt 1
        /// </summary>
        Statute,

        /// <summary>
        /// Projekt ustawy (forma legislacyjna)
        /// - Struktura i nazewnictwo jak ustawa
        /// </summary>
        Bill,

        /// <summary>
        /// Rozporządzenie (forma legislacyjna)
        /// - Artykuły oznaczane znakiem paragrafu "§"
        /// - Ustępy bez przedrostka, tylko numer kolejny
        /// Przykład: § 5 ust. 2 pkt 1
        /// </summary>
        Regulation,

        /// <summary>
        /// Kodeks (forma legislacyjna)
        /// - Artykuły oznaczane "Art."
        /// - Ustępy oznaczane znakiem paragrafu "§" i nazywane paragrafami
        /// Przykład: Art. 5 § 2 pkt 1
        /// </summary>
        Code,

        /// <summary>
        /// Ordynancja (forma legislacyjna)
        /// - Struktura i nazewnictwo jak ustawa
        /// </summary>
        Ordinance,

        /// <summary>
        /// Ocena skutków regulacji (forma legislacyjna)
        /// - Struktura i nazewnictwo do doprecyzowania
        /// </summary>
        RegulatoryImpactAssessment,

        /// <summary>
        /// Ustawa zmieniająca (§ 96 ZTP) — „o zmianie ustawy…".
        /// Struktura i nazewnictwo jak ustawa; treść to komendy nowelizacyjne.
        /// </summary>
        AmendingStatute,

        /// <summary>
        /// Obwieszczenie o ogłoszeniu tekstu jednolitego (§ 102-106 ZTP).
        /// Zawiera w załączniku pełny akt bazowy z markerami „(uchylony)" itp.
        /// </summary>
        Announcement,

        /// <summary>
        /// Uchwała (§ 138a-139 ZTP) — jednostka podstawowa „§".
        /// </summary>
        Resolution,

        /// <summary>
        /// Zarządzenie (§ 138a-139 ZTP) — jednostka podstawowa „§".
        /// </summary>
        ExecutiveOrder,

        /// <summary>
        /// Akt prawa miejscowego (§ 143 ZTP) — jednostka podstawowa „§";
        /// wydawany przez organy JST, publikowany w Dz. Urz. Woj.
        /// </summary>
        LocalLegalAct
    }

    /// <summary>
    /// Helper do konwersji typów aktów na etykiety wyświetlające.
    /// </summary>
    public static class LegalActTypeExtensions
    {
        /// <summary>
        /// Zwraca etykietę dla głównej jednostki redakcyjnej (artykułu).
        /// </summary>
        public static string GetMainUnitLabel(this LegalActType type) =>
            type switch
            {
                LegalActType.Statute => "art.",
                LegalActType.Regulation => "§",
                LegalActType.Code => "art.",
                LegalActType.AmendingStatute => "art.",
                LegalActType.Resolution => "§",
                LegalActType.ExecutiveOrder => "§",
                LegalActType.LocalLegalAct => "§",
                _ => "art."
            };

        /// <summary>
        /// Zwraca etykietę dla podjednostki (ustępu/paragrafu).
        /// W ustawie i rozporządzeniu: brak przedrostka (tylko numer)
        /// W kodeksie: "§" (paragrafy)
        /// </summary>
        public static string GetSubUnitLabel(this LegalActType type) =>
            type switch
            {
                LegalActType.Statute => "",  // brak przedrostka
                LegalActType.Regulation => "", // brak przedrostka
                LegalActType.Code => "§",   // paragrafy
                _ => ""
            };

        /// <summary>
        /// Zwraca przyjazną nazwę typu aktu.
        /// </summary>
        public static string ToFriendlyString(this LegalActType type) =>
            type switch
            {
                LegalActType.Statute => "ustawa",
                LegalActType.Regulation => "rozporządzenie",
                LegalActType.Code => "kodeks",
                LegalActType.Bill => "projekt ustawy",
                LegalActType.Ordinance => "ordynancja",
                LegalActType.RegulatoryImpactAssessment => "OSR ex post",
                LegalActType.AmendingStatute => "ustawa zmieniająca",
                LegalActType.Announcement => "obwieszczenie (tekst jednolity)",
                LegalActType.Resolution => "uchwała",
                LegalActType.ExecutiveOrder => "zarządzenie",
                LegalActType.LocalLegalAct => "akt prawa miejscowego",
                _ => "nieznany"
            };
    }
}
