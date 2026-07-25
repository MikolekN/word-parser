#nullable enable

namespace ModelDto.SystematizingUnits
{
    /// <summary>
    /// Kontrakt dla jednostek systematyzujących (organizacyjnych) aktu prawnego.
    /// 
    /// Jednostki systematyzujące służą do strukturyzacji aktu prawnego w bloki tematyczne,
    /// ale same nie zawierają treści prawnej (w przeciwieństwie do jednostek redakcyjnych).
    /// 
    /// Przykłady: Część, Księga, Tytuł, Dział, Rozdział, Podrozdział.
    /// 
    /// Uwaga: Każdy akt zawiera pełną minimalną hierarchię jednostek systematyzujących,
    /// a jednostki nieobecne w tekście są oznaczane jako IsImplicit = true.
    /// 
    /// Każda jednostka systematyzująca ma:
    /// - Heading: krótki tytuł opisujący zakres tematyczny (np. "Przepisy ogólne", "Odpowiedzialność karna")
    /// - Number: numer jednostki (np. "I", "II", "1", "2")
    /// - IsImplicit: czy jednostka jest domyślna/niejawna (nie występuje w tekście)
    /// - Hierarchię: zawiera inne jednostki systematyzujące (najniżej: oddział z artykułami)
    /// </summary>
    public interface ISystematizingUnit
    {
        /// <summary>
        /// Krótki tytuł/nagłówek jednostki systematyzującej.
        /// Przykłady: "Przepisy ogólne", "Odpowiedzialność karna", "Postępowanie administracyjne".
        /// </summary>
        string Heading { get; set; }

        /// <summary>
        /// Czy jednostka jest domyślna/ukryta (niejawna w dokumencie).
        /// </summary>
        bool IsImplicit { get; set; }
    }
}
