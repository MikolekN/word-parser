#nullable enable
using ModelDto;
using WordParserCore.Ingest;

namespace WordParserCore
{
	/// <summary>
	/// Koperta wyniku parsowania uniwersalnego wejścia: raport klasyfikacji dokumentu
	/// oraz (opcjonalnie) zbudowany model. <see cref="Document"/> jest null, gdy polityka
	/// pominęła budowę modelu (<see cref="ParsePolicy.ClassifyOnly"/> albo
	/// <see cref="ParsePolicy.ParseWhenLegalAct"/> dla dokumentu nierozpoznanego jako akt).
	/// </summary>
	public sealed record ParseResult
	{
		public required DocumentClassificationResult Classification { get; init; }

		public LegalDocument? Document { get; init; }

		/// <summary>Rozpoznany (lub wymuszony) format źródłowy; Unknown przy parsowaniu z gotowych bloków.</summary>
		public SourceFormat SourceFormat { get; init; }

		/// <summary>Liczba bloków reprezentacji pośredniej odczytanych z dokumentu (łącznie z pustymi).</summary>
		public int BlockCount { get; init; }
	}
}
