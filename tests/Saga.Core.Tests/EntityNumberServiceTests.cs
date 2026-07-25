using Saga.Model;
using Saga.Core.Services;
using Saga.Core.Services.Classify;
using Saga.Core.Services.Parsing;
using Xunit;

namespace Saga.Core.Tests
{
	/// <summary>
	/// Testy parsowania i formatowania numerów encji — w szczególności OBU kanałów
	/// indeksu górnego: historycznego "5a^1" oraz "5a[1]" (notacja § 89 ust. 6 ZTP,
	/// emitowana przez ParagraphExtensions.GetFullText).
	/// </summary>
	public class EntityNumberServiceTests
	{
		private readonly EntityNumberService _service = new();

		// ============================================================
		// Parse — oba kanały indeksu górnego
		// ============================================================

		[Theory]
		[InlineData("5a^1")]
		[InlineData("5a[1]")]
		public void Parse_SuperscriptBothChannels_FillsAllComponents(string raw)
		{
			var number = _service.Parse(raw);

			Assert.Equal(5, number.NumericPart);
			Assert.Equal("a", number.LexicalPart);
			Assert.Equal("1", number.Superscript);
			Assert.Equal("5a", number.Value);
			Assert.Equal(raw, number.RawValue);
		}

		[Fact]
		public void Parse_BracketSuperscriptWithTrailingDot_DotIsDropped()
		{
			var number = _service.Parse("5[2].");

			Assert.Equal(5, number.NumericPart);
			Assert.Equal(string.Empty, number.LexicalPart);
			Assert.Equal("2", number.Superscript);
			Assert.Equal("5", number.Value);
		}

		[Fact]
		public void Parse_NoSuperscript_SuperscriptStaysEmpty()
		{
			var number = _service.Parse("12b");

			Assert.Equal(12, number.NumericPart);
			Assert.Equal("b", number.LexicalPart);
			Assert.Equal(string.Empty, number.Superscript);
			Assert.Equal("12b", number.Value);
		}

		// ============================================================
		// FormatToString — emisja w notacji [x] (§ 89 ust. 6 ZTP)
		// ============================================================

		[Theory]
		[InlineData("5a^1", "5a[1]")]
		[InlineData("5a[1]", "5a[1]")]
		[InlineData("12b", "12b")]
		public void FormatToString_EmitsBracketNotation(string raw, string expected)
		{
			var number = _service.Parse(raw);

			Assert.Equal(expected, _service.FormatToString(number));
		}

		// ============================================================
		// Przechwytywanie numerów z tekstu akapitu (ParsingFactories)
		// ============================================================

		[Theory]
		[InlineData("Art. 5[2]. Treść artykułu")]
		[InlineData("„Art. 5[2]. Treść artykułu w nowelizacji")]
		public void ParseArticleNumber_BracketSuperscript_Extracted(string text)
		{
			var number = ParsingFactories.ParseArticleNumber(text);

			Assert.NotNull(number);
			Assert.Equal(5, number!.NumericPart);
			Assert.Equal("2", number.Superscript);
			Assert.Equal("5", number.Value);
		}

		[Fact]
		public void GetArticleTail_BracketSuperscript_TailWithoutBrackets()
		{
			var tail = ParsingFactories.GetArticleTail("Art. 5[2]. Treść artykułu");

			Assert.Equal("Treść artykułu", tail);
		}

		[Fact]
		public void ParseParagraphNumber_BracketSuperscript_Extracted()
		{
			var number = ParsingFactories.ParseParagraphNumber("2a[1]. Treść ustępu");

			Assert.NotNull(number);
			Assert.Equal(2, number!.NumericPart);
			Assert.Equal("a", number.LexicalPart);
			Assert.Equal("1", number.Superscript);
		}

		[Fact]
		public void ParsePointNumber_BracketSuperscript_Extracted()
		{
			var number = ParsingFactories.ParsePointNumber("3[2]) treść punktu;");

			Assert.NotNull(number);
			Assert.Equal(3, number!.NumericPart);
			Assert.Equal("2", number.Superscript);
		}

		[Fact]
		public void ParseLetterNumber_BracketSuperscript_Extracted()
		{
			var number = ParsingFactories.ParseLetterNumber("a[1]) treść litery,");

			Assert.NotNull(number);
			Assert.Equal("a", number!.LexicalPart);
			Assert.Equal("1", number.Superscript);
		}

		// ============================================================
		// Klasyfikacja jednostek z indeksem górnym (gałąź bezstylowa)
		// ============================================================

		[Theory]
		[InlineData("Art. 5[2]. Treść artykułu", ParagraphKind.Article)]
		[InlineData("2[1]. Treść ustępu", ParagraphKind.Paragraph)]
		[InlineData("3[2]) treść punktu;", ParagraphKind.Point)]
		[InlineData("a[1]) treść litery,", ParagraphKind.Letter)]
		public void Classify_SuperscriptNumber_WithoutStyle_ReturnsKind(
			string text, ParagraphKind expected)
		{
			var result = new ParagraphClassifier().Classify(new ClassificationInput(text, null));

			Assert.Equal(expected, result.Kind);
			Assert.Equal(90, result.Confidence); // 100 - StyleAbsentPenalty(10)
		}

		[Fact]
		public void ParsePointNumber_FootnoteReferenceAfterUnit_IsNotSuperscript()
		{
			// Odnośnik przypisu (§ 163 ZTP) ma w kanale GetFullText postać [N)] —
			// nie może być mylony z indeksem górnym numeru jednostki [N]
			var afterUnit = ParsingFactories.ParsePointNumber("3)[1)] treść punktu;");
			var malformed = ParsingFactories.ParsePointNumber("3[1)] treść punktu;");

			Assert.NotNull(afterUnit);
			Assert.Equal(3, afterUnit!.NumericPart);
			Assert.Equal(string.Empty, afterUnit.Superscript);
			Assert.Null(malformed);
		}
	}
}
