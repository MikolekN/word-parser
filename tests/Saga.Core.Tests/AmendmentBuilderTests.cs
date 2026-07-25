using System.Collections.Generic;
using ModelDto;
using ModelDto.EditorialUnits;
using WordParserCore.Helpers;
using WordParserCore.Services.Parsing;
using WordParserCore.Services.Parsing.Builders;
using Xunit;

namespace WordParserCore.Tests
{
	public class AmendmentBuilderTests
	{
		private readonly AmendmentBuilder _builder = new();

		// ============================================================
		// Puste wejście
		// ============================================================

		[Fact]
		public void Build_EmptyParagraphs_ReturnsEmptyContent()
		{
			var input = new AmendmentBuildInput(
				new List<CollectedAmendmentParagraph>(),
				null,
				AmendmentOperationType.Modification);

			var content = _builder.Build(input);

			Assert.Equal(AmendmentObjectType.None, content.ObjectType);
			Assert.Empty(content.Articles);
			Assert.Empty(content.Paragraphs);
			Assert.Empty(content.Points);
			Assert.Empty(content.Letters);
			Assert.Empty(content.Tirets);
			Assert.Null(content.PlainText);
		}

		// ============================================================
		// Artykuł — pojedynczy
		// ============================================================

		[Fact]
		public void Build_SingleArticle_CreatesArticleWithImplicitParagraph()
		{
			var paragraphs = new List<CollectedAmendmentParagraph>
			{
				CreateParagraph("Art. 5a. Treść nowego artykułu.",
					"ZARTzmartartykuempunktem", AmendmentTargetKind.Article)
			};

			var input = new AmendmentBuildInput(paragraphs, null, AmendmentOperationType.Insertion);
			var content = _builder.Build(input);

			Assert.Equal(AmendmentObjectType.Article, content.ObjectType);
			Assert.Single(content.Articles);
			Assert.Equal("5a", content.Articles[0].Number?.Value);
			Assert.Single(content.Articles[0].Paragraphs);
			Assert.True(content.Articles[0].Paragraphs[0].IsImplicit);
			Assert.Equal("Treść nowego artykułu.", content.Articles[0].Paragraphs[0].ContentText);
		}

		// ============================================================
		// Artykuł + ustępy
		// ============================================================

		[Fact]
		public void Build_ArticleWithParagraphs_CreatesHierarchy()
		{
			var paragraphs = new List<CollectedAmendmentParagraph>
			{
				CreateParagraph("Art. 10. 1. Treść pierwszego ustępu.",
					"ZARTzmartartykuempunktem", AmendmentTargetKind.Article),
				CreateParagraph("2. Treść drugiego ustępu.",
					"ZUSTzmustartykuempunktem", AmendmentTargetKind.Paragraph)
			};

			var input = new AmendmentBuildInput(paragraphs, null, AmendmentOperationType.Modification);
			var content = _builder.Build(input);

			Assert.Single(content.Articles);
			var article = content.Articles[0];
			Assert.Equal("10", article.Number?.Value);
			Assert.Equal(2, article.Paragraphs.Count);
			Assert.Equal("1", article.Paragraphs[0].Number?.Value);
			Assert.False(article.Paragraphs[0].IsImplicit);
			Assert.Equal("2", article.Paragraphs[1].Number?.Value);
		}

		[Fact]
		public void Build_ArticleWithParagraphAndPoints_FullNesting()
		{
			var paragraphs = new List<CollectedAmendmentParagraph>
			{
				CreateParagraph("Art. 7. 1. Wprowadzenie do wyliczenia:",
					"ZARTzmartartykuempunktem", AmendmentTargetKind.Article),
				CreateParagraph("1) treść punktu pierwszego;",
					"ZPKTzmpktartykuempunktem", AmendmentTargetKind.Point),
				CreateParagraph("2) treść punktu drugiego.",
					"ZPKTzmpktartykuempunktem", AmendmentTargetKind.Point)
			};

			var input = new AmendmentBuildInput(paragraphs, null, AmendmentOperationType.Modification);
			var content = _builder.Build(input);

			Assert.Single(content.Articles);
			var paragraph = content.Articles[0].Paragraphs[0];
			Assert.Equal("1", paragraph.Number?.Value);
			Assert.Equal(2, paragraph.Points.Count);
			Assert.Equal("1", paragraph.Points[0].Number?.Value);
			Assert.Equal("2", paragraph.Points[1].Number?.Value);
		}

		// ============================================================
		// Ustępy (standalone — bez artykułu)
		// ============================================================

		[Fact]
		public void Build_StandaloneParagraphs_GoesToContentParagraphs()
		{
			var paragraphs = new List<CollectedAmendmentParagraph>
			{
				CreateParagraph("2. Treść zmienianego ustępu.",
					"ZUSTzmustartykuempunktem", AmendmentTargetKind.Paragraph)
			};

			var input = new AmendmentBuildInput(paragraphs, null, AmendmentOperationType.Modification);
			var content = _builder.Build(input);

			Assert.Equal(AmendmentObjectType.Paragraph, content.ObjectType);
			Assert.Empty(content.Articles);
			Assert.Single(content.Paragraphs);
			Assert.Equal("2", content.Paragraphs[0].Number?.Value);
			Assert.Equal("Treść zmienianego ustępu.", content.Paragraphs[0].ContentText);
		}

		[Fact]
		public void Build_MultipleParagraphs_AllCollected()
		{
			var paragraphs = new List<CollectedAmendmentParagraph>
			{
				CreateParagraph("1. Ustęp pierwszy.", "ZUSTzmustartykuempunktem", AmendmentTargetKind.Paragraph),
				CreateParagraph("2. Ustęp drugi.", "ZUSTzmustartykuempunktem", AmendmentTargetKind.Paragraph),
				CreateParagraph("3. Ustęp trzeci.", "ZUSTzmustartykuempunktem", AmendmentTargetKind.Paragraph)
			};

			var input = new AmendmentBuildInput(paragraphs, null, AmendmentOperationType.Modification);
			var content = _builder.Build(input);

			Assert.Equal(3, content.Paragraphs.Count);
			Assert.Equal("1", content.Paragraphs[0].Number?.Value);
			Assert.Equal("3", content.Paragraphs[2].Number?.Value);
		}

		// ============================================================
		// Punkty (standalone)
		// ============================================================

		[Fact]
		public void Build_StandalonePoints_GoesToContentPoints()
		{
			var paragraphs = new List<CollectedAmendmentParagraph>
			{
				CreateParagraph("1) treść punktu pierwszego;",
					"ZPKTzmpktartykuempunktem", AmendmentTargetKind.Point),
				CreateParagraph("2) treść punktu drugiego;",
					"ZPKTzmpktartykuempunktem", AmendmentTargetKind.Point)
			};

			var input = new AmendmentBuildInput(paragraphs, null, AmendmentOperationType.Modification);
			var content = _builder.Build(input);

			Assert.Equal(AmendmentObjectType.Point, content.ObjectType);
			Assert.Equal(2, content.Points.Count);
			Assert.Equal("1", content.Points[0].Number?.Value);
			Assert.Equal("2", content.Points[1].Number?.Value);
		}

		// ============================================================
		// Litery (standalone)
		// ============================================================

		[Fact]
		public void Build_StandaloneLetters_GoesToContentLetters()
		{
			var paragraphs = new List<CollectedAmendmentParagraph>
			{
				CreateParagraph("a) treść litery a;",
					"ZLITzmlitartykuempunktem", AmendmentTargetKind.Letter),
				CreateParagraph("b) treść litery b;",
					"ZLITzmlitartykuempunktem", AmendmentTargetKind.Letter)
			};

			var input = new AmendmentBuildInput(paragraphs, null, AmendmentOperationType.Modification);
			var content = _builder.Build(input);

			Assert.Equal(AmendmentObjectType.Letter, content.ObjectType);
			Assert.Equal(2, content.Letters.Count);
			Assert.Equal("a", content.Letters[0].Number?.Value);
			Assert.Equal("b", content.Letters[1].Number?.Value);
		}

		// ============================================================
		// Tirety (standalone)
		// ============================================================

		[Fact]
		public void Build_StandaloneTirets_GoesToContentTirets()
		{
			var paragraphs = new List<CollectedAmendmentParagraph>
			{
				CreateParagraph("\u2013 treść tiretu pierwszego;",
					"ZTIRzmtirartykuempunktem", AmendmentTargetKind.Tiret),
				CreateParagraph("\u2013 treść tiretu drugiego;",
					"ZTIRzmtirartykuempunktem", AmendmentTargetKind.Tiret)
			};

			var input = new AmendmentBuildInput(paragraphs, null, AmendmentOperationType.Modification);
			var content = _builder.Build(input);

			Assert.Equal(AmendmentObjectType.Tiret, content.ObjectType);
			Assert.Equal(2, content.Tirets.Count);
			Assert.Equal("1", content.Tirets[0].Number?.Value);
			Assert.Equal("2", content.Tirets[1].Number?.Value);
		}

		// ============================================================
		// Pełna hierarchia: ART → UST → PKT → LIT → TIR
		// ============================================================

		[Fact]
		public void Build_FullHierarchy_ArticleParagraphPointLetterTiret()
		{
			var paragraphs = new List<CollectedAmendmentParagraph>
			{
				CreateParagraph("Art. 7. 1. Wprowadzenie do wyliczenia:",
					"ZARTzmartartykuempunktem", AmendmentTargetKind.Article),
				CreateParagraph("1) treść punktu z literami:",
					"ZPKTzmpktartykuempunktem", AmendmentTargetKind.Point),
				CreateParagraph("a) treść litery a z tiretami:",
					"ZLITzmlitartykuempunktem", AmendmentTargetKind.Letter),
				CreateParagraph("\u2013 treść tiretu;",
					"ZTIRzmtirartykuempunktem", AmendmentTargetKind.Tiret),
				CreateParagraph("2. Drugi ustęp.",
					"ZUSTzmustartykuempunktem", AmendmentTargetKind.Paragraph)
			};

			var input = new AmendmentBuildInput(paragraphs, null, AmendmentOperationType.Modification);
			var content = _builder.Build(input);

			Assert.Equal(AmendmentObjectType.Article, content.ObjectType);
			Assert.Single(content.Articles);

			var article = content.Articles[0];
			Assert.Equal("7", article.Number?.Value);
			Assert.Equal(2, article.Paragraphs.Count);

			var paragraph1 = article.Paragraphs[0];
			Assert.Equal("1", paragraph1.Number?.Value);
			Assert.Single(paragraph1.Points);

			var point = paragraph1.Points[0];
			Assert.Equal("1", point.Number?.Value);
			Assert.Single(point.Letters);

			var letter = point.Letters[0];
			Assert.Equal("a", letter.Number?.Value);
			Assert.Single(letter.Tirets);

			var tiret = letter.Tirets[0];
			Assert.Equal("1", tiret.Number?.Value);
			Assert.Equal("treść tiretu;", tiret.ContentText);

			var paragraph2 = article.Paragraphs[1];
			Assert.Equal("2", paragraph2.Number?.Value);
		}

		// ============================================================
		// Wiele artykułów
		// ============================================================

		[Fact]
		public void Build_MultipleArticles_AllCollectedInContent()
		{
			var paragraphs = new List<CollectedAmendmentParagraph>
			{
				CreateParagraph("Art. 5a. Treść artykułu 5a.",
					"ZARTzmartartykuempunktem", AmendmentTargetKind.Article),
				CreateParagraph("Art. 5b. Treść artykułu 5b.",
					"ZARTzmartartykuempunktem", AmendmentTargetKind.Article),
				CreateParagraph("Art. 5c. Treść artykułu 5c.",
					"ZARTzmartartykuempunktem", AmendmentTargetKind.Article)
			};

			var input = new AmendmentBuildInput(paragraphs, null, AmendmentOperationType.Insertion);
			var content = _builder.Build(input);

			Assert.Equal(3, content.Articles.Count);
			Assert.Equal("5a", content.Articles[0].Number?.Value);
			Assert.Equal("5b", content.Articles[1].Number?.Value);
			Assert.Equal("5c", content.Articles[2].Number?.Value);
		}

		// ============================================================
		// Fallback tekstowy (brak AmendmentStyleInfo)
		// ============================================================

		[Fact]
		public void Build_NoStyleInfo_UsesTextClassification()
		{
			var paragraphs = new List<CollectedAmendmentParagraph>
			{
				new("Art. 5b. Treść artykułu bez stylu.", null, null),
				new("1. Treść ustępu.", null, null)
			};

			var input = new AmendmentBuildInput(paragraphs, null, AmendmentOperationType.Modification);
			var content = _builder.Build(input);

			Assert.Equal(AmendmentObjectType.Article, content.ObjectType);
			Assert.Single(content.Articles);
			Assert.Equal("5b", content.Articles[0].Number?.Value);
			// Artykuł ma implicit paragraph z ogona + jawny ustęp 1
			Assert.Equal(2, content.Articles[0].Paragraphs.Count);
			Assert.True(content.Articles[0].Paragraphs[0].IsImplicit);
			Assert.Equal("1", content.Articles[0].Paragraphs[1].Number?.Value);
		}

		[Fact]
		public void Build_NoStyleInfo_Points_UsesTextFallback()
		{
			var paragraphs = new List<CollectedAmendmentParagraph>
			{
				new("1) treść punktu;", null, null),
				new("2) treść drugiego punktu.", null, null)
			};

			var input = new AmendmentBuildInput(paragraphs, null, AmendmentOperationType.Modification);
			var content = _builder.Build(input);

			Assert.Equal(AmendmentObjectType.Point, content.ObjectType);
			Assert.Equal(2, content.Points.Count);
		}

		// ============================================================
		// Unknown → default case = AppendPlainText
		// ============================================================

		[Fact]
		public void Build_UnclassifiableParagraph_TreatedAsPlainText()
		{
			// Akapit Unknown (brak wzorca) → default case → AppendPlainText
			var paragraphs = new List<CollectedAmendmentParagraph>
			{
				new("Jakiś tekst bez struktury numerowania.", null, null)
			};

			var input = new AmendmentBuildInput(paragraphs, null, AmendmentOperationType.Modification);
			var content = _builder.Build(input);

			Assert.Equal("Jakiś tekst bez struktury numerowania.", content.PlainText);
		}

		[Fact]
		public void Build_MultiplePlainTexts_ConcatenatedWithNewlines()
		{
			var paragraphs = new List<CollectedAmendmentParagraph>
			{
				new("Linia pierwsza.", null, null),
				new("Linia druga.", null, null)
			};

			var input = new AmendmentBuildInput(paragraphs, null, AmendmentOperationType.Modification);
			var content = _builder.Build(input);

			Assert.Contains("Linia pierwsza.", content.PlainText);
			Assert.Contains("Linia druga.", content.PlainText);
		}

		// ============================================================
		// Część wspólna (CommonPart)
		// ============================================================

		[Fact]
		public void Build_CommonPartParagraph_CreatesCommonPart()
		{
			var styleInfo = new AmendmentStyleInfo
			{
				Instrument = AmendmentInstrument.ArticleOrPoint,
				TargetKind = AmendmentTargetKind.CommonPart,
				CommonPartOf = AmendmentTargetKind.Point,
				ShortCode = "Z/CZ_WSP_PKT",
				DisplayName = "Z/CZ_WSP_PKT – zm. części wsp. pkt artykułem (punktem)"
			};

			var paragraphs = new List<CollectedAmendmentParagraph>
			{
				new("– wspólna treść po wyliczeniu.", "ZCZWSPPKTzmczciwsppktartykuempunktem", styleInfo)
			};

			var input = new AmendmentBuildInput(paragraphs, null, AmendmentOperationType.Modification);
			var content = _builder.Build(input);

			Assert.Equal(AmendmentObjectType.CommonPart, content.ObjectType);
			Assert.Single(content.CommonParts);
			Assert.Equal(CommonPartType.WrapUp, content.CommonParts[0].Type);
		}

		// ============================================================
		// DetermineObjectType
		// ============================================================

		[Theory]
		[InlineData(AmendmentTargetKind.Article, AmendmentObjectType.Article)]
		[InlineData(AmendmentTargetKind.Paragraph, AmendmentObjectType.Paragraph)]
		[InlineData(AmendmentTargetKind.Point, AmendmentObjectType.Point)]
		[InlineData(AmendmentTargetKind.Letter, AmendmentObjectType.Letter)]
		[InlineData(AmendmentTargetKind.Tiret, AmendmentObjectType.Tiret)]
		[InlineData(AmendmentTargetKind.CommonPart, AmendmentObjectType.CommonPart)]
		public void DetermineObjectType_BasedOnFirstParagraph(
			AmendmentTargetKind targetKind, AmendmentObjectType expected)
		{
			var paragraphs = new List<CollectedAmendmentParagraph>
			{
				CreateParagraph("dummy", "style", targetKind)
			};

			var result = AmendmentBuilder.DetermineObjectType(paragraphs);

			Assert.Equal(expected, result);
		}

		[Fact]
		public void DetermineObjectType_EmptyList_ReturnsNone()
		{
			var result = AmendmentBuilder.DetermineObjectType(new List<CollectedAmendmentParagraph>());
			Assert.Equal(AmendmentObjectType.None, result);
		}

		// ============================================================
		// ClassifyAmendmentParagraph
		// ============================================================

		[Fact]
		public void ClassifyAmendmentParagraph_WithStyleInfo_UsesTargetKind()
		{
			var para = CreateParagraph("1. Treść", "ZUSTzmustartykuempunktem",
				AmendmentTargetKind.Paragraph);

			var result = AmendmentBuilder.ClassifyAmendmentParagraph(para);

			Assert.Equal(AmendmentEntityKind.Paragraph, result);
		}

		[Fact]
		public void ClassifyAmendmentParagraph_WithoutStyleInfo_UsesTextFallback()
		{
			var para = new CollectedAmendmentParagraph("1) punkt;", null, null);

			var result = AmendmentBuilder.ClassifyAmendmentParagraph(para);

			Assert.Equal(AmendmentEntityKind.Point, result);
		}

		[Theory]
		[InlineData("Art. 5. Treść", AmendmentEntityKind.Article)]
		[InlineData("1. Treść ustępu.", AmendmentEntityKind.Paragraph)]
		[InlineData("1) treść punktu;", AmendmentEntityKind.Point)]
		[InlineData("a) treść litery;", AmendmentEntityKind.Letter)]
		[InlineData("\u2013 treść tiretu;", AmendmentEntityKind.Tiret)]
		[InlineData("Zwykły tekst", AmendmentEntityKind.Unknown)]
		public void ClassifyAmendmentParagraph_NoStyleInfo_TextBased(
			string text, AmendmentEntityKind expected)
		{
			var para = new CollectedAmendmentParagraph(text, null, null);

			var result = AmendmentBuilder.ClassifyAmendmentParagraph(para);

			Assert.Equal(expected, result);
		}

		// ============================================================
		// MapTargetKindToEntityKind
		// ============================================================

		[Theory]
		[InlineData(AmendmentTargetKind.Article, AmendmentEntityKind.Article)]
		[InlineData(AmendmentTargetKind.Paragraph, AmendmentEntityKind.Paragraph)]
		[InlineData(AmendmentTargetKind.Point, AmendmentEntityKind.Point)]
		[InlineData(AmendmentTargetKind.Letter, AmendmentEntityKind.Letter)]
		[InlineData(AmendmentTargetKind.Tiret, AmendmentEntityKind.Tiret)]
		[InlineData(AmendmentTargetKind.DoubleTiret, AmendmentEntityKind.Tiret)]
		[InlineData(AmendmentTargetKind.CommonPart, AmendmentEntityKind.CommonPart)]
		[InlineData(AmendmentTargetKind.Fragment, AmendmentEntityKind.PlainText)]
		[InlineData(AmendmentTargetKind.Citation, AmendmentEntityKind.PlainText)]
		[InlineData(AmendmentTargetKind.PenalSanction, AmendmentEntityKind.PlainText)]
		[InlineData(AmendmentTargetKind.NonArticleText, AmendmentEntityKind.PlainText)]
		[InlineData(AmendmentTargetKind.Unknown, AmendmentEntityKind.Unknown)]
		public void MapTargetKindToEntityKind_AllMappings(
			AmendmentTargetKind targetKind, AmendmentEntityKind expected)
		{
			Assert.Equal(expected, AmendmentBuilder.MapTargetKindToEntityKind(targetKind));
		}

		// ============================================================
		// Intro CommonPart wiązanie
		// ============================================================

		[Fact]
		public void Build_PointsInParagraph_IntroAttachedBeforeFirstPoint()
		{
			var paragraphs = new List<CollectedAmendmentParagraph>
			{
				CreateParagraph("1. Wprowadzenie do wyliczenia:",
					"ZUSTzmustartykuempunktem", AmendmentTargetKind.Paragraph),
				CreateParagraph("1) treść punktu;",
					"ZPKTzmpktartykuempunktem", AmendmentTargetKind.Point)
			};

			var input = new AmendmentBuildInput(paragraphs, null, AmendmentOperationType.Modification);
			var content = _builder.Build(input);

			Assert.Single(content.Paragraphs);
			var para = content.Paragraphs[0];
			// Intro powinno być dołączone jako CommonPart
			Assert.Contains(para.CommonParts, cp => cp.Type == CommonPartType.Intro);
		}

		// ============================================================
		// Builder jest wielokrotnego użytku (reset stanu)
		// ============================================================

		[Fact]
		public void Build_CalledTwice_StateIsReset()
		{
			var input1 = new AmendmentBuildInput(
				new List<CollectedAmendmentParagraph>
				{
					CreateParagraph("Art. 5. Treść.", "ZARTzmartartykuempunktem", AmendmentTargetKind.Article)
				},
				null, AmendmentOperationType.Insertion);

			var input2 = new AmendmentBuildInput(
				new List<CollectedAmendmentParagraph>
				{
					CreateParagraph("1) punkt;", "ZPKTzmpktartykuempunktem", AmendmentTargetKind.Point)
				},
				null, AmendmentOperationType.Modification);

			var content1 = _builder.Build(input1);
			var content2 = _builder.Build(input2);

			// Drugie wywołanie nie powinno mieć stanów z pierwszego
			Assert.Single(content1.Articles);
			Assert.Empty(content1.Points);
			Assert.Empty(content2.Articles);
			Assert.Single(content2.Points);
		}

		// ============================================================
		// Helpers
		// ============================================================

		private static CollectedAmendmentParagraph CreateParagraph(
			string text, string? styleId, AmendmentTargetKind targetKind)
		{
			var styleInfo = new AmendmentStyleInfo
			{
				Instrument = AmendmentInstrument.ArticleOrPoint,
				TargetKind = targetKind,
				ShortCode = "test",
				DisplayName = "test"
			};
			return new CollectedAmendmentParagraph(text, styleId, styleInfo);
		}
	}
}
