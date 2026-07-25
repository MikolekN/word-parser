using System;
using System.IO;
using System.Linq;
using System.Text;
using Saga.Core.Ingest;
using Xunit;

namespace Saga.Core.Tests
{
	/// <summary>
	/// Testy detekcji formatu źródłowego (Etap 10): sygnatury bajtowe (ZIP/OOXML, %PDF- w pierwszych
	/// 1024 bajtach), heurystyka tekstu zgrana z możliwościami PlainTextBlockReader (BOM-y, UTF-16
	/// bez BOM, brak NUL-i) oraz rozszerzenie pliku wyłącznie jako rozstrzygnięcie remisów.
	/// </summary>
	public class SourceFormatDetectorTests
	{
		static SourceFormatDetectorTests()
		{
			// CP1250 dla fikstur legacy — rejestracja jest idempotentna.
			Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
		}

		private static SourceFormat Detect(byte[] bytes, string? fileNameHint = null)
		{
			using var stream = new MemoryStream(bytes);
			return SourceFormatDetector.Detect(stream, fileNameHint);
		}

		// ============================================================
		// PDF
		// ============================================================

		[Fact]
		public void PdfHeaderAtStart_IsPdf()
		{
			var bytes = Encoding.ASCII.GetBytes("%PDF-1.7\n1 0 obj\n<< /Type /Catalog >>\nendobj\n");
			Assert.Equal(SourceFormat.Pdf, Detect(bytes));
		}

		[Fact]
		public void PdfHeaderAfterJunk_WithinFirstKilobyte_IsPdf()
		{
			// Spec PDF dopuszcza śmieci przed nagłówkiem, o ile „%PDF-" zaczyna się w pierwszych 1024 bajtach.
			var junk = Enumerable.Repeat((byte)0x01, 512).ToArray();
			var bytes = junk.Concat(Encoding.ASCII.GetBytes("%PDF-1.4\n")).ToArray();
			Assert.Equal(SourceFormat.Pdf, Detect(bytes));
		}

		[Fact]
		public void PdfHeaderBeyondFirstKilobyte_IsNotPdf()
		{
			// NUL-e po obu parzystościach — treść nie przechodzi też heurystyki tekstu.
			var junk = new byte[1500];
			for (int i = 0; i < junk.Length; i++)
				junk[i] = (i % 3 == 0) ? (byte)0x00 : (byte)0xA7;
			var bytes = junk.Concat(Encoding.ASCII.GetBytes("%PDF-1.4\n")).ToArray();
			Assert.Equal(SourceFormat.Unknown, Detect(bytes));
		}

		[Fact]
		public void RealPdfFixture_IsPdf()
		{
			var pdf = new TestPdfBuilder().AddPage();
			pdf.AddText("Art. 1. Tekst.", 57, 700);
			Assert.Equal(SourceFormat.Pdf, Detect(pdf.Build()));
		}

		[Fact]
		public void TextWithBom_QuotingPdfSignature_IsPlainText()
		{
			// Jawny BOM tekstowy bije podciąg „%PDF-" w treści (np. notatka o formatach plików).
			var payload = new UTF8Encoding(false).GetBytes("Sygnatura formatu PDF to %PDF- na początku pliku.");
			var bytes = new byte[] { 0xEF, 0xBB, 0xBF }.Concat(payload).ToArray();
			Assert.Equal(SourceFormat.PlainText, Detect(bytes));
		}

		// ============================================================
		// DOCX (kontener ZIP)
		// ============================================================

		[Fact]
		public void ZipSignature_IsDocx()
		{
			var bytes = new byte[] { 0x50, 0x4B, 0x03, 0x04, 0x14, 0x00, 0x06, 0x00 };
			Assert.Equal(SourceFormat.Docx, Detect(bytes));
		}

		// ============================================================
		// Tekst
		// ============================================================

		[Fact]
		public void Utf8WithoutBom_IsPlainText()
		{
			var bytes = new UTF8Encoding(false).GetBytes("Art. 1. Ustawa reguluje sprawę żółwi.");
			Assert.Equal(SourceFormat.PlainText, Detect(bytes));
		}

		[Fact]
		public void Utf8WithBom_IsPlainText()
		{
			var payload = new UTF8Encoding(false).GetBytes("Art. 1. Tekst.");
			var bytes = new byte[] { 0xEF, 0xBB, 0xBF }.Concat(payload).ToArray();
			Assert.Equal(SourceFormat.PlainText, Detect(bytes));
		}

		[Fact]
		public void Utf16LeWithBom_IsPlainText()
		{
			var bytes = Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes("Art. 1. Tekst.")).ToArray();
			Assert.Equal(SourceFormat.PlainText, Detect(bytes));
		}

		[Fact]
		public void BomlessUtf16_PolishText_IsPlainText()
		{
			// Wysokie bajty tekstu łacińskiego = NUL-e skupione po jednej parzystości.
			var bytes = Encoding.Unicode.GetBytes("Art. 1. Ustawa wchodzi w życie po upływie 14 dni od dnia ogłoszenia.");
			Assert.Equal(SourceFormat.PlainText, Detect(bytes));
		}

		[Fact]
		public void Cp1250Text_NoNulBytes_IsPlainText()
		{
			var bytes = Encoding.GetEncoding(1250).GetBytes("Załącznik: paragraf § 5, litera ą, ć, ę, ł, ń, ś, ź, ż.");
			Assert.Equal(SourceFormat.PlainText, Detect(bytes));
		}

		// ============================================================
		// Przypadki niekonkluzywne — rozszerzenie rozstrzyga remis
		// ============================================================

		[Fact]
		public void BinaryWithNuls_NoSignatureNoHint_IsUnknown()
		{
			// Struktura à la nagłówek PNG: pojedyncze NUL-e po obu parzystościach (<25% próbki).
			var bytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52 };
			Assert.Equal(SourceFormat.Unknown, Detect(bytes));
		}

		[Fact]
		public void BinaryWithNuls_PdfExtensionHint_ResolvesToPdf()
		{
			var bytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52 };
			Assert.Equal(SourceFormat.Pdf, Detect(bytes, "skan.pdf"));
		}

		[Fact]
		public void EmptyStream_NoHint_IsUnknown()
		{
			Assert.Equal(SourceFormat.Unknown, Detect(Array.Empty<byte>()));
		}

		[Fact]
		public void EmptyStream_TxtHint_IsPlainText()
		{
			Assert.Equal(SourceFormat.PlainText, Detect(Array.Empty<byte>(), "pusty.txt"));
		}

		[Fact]
		public void TextContent_TxtHintIrrelevant_SignatureWins()
		{
			// Rozszerzenie NIE nadpisuje jednoznacznej sygnatury — .txt na kontenerze ZIP to nadal DOCX.
			var bytes = new byte[] { 0x50, 0x4B, 0x03, 0x04, 0x14, 0x00 };
			Assert.Equal(SourceFormat.Docx, Detect(bytes, "dokument.txt"));
		}

		// ============================================================
		// Kontrakt strumienia
		// ============================================================

		[Fact]
		public void Detect_RestoresPositionToZero_AndSeeksItself()
		{
			var bytes = new UTF8Encoding(false).GetBytes("Art. 1. Tekst.");
			using var stream = new MemoryStream(bytes);
			stream.Position = 5; // detekcja ma czytać od początku niezależnie od pozycji wejściowej

			var format = SourceFormatDetector.Detect(stream);

			Assert.Equal(SourceFormat.PlainText, format);
			Assert.Equal(0, stream.Position);
		}

		[Fact]
		public void NonSeekableStream_Throws()
		{
			using var stream = new NonSeekableReadStream(new byte[] { 0x41 });
			Assert.Throws<ArgumentException>(() => SourceFormatDetector.Detect(stream));
		}
	}

	/// <summary>Strumień tylko-do-odczytu bez seekowania — do testów kontraktów strumieniowych.</summary>
	internal sealed class NonSeekableReadStream : Stream
	{
		private readonly MemoryStream _inner;

		public NonSeekableReadStream(byte[] bytes) => _inner = new MemoryStream(bytes);

		public override bool CanRead => true;
		public override bool CanSeek => false;
		public override bool CanWrite => false;
		public override long Length => throw new NotSupportedException();
		public override long Position
		{
			get => throw new NotSupportedException();
			set => throw new NotSupportedException();
		}

		public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
		public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
		public override void SetLength(long value) => throw new NotSupportedException();
		public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
		public override void Flush() { }

		protected override void Dispose(bool disposing)
		{
			if (disposing)
				_inner.Dispose();
			base.Dispose(disposing);
		}
	}
}
