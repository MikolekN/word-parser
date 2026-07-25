using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using Word = DocumentFormat.OpenXml.Wordprocessing;
using Saga.Core.Exceptions;
using Saga.Core.Helpers;

#nullable enable

namespace Saga.Core.Ingest
{
	/// <summary>
	/// Adapter DOCX → bloki reprezentacji pośredniej.
	/// Jedyny most OpenXml→IR w potoku; iteracja identyczna z dotychczasową
	/// (Descendants — obejmuje także akapity w tabelach, parytet z golden doc001).
	/// Metadane układu (Layout) czytane z właściwości akapitu i runów; żaden etap
	/// przed Etapem 6-8 ich nie konsumuje, więc pozostają addytywne (snapshot bez zmian).
	/// </summary>
	public sealed class DocxBlockReader : IDocumentBlockReader
	{
		public SourceFormat Format => SourceFormat.Docx;

		public IReadOnlyList<DocumentBlock> ReadBlocks(Stream stream)
		{
			ArgumentNullException.ThrowIfNull(stream);
			if (stream.CanSeek)
				stream.Position = 0;

			GuardDeclaredArchiveSize(stream);

			try
			{
				using var wordDocument = WordprocessingDocument.Open(stream, false);
				return ReadBlocks(wordDocument);
			}
			catch (Exception ex) when (ex is OpenXmlPackageException or FileFormatException
				or InvalidDataException or System.Xml.XmlException)
			{
				// Parytet kontraktu z PdfBlockReader: uszkodzony/nieoczekiwany kontener ma jeden,
				// przewidywalny typ wyjątku — inaczej FileFormatException (FormatException!) ominąłby
				// obsługę błędów CLI/Web i kończył się surowym stack trace / HTTP 500.
				throw new UnsupportedDocumentFormatException(
					"Plik nie jest poprawnym dokumentem Word (DOCX) — kontener jest uszkodzony lub ma nieoczekiwaną zawartość.", ex);
			}
		}

		/// <summary>
		/// Strażnik przed bombami dekompresyjnymi: suma DEKLAROWANYCH rozmiarów wpisów ZIP
		/// (katalog centralny, bez dekompresji) nie może przekroczyć limitu — OpenXml materializuje
		/// części pakietu w pamięci. Deklaracje mogą kłamać, więc strażnik odcina naiwne bomby
		/// i przypadkowe olbrzymy; nie zastępuje limitów pamięci hosta.
		/// </summary>
		private static void GuardDeclaredArchiveSize(Stream stream)
		{
			const long maxDeclaredUncompressedBytes = 512L * 1024 * 1024;

			try
			{
				using var archive = new System.IO.Compression.ZipArchive(
					stream, System.IO.Compression.ZipArchiveMode.Read, leaveOpen: true);

				long totalDeclared = 0;
				foreach (var entry in archive.Entries)
				{
					totalDeclared += entry.Length;
					if (totalDeclared > maxDeclaredUncompressedBytes)
						throw new UnsupportedDocumentFormatException(
							$"Dokument DOCX deklaruje ponad {maxDeclaredUncompressedBytes / (1024 * 1024)} MB danych po dekompresji — odmowa przetworzenia.");
				}
			}
			catch (InvalidDataException ex)
			{
				throw new UnsupportedDocumentFormatException(
					"Plik nie jest poprawnym dokumentem Word (DOCX) — archiwum jest uszkodzone.", ex);
			}
			finally
			{
				if (stream.CanSeek)
					stream.Position = 0;
			}
		}

		public IReadOnlyList<DocumentBlock> ReadBlocks(WordprocessingDocument wordDocument)
		{
			var mainPart = wordDocument.MainDocumentPart ??
				throw new ParsingException("MainDocumentPart dokumentu jest null - plik moze byc uszkodzony lub pusty.");

			var blocks = new List<DocumentBlock>();
			var index = 0;
			foreach (var paragraph in mainPart.Document.Descendants<Word.Paragraph>())
			{
				blocks.Add(ToBlock(paragraph, index++));
			}

			return blocks;
		}

		/// <summary>
		/// Konwersja pojedynczego akapitu OpenXml na blok IR.
		/// Tekst surowy (bez Trim/Sanitize) — normalizację wykonuje orkiestrator.
		/// </summary>
		internal static DocumentBlock ToBlock(Word.Paragraph paragraph, int? blockIndex)
			=> new()
			{
				Text    = paragraph.GetFullText(),
				StyleId = paragraph.StyleId(),
				Layout  = ExtractLayout(paragraph),
				Source  = new BlockSourceLocation { BlockIndex = blockIndex },
			};

		// ============================================================
		// Ekstrakcja metadanych układu
		// ============================================================

		/// <summary>
		/// Wyciąga metadane układu z właściwości akapitu i runów. Zwraca null, gdy
		/// akapit nie niesie żadnego sygnału układu (parytet z PDF/TXT bez layoutu).
		/// </summary>
		private static BlockLayoutInfo? ExtractLayout(Word.Paragraph paragraph)
		{
			var pPr = paragraph.ParagraphProperties;
			var indentation = pPr?.Indentation;

			// Left może być zapisane jako w:left (transitional) albo w:start (ISO strict / LibreOffice).
			int? left      = ParseTwips(indentation?.Left) ?? ParseTwips(indentation?.Start);
			int? firstLine = ParseTwips(indentation?.FirstLine);
			int? hanging   = ParseTwips(indentation?.Hanging);
			var  alignment = MapAlignment(pPr?.Justification?.Val);

			// Formatowanie znakowe — dominanta ważona liczbą widocznych znaków runu.
			// (Kursywa i pogrubienie to markery tekstów jednolitych — § 106a/§ 108a-b ZTP.)
			long boldTrue = 0, boldFalse = 0, italicTrue = 0, italicFalse = 0;
			var  sizeWeights = new Dictionary<double, long>();

			foreach (var run in paragraph.Descendants<Word.Run>())
			{
				int weight = VisibleLength(run);
				if (weight == 0)
					continue;

				var runProperties = run.RunProperties;

				var bold = RunFlag(runProperties?.Bold);
				if (bold == true) boldTrue += weight;
				else if (bold == false) boldFalse += weight;

				var italic = RunFlag(runProperties?.Italic);
				if (italic == true) italicTrue += weight;
				else if (italic == false) italicFalse += weight;

				var size = ParseSize(runProperties?.FontSize?.Val);
				if (size is { } s)
					sizeWeights[s] = sizeWeights.TryGetValue(s, out var w) ? w + weight : weight;
			}

			bool?   isBold   = DominantFlag(boldTrue, boldFalse);
			bool?   isItalic = DominantFlag(italicTrue, italicFalse);
			double? fontSize = DominantSize(sizeWeights);

			if (left is null && firstLine is null && hanging is null && alignment is null
				&& isBold is null && isItalic is null && fontSize is null)
				return null;

			return new BlockLayoutInfo
			{
				LeftIndentTwips      = left,
				FirstLineIndentTwips = firstLine,
				HangingIndentTwips   = hanging,
				Alignment            = alignment,
				IsBold               = isBold,
				IsItalic             = isItalic,
				FontSizeHalfPoints   = fontSize,
			};
		}

		/// <summary>Parsuje miarę twips (Word emituje całkowite twips; miary uniwersalne → null).</summary>
		private static int? ParseTwips(StringValue? value)
			=> value?.Value is { } s
			   && int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var twips)
				? twips
				: null;

		/// <summary>
		/// Parsuje rozmiar czcionki (half-points; ST_HpsMeasure = nieujemna liczba).
		/// Odrzuca wartości niepoprawne (NaN/Infinity/ujemne) — inaczej NaN zatruwałby
		/// słownik dominanty (NaN.Equals(NaN)) i psuł wszystkie przyszłe porównania rozmiaru.
		/// </summary>
		private static double? ParseSize(StringValue? value)
			=> value?.Value is { } s
			   && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var size)
			   && double.IsFinite(size) && size >= 0
				? size
				: null;

		/// <summary>
		/// Odczytuje flagę OnOff (Bold/Italic). Element obecny bez val = true (semantyka OOXML);
		/// element nieobecny = null (dziedziczenie ze stylu — nierozwiązywane na poziomie IR).
		/// Val spoza zbioru {true,false,on,off,0,1} (obce generatory, ręczna edycja) — odczyt
		/// .Value RZUCIŁBY FormatException, więc czytamy dopiero po HasValue; nierozpoznany
		/// val traktujemy jak obecność flagi (true), spójnie z elementem bez val.
		/// </summary>
		private static bool? RunFlag(Word.OnOffType? onOff)
		{
			if (onOff is null)
				return null;

			var val = onOff.Val;
			if (val is null)
				return true;
			return val.HasValue ? val.Value : true;
		}

		/// <summary>Dominanta flagi: zwycięzca wagowy; brak głosów lub remis → null (dwuznaczne).</summary>
		private static bool? DominantFlag(long trueWeight, long falseWeight)
		{
			if (trueWeight == 0 && falseWeight == 0)
				return null;
			if (trueWeight == falseWeight)
				return null;
			return trueWeight > falseWeight;
		}

		/// <summary>Dominanta rozmiaru: największa waga; remis rozstrzyga większy rozmiar (determinizm).</summary>
		private static double? DominantSize(Dictionary<double, long> weights)
		{
			if (weights.Count == 0)
				return null;

			double best = 0;
			long bestWeight = -1;
			foreach (var kv in weights)
			{
				if (kv.Value > bestWeight || (kv.Value == bestWeight && kv.Key > best))
				{
					best = kv.Key;
					bestWeight = kv.Value;
				}
			}
			return best;
		}

		/// <summary>
		/// Liczba widocznych (niebiałych) znaków runu — waga głosu w dominancie formatowania.
		/// Parytet z GetFullText: liczy tekst oraz tiret zapisany symbolem (w:sym F02D → en-dash),
		/// który jest realnym znakiem ciała bloku. Odnośniki przypisów (indeks górny) celowo NIE
		/// głosują — to metadane, nie formatowanie ciała, i zaniżałyby dominantę rozmiaru.
		/// </summary>
		private static int VisibleLength(Word.Run run)
		{
			int count = 0;
			foreach (var child in run.ChildElements)
			{
				switch (child)
				{
					case Word.Text text:
						foreach (var c in text.Text)
							if (!char.IsWhiteSpace(c))
								count++;
						break;

					case Word.SymbolChar sym
						when string.Equals(sym.Font?.Value, "Symbol", StringComparison.OrdinalIgnoreCase)
						  && string.Equals(sym.Char?.Value, "F02D", StringComparison.OrdinalIgnoreCase):
						count++;
						break;
				}
			}
			return count;
		}

		private static BlockAlignment? MapAlignment(EnumValue<Word.JustificationValues>? justification)
		{
			// Val spoza schematu ST_Jc (obce generatory, pusty val) — odczyt .Value RZUCIŁBY
			// FormatException i przerwał odczyt całego dokumentu; HasValue chroni przed tym.
			if (justification is null || !justification.HasValue)
				return null;

			var value = justification.Value;
			if (value == Word.JustificationValues.Left || value == Word.JustificationValues.Start)
				return BlockAlignment.Left;
			if (value == Word.JustificationValues.Center)
				return BlockAlignment.Center;
			if (value == Word.JustificationValues.Right || value == Word.JustificationValues.End)
				return BlockAlignment.Right;
			if (value == Word.JustificationValues.Both || value == Word.JustificationValues.Distribute)
				return BlockAlignment.Justify;

			return null;
		}
	}
}
