using ModelDto;
using WordParserCore.Ingest;
using WordParserCore.Services.Classify;
using WordParserCore.Services.Parsing;
using Xunit;

namespace WordParserCore.Tests
{
	/// <summary>
	/// Testy zaostrzonego wzorca litery (Etap 7c, § 56 ZTP): litera to MAŁA litera alfabetu łacińskiego
	/// bez polskich znaków, a po wyczerpaniu alfabetu dwuznak „za"…„zz". Wersaliki, znaki diakrytyczne
	/// oraz tokeny 3+ znaków NIE są literą redakcyjną (najczęściej wyliczenie załącznika lub proza).
	/// Zaostrzenie dotyczy TYLKO rozpoznania z tekstu; ścieżka stylowa (LIT) pozostaje bez zmian.
	/// </summary>
	public class LetterDesignationTests
	{
		private static ParagraphKind Kind(string text) =>
			new ParagraphClassifier().Classify(new ClassificationInput(text, null)).Kind;

		[Theory]
		[InlineData("a) treść litery,")]
		[InlineData("b) treść litery,")]
		[InlineData("z) treść litery,")]
		[InlineData("za) treść litery,")]
		[InlineData("zz) treść litery,")]
		[InlineData("a[1]) treść litery z odnośnikiem,")]
		public void ValidLowercaseDesignation_IsLetter(string text) =>
			Assert.Equal(ParagraphKind.Letter, Kind(text));

		[Theory]
		[InlineData("A) wyliczenie wersalikiem,")]   // wielka litera
		[InlineData("IV) rzymski token,")]            // wielkie litery
		[InlineData("ą) polski znak diakrytyczny,")]  // spoza [a-z]
		[InlineData("abc) token trzyznakowy,")]       // > 2 znaki
		public void InvalidDesignation_IsNotLetter(string text) =>
			Assert.NotEqual(ParagraphKind.Letter, Kind(text));

		[Fact]
		public void StyleClassifiedLetter_WithNonConformingMarker_StillStripsPrefixAndParsesNumber()
		{
			// Ścieżka stylowa bez zmian: litera ze stylu LIT z markerem spoza § 56 (wersalik „A)") wciąż ma
			// obcięty prefiks i wydobyty numer (parsowanie/obcięcie są szersze niż rozpoznanie z tekstu).
			var document = new LegalDocument { Type = LegalActType.Statute };
			var subchapter = document.RootPart.Books[0].Titles[0].Divisions[0].Chapters[0].Subchapters[0];
			var ctx = new ParsingContext(document, subchapter);
			var orch = new ParserOrchestrator();

			orch.ProcessBlock(new DocumentBlock { Text = "Art. 1. Wprowadzenie." }, ctx);
			orch.ProcessBlock(new DocumentBlock { Text = "1) punkt:" }, ctx);
			orch.ProcessBlock(new DocumentBlock { Text = "A) tresc litery", StyleId = "LIT" }, ctx);

			var letter = ctx.CurrentLetter!;
			Assert.DoesNotContain("A)", letter.ContentText);
			Assert.False(string.IsNullOrEmpty(letter.Number?.Value));
		}
	}
}
