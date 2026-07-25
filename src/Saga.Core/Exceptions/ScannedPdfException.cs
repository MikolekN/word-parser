namespace Saga.Core.Exceptions
{
	/// <summary>
	/// PDF bez użytecznej warstwy tekstowej (skan/obraz). OCR jest poza zakresem parsera —
	/// dokument jest odrzucany z czytelnym komunikatem (rozstrzygnięcie planu przebudowy).
	/// </summary>
	public sealed class ScannedPdfException : ParsingException
	{
		public ScannedPdfException(string message) : base(message) { }
	}
}
