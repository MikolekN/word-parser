using System;
using ModelDto;

#nullable enable

namespace ModelDto.EditorialUnits
{
    /// <summary>
    /// Czesc wspolna (intro/wrapUp) - wirtualna jednostka redakcyjna reprezentujaca
    /// tekst wystepujacy przed lista elementow wyliczeniowych lub po niej.
    /// 
    /// CommonPart jest na tym samym poziomie hierarchii co elementy wyliczeniowe,
    /// ktorych dotyczy (np. rodzenstwo punktow, liter, tiretow).
    /// 
    /// Typowe poziomy wystepowania:
    /// - Paragraph: CommonParts (wobec listy punktow)
    /// - Point: CommonParts (wobec listy liter)
    /// - Letter: CommonParts (wobec listy tiretow)
    /// 
    /// CommonPart NIE wystepuje w Article jako bezposrednie dziecko.
    /// 
    /// Nie jest jednostka prawa w tradycyjnym sensie, lecz adresowalnym fragmentem
    /// w systemie redakcyjnym i w modelu zmian legislacyjnych.
    /// 
    /// W publikacyjnym XML (AKN/ELI) CommonPart nie pojawia sie jawnie - jego zawartosc
    /// trafia bezposrednio do elementow intro lub wrapUp.
    /// 
    /// Przyklad hierarchii:
    ///   Article (art_5)
    ///     └─ Paragraph (art_5__ust_1)
    ///         ├─ CommonPart Intro (art_5__ust_1__intro)
    ///         ├─ Point (art_5__ust_1__pkt_1)
    ///         │   ├─ CommonPart Intro (art_5__ust_1__pkt_1__intro)
    ///         │   ├─ Letter (art_5__ust_1__pkt_1__lit_a)
    ///         │   │   ├─ CommonPart Intro (art_5__ust_1__pkt_1__lit_a__intro)
    ///         │   │   ├─ Tiret (art_5__ust_1__pkt_1__lit_a__tir_1)
    ///         │   │   └─ CommonPart WrapUp (art_5__ust_1__pkt_1__lit_a__wrapUp)
    ///         │   └─ CommonPart WrapUp (art_5__ust_1__pkt_1__wrapUp)
    ///         └─ CommonPart WrapUp (art_5__ust_1__wrapUp)
    /// </summary>
    public class CommonPart : BaseEntity
    {
        /// <summary>
        /// Typ czesci wspolnej: Intro (przed lista) lub WrapUp (po liscie).
        /// </summary>
        public CommonPartType Type { get; set; } = CommonPartType.Intro;

        /// <summary>
        /// Hierarchiczny identyfikator jednostki nadrzednej (np. "art_5__ust_1").
        /// Sluzy do precyzyjnego odwolania w systemie zmian legislacyjnych.
        /// </summary>
        public string ParentEId { get; set; } = string.Empty;

        /// <summary>
        /// Opcjonalne powiazanie z segmentem tekstu (np. zdaniem) w jednostce nadrzednej.
        /// Pozwala wskazac, ktore zdanie pelni role intro/wrapUp.
        /// </summary>
        public int? SourceSegmentOrder { get; set; }

        public CommonPart()
        {
            UnitType = UnitType.CommonPart;
            EIdPrefix = Type.ToEIdSegment();
        }

        /// <summary>
        /// Konstruktor z parametrami.
        /// </summary>
        public CommonPart(CommonPartType type, string parentEId, string contentText, int? sourceSegmentOrder = null) : this()
        {
            Type = type;
            ParentEId = parentEId;
            ContentText = contentText;
            SourceSegmentOrder = sourceSegmentOrder;
            EIdPrefix = type.ToEIdSegment();
        }

        /// <summary>
        /// Zwraca segment ID dla czesci wspolnej (np. "intro" lub "wrapUp").
        /// Nie dodaje przedrostka numerycznego - CommonPart nie ma numeru.
        /// </summary>
        protected override string GetLocalIdSegment()
        {
            return Type.ToEIdSegment();
        }

        /// <summary>
        /// Wlasciwosc wyswietlajaca typ czesci wspolnej do celow UI.
        /// </summary>
        public string TypeDisplayLabel => Type.ToFriendlyString();

        public override string ToString()
        {
            return $"CommonPart [{Type}] w {ParentEId}: {ContentText}";
        }
    }
}
