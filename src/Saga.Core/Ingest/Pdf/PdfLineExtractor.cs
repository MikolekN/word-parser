#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UglyToad.PdfPig.Content;

namespace Saga.Core.Ingest.Pdf
{
	/// <summary>
	/// Odtwarza wiersze tekstu z liter strony PDF (PdfPig): grupowanie po linii bazowej
	/// (oś Y PdfPig rośnie do góry — sortowanie malejąco), scalanie liter w tekst z wstawianiem
	/// spacji z odstępów oraz detekcja indeksu górnego → notacja [x] (§ 89 ust. 6 ZTP; łapie też
	/// odnośniki § 163). Kryteria superscriptu: rozmiar ≤ 0,75× dominanty wiersza ORAZ linia bazowa
	/// podniesiona o &gt; 0,2× rozmiaru dominanty względem bazy wiersza.
	/// </summary>
	internal static class PdfLineExtractor
	{
		private const double SuperscriptSizeRatio = 0.75;
		private const double SuperscriptRaiseRatio = 0.2;
		private const double LineClusterToleranceRatio = 0.7;

		// 0,2 em (nie 0,25): PDF-y bez glifów spacji (odstępy słów przez przesunięcia TJ) mają odstępy
		// międzywyrazowe 0,20-0,25 em przy wyjustowaniu; kerning międzyliterowy nie zbliża się do 0,2 em.
		private const double SpaceGapRatio = 0.2;

		public static List<PdfTextLine> ExtractLines(Page page)
		{
			var letters = page.Letters;
			if (letters.Count == 0)
				return new List<PdfTextLine>();

			var clusters = ClusterIntoLines(letters);
			var lines = new List<PdfTextLine>(clusters.Count);
			foreach (var cluster in clusters)
			{
				var line = BuildLine(cluster, page);
				if (!string.IsNullOrWhiteSpace(line.Text))
					lines.Add(line);
			}

			// Porządek czytania: od góry strony (Y malejąco).
			return lines.OrderByDescending(l => l.BaselineY).ToList();
		}

		/// <summary>
		/// Grupuje litery w wiersze po Y linii bazowej z tolerancją względną do rozmiaru fontu —
		/// tolerancja musi wchłonąć litery indeksu górnego (podniesiona baza) do wiersza macierzystego.
		/// </summary>
		private static List<List<Letter>> ClusterIntoLines(IReadOnlyList<Letter> letters)
		{
			var sorted = letters.OrderByDescending(l => l.StartBaseLine.Y).ToList();
			var clusters = new List<List<Letter>>();
			List<Letter>? current = null;
			double currentBaseY = 0;

			foreach (var letter in sorted)
			{
				var tolerance = Math.Max(2.0, LineClusterToleranceRatio * Math.Max(letter.PointSize, 1.0));
				if (current == null || currentBaseY - letter.StartBaseLine.Y > tolerance)
				{
					current = new List<Letter>();
					clusters.Add(current);
					currentBaseY = letter.StartBaseLine.Y;
				}
				current.Add(letter);
			}

			return clusters;
		}

		private static PdfTextLine BuildLine(List<Letter> cluster, Page page)
		{
			// Baza wiersza = dominanta Y; remis rozstrzyga NIŻSZE Y (superscript ma bazę podniesioną —
			// wybór wyższej przy remisie zerowałby test podniesienia i gubił kanał [x]).
			var baselineY = Mode(cluster.Select(l => Math.Round(l.StartBaseLine.Y, 1)), preferHigher: false);
			// Rozmiar: remis rozstrzyga WIĘKSZY (tekst podstawowy > superscript).
			var dominantSize = Mode(cluster.Select(l => Math.Round(l.PointSize, 1)), preferHigher: true);

			var ordered = cluster.OrderBy(l => l.StartBaseLine.X).ToList();
			var sb = new StringBuilder();
			bool inSuperscript = false;
			Letter? prev = null;

			foreach (var letter in ordered)
			{
				bool isSuper = dominantSize > 0
					&& letter.PointSize <= SuperscriptSizeRatio * dominantSize
					&& letter.StartBaseLine.Y - baselineY > SuperscriptRaiseRatio * dominantSize;

				if (prev != null)
				{
					var gap = letter.StartBaseLine.X - prev.EndBaseLine.X;
					if (gap > SpaceGapRatio * Math.Max(dominantSize, 1.0))
					{
						if (inSuperscript)
						{
							sb.Append(']');
							inSuperscript = false;
						}
						sb.Append(' ');
					}
				}

				if (isSuper && !inSuperscript)
				{
					sb.Append('[');
					inSuperscript = true;
				}
				else if (!isSuper && inSuperscript)
				{
					sb.Append(']');
					inSuperscript = false;
				}

				sb.Append(letter.Value);
				prev = letter;
			}

			if (inSuperscript)
				sb.Append(']');

			return new PdfTextLine
			{
				Text = sb.ToString(),
				PageNumber = page.Number,
				StartX = ordered[0].StartBaseLine.X,
				BaselineY = baselineY,
				DominantSize = dominantSize,
				PageHeight = page.Height,
			};
		}

		/// <summary>Dominanta (moda) wartości; kierunek rozstrzygania remisów podaje wywołujący.</summary>
		private static double Mode(IEnumerable<double> values, bool preferHigher)
		{
			var groups = values.GroupBy(v => v).OrderByDescending(g => g.Count());
			var ordered = preferHigher
				? groups.ThenByDescending(g => g.Key)
				: groups.ThenBy(g => g.Key);
			return ordered.First().Key;
		}
	}
}
