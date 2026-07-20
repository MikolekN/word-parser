#nullable enable
using System;
using System.IO;

namespace WordParserCore.Ingest
{
	/// <summary>
	/// Rozpoznaje format źródłowy dokumentu po zawartości (sniffing sygnatur), nie po rozszerzeniu.
	/// Kolejność: sygnatura ZIP/OOXML na offsecie 0 → nagłówek „%PDF-" w pierwszych 1024 bajtach
	/// (spec PDF dopuszcza śmieci przed nagłówkiem) → heurystyka tekstu (BOM, UTF-16 bez BOM,
	/// brak bajtów NUL). Rozszerzenie pliku rozstrzyga WYŁĄCZNIE przypadki niekonkluzywne
	/// (pusty plik, binaria bez sygnatury). Po detekcji strumień wraca na pozycję 0.
	/// </summary>
	public static class SourceFormatDetector
	{
		// Nagłówek PDF musi się zaczynać w pierwszych 1024 bajtach pliku (ISO 32000, implementacje Adobe).
		private const int SniffLength = 1024;

		private static readonly byte[] PdfSignature = { 0x25, 0x50, 0x44, 0x46, 0x2D }; // %PDF-

		public static SourceFormat Detect(Stream stream, string? fileNameHint = null)
		{
			ArgumentNullException.ThrowIfNull(stream);
			if (!stream.CanSeek)
				throw new ArgumentException(
					"Strumień musi być seekowalny — detekcja czyta nagłówek i cofa pozycję na początek.",
					nameof(stream));

			stream.Position = 0;
			var buffer = new byte[SniffLength];
			int read = 0;
			int chunk;
			while (read < buffer.Length && (chunk = stream.Read(buffer, read, buffer.Length - read)) > 0)
				read += chunk;
			stream.Position = 0;

			if (read == 0)
				return FromExtension(fileNameHint);

			// Kontener ZIP (OOXML) — walidację, czy to naprawdę dokument Word, wykonuje reader.
			if (read >= 4 && buffer[0] == 0x50 && buffer[1] == 0x4B && buffer[2] == 0x03 && buffer[3] == 0x04)
				return SourceFormat.Docx;

			// Jawny BOM tekstowy wygrywa z „%PDF-" w treści: plik tekstowy może CYTOWAĆ sygnaturę
			// (notatka o formatach), a BOM jest deklaracją silniejszą niż podciąg w oknie 1024 B.
			if (HasTextBom(buffer, read))
				return SourceFormat.PlainText;

			if (ContainsPdfHeader(buffer, read))
				return SourceFormat.Pdf;

			if (LooksLikeText(buffer, read))
				return SourceFormat.PlainText;

			return FromExtension(fileNameHint);
		}

		private static bool ContainsPdfHeader(byte[] buffer, int length)
		{
			int limit = length - PdfSignature.Length;
			for (int i = 0; i <= limit; i++)
			{
				bool match = true;
				for (int j = 0; j < PdfSignature.Length; j++)
				{
					if (buffer[i + j] != PdfSignature[j])
					{
						match = false;
						break;
					}
				}
				if (match)
					return true;
			}
			return false;
		}

		/// <summary>BOM tekstowy: UTF-32 przed UTF-16 (prefiksy się pokrywają) — kolejność jak w readerze.</summary>
		private static bool HasTextBom(byte[] buffer, int length)
		{
			if (length >= 4 && buffer[0] == 0xFF && buffer[1] == 0xFE && buffer[2] == 0x00 && buffer[3] == 0x00)
				return true; // UTF-32 LE
			if (length >= 4 && buffer[0] == 0x00 && buffer[1] == 0x00 && buffer[2] == 0xFE && buffer[3] == 0xFF)
				return true; // UTF-32 BE
			if (length >= 3 && buffer[0] == 0xEF && buffer[1] == 0xBB && buffer[2] == 0xBF)
				return true; // UTF-8
			if (length >= 2 && ((buffer[0] == 0xFF && buffer[1] == 0xFE) || (buffer[0] == 0xFE && buffer[1] == 0xFF)))
				return true; // UTF-16 LE/BE

			return false;
		}

		/// <summary>
		/// Heurystyka tekstu BEZ BOM, zgrana z możliwościami <see cref="PlainTextBlockReader"/>:
		/// UTF-16 bez BOM (dużo NUL-i skupionych po jednej parzystości) albo brak NUL-i
		/// (ścisłe UTF-8 lub fallback CP1250 dekodują wszystko bez NUL).
		/// </summary>
		private static bool LooksLikeText(byte[] buffer, int length)
		{
			int nulOdd = 0, nulEven = 0;
			for (int i = 0; i < length; i++)
			{
				if (buffer[i] != 0x00)
					continue;
				if ((i & 1) == 0) nulEven++;
				else nulOdd++;
			}

			int nulTotal = nulOdd + nulEven;
			if (nulTotal == 0)
				return true; // tekst jednobajtowy (UTF-8/CP1250)

			// UTF-16 bez BOM: NUL-e stanowią ≥25% próbki i siedzą niemal wyłącznie po jednej
			// parzystości (wysokie bajty tekstu łacińskiego). Binaria mają NUL-e po obu stronach.
			if (nulTotal * 4 >= length)
			{
				int major = Math.Max(nulOdd, nulEven);
				if (major * 10 >= nulTotal * 9)
					return true;
			}

			return false;
		}

		private static SourceFormat FromExtension(string? fileNameHint)
		{
			var extension = string.IsNullOrWhiteSpace(fileNameHint)
				? null
				: Path.GetExtension(fileNameHint);

			return extension?.ToLowerInvariant() switch
			{
				".txt" or ".text" => SourceFormat.PlainText,
				".pdf" => SourceFormat.Pdf,
				".docx" => SourceFormat.Docx,
				_ => SourceFormat.Unknown,
			};
		}
	}
}
