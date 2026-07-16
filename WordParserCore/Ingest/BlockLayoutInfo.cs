#nullable enable

namespace WordParserCore.Ingest
{
	/// <summary>
	/// Metadane układu bloku — jednostka kanoniczna: twips (1 pt = 20 twips).
	/// Mapowanie twips → poziom hierarchii to zadanie klasyfikatora, nie IR.
	/// Kursywa (IsItalic) jest w tekstach jednolitych markerem aktów/przepisów,
	/// które utraciły moc (§ 108a ZTP), oraz zlikwidowanych/przekształconych organów
	/// (§ 108b); pogrubienie (IsBold) — przyszłych brzmień (§ 106a ust. 4).
	/// </summary>
	public sealed record BlockLayoutInfo
	{
		/// <summary>Wcięcie lewe bloku (DOCX: Indentation.Left; PDF: pozycja X kontynuacji).</summary>
		public int? LeftIndentTwips { get; init; }

		/// <summary>Wcięcie pierwszego wiersza (§ 58 ZTP: art./ust. zaczynają się od akapitu).</summary>
		public int? FirstLineIndentTwips { get; init; }

		/// <summary>Wysunięcie (tylko DOCX: Indentation.Hanging).</summary>
		public int? HangingIndentTwips { get; init; }

		public BlockAlignment? Alignment { get; init; }

		/// <summary>Dominanta pogrubienia runów/liter bloku.</summary>
		public bool? IsBold { get; init; }

		/// <summary>Dominanta kursywy runów/liter bloku.</summary>
		public bool? IsItalic { get; init; }

		/// <summary>Dominujący rozmiar czcionki (half-points, jak w OpenXml).</summary>
		public double? FontSizeHalfPoints { get; init; }
	}
}
