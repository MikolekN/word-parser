#nullable enable

namespace Saga.Core.Ingest
{
	/// <summary>
	/// Położenie bloku w dokumencie źródłowym — do diagnostyki
	/// i lokalizowania sygnałów klasyfikacji.
	/// </summary>
	public readonly record struct BlockSourceLocation
	{
		/// <summary>
		/// Indeks bloku w całym dokumencie (0-based, ciągły);
		/// null, gdy lokalizacja jest nieznana (np. blok utworzony poza pełnym odczytem dokumentu).
		/// </summary>
		public int? BlockIndex { get; init; }

		/// <summary>Numer strony (1-based) — tylko PDF; null dla DOCX/TXT.</summary>
		public int? PageNumber { get; init; }

		/// <summary>Numer pierwszej linii źródłowej bloku — PDF/TXT; null dla DOCX.</summary>
		public int? LineNumber { get; init; }
	}
}
