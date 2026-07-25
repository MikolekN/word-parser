using System.Collections.Generic;

namespace Saga.Core.Ingest
{
	/// <summary>
	/// Składa wiersze źródłowe (TXT) w bloki reprezentacji pośredniej: <b>jeden niepusty wiersz = jeden blok</b>,
	/// puste wiersze pomijane. Nowa linia jest traktowana jako granica akapitu — model najbliższy DOCX
	/// (jeden akapit = jeden blok) dla dominującego przypadku wejścia (eksport Word→TXT, kopiuj-wklej,
	/// zrzut tekstu), gdzie każda jednostka/wiersz tytułowy stoi w osobnej linii.
	///
	/// Świadomie NIE odtwarzamy akapitów z twardo zawijanego (fixed-width) tekstu przez doklejanie
	/// kontynuacji: heurystyka „dokup wiersz do poprzedniego" jest nierozstrzygalna (myli wiersze
	/// strefy tytułowej „USTAWA/z dnia/o…" ze sobą, dzieli zdania na tokenach markerowych, a dehyfenacja
	/// niszczy dywizy złożeń typu „społeczno-gospodarczy"). Rejoin z geometrii należy do adaptera PDF (Etap 9),
	/// gdzie granice wierszy odtwarza się z pozycji, a nie zgaduje.
	/// </summary>
	internal static class BlockAssembler
	{
		public static IReadOnlyList<DocumentBlock> Assemble(IEnumerable<TextLine> lines)
		{
			var blocks = new List<DocumentBlock>();
			int blockIndex = 0;
			int lineNo = 0;

			foreach (var line in lines)
			{
				lineNo++;
				if (line.IsBlank)
					continue;

				blocks.Add(new DocumentBlock
				{
					Text = line.Text.Trim(),
					Source = new BlockSourceLocation { BlockIndex = blockIndex, LineNumber = line.LineNumber ?? lineNo },
					Role = BlockRole.Body,
				});
				blockIndex++;
			}

			return blocks;
		}
	}
}
