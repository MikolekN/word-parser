using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace WordParserCore.Ingest
{
	/// <summary>
	/// Adapter zwykłego tekstu (TXT) → bloki reprezentacji pośredniej.
	/// Detekcja kodowania: BOM (UTF-32/UTF-16/UTF-8) → heurystyka UTF-16 bez BOM → ścisły UTF-8
	/// → fallback CP1250 (typowe dla polskich plików legacy). Normalizacja i segmentacja są wspólne
	/// z przyszłym adapterem PDF (TextNormalizer + BlockAssembler).
	/// </summary>
	public sealed class PlainTextBlockReader : IDocumentBlockReader
	{
		static PlainTextBlockReader()
		{
			// CP1250 nie jest wbudowane w .NET Core+ — rejestracja dostawcy stron kodowych.
			Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
		}

		public SourceFormat Format => SourceFormat.PlainText;

		public IReadOnlyList<DocumentBlock> ReadBlocks(Stream stream)
		{
			ArgumentNullException.ThrowIfNull(stream);

			var bytes = ReadAllBytes(stream);
			var text = Decode(bytes);
			var normalized = TextNormalizer.Normalize(text);

			var lines = normalized
				.Split('\n')
				.Select((lineText, index) => new TextLine(lineText, index + 1));

			return BlockAssembler.Assemble(lines);
		}

		private static byte[] ReadAllBytes(Stream stream)
		{
			// Czytamy cały dokument od początku niezależnie od typu strumienia (parytet ścieżek);
			// jeśli strumień jest seekowalny, ustawiamy pozycję na 0.
			if (stream.CanSeek)
				stream.Position = 0;

			using var buffer = new MemoryStream();
			stream.CopyTo(buffer);
			return buffer.ToArray();
		}

		/// <summary>
		/// Dekoduje bajty na tekst. Kolejność: BOM (UTF-32 przed UTF-16 — prefiksy się pokrywają)
		/// → heurystyka UTF-16 bez BOM → ścisły UTF-8 → CP1250.
		/// </summary>
		private static string Decode(byte[] bytes)
		{
			if (bytes.Length == 0)
				return string.Empty;

			// BOM UTF-32 musi być sprawdzony PRZED UTF-16 LE (FF FE …) — FF FE 00 00 to prefiks UTF-32 LE.
			if (bytes.Length >= 4 && bytes[0] == 0xFF && bytes[1] == 0xFE && bytes[2] == 0x00 && bytes[3] == 0x00)
				return new UTF32Encoding(bigEndian: false, byteOrderMark: true).GetString(bytes, 4, bytes.Length - 4);
			if (bytes.Length >= 4 && bytes[0] == 0x00 && bytes[1] == 0x00 && bytes[2] == 0xFE && bytes[3] == 0xFF)
				return new UTF32Encoding(bigEndian: true, byteOrderMark: true).GetString(bytes, 4, bytes.Length - 4);

			if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
				return new UTF8Encoding(false).GetString(bytes, 3, bytes.Length - 3);
			if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
				return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
			if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
				return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);

			// UTF-16 bez BOM: znaki NUL są wszechobecne w UTF-16 tekstu głównie ASCII/łacińskiego;
			// ścisły UTF-8 by ich NIE odrzucił (0x00 = U+0000 jest poprawne), więc wykrywamy je wcześniej.
			var bomlessUtf16 = TryDecodeBomlessUtf16(bytes);
			if (bomlessUtf16 != null)
				return bomlessUtf16;

			try
			{
				var strictUtf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
				return strictUtf8.GetString(bytes);
			}
			catch (DecoderFallbackException)
			{
				return Encoding.GetEncoding(1250).GetString(bytes);
			}
		}

		/// <summary>
		/// Wykrywa UTF-16 bez BOM po wysokim udziale bajtów NUL (tekst ich nie zawiera).
		/// Zwraca zdekodowany tekst albo null, gdy to nie UTF-16.
		/// </summary>
		private static string? TryDecodeBomlessUtf16(byte[] bytes)
		{
			if (bytes.Length < 4 || bytes.Length % 2 != 0)
				return null;

			int limit = Math.Min(bytes.Length, 4096);
			int nulOdd = 0, nulEven = 0;
			for (int i = 0; i < limit; i++)
			{
				if (bytes[i] != 0x00)
					continue;
				if ((i & 1) == 0) nulEven++;
				else nulOdd++;
			}

			// Mniej niż ~25% bajtów NUL → to nie UTF-16 (UTF-8/CP1250 nie zawierają NUL-i)
			if ((nulOdd + nulEven) * 4 < limit)
				return null;

			// LE: wysoki bajt (indeks nieparzysty) = 0; BE: niski bajt (indeks parzysty) = 0
			return nulOdd >= nulEven
				? Encoding.Unicode.GetString(bytes)
				: Encoding.BigEndianUnicode.GetString(bytes);
		}
	}
}
