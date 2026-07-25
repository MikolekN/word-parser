namespace WordParserCore.Ingest
{
	/// <summary>
	/// Pojedynczy wiersz źródłowy przed złożeniem w blok. Wspólna jednostka wejścia
	/// dla BlockAssembler (TXT dziś, PDF w Etapie 9). LineNumber jest 1-based; null gdy nieznany.
	/// </summary>
	internal readonly record struct TextLine(string Text, int? LineNumber = null)
	{
		public bool IsBlank => string.IsNullOrWhiteSpace(Text);
	}
}
