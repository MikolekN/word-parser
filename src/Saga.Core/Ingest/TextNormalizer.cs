using System.Text;

namespace WordParserCore.Ingest
{
	/// <summary>
	/// Normalizacja surowego tekstu z formatów tekstowych (TXT/PDF) do kanału zgodnego
	/// z DOCX GetFullText — warunek spójności kanału między adapterami (R9 planu).
	/// Robi WYŁĄCZNIE to, czego nie zrobi późniejszy Sanitize orkiestratora:
	///   - ujednolica końce linii (CRLF/CR → LF),
	///   - usuwa znaki niewidoczne, których DOCX GetFullText nie emituje, a Sanitize nie zdejmuje
	///     (nie są białymi znakami): miękki dywiz U+00AD oraz znaki zerowej szerokości / formatujące
	///     U+200B/U+200C/U+200D/U+2060/U+FEFF (artefakty kopiuj-wklej, sklejanych plików).
	/// Zwijanie białych znaków i normalizację półpauzy pozostawia wspólnemu Sanitize (parytet obu ścieżek).
	/// </summary>
	internal static class TextNormalizer
	{
		public static string Normalize(string raw)
		{
			if (string.IsNullOrEmpty(raw))
				return string.Empty;

			var text = raw.Replace("\r\n", "\n").Replace('\r', '\n');

			var sb = new StringBuilder(text.Length);
			foreach (var c in text)
			{
				if (IsRemovableInvisible(c))
					continue;
				sb.Append(c);
			}
			return sb.ToString();
		}

		private static bool IsRemovableInvisible(char c) =>
			c is '\u00AD'   // miękki dywiz
			  or '\u200B'   // zero-width space
			  or '\u200C'   // zero-width non-joiner
			  or '\u200D'   // zero-width joiner
			  or '\u2060'   // word joiner
			  or '\uFEFF';  // zero-width no-break space / BOM w środku strumienia
	}
}
