using System.Collections.Generic;
using ModelDto;
using WordParserCore.Ingest;

namespace WordParserCore.Services.Classify.Document
{
	/// <summary>
	/// Klasyfikator dokumentu: rozpoznaje, czy zbiór bloków to normatywny akt prawny
	/// i jakiego rodzaju (wg ZTP). Nie decyduje o parsowaniu — zwraca raport dla wywołującego.
	/// </summary>
	public interface IDocumentClassifier
	{
		DocumentClassificationResult Classify(IReadOnlyList<DocumentBlock> blocks);
	}
}
