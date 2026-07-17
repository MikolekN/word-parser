using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;
using WordParserCore.Ingest;
using Xunit;

namespace WordParserCore.Tests
{
	/// <summary>
	/// Testy adaptera DOCX → IR (DocxBlockReader.ToBlock), w szczególności ekstrakcji
	/// metadanych układu (Etap 3 planu). Layout jest addytywny — żaden etap go jeszcze
	/// nie konsumuje, więc snapshot doc001 pozostaje bez zmian; tu weryfikujemy sam odczyt.
	/// </summary>
	public class DocxBlockReaderTests
	{
		private static DocumentBlock ToBlock(Paragraph paragraph)
			=> DocxBlockReader.ToBlock(paragraph, blockIndex: 7);

		/// <summary>Akapit z surowego XML — do wartości spoza schematu (obce generatory, ręczna edycja).</summary>
		private static Paragraph FromXml(string bodyXml)
			=> new Paragraph(
				$@"<w:p xmlns:w=""http://schemas.openxmlformats.org/wordprocessingml/2006/main"">{bodyXml}</w:p>");

		/// <summary>Run z tekstem i opcjonalnym formatowaniem znakowym.</summary>
		private static Run Formatted(string text, bool? bold = null, bool? italic = null, string? fontSize = null)
		{
			var runProperties = new RunProperties();
			if (bold == true) runProperties.Bold = new Bold();
			else if (bold == false) runProperties.Bold = new Bold { Val = false };
			if (italic == true) runProperties.Italic = new Italic();
			else if (italic == false) runProperties.Italic = new Italic { Val = false };
			if (fontSize != null) runProperties.FontSize = new FontSize { Val = fontSize };
			return new Run(runProperties, new Text(text));
		}

		// ============================================================
		// Parytet pól bloku (Text / StyleId / Source)
		// ============================================================

		[Fact]
		public void ToBlock_PreservesTextStyleAndIndex()
		{
			var paragraph = new Paragraph(
				new ParagraphProperties(new ParagraphStyleId { Val = "ART" }),
				new Run(new Text("Art. 5. Treść.")));

			var block = ToBlock(paragraph);

			Assert.Equal("Art. 5. Treść.", block.Text);
			Assert.Equal("ART", block.StyleId);
			Assert.Equal(7, block.Source.BlockIndex);
		}

		[Fact]
		public void ToBlock_BareParagraph_HasNullLayout()
		{
			var block = ToBlock(new Paragraph(new Run(new Text("Zwykły tekst."))));

			Assert.Null(block.Layout);
		}

		// ============================================================
		// Wcięcia
		// ============================================================

		[Fact]
		public void ToBlock_Indentation_ExtractedAsTwips()
		{
			var paragraph = new Paragraph(
				new ParagraphProperties(
					new Indentation { Left = "708", FirstLine = "360", Hanging = "284" }),
				new Run(new Text("Treść.")));

			var layout = ToBlock(paragraph).Layout;

			Assert.NotNull(layout);
			Assert.Equal(708, layout!.LeftIndentTwips);
			Assert.Equal(360, layout.FirstLineIndentTwips);
			Assert.Equal(284, layout.HangingIndentTwips);
		}

		[Fact]
		public void ToBlock_NegativeLeftIndent_Parsed()
		{
			var paragraph = new Paragraph(
				new ParagraphProperties(new Indentation { Left = "-142" }),
				new Run(new Text("Treść.")));

			Assert.Equal(-142, ToBlock(paragraph).Layout!.LeftIndentTwips);
		}

		[Fact]
		public void ToBlock_NonIntegerTwips_Ignored()
		{
			// Miara uniwersalna (np. "0.5in") nie jest całkowitym twips — pomijamy bez błędu.
			var paragraph = new Paragraph(
				new ParagraphProperties(new Indentation { Left = "0.5in" }),
				new Run(new Text("Treść.")));

			var block = ToBlock(paragraph);

			Assert.Null(block.Layout); // brak innych sygnałów → cały Layout null
		}

		// ============================================================
		// Wyrównanie
		// ============================================================

		private static BlockAlignment? AlignmentOf(JustificationValues value)
		{
			var paragraph = new Paragraph(
				new ParagraphProperties(new Justification { Val = value }),
				new Run(new Text("Treść.")));
			return ToBlock(paragraph).Layout?.Alignment;
		}

		[Fact]
		public void ToBlock_Alignment_MappedForAllValues()
		{
			Assert.Equal(BlockAlignment.Left, AlignmentOf(JustificationValues.Left));
			Assert.Equal(BlockAlignment.Left, AlignmentOf(JustificationValues.Start));
			Assert.Equal(BlockAlignment.Center, AlignmentOf(JustificationValues.Center));
			Assert.Equal(BlockAlignment.Right, AlignmentOf(JustificationValues.Right));
			Assert.Equal(BlockAlignment.Right, AlignmentOf(JustificationValues.End));
			Assert.Equal(BlockAlignment.Justify, AlignmentOf(JustificationValues.Both));
			Assert.Equal(BlockAlignment.Justify, AlignmentOf(JustificationValues.Distribute));
		}

		// ============================================================
		// Pogrubienie / kursywa — dominanta ważona liczbą znaków
		// ============================================================

		[Fact]
		public void ToBlock_BoldRun_PresentWithoutVal_IsTrue()
		{
			var paragraph = new Paragraph(Formatted("Nowe brzmienie", bold: true));

			Assert.True(ToBlock(paragraph).Layout!.IsBold);
		}

		[Fact]
		public void ToBlock_BoldValFalse_IsFalse()
		{
			var paragraph = new Paragraph(Formatted("Zwykły", bold: false));

			Assert.False(ToBlock(paragraph).Layout!.IsBold);
		}

		[Fact]
		public void ToBlock_MixedBold_DominantByCharacterWeight()
		{
			// 14 znaków pogrubionych vs 6 niepogrubionych → dominanta = true
			var paragraph = new Paragraph(
				Formatted("Nowe brzmienie", bold: true),
				Formatted("zwykłe", bold: false));

			Assert.True(ToBlock(paragraph).Layout!.IsBold);
		}

		[Fact]
		public void ToBlock_ItalicRun_IsTrue()
		{
			var paragraph = new Paragraph(Formatted("utracił moc", italic: true));

			Assert.True(ToBlock(paragraph).Layout!.IsItalic);
		}

		[Fact]
		public void ToBlock_NoBoldSpecified_IsNull()
		{
			var paragraph = new Paragraph(new Run(new Text("Treść bez formatowania runu.")));

			Assert.Null(ToBlock(paragraph).Layout);
		}

		[Fact]
		public void ToBlock_WhitespaceOnlyBoldRun_DoesNotVote()
		{
			// Run pogrubiony zawierający tylko spacje nie głosuje (waga 0);
			// pozostały run nie deklaruje pogrubienia → dominanta null (nie true).
			var paragraph = new Paragraph(
				Formatted("   ", bold: true),
				new Run(new Text("Treść.")));

			Assert.Null(ToBlock(paragraph).Layout?.IsBold);
		}

		// ============================================================
		// Rozmiar czcionki — dominanta ważona liczbą znaków
		// ============================================================

		[Fact]
		public void ToBlock_SingleFontSize_Extracted()
		{
			var paragraph = new Paragraph(Formatted("Treść.", fontSize: "24"));

			Assert.Equal(24d, ToBlock(paragraph).Layout!.FontSizeHalfPoints);
		}

		[Fact]
		public void ToBlock_MixedFontSize_DominantByCharacterWeight()
		{
			// "Treść." (5 znaków bez kropki? liczymy niebiałe: 6) rozmiar 20;
			// "xx" rozmiar 28 → dominanta 20.
			var paragraph = new Paragraph(
				Formatted("Treść.", fontSize: "20"),
				Formatted("xx", fontSize: "28"));

			Assert.Equal(20d, ToBlock(paragraph).Layout!.FontSizeHalfPoints);
		}

		// ============================================================
		// Kombinacja sygnałów
		// ============================================================

		[Fact]
		public void ToBlock_CombinedLayout_AllFieldsPopulated()
		{
			var paragraph = new Paragraph(
				new ParagraphProperties(
					new Justification { Val = JustificationValues.Center },
					new Indentation { Left = "1416" }),
				Formatted("USTAWA", bold: true, fontSize: "28"));

			var layout = ToBlock(paragraph).Layout;

			Assert.NotNull(layout);
			Assert.Equal(1416, layout!.LeftIndentTwips);
			Assert.Equal(BlockAlignment.Center, layout.Alignment);
			Assert.True(layout.IsBold);
			Assert.Equal(28d, layout.FontSizeHalfPoints);
		}

		// ============================================================
		// Regresje z przeglądu adwersaryjnego Etapu 3
		// ============================================================

		[Fact]
		public void ToBlock_StartIndent_MappedToLeft()
		{
			// ISO strict / LibreOffice zapisują lewe wcięcie jako w:start zamiast w:left.
			var paragraph = new Paragraph(
				new ParagraphProperties(new Indentation { Start = "708" }),
				new Run(new Text("Treść.")));

			Assert.Equal(708, ToBlock(paragraph).Layout!.LeftIndentTwips);
		}

		[Fact]
		public void ToBlock_BoldSymbolTiretRun_Votes()
		{
			// Tiret zapisany symbolem (w:sym F02D) w pogrubionym runie musi głosować za bold —
			// wcześniej dostawał wagę 0 i nie liczył się do dominanty.
			var symbol = new SymbolChar { Font = new StringValue("Symbol"), Char = new HexBinaryValue("F02D") };
			var paragraph = new Paragraph(
				new Run(new RunProperties { Bold = new Bold() }, symbol),
				new Run(new Text(" przepis stosuje się.")));

			Assert.True(ToBlock(paragraph).Layout!.IsBold);
		}

		[Fact]
		public void ToBlock_JustificationWithoutVal_AlignmentNull()
		{
			var paragraph = new Paragraph(
				new ParagraphProperties(new Justification()),
				new Run(new Text("Treść.")));

			Assert.Null(ToBlock(paragraph).Layout?.Alignment);
		}

		[Fact]
		public void ToBlock_UnmappedButValidJustification_AlignmentNull()
		{
			// Wartość poprawna schematowo, lecz bez odpowiednika w BlockAlignment → null (bez wyjątku).
			Assert.Null(AlignmentOf(JustificationValues.ThaiDistribute));
		}

		[Fact]
		public void ToBlock_OutOfSchemaJustification_DoesNotThrow()
		{
			// w:jc z wartością spoza ST_Jc: odczyt .Value rzuciłby FormatException i przerwał
			// odczyt całego dokumentu. HasValue chroni — Layout bez wyrównania, brak wyjątku.
			var paragraph = FromXml(
				@"<w:pPr><w:jc w:val=""garbageValue""/></w:pPr><w:r><w:t>Treść.</w:t></w:r>");

			var block = ToBlock(paragraph);

			Assert.Null(block.Layout?.Alignment);
		}

		[Fact]
		public void ToBlock_OutOfSchemaBoldToken_DoesNotThrow_TreatedAsBold()
		{
			// w:b z tokenem spoza {true,false,on,off,0,1}: .Value rzuciłby FormatException.
			// Element obecny z nierozpoznanym val traktujemy jak obecność flagi (true).
			var paragraph = FromXml(
				@"<w:r><w:rPr><w:b w:val=""maybe""/></w:rPr><w:t>Treść.</w:t></w:r>");

			Assert.True(ToBlock(paragraph).Layout!.IsBold);
		}

		[Fact]
		public void ToBlock_NonFiniteFontSize_Ignored()
		{
			// w:sz val="NaN": double.TryParse przyjąłby NaN i zatruł słownik dominanty.
			// Guard IsFinite odrzuca → rozmiar pominięty (brak innego sygnału → Layout null).
			var paragraph = FromXml(
				@"<w:r><w:rPr><w:sz w:val=""NaN""/></w:rPr><w:t>Treść.</w:t></w:r>");

			Assert.Null(ToBlock(paragraph).Layout?.FontSizeHalfPoints);
		}

		[Fact]
		public void ToBlock_NegativeFontSize_Ignored()
		{
			var paragraph = FromXml(
				@"<w:r><w:rPr><w:sz w:val=""-24""/></w:rPr><w:t>Treść.</w:t></w:r>");

			Assert.Null(ToBlock(paragraph).Layout?.FontSizeHalfPoints);
		}
	}
}
