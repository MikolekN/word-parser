#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;
using WordParserCore.Exceptions;

namespace WordParserCore.Ingest.Pdf
{
	/// <summary>
	/// Adapter PDF (warstwa tekstowa) → bloki reprezentacji pośredniej. Potok:
	/// 1. Otwarcie (PdfPig); dokument zaszyfrowany → <see cref="UnsupportedDocumentFormatException"/>.
	/// 2. Litery → wiersze (<see cref="PdfLineExtractor"/>: baseline, spacje z odstępów, superscript → [x]).
	/// 3. Detekcja skanu (średnio &lt; 20 znaków/stronę, &gt; 50% stron pustych lub &gt; 5% U+FFFD)
	///    → <see cref="ScannedPdfException"/> (OCR poza zakresem).
	/// 4. Filtr artefaktów stron (<see cref="PageArtifactFilter"/>): nagłówki/stopki/paginacja.
	/// 5. Strefa przypisów (mniejszy font na dole strony, start „N)") → bloki <see cref="BlockRole.FootnoteText"/>
	///    emitowane NA KOŃCU (parser je pomija; klasyfikator dokumentu czyta z nich sygnały TJ).
	/// 6. Składanie wierszy w bloki-akapity: granica na markerze jednostki (nadzbiór wzorców — tylko
	///    segmentacja), skoku wcięcia (&gt; 0,5 em) lub odstępie pionowym (&gt; 1,9× typowej interlinii);
	///    granica strony NIE jest granicą bloku (jednostka przecięta stroną się skleja). Dehyfenacja
	///    wyłącznie „-" (U+002D) na końcu wiersza + mała litera na początku następnego.
	/// 7. Wcięcia: X pierwszego wiersza bloku względem dominującego lewego marginesu → twips (1 pt = 20 twips).
	/// Celowo NIE nadaje StyleId (zabetonowałoby błędy) — klasyfikacja idzie gałęzią bezstylową.
	/// </summary>
	public sealed class PdfBlockReader : IDocumentBlockReader
	{
		private const int MinAverageLettersPerPage = 20;
		private const double MaxEmptyPageRatio = 0.5;
		private const double MaxReplacementCharRatio = 0.05;
		private const double FootnoteSizeRatio = 0.85;
		private const double ParagraphGapRatio = 1.9;
		private const double PointsToTwips = 20.0;

		/// <summary>
		/// Markery BEZWARUNKOWE początku jednostki/nagłówka — segmentacja bloków (nie klasyfikacja).
		/// Tiret i litera są bezwarunkowe: element listy przed częścią wspólną kanonicznie NIE ma
		/// interpunkcji końcowej (§ 57 ZTP), więc warunek interpunkcyjny gubiłby granice tiretów.
		/// </summary>
		private static readonly Regex UnconditionalMarkerPattern = new(
			@"^(?:Art\.|§\s*\d|USTAWA\b|ROZPORZĄDZENIE\b|OBWIESZCZENIE\b|UCHWAŁA\b|ZARZĄDZENIE\b" +
			@"|CZĘŚĆ\s|KSIĘGA\s|TYTUŁ\s|DZIAŁ\s|ROZDZIAŁ\s|Rozdział\s|ODDZIAŁ\s|Oddział\s" +
			@"|[a-z]{1,2}\)\s|[–\-−]\s)",
			RegexOptions.Compiled);

		/// <summary>
		/// Markery WARUNKOWE — cyfrowe „N)/N." (limit 3 cyfry) i cytat otwierający „. Odpalają tylko,
		/// gdy poprzedni wiersz wygląda na DOMKNIĘTY (kończy się interpunkcją [;:,.] i nie skrótem
		/// „poz./nr/str./art./ust./pkt/lit."): zawinięcie „…(Dz. U. poz. ⏎ 1234) wprowadza…" ani
		/// „…zastępuje się wyrazami ⏎ „centrum…"" nie może rozcinać zdania (fałszywy punkt/cytat).
		/// </summary>
		private static readonly Regex ConditionalMarkerPattern = new(
			@"^(?:\d{1,3}[a-zA-Z]*[\.\)]\s|„)", RegexOptions.Compiled);

		/// <summary>Poprzedni wiersz domknięty: kończy się interpunkcją, ale nie skrótem referencyjnym.</summary>
		private static readonly Regex ClosedLineEndPattern = new(
			@"[;:,.]$", RegexOptions.Compiled);

		private static readonly Regex ReferenceAbbreviationEndPattern = new(
			@"(?:\b(?:poz|[Nn]r|str|art|ust|pkt|lit)\.?|§)\s*$", RegexOptions.Compiled);

		/// <summary>Początek przypisu: „N)" (także w kanale [N)]) lub gwiazdki — nawias WYMAGANY
		/// (bez niego goła liczba „2023 r. …" na początku zawiniętej kontynuacji rozcinałaby przypis).</summary>
		private static readonly Regex FootnoteStartPattern = new(
			@"^(?:\[?\d+\)\]?\s|\*+\s?)", RegexOptions.Compiled);

		public SourceFormat Format => SourceFormat.Pdf;

		public IReadOnlyList<DocumentBlock> ReadBlocks(Stream stream)
		{
			ArgumentNullException.ThrowIfNull(stream);
			if (stream.CanSeek)
				stream.Position = 0;

			using var document = OpenDocument(stream);

			int pageCount = document.NumberOfPages;
			if (pageCount == 0)
				return Array.Empty<DocumentBlock>();

			var allLines = new List<PdfTextLine>();
			var pageLetterCounts = new List<int>(pageCount);
			int totalChars = 0, replacementChars = 0;

			for (int i = 1; i <= pageCount; i++)
			{
				var page = document.GetPage(i);
				pageLetterCounts.Add(page.Letters.Count);
				foreach (var letter in page.Letters)
				{
					totalChars += letter.Value.Length;
					// Zliczanie per ZNAK — Letter.Value bywa wieloznakowe (ligatury/ToUnicode),
					// a znak zastępczy może siedzieć wewnątrz takiej wartości.
					replacementChars += letter.Value.Count(c => c == '�');
				}

				allLines.AddRange(PdfLineExtractor.ExtractLines(page));
			}

			DetectScan(pageLetterCounts, totalChars, replacementChars);

			var lines = PageArtifactFilter.Filter(allLines, pageCount);
			if (lines.Count == 0)
				throw new ScannedPdfException(
					"Warstwa tekstowa PDF zawiera wyłącznie artefakty stron (nagłówki/paginację) — " +
					"treść dokumentu jest prawdopodobnie zeskanowana; OCR jest poza zakresem parsera.");

			var docDominantSize = DominantBodySize(lines);
			var (bodyLines, footnoteLines) = SplitFootnoteZone(lines, docDominantSize);

			var dominantLeft = bodyLines.Count > 0
				? Mode(bodyLines.Select(l => Math.Round(l.StartX)))
				: 0;

			var blocks = AssembleBlocks(bodyLines, docDominantSize, dominantLeft, BlockRole.Body,
				startIndex: 0, forceBoundary: null);
			var footnotes = AssembleBlocks(footnoteLines, docDominantSize, dominantLeft, BlockRole.FootnoteText,
				startIndex: blocks.Count, forceBoundary: l => FootnoteStartPattern.IsMatch(l.Text));

			blocks.AddRange(footnotes);
			return blocks;
		}

		private static PdfDocument OpenDocument(Stream stream)
		{
			try
			{
				return PdfDocument.Open(stream);
			}
			catch (UglyToad.PdfPig.Exceptions.PdfDocumentEncryptedException ex)
			{
				throw new UnsupportedDocumentFormatException(
					"Dokument PDF jest zaszyfrowany hasłem — parsowanie niemożliwe bez odszyfrowania.", ex);
			}
			catch (UglyToad.PdfPig.Core.PdfDocumentFormatException ex)
			{
				throw new UnsupportedDocumentFormatException(
					"Plik PDF jest uszkodzony lub niezgodny ze specyfikacją — nie można odczytać dokumentu.", ex);
			}
		}

		/// <summary>Minimalna średnia liczba liter na NIEPUSTEJ stronie, poniżej której puste strony
		/// wskazują na skan częściowy (a nie np. graficzny załącznik przy pełnotekstowej treści).</summary>
		private const int MinAverageLettersPerNonEmptyPage = 100;

		private static void DetectScan(List<int> pageLetterCounts, int totalChars, int replacementChars)
		{
			int pageCount = pageLetterCounts.Count;
			int totalLetters = pageLetterCounts.Sum();
			int emptyPages = pageLetterCounts.Count(c => c == 0);

			if ((double)totalLetters / pageCount < MinAverageLettersPerPage)
				throw new ScannedPdfException(
					"PDF nie zawiera użytecznej warstwy tekstowej (prawdopodobnie skan) — " +
					"wymagany dokument z tekstem; OCR jest poza zakresem parsera.");

			// Puste strony sygnalizują skan częściowy tylko, gdy strony NIEPUSTE też są ubogie w tekst
			// (nagłówki). Akt z pełnym tekstem + wielostronicowym załącznikiem graficznym przechodzi.
			int nonEmptyPages = pageCount - emptyPages;
			if (nonEmptyPages > 0
				&& (double)emptyPages / pageCount > MaxEmptyPageRatio
				&& (double)totalLetters / nonEmptyPages < MinAverageLettersPerNonEmptyPage)
				throw new ScannedPdfException(
					"Ponad połowa stron PDF nie zawiera tekstu, a pozostałe są ubogie w treść " +
					"(prawdopodobnie skan częściowy) — wymagany dokument z pełną warstwą tekstową.");

			if (totalChars > 0 && (double)replacementChars / totalChars > MaxReplacementCharRatio)
				throw new ScannedPdfException(
					"Warstwa tekstowa PDF zawiera zbyt wiele znaków nieodwzorowanych (uszkodzone kodowanie) — " +
					"tekst nie nadaje się do parsowania.");
		}

		/// <summary>
		/// Rozmiar fontu TREŚCI dokumentu: NAJWIĘKSZY rozmiar o istotnym udziale znaków (≥ 15%).
		/// Moda po WIERSZACH zawodzi, gdy wiersze przypisów przeważają liczebnie (krótka nowela
		/// z długim odnośnikiem) — dominanta spadałaby do rozmiaru przypisów i wyłączała ich strefę.
		/// Próg 15% odsiewa tytuły/nagłówki (mały udział), a przepuszcza treść nawet przy
		/// dokumencie zdominowanym objętościowo przez odnośniki.
		/// </summary>
		private static double DominantBodySize(List<PdfTextLine> lines)
		{
			var weights = lines
				.GroupBy(l => l.DominantSize)
				.Select(g => (Size: g.Key, Weight: g.Sum(l => l.Text.Length)))
				.ToList();
			var totalWeight = weights.Sum(w => w.Weight);

			var substantial = weights.Where(w => w.Weight >= 0.15 * totalWeight).ToList();
			return substantial.Count > 0
				? substantial.Max(w => w.Size)
				: weights.OrderByDescending(w => w.Weight).First().Size;
		}

		/// <summary>
		/// Wydziela strefę przypisów: na każdej stronie spójny blok wierszy NA DOLE strony pisany
		/// mniejszym fontem (≤ 0,85× dominanty dokumentu), którego pierwszy wiersz wygląda jak
		/// początek przypisu („N)") ALBO który kontynuuje przypis z poprzedniej strony (odnośnik
		/// wykazu zmian TJ regularnie zajmuje 2+ stron, a jego kontynuacja zaczyna się środkiem
		/// zdania). Przypisy wracają osobną listą (emisja na końcu dokumentu).
		/// </summary>
		private static (List<PdfTextLine> Body, List<PdfTextLine> Footnotes) SplitFootnoteZone(
			List<PdfTextLine> lines, double docDominantSize)
		{
			var body = new List<PdfTextLine>();
			var footnotes = new List<PdfTextLine>();
			bool previousPageHadFootnotes = false;

			foreach (var pageGroup in lines.GroupBy(l => l.PageNumber).OrderBy(g => g.Key))
			{
				// Wiersze strony w porządku czytania (Y malejąco) — sufiks małego fontu od dołu strony.
				var pageLines = pageGroup.OrderByDescending(l => l.BaselineY).ToList();

				int zoneStart = pageLines.Count;
				while (zoneStart > 0 && pageLines[zoneStart - 1].DominantSize <= FootnoteSizeRatio * docDominantSize)
					zoneStart--;

				bool hasZone = zoneStart < pageLines.Count
					&& (FootnoteStartPattern.IsMatch(pageLines[zoneStart].Text) || previousPageHadFootnotes);

				if (hasZone)
				{
					body.AddRange(pageLines.Take(zoneStart));
					footnotes.AddRange(pageLines.Skip(zoneStart));
				}
				else
				{
					body.AddRange(pageLines);
				}

				previousPageHadFootnotes = hasZone;
			}

			return (body, footnotes);
		}

		/// <summary>
		/// Skleja wiersze w bloki-akapity. Granica bloku: marker bezwarunkowy / marker warunkowy po
		/// wierszu domkniętym interpunkcją / odstęp pionowy &gt; 1,9× typowej interlinii / predykat
		/// wymuszony (przypisy). Granica STRONY nie jest granicą bloku (jednostka przecięta stroną
		/// się skleja). CELOWO bez granicy na skoku wcięcia: skład Dziennika Ustaw używa wcięcia
		/// WISZĄCEGO (marker przy marginesie, kontynuacje głębiej) — granica wcięciowa cięłaby każdy
		/// zawinięty punkt/literę/tiret i gubiła tekst. Dywiz na końcu wiersza: sklejenie bezpośrednie
		/// z ZACHOWANIEM dywizu (decyzja użytkownika — patrz komentarz przy sklejaniu).
		/// </summary>
		private static List<DocumentBlock> AssembleBlocks(
			List<PdfTextLine> lines, double emSize, double dominantLeft, BlockRole role,
			int startIndex, Func<PdfTextLine, bool>? forceBoundary)
		{
			var blocks = new List<DocumentBlock>();
			if (lines.Count == 0)
				return blocks;

			var typicalGap = TypicalLineGap(lines, emSize);

			var buffer = new StringBuilder();
			PdfTextLine? blockFirst = null;
			PdfTextLine? prev = null;

			void FlushBlock()
			{
				if (blockFirst == null || buffer.Length == 0)
					return;
				blocks.Add(new DocumentBlock
				{
					Text = buffer.ToString(),
					Role = role,
					Layout = new BlockLayoutInfo
					{
						LeftIndentTwips = (int)Math.Round(Math.Max(0, blockFirst.StartX - dominantLeft) * PointsToTwips),
						FontSizeHalfPoints = blockFirst.DominantSize * 2,
					},
					Source = new BlockSourceLocation
					{
						BlockIndex = startIndex + blocks.Count,
						PageNumber = blockFirst.PageNumber,
					},
				});
				buffer.Clear();
				blockFirst = null;
			}

			foreach (var line in lines)
			{
				bool boundary = prev == null
					|| (forceBoundary?.Invoke(line) ?? IsMarkerBoundary(line, prev))
					|| (line.PageNumber == prev.PageNumber
						&& prev.BaselineY - line.BaselineY > ParagraphGapRatio * typicalGap);

				if (boundary)
				{
					FlushBlock();
					blockFirst = line;
					buffer.Append(line.Text);
				}
				else
				{
					// Kontynuacja akapitu: wiersz zakończony dywizem skleja się BEZPOŚREDNIO (dywiz zostaje).
					// DECYZJA UŻYTKOWNIKA (2026-07-19, zamiast dehyfenacji z planu): bez słownika nie da się
					// odróżnić dywizu złożenia („społeczno-gospodarczy") od dzielenia wyrazów składem
					// („posta-nowienia"); usuwanie scala złożenia NIEODWRACALNIE, zachowanie zostawia
					// widoczny i odwracalny artefakt — mniejsza szkoda dla wierności tekstu prawnego.
					if (buffer.Length > 0 && buffer[^1] == '-')
					{
						buffer.Append(line.Text);
					}
					else
					{
						buffer.Append(' ');
						buffer.Append(line.Text);
					}
				}

				prev = line;
			}

			FlushBlock();
			return blocks;
		}

		/// <summary>
		/// Granica na markerze jednostki: bezwarunkowa (Art./§/tiret/litera/nagłówki) albo warunkowa
		/// (cyfrowa „N)/N.", cytat „) — ta druga tylko po wierszu domkniętym interpunkcją i niekończącym
		/// się skrótem referencyjnym („poz.", „nr", „art."…), by zawinięcie zdania nie tworzyło
		/// fałszywego punktu/cytatu.
		/// </summary>
		private static bool IsMarkerBoundary(PdfTextLine line, PdfTextLine prev)
		{
			if (UnconditionalMarkerPattern.IsMatch(line.Text))
				return true;

			if (!ConditionalMarkerPattern.IsMatch(line.Text))
				return false;

			var prevText = prev.Text.TrimEnd();
			return ClosedLineEndPattern.IsMatch(prevText)
				&& !ReferenceAbbreviationEndPattern.IsMatch(prevText);
		}

		/// <summary>Typowa interlinia: mediana dodatnich odstępów Y kolejnych wierszy tej samej strony.</summary>
		private static double TypicalLineGap(List<PdfTextLine> lines, double emSize)
		{
			var gaps = new List<double>();
			for (int i = 1; i < lines.Count; i++)
			{
				if (lines[i].PageNumber != lines[i - 1].PageNumber)
					continue;
				var gap = lines[i - 1].BaselineY - lines[i].BaselineY;
				if (gap > 0)
					gaps.Add(gap);
			}

			if (gaps.Count == 0)
				return emSize * 1.2;

			gaps.Sort();
			return gaps[gaps.Count / 2];
		}

		private static double Mode(IEnumerable<double> values) =>
			values.GroupBy(v => v)
				.OrderByDescending(g => g.Count())
				.ThenByDescending(g => g.Key)
				.First().Key;
	}
}
