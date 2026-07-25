namespace Saga.Core.Ingest
{
	/// <summary>
	/// Rola bloku w dokumencie. Bloki przypisów (np. odnośniki u dołu strony PDF)
	/// są pomijane przez parser strukturalny, ale mogą zasilać klasyfikator dokumentu
	/// (sygnały tekstu jednolitego — § 102-106 ZTP).
	/// </summary>
	public enum BlockRole
	{
		Body,
		FootnoteText,
	}
}
