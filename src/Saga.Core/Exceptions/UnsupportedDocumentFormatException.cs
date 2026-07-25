namespace Saga.Core.Exceptions
{
	/// <summary>
	/// Dokument w formacie nieobsługiwanym przez potok parsowania (np. PDF zaszyfrowany hasłem,
	/// nierozpoznana sygnatura pliku). Komunikat wyjaśnia przyczynę po polsku.
	/// </summary>
	public sealed class UnsupportedDocumentFormatException : ParsingException
	{
		public UnsupportedDocumentFormatException(string message) : base(message) { }
		public UnsupportedDocumentFormatException(string message, System.Exception innerException)
			: base(message, innerException) { }
	}
}
