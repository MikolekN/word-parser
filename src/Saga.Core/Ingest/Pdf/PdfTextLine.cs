#nullable enable

namespace WordParserCore.Ingest.Pdf
{
	/// <summary>
	/// Wiersz tekstu odtworzony z liter strony PDF (współrzędne w punktach; oś Y rośnie DO GÓRY).
	/// Tekst niesie już kanał indeksu górnego w notacji [x] (parytet z GetFullText DOCX).
	/// </summary>
	internal sealed record PdfTextLine
	{
		public required string Text { get; init; }

		/// <summary>Numer strony (1-based).</summary>
		public required int PageNumber { get; init; }

		/// <summary>X początku wiersza (punkt zerowy pierwszej litery).</summary>
		public required double StartX { get; init; }

		/// <summary>Linia bazowa wiersza (dominanta Y liter — superscripty nie przesuwają bazy).</summary>
		public required double BaselineY { get; init; }

		/// <summary>Dominujący rozmiar fontu wiersza (punkty).</summary>
		public required double DominantSize { get; init; }

		/// <summary>Wysokość strony (do klasyfikacji stref marginesów/przypisów).</summary>
		public required double PageHeight { get; init; }
	}
}
