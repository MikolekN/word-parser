#nullable enable

namespace ModelDto
{
    /// <summary>
    /// Pojedynczy sygnał klasyfikacyjny z dowodem — wynik dopasowania wzorca ZTP
    /// w konkretnym bloku dokumentu. Zbiór sygnałów uzasadnia decyzję DocumentClassifier
    /// (każdy niesie własną punktację i, opcjonalnie, wspierany rodzaj aktu).
    /// </summary>
    public sealed record DocumentSignal
    {
        /// <summary>Rodzaj sygnału.</summary>
        public required DocumentSignalKind Kind { get; init; }

        /// <summary>Waga punktowa wniesiona przez sygnał (może być 0 dla sygnałów czysto informacyjnych).</summary>
        public int Score { get; init; }

        /// <summary>Rodzaj aktu wspierany przez sygnał (null, gdy sygnał nie różnicuje typu).</summary>
        public LegalActType? SupportsType { get; init; }

        /// <summary>Indeks bloku źródłowego (diagnostyka; -1 gdy nie dotyczy pojedynczego bloku).</summary>
        public int BlockIndex { get; init; } = -1;

        /// <summary>Dopasowany fragment tekstu (dowód).</summary>
        public string MatchedText { get; init; } = string.Empty;

        /// <summary>Opis po polsku, co sygnał oznacza.</summary>
        public string Description { get; init; } = string.Empty;
    }
}
