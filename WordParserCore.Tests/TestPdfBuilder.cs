using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace WordParserCore.Tests
{
	/// <summary>
	/// Minimalny generator PDF do fikstur testowych adaptera PDF (Etap 9) — bez zależności od plików
	/// fontów. Polskie znaki i cudzysłowy typograficzne realizuje przez Helveticę (Standard 14)
	/// z kodowaniem /Differences (nazwy glifów Adobe: aogonek, sacute, quotedblbase…), które
	/// ekstrakcja PdfPig mapuje z powrotem na Unicode. Pełna kontrola pozycji (x, y w punktach,
	/// oś Y rośnie DO GÓRY) i rozmiaru fontu — konieczna dla testów superscriptu/wcięć/artefaktów.
	/// </summary>
	internal sealed class TestPdfBuilder
	{
		internal sealed record TextRun(string Text, double X, double Y, double Size);

		private readonly List<List<TextRun>> _pages = new();
		private readonly Dictionary<char, (byte Code, string GlyphName)> _mapped = new();
		private byte _nextCode = 0x80;

		public double PageWidth { get; init; } = 595;   // A4 w punktach
		public double PageHeight { get; init; } = 842;

		private static readonly Dictionary<char, string> GlyphNames = new()
		{
			['ą'] = "aogonek", ['ć'] = "cacute", ['ę'] = "eogonek", ['ł'] = "lslash", ['ń'] = "nacute",
			['ó'] = "oacute", ['ś'] = "sacute", ['ź'] = "zacute", ['ż'] = "zdotaccent",
			['Ą'] = "Aogonek", ['Ć'] = "Cacute", ['Ę'] = "Eogonek", ['Ł'] = "Lslash", ['Ń'] = "Nacute",
			['Ó'] = "Oacute", ['Ś'] = "Sacute", ['Ź'] = "Zacute", ['Ż'] = "Zdotaccent",
			['„'] = "quotedblbase", ['”'] = "quotedblright", ['“'] = "quotedblleft",
			['–'] = "endash", ['—'] = "emdash", ['§'] = "section",
		};

		public TestPdfBuilder AddPage()
		{
			_pages.Add(new List<TextRun>());
			return this;
		}

		/// <summary>Dodaje tekst na OSTATNIEJ stronie. Współrzędne w punktach; Y rośnie do góry.</summary>
		public TestPdfBuilder AddText(string text, double x, double y, double size = 11)
		{
			if (_pages.Count == 0)
				AddPage();
			_pages[^1].Add(new TextRun(text, x, y, size));
			return this;
		}

		public byte[] Build()
		{
			// Najpierw przejdź wszystkie teksty, by zebrać mapowanie znaków spoza ASCII.
			foreach (var page in _pages)
				foreach (var run in page)
					foreach (var c in run.Text)
						MapChar(c);

			var objects = new List<string>();

			// 1: katalog, 2: drzewo stron, 3: font, 4: encoding; strony i strumienie dalej.
			int pageObjStart = 5;
			var pageRefs = new List<string>();
			for (int i = 0; i < _pages.Count; i++)
				pageRefs.Add($"{pageObjStart + i * 2} 0 R");

			objects.Add("<< /Type /Catalog /Pages 2 0 R >>");
			objects.Add($"<< /Type /Pages /Kids [{string.Join(" ", pageRefs)}] /Count {_pages.Count} >>");
			objects.Add("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding 4 0 R >>");
			objects.Add(BuildEncodingObject());

			for (int i = 0; i < _pages.Count; i++)
			{
				int contentObj = pageObjStart + i * 2 + 1;
				objects.Add($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {Pt(PageWidth)} {Pt(PageHeight)}] " +
					$"/Resources << /Font << /F1 3 0 R >> >> /Contents {contentObj} 0 R >>");

				var content = BuildContentStream(_pages[i]);
				objects.Add($"<< /Length {content.Length} >>\nstream\n{content}\nendstream");
			}

			return AssembleFile(objects);
		}

		private void MapChar(char c)
		{
			if (c < 0x80 || _mapped.ContainsKey(c))
				return;
			if (!GlyphNames.TryGetValue(c, out var glyph))
				throw new InvalidOperationException($"TestPdfBuilder: brak mapowania glifu dla znaku '{c}' (U+{(int)c:X4}).");
			if (_nextCode == 0)
				throw new InvalidOperationException("TestPdfBuilder: wyczerpano kody 0x80-0xFF.");
			_mapped[c] = (_nextCode, glyph);
			_nextCode++;
		}

		private string BuildEncodingObject()
		{
			var sb = new StringBuilder("<< /Type /Encoding /BaseEncoding /WinAnsiEncoding /Differences [");
			foreach (var (_, (code, glyph)) in _mapped)
				sb.Append(CultureInfo.InvariantCulture, $" {code} /{glyph}");
			sb.Append(" ] >>");
			return sb.ToString();
		}

		private string BuildContentStream(List<TextRun> runs)
		{
			var sb = new StringBuilder();
			foreach (var run in runs)
			{
				sb.Append(CultureInfo.InvariantCulture,
					$"BT /F1 {Pt(run.Size)} Tf 1 0 0 1 {Pt(run.X)} {Pt(run.Y)} Tm (");
				foreach (var c in run.Text)
				{
					if (c == '(' || c == ')' || c == '\\')
						sb.Append('\\').Append(c);
					else if (c < 0x80)
						sb.Append(c);
					else
						sb.Append('\\').Append(Convert.ToString(_mapped[c].Code, 8).PadLeft(3, '0'));
				}
				sb.Append(") Tj ET\n");
			}
			return sb.ToString().TrimEnd('\n');
		}

		private static string Pt(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);

		private static byte[] AssembleFile(List<string> objects)
		{
			var sb = new StringBuilder();
			sb.Append("%PDF-1.4\n");

			var offsets = new List<int>();
			for (int i = 0; i < objects.Count; i++)
			{
				offsets.Add(Encoding.Latin1.GetByteCount(sb.ToString()));
				sb.Append(CultureInfo.InvariantCulture, $"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
			}

			int xrefOffset = Encoding.Latin1.GetByteCount(sb.ToString());
			sb.Append(CultureInfo.InvariantCulture, $"xref\n0 {objects.Count + 1}\n");
			sb.Append("0000000000 65535 f \n");
			foreach (var off in offsets)
				sb.Append(CultureInfo.InvariantCulture, $"{off:0000000000} 00000 n \n");
			sb.Append(CultureInfo.InvariantCulture,
				$"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xrefOffset}\n%%EOF");

			return Encoding.Latin1.GetBytes(sb.ToString());
		}
	}
}
