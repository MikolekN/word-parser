using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;
using Xunit;

namespace WordParserCore.Tests
{
	public class GetFullTextTests
	{
		[Fact]
		public void GetFullText_PlainText_ReturnsText()
		{
			var paragraph = new Paragraph(
				new Run(new Text("Tresc akapitu.")));

			var result = paragraph.GetFullText();

			Assert.Equal("Tresc akapitu.", result);
		}

		[Fact]
		public void GetFullText_SymbolCharF02D_ReturnsEnDash()
		{
			// Symulacja: <w:sym w:font="Symbol" w:char="F02D"/>
			var sym = new SymbolChar
			{
				Font = new StringValue("Symbol"),
				Char = new HexBinaryValue("F02D")
			};
			var paragraph = new Paragraph(
				new Run(sym, new Text(" tresc po polpauzie")));

			var result = paragraph.GetFullText();

			Assert.StartsWith("\u2013", result);
			Assert.Equal("\u2013 tresc po polpauzie", result);
		}

		[Fact]
		public void GetFullText_FootnoteWithSuperscript_WrappedInBrackets()
		{
			// Symulacja: <w:footnoteReference w:id="36"/> + <w:rStyle val="IGindeksgrny"/><w:t>)</w:t>
			// Oba runy sa w indeksie gornym → splaszczone do [36)]
			var fnRef = new FootnoteReference { Id = 36 };
			var paragraph = new Paragraph(
				new Run(new Text("Tekst")),
				new Run(
					new RunProperties { RunStyle = new RunStyle { Val = "Odwoanieprzypisudolnego" } },
					fnRef),
				new Run(
					new RunProperties { RunStyle = new RunStyle { Val = "IGindeksgrny" } },
					new Text(")")));

			var result = paragraph.GetFullText();

			Assert.Equal("Tekst[36)]", result);
		}

		[Fact]
		public void GetFullText_SuperscriptByVerticalAlign_WrappedInBrackets()
		{
			// Run z VerticalTextAlignment=Superscript (bez stylu)
			var paragraph = new Paragraph(
				new Run(new Text("E=mc")),
				new Run(
					new RunProperties
					{
						VerticalTextAlignment = new VerticalTextAlignment
							{ Val = VerticalPositionValues.Superscript }
					},
					new Text("2")));

			var result = paragraph.GetFullText();

			Assert.Equal("E=mc[2]", result);
		}

		[Fact]
		public void GetFullText_SuperscriptFollowedByNormalText_BracketsClose()
		{
			// Indeks gorny w srodku zdania → nawias zamyka sie przed normalnym tekstem
			var paragraph = new Paragraph(
				new Run(new Text("przypis")),
				new Run(
					new RunProperties { RunStyle = new RunStyle { Val = "IGindeksgrny" } },
					new Text("1)")),
				new Run(new Text(" kontynuacja.")));

			var result = paragraph.GetFullText();

			Assert.Equal("przypis[1)] kontynuacja.", result);
		}

		[Fact]
		public void GetFullText_TabChar_EmitsTab()
		{
			var paragraph = new Paragraph(
				new Run(new Text("Art. 5."), new TabChar(), new Text("Tresc artykulu.")));

			var result = paragraph.GetFullText();

			Assert.Equal("Art. 5.\tTresc artykulu.", result);
		}

		[Fact]
		public void GetFullText_WrapUpWithSymbolChar_ReturnsEnDashPrefix()
		{
			// Czesc wspolna zaczyna sie od <w:sym F02D/> zamiast literalnego en-dash
			var sym = new SymbolChar
			{
				Font = new StringValue("Symbol"),
				Char = new HexBinaryValue("F02D")
			};
			var paragraph = new Paragraph(
				new Run(sym),
				new Run(new Text(" zachowuja dopuszczenie.")));

			var result = paragraph.GetFullText();

			Assert.Equal("\u2013 zachowuja dopuszczenie.", result);
		}

		[Fact]
		public void GetFullText_MultipleRuns_ConcatenatesAll()
		{
			var paragraph = new Paragraph(
				new Run(new Text("Czesc ")),
				new Run(new Text("pierwsza i ")),
				new Run(new Text("druga.")));

			var result = paragraph.GetFullText();

			Assert.Equal("Czesc pierwsza i druga.", result);
		}

		[Fact]
		public void GetFullText_SymbolCharOtherFont_Ignored()
		{
			// SymbolChar z innym fontem niz Symbol - nie konwertujemy
			var sym = new SymbolChar
			{
				Font = new StringValue("Wingdings"),
				Char = new HexBinaryValue("F02D")
			};
			var paragraph = new Paragraph(
				new Run(sym, new Text("tekst")));

			var result = paragraph.GetFullText();

			Assert.Equal("tekst", result);
		}

		[Fact]
		public void GetFullText_SymbolCharOtherChar_Ignored()
		{
			// SymbolChar z font=Symbol ale innym kodem niz F02D - nie konwertujemy
			var sym = new SymbolChar
			{
				Font = new StringValue("Symbol"),
				Char = new HexBinaryValue("F0B7")
			};
			var paragraph = new Paragraph(
				new Run(sym, new Text("tekst")));

			var result = paragraph.GetFullText();

			Assert.Equal("tekst", result);
		}

		[Fact]
		public void GetFullText_EmptyParagraph_ReturnsEmpty()
		{
			var paragraph = new Paragraph();

			var result = paragraph.GetFullText();

			Assert.Equal(string.Empty, result);
		}
	}
}
