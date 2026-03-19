using System;
using System.Collections.Generic;
using System.Linq;

#nullable enable

namespace ModelDto
{
    /// <summary>
    /// Model nowelizacji - pełna reprezentacja zmiany w akcie prawnym.
    /// Nowelizacja może dotyczyć uchylenia, dodania lub zmiany brzmienia jednostek redakcyjnych.
    /// </summary>
    public class Amendment
    {
        /// <summary>
        /// Unikalny identyfikator nowelizacji.
        /// </summary>
        public Guid Guid { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Rodzaj operacji nowelizacyjnej (uchylenie, dodanie, zmiana brzmienia).
        /// </summary>
        public AmendmentOperationType OperationType { get; set; }

        /// <summary>
        /// Treść nowelizacji w przypadku dodania lub zmiany brzmienia.
        /// Może zawierać hierarchiczną strukturę (artykuł z ustępami, punktami, literami, tiretami).
        /// Null w przypadku uchylenia.
        /// </summary>
        public AmendmentContent? Content { get; set; }

        /// <summary>
        /// Informacja o ustawie, której dotyczy nowelizacja (rok i lista pozycji publikatora).
        /// </summary>
        public JournalInfo TargetLegalAct { get; set; } = new();

        /// <summary>
        /// Cel nowelizacji - jednostki redakcyjne których dotyczy zmiana.
        /// Może być lista celów np. punkty 1-4 ustępu 2 artykułu 5.
        /// </summary>
        public List<StructuralAmendmentReference> Targets { get; set; } = new();

        /// <summary>
        /// Data wejścia w życie nowelizacji.
        /// </summary>
        public DateTime? EffectiveDate { get; set; }

        public override string ToString()
        {
            var targetStr = string.Join(", ", Targets.Select(t => t.ToString()));
            return $"{OperationType}: {targetStr} (wejście w życie: {EffectiveDate?.ToShortDateString() ?? "nieokreślone"})";
        }
    }

    
}