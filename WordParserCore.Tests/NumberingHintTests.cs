using ModelDto;
using WordParserCore.Services.Classify;
using Xunit;

namespace WordParserCore.Tests
{
	/// <summary>
	/// Testy jednostkowe dla <see cref="NumberingHint.IsContinuous"/> i
	/// <see cref="NumberingHint.GetNextLetterValue"/>.
	/// Pokrywają scenariusze ciągłości numeracji wcześniej testowane przez
	/// usunięty NumberingContinuityValidator.
	/// </summary>
	public class NumberingHintTests
	{
		#region Numeracja numeryczna (artykuły, ustępy, punkty)

		[Fact]
		public void IsContinuous_Sequential_ReturnsTrue()
		{
			// 1 → 2 → 3
			Assert.True(Hint(1).IsContinuous(Numeric(2)));
			Assert.True(Hint(2).IsContinuous(Numeric(3)));
		}

		[Fact]
		public void IsContinuous_SameNumericPartWithSuffix_ReturnsTrue()
		{
			// Art. 2 → Art. 2a → Art. 3 (ten sam NumericPart = 2)
			Assert.True(Hint(2).IsContinuous(Numeric(2, "a")));
			Assert.True(Hint(2, "a").IsContinuous(Numeric(3)));
		}

		[Fact]
		public void IsContinuous_MultipleSuffixes_ReturnsTrue()
		{
			// Art. 5 → Art. 5a → Art. 5b → Art. 6
			Assert.True(Hint(5).IsContinuous(Numeric(5, "a")));
			Assert.True(Hint(5, "a").IsContinuous(Numeric(5, "b")));
			Assert.True(Hint(5, "b").IsContinuous(Numeric(6)));
		}

		[Fact]
		public void IsContinuous_Gap_ReturnsFalse()
		{
			// 1 → 5 (luka)
			Assert.False(Hint(1).IsContinuous(Numeric(5)));
		}

		[Fact]
		public void IsContinuous_GapWithSuffix_ReturnsFalse()
		{
			// Art. 1 → Art. 5a (luka mimo sufiksu)
			Assert.False(Hint(1).IsContinuous(Numeric(5, "a")));
		}

		[Fact]
		public void IsContinuous_NoPreviousNumber_AlwaysTrue()
		{
			// Pierwsza encja w sekwencji: brak poprzedniego numeru
			var hint = new NumberingHint { ExpectedKind = ParagraphKind.Article, ExpectedNumber = null };
			Assert.True(hint.IsContinuous(Numeric(1)));
			Assert.True(hint.IsContinuous(Numeric(5)));
		}

		#endregion

		#region Ciągłość liter

		[Fact]
		public void IsContinuous_LettersSequential_ReturnsTrue()
		{
			// a → b → c
			Assert.True(LetterHint("a").IsContinuous(Letter("b")));
			Assert.True(LetterHint("b").IsContinuous(Letter("c")));
		}

		[Fact]
		public void IsContinuous_LetterGap_ReturnsFalse()
		{
			// a → d (luka)
			Assert.False(LetterHint("a").IsContinuous(Letter("d")));
		}

		[Fact]
		public void IsContinuous_SameLexicalWithSuperscript_ReturnsTrue()
		{
			// lit. a → lit. a^1 (ten sam LexicalPart)
			Assert.True(LetterHint("a").IsContinuous(Letter("a", "1")));
		}

		#endregion

		#region GetNextLetterValue

		[Theory]
		[InlineData("a", "b")]
		[InlineData("b", "c")]
		[InlineData("z", "aa")]
		[InlineData("aa", "ab")]
		[InlineData("az", "ba")]
		public void GetNextLetterValue_ReturnsExpected(string current, string expected)
		{
			Assert.Equal(expected, NumberingHint.GetNextLetterValue(current));
		}

		[Fact]
		public void GetNextLetterValue_NullOrEmpty_ReturnsNull()
		{
			Assert.Null(NumberingHint.GetNextLetterValue(null));
			Assert.Null(NumberingHint.GetNextLetterValue(""));
		}

		#endregion

		#region Helpers

		private static EntityNumber Numeric(int numericPart, string lexicalPart = "")
			=> new EntityNumber { Value = numericPart + lexicalPart, NumericPart = numericPart, LexicalPart = lexicalPart };

		private static EntityNumber Letter(string lexicalPart, string superscript = "")
			=> new EntityNumber { Value = lexicalPart, NumericPart = 0, LexicalPart = lexicalPart, Superscript = superscript };

		private static NumberingHint Hint(int numericPart, string lexicalPart = "")
			=> new NumberingHint { ExpectedKind = ParagraphKind.Article, ExpectedNumber = Numeric(numericPart, lexicalPart) };

		private static NumberingHint LetterHint(string lexicalPart)
			=> new NumberingHint { ExpectedKind = ParagraphKind.Letter, ExpectedNumber = Letter(lexicalPart) };

		#endregion
	}
}
