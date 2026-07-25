using System.IO;
using System.Linq;
using System.Text;
using Saga.Core.Ingest;
using Xunit;

namespace Saga.Core.Tests
{
	/// <summary>
	/// Testy adaptera TXT → IR (Etap 5): detekcja kodowania, normalizacja, segmentacja bloków.
	/// </summary>
	public class PlainTextBlockReaderTests
	{
		private static System.Collections.Generic.IReadOnlyList<DocumentBlock> Read(byte[] bytes)
		{
			using var ms = new MemoryStream(bytes);
			return new PlainTextBlockReader().ReadBlocks(ms);
		}

		private static System.Collections.Generic.IReadOnlyList<DocumentBlock> Read(string text, Encoding encoding)
			=> Read(encoding.GetBytes(text));

		/// <summary>Bajty z preambułą (BOM) — GetBytes samo BOM-u nie dołącza, robi to dopiero GetPreamble.</summary>
		private static System.Collections.Generic.IReadOnlyList<DocumentBlock> ReadWithBom(string text, Encoding encoding)
			=> Read(encoding.GetPreamble().Concat(encoding.GetBytes(text)).ToArray());

		// ============================================================
		// Kodowanie
		// ============================================================

		[Fact]
		public void Read_Utf8NoBom_PreservesPolishDiacritics()
		{
			var blocks = Read("Art. 1. Ochrona zdrowia i bezpieczeństwo.\nArt. 2. Wejście w życie.",
				new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

			Assert.Equal(2, blocks.Count);
			Assert.Equal("Art. 1. Ochrona zdrowia i bezpieczeństwo.", blocks[0].Text);
			Assert.Equal("Art. 2. Wejście w życie.", blocks[1].Text);
		}

		[Fact]
		public void Read_Utf8WithBom_StripsBomAndDecodes()
		{
			var blocks = ReadWithBom("Art. 1. Zażółć gęślą jaźń.\nArt. 2. Druga treść.",
				new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

			Assert.Equal(2, blocks.Count);
			Assert.Equal("Art. 1. Zażółć gęślą jaźń.", blocks[0].Text);
			Assert.False(blocks[0].Text.Contains('\uFEFF'), "Znak BOM nie powinien pozostać w tekście.");
		}

		[Fact]
		public void Read_Cp1250_FallbackDecodesPolishDiacritics()
		{
			// Rejestracja dostawcy stron kodowych następuje w statycznym konstruktorze readera.
			var reader = new PlainTextBlockReader();
			var cp1250 = Encoding.GetEncoding(1250);
			var bytes = cp1250.GetBytes("Art. 1. Zażółć gęślą jaźń.\nArt. 2. Koniec.");

			using var ms = new MemoryStream(bytes);
			var blocks = reader.ReadBlocks(ms);

			Assert.Equal(2, blocks.Count);
			Assert.Equal("Art. 1. Zażółć gęślą jaźń.", blocks[0].Text);
		}

		// ============================================================
		// Normalizacja
		// ============================================================

		[Fact]
		public void Read_CrlfLineEndings_AreNormalized()
		{
			var blocks = Read("Art. 1. Pierwszy.\r\nArt. 2. Drugi.", new UTF8Encoding(false));

			Assert.Equal(2, blocks.Count);
			Assert.Equal("Art. 2. Drugi.", blocks[1].Text);
		}

		[Fact]
		public void Read_SoftHyphen_IsRemoved()
		{
			// Miękki dywiz U+00AD w środku wyrazu — DOCX go nie emituje, TXT też nie powinien.
			var blocks = Read("Art. 1. wy\u00ADraz z miękkim dywizem.", new UTF8Encoding(false));

			Assert.Single(blocks);
			Assert.Equal("Art. 1. wyraz z miękkim dywizem.", blocks[0].Text);
		}

		// ============================================================
		// Segmentacja bloków
		// ============================================================

		[Fact]
		public void Read_EachNonBlankLine_IsSeparateBlock()
		{
			// Model TXT: jeden wiersz = jeden blok (nowa linia = granica akapitu).
			var blocks = Read("Art. 1. Pierwszy przepis.\nDrugi wiersz treści.", new UTF8Encoding(false));

			Assert.Equal(2, blocks.Count);
			Assert.Equal("Art. 1. Pierwszy przepis.", blocks[0].Text);
			Assert.Equal("Drugi wiersz treści.", blocks[1].Text);
		}

		[Fact]
		public void Read_TitleZoneLines_AreSeparateBlocks()
		{
			// Strefa tytułowa (nagłówek/data/przedmiot) NIE może być sklejona w jeden blok —
			// klasyfikator wymaga osobnych bloków „USTAWA", „z dnia…", „o …".
			var blocks = Read("USTAWA\nz dnia 5 marca 2024 r.\no ochronie danych osobowych", new UTF8Encoding(false));

			Assert.Equal(3, blocks.Count);
			Assert.Equal("USTAWA", blocks[0].Text);
			Assert.Equal("z dnia 5 marca 2024 r.", blocks[1].Text);
			Assert.Equal("o ochronie danych osobowych", blocks[2].Text);
		}

		[Fact]
		public void Read_BlankLine_SeparatesBlocks()
		{
			var blocks = Read("Pierwszy akapit narracyjny.\n\nDrugi akapit narracyjny.", new UTF8Encoding(false));

			Assert.Equal(2, blocks.Count);
			Assert.Equal("Pierwszy akapit narracyjny.", blocks[0].Text);
			Assert.Equal("Drugi akapit narracyjny.", blocks[1].Text);
		}

		[Fact]
		public void Read_UnitMarker_StartsNewBlockWithoutBlankLine()
		{
			var blocks = Read("Art. 1. Pierwszy.\nArt. 2. Drugi.\n§ 3. Trzeci.", new UTF8Encoding(false));

			Assert.Equal(3, blocks.Count);
			Assert.Equal("§ 3. Trzeci.", blocks[2].Text);
		}

		[Fact]
		public void Read_TiretMarkers_EachStartsBlock()
		{
			var blocks = Read("- pierwszy tiret,\n- drugi tiret,\n- trzeci tiret.", new UTF8Encoding(false));

			Assert.Equal(3, blocks.Count);
		}

		[Fact]
		public void Read_PointsAndLetters_EachStartsBlock()
		{
			var blocks = Read("1) punkt pierwszy;\n2) punkt drugi:\na) litera a,\nb) litera b.", new UTF8Encoding(false));

			Assert.Equal(4, blocks.Count);
		}

		[Fact]
		public void Read_BlockSource_HasSequentialIndexAndLineNumber()
		{
			var blocks = Read("Art. 1. Pierwszy.\n\nArt. 2. Drugi.", new UTF8Encoding(false));

			Assert.Equal(0, blocks[0].Source.BlockIndex);
			Assert.Equal(1, blocks[1].Source.BlockIndex);
			Assert.Equal(1, blocks[0].Source.LineNumber);
			Assert.Equal(3, blocks[1].Source.LineNumber);
		}

		[Fact]
		public void Read_EmptyStream_ReturnsNoBlocks()
		{
			var blocks = Read(System.Array.Empty<byte>());

			Assert.Empty(blocks);
		}

		[Fact]
		public void Read_WhitespaceOnly_ReturnsNoBlocks()
		{
			var blocks = Read("   \n\t\n  ", new UTF8Encoding(false));

			Assert.Empty(blocks);
		}

		[Fact]
		public void Read_PlainTextBlocks_HaveNoStyleId()
		{
			var blocks = Read("Art. 1. Treść.", new UTF8Encoding(false));

			Assert.All(blocks, b => Assert.Null(b.StyleId));
		}

		// ============================================================
		// Regresje z przeglądu adwersaryjnego Etapu 5
		// ============================================================

		[Fact]
		public void Read_Utf16LeWithBom_Decodes()
		{
			var blocks = ReadWithBom("Art. 1. Zażółć gęślą jaźń.\nArt. 2. Koniec.", Encoding.Unicode);

			Assert.Equal(2, blocks.Count);
			Assert.Equal("Art. 1. Zażółć gęślą jaźń.", blocks[0].Text);
		}

		[Fact]
		public void Read_Utf16BeWithBom_Decodes()
		{
			var blocks = ReadWithBom("Art. 1. Zażółć gęślą jaźń.\nArt. 2. Koniec.", Encoding.BigEndianUnicode);

			Assert.Equal(2, blocks.Count);
			Assert.Equal("Art. 1. Zażółć gęślą jaźń.", blocks[0].Text);
		}

		[Fact]
		public void Read_Utf16LeWithoutBom_IsDetectedAndDecoded()
		{
			// UTF-16 LE bez BOM: ścisły UTF-8 nie odrzuca bajtów NUL — potrzebna heurystyka NUL.
			var leNoBom = new UnicodeEncoding(bigEndian: false, byteOrderMark: false);
			var blocks = Read("Art. 1. Zażółć gęślą jaźń.\nArt. 2. Koniec.", leNoBom);

			Assert.Equal(2, blocks.Count);
			Assert.Equal("Art. 1. Zażółć gęślą jaźń.", blocks[0].Text);
			Assert.DoesNotContain('\u0000', blocks[0].Text);
		}

		[Fact]
		public void Read_Utf16BeWithoutBom_IsDetectedAndDecoded()
		{
			var beNoBom = new UnicodeEncoding(bigEndian: true, byteOrderMark: false);
			var blocks = Read("Art. 1. Zażółć gęślą jaźń.\nArt. 2. Koniec.", beNoBom);

			Assert.Equal(2, blocks.Count);
			Assert.Equal("Art. 1. Zażółć gęślą jaźń.", blocks[0].Text);
		}

		[Fact]
		public void Read_Utf32LeWithBom_IsNotMisdetectedAsUtf16()
		{
			// BOM UTF-32 LE (FF FE 00 00) zaczyna się jak BOM UTF-16 LE (FF FE) — musi być rozpoznany wcześniej.
			var blocks = ReadWithBom("Art. 1. Zażółć gęślą jaźń.\nArt. 2. Koniec.",
				new UTF32Encoding(bigEndian: false, byteOrderMark: true));

			Assert.Equal(2, blocks.Count);
			Assert.Equal("Art. 1. Zażółć gęślą jaźń.", blocks[0].Text);
		}

		[Fact]
		public void Read_ZeroWidthCharacters_AreStripped()
		{
			// U+200B / U+FEFF w środku / U+2060 — nie są białymi znakami, Sanitize ich nie zdejmuje,
			// a psują dopasowanie markera jednostki. TextNormalizer musi je usunąć.
			var blocks = Read("\uFEFFArt.\u200B 1.\u2060 Treść z niewidocznymi znakami.", new UTF8Encoding(false));

			Assert.Single(blocks);
			Assert.Equal("Art. 1. Treść z niewidocznymi znakami.", blocks[0].Text);
		}

		[Fact]
		public void ReadBlocks_NullStream_ThrowsArgumentNullException()
		{
			Assert.Throws<System.ArgumentNullException>(() => new PlainTextBlockReader().ReadBlocks(null!));
		}

		[Fact]
		public void ReadBlocks_StreamAtNonZeroPosition_ReadsFromStart()
		{
			var bytes = new UTF8Encoding(false).GetBytes("Art. 1. Pełna treść od początku.");
			using var ms = new MemoryStream(bytes) { Position = 5 };

			var blocks = new PlainTextBlockReader().ReadBlocks(ms);

			Assert.Single(blocks);
			Assert.Equal("Art. 1. Pełna treść od początku.", blocks[0].Text);
		}
	}
}
