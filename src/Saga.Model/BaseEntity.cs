using System;
using System.Collections.Generic;
using ModelDto.EditorialUnits;
using ModelDto.SystematizingUnits;

#nullable enable

namespace ModelDto
{
    /// <summary>
    /// Bazowy DTO dla wszystkich encji modelu (artykuł, ustęp, punkt, litera, tiret, jednostki systematyzujące).
    /// Zawiera dane opisujące strukturę i zależności bez logiki parsowania.
    /// Wprowadza: UnitType, DisplayLabel, EIdPrefix oraz centralne budowanie stabilnego eId.
    /// </summary>
    public enum UnitType
    {
        Unknown,
        // editorial units
        Article,
        Paragraph, // ustęp
        Point,
        Letter,
        Tiret,
        CommonPart, // część wspólna (intro/wrapUp) - wirtualny byt redakcyjny
        // systematizing units
        Part,
        Book,
        Title,
        Division,
        Chapter,
        Subchapter
    }

    public abstract class BaseEntity
    {
        public Guid Guid { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Typ jednostki semantycznej (np. Article/Paragraph/Point/Letter/Tiret/...)
        /// </summary>
        public UnitType UnitType { get; set; } = UnitType.Unknown;
        
        /// <summary>
        /// Etykieta do wyświetlenia (np. "art.", "ust.", "§")
        /// </summary>
        public string DisplayLabel { get; set; } = string.Empty;        
        
        /// <summary>
        /// Prefiks używany przy generowaniu eId (np. "art"/"ust"/"pkt"/"lit"/"tir"/"cz"/"ks" ...).
        /// Można go nadpisać w klasach pochodnych jeżeli konieczne.
        /// </summary>
        public string EIdPrefix { get; set; } = string.Empty;
       
        /// <summary>
        /// Numer encji (np. 10 dla artykułu, 2 dla ustępu, f dla litery).
        /// Zawiera rozbicie na komponenty: część liczbowa, tekstowa i indeks górny.
        /// </summary>
        public EntityNumber? Number { get; set; }

        //TODO: ContentText to tekst jednostki redakcyjnej. Tymczasowo jest to string, ale trzeba uwzgędnić:
        // - możliwość występowania elementów osadzonych, jak tabele, grafiki, wzory matematyczne itp.
        // - obecnie konwertujemy wszystko do tekstu płaskiego, ale w przyszłości będzie potrzebny model drzewa, 
        //  dzieląc na poszczególne akapity (zdania)
        // Do rozważenia - Content trzymać w formie HTML z formatowaniem?
        public string ContentText { get; set; } = string.Empty;

        /// <summary>
        /// Data wejścia w życie danej jednostki redakcyjnej.
        /// </summary>
        public DateTime EffectiveDate { get; set; }
        
        /// <summary>
        /// Komunikaty walidacji/diagnostyki zestawiane podczas parsowania
        /// (błędy, ostrzeżenia, informacje o jakości dokumentu).
        /// </summary>
        public List<ValidationMessage> ValidationMessages { get; set; } = new();

        // Referencje do encji nadrzędnych (ułatwiają nawigację w modelu)
        public BaseEntity? Parent { get; set; }
        public Article? Article { get; set; }
        public Paragraph? Paragraph { get; set; }
        public Point? Point { get; set; }
        public Letter? Letter { get; set; }
        public Tiret? Tiret { get; set; }

        /// <summary>
        /// Lokalny segment identyfikatora (np. "art_10" albo "lit_a").
        /// Można nadpisać w klasach pochodnych, gdy format różni się.
        /// </summary>
        protected virtual string GetLocalIdSegment()
        {
            if (string.IsNullOrEmpty(EIdPrefix)) return string.Empty;

            // prefer specjalne pola
            if (this is Letter l)
            {
                return string.IsNullOrEmpty(l.Number?.Value) ? EIdPrefix : $"{EIdPrefix}_{l.Number.Value}";
            }

            if (this is Tiret t)
            {
                return string.IsNullOrEmpty(t.Number?.Value) ? EIdPrefix : $"{EIdPrefix}_{t.Number.Value}";
            }

            if (!string.IsNullOrEmpty(Number?.Value))
            {
                return $"{EIdPrefix}_{Number.Value}";
            }

            return EIdPrefix;
        }

        /// <summary>
        /// Hierarchiczne, stabilne i parsowalne Id (eId) budowane od korzenia w dół,
        /// używa separatora "__" zgodnie z założeniem (np. "art_10__ust_2__pkt_3").
        /// </summary>
        public virtual string Id
        {
            get
            {
                var parts = new List<string>();
                BaseEntity? current = this;
                while (current != null)
                {
                    if (current is ISystematizingUnit systematizingUnit && systematizingUnit.IsImplicit)
                    {
                        current = current.Parent;
                        continue;
                    }
                    var seg = current.GetLocalIdSegment();
                    if (!string.IsNullOrEmpty(seg)) parts.Add(seg);
                    current = current.Parent;
                }
                parts.Reverse();
                return string.Join("__", parts);
            }
        }
    }
}
