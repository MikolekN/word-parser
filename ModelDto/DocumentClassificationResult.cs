using System.Collections.Generic;

#nullable enable

namespace ModelDto
{
    /// <summary>
    /// Wynik klasyfikacji dokumentu: czy to normatywny akt prawny, jakiego rodzaju,
    /// z jaką pewnością i na podstawie jakich sygnałów (ZTP). Decyzję „czy parsować"
    /// podejmuje wywołujący — klasyfikator wyłącznie raportuje.
    /// </summary>
    public sealed record DocumentClassificationResult
    {
        /// <summary>Rozpoznany rodzaj aktu; null gdy dokument nie jest aktem normatywnym.</summary>
        public LegalActType? ActType { get; init; }

        /// <summary>Czy dokument uznano za normatywny akt prawny (winner ≥ próg).</summary>
        public bool IsNormativeAct { get; init; }

        /// <summary>Czy to tekst jednolity (obwieszczenie „w sprawie ogłoszenia jednolitego tekstu").</summary>
        public bool IsConsolidatedText { get; init; }

        /// <summary>Czy to akt zmieniający („o zmianie…" / komendy nowelizacyjne).</summary>
        public bool IsAmending { get; init; }

        /// <summary>
        /// Pewność klasyfikacji 1–100. Dla aktu = wynik zwycięzcy; dla nie-aktu = pewność,
        /// że to NIE akt. Interpretacja: ≥75 wysoka, 40–74 średnia, &lt;40 nie parsować.
        /// </summary>
        public int Confidence { get; init; }

        /// <summary>Sygnały klasyfikacyjne (dowody), posortowane malejąco wg wagi.</summary>
        public IReadOnlyList<DocumentSignal> Signals { get; init; } = new List<DocumentSignal>();

        /// <summary>Zwięzłe uzasadnienie decyzji po polsku.</summary>
        public string Justification { get; init; } = string.Empty;
    }
}
