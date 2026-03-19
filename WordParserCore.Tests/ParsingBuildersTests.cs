using ModelDto;
using ModelDto.EditorialUnits;
using ModelDto.SystematizingUnits;
using WordParserCore.Services.Parsing;
using WordParserCore.Services.Parsing.Builders;
using Xunit;

namespace WordParserCore.Tests
{
    public class ArticleBuilderTests
    {
        [Fact]
        public void Build_ArticleWithTailWithoutNumber_CreatesImplicitParagraph()
        {
            var builder = new ArticleBuilder();
            var subchapter = new Subchapter();

            var result = builder.Build(new ArticleBuildInput(subchapter, "Art. 2. Tresc artykulu"));

            Assert.NotNull(result.Article);
            Assert.Single(result.Article.Paragraphs);
            Assert.True(result.Paragraph.IsImplicit);
            Assert.Equal("Tresc artykulu", result.Paragraph.ContentText);
        }

        [Fact]
        public void Build_ArticleWithNumberedTail_CreatesExplicitParagraph()
        {
            var builder = new ArticleBuilder();
            var subchapter = new Subchapter();

            var result = builder.Build(new ArticleBuildInput(subchapter, "Art. 3. 1. Tresc ustepu"));

            Assert.False(result.Paragraph.IsImplicit);
            Assert.Equal("1", result.Paragraph.Number?.Value);
        }
    }

    public class ParagraphBuilderTests
    {
        [Fact]
        public void Build_ReusesImplicitParagraphWhenEmpty()
        {
            var article = new Article();
            var implicitParagraph = ParsingFactories.CreateImplicitParagraph(article);
            article.Paragraphs.Add(implicitParagraph);

            var builder = new ParagraphBuilder();
            var result = builder.Build(new ParagraphBuildInput(article, implicitParagraph, "1. Tresc"));

            Assert.Same(implicitParagraph, result);
            Assert.False(result.IsImplicit);
            Assert.Equal("1", result.Number?.Value);
        }

        [Fact]
        public void EnsureForPoint_CreatesImplicitWhenMissing()
        {
            var article = new Article();
            var builder = new ParagraphBuilder();

            var ensured = builder.EnsureForPoint(article, null);

            Assert.True(ensured.CreatedImplicit);
            Assert.Single(article.Paragraphs);
            Assert.True(ensured.Paragraph.IsImplicit);
        }
    }

    public class PointBuilderTests
    {
        [Fact]
        public void Build_ParsesPointNumberWithoutSpace()
        {
            var article = new Article();
            var paragraph = new Paragraph { Article = article, Parent = article };
            var builder = new PointBuilder();

            var point = builder.Build(new PointBuildInput(paragraph, article, "13a)Tekst"));

            Assert.Equal("13a", point.Number?.Value);
        }

        [Fact]
        public void EnsureForLetter_CreatesImplicitPointWhenMissing()
        {
            var article = new Article();
            var paragraph = new Paragraph { Article = article, Parent = article };
            var builder = new PointBuilder();

            var ensured = builder.EnsureForLetter(paragraph, article, null);

            Assert.True(ensured.CreatedImplicit);
            Assert.Single(paragraph.Points);
        }
    }

    public class LetterBuilderTests
    {
        [Fact]
        public void Build_ParsesLetterNumberWithoutSpace()
        {
            var article = new Article();
            var paragraph = new Paragraph { Article = article, Parent = article };
            var point = new Point { Article = article, Paragraph = paragraph, Parent = paragraph };
            var builder = new LetterBuilder();

            var letter = builder.Build(new LetterBuildInput(point, paragraph, article, "abzz)Tekst"));

            Assert.Equal("abzz", letter.Number?.Value);
        }

        [Fact]
        public void EnsureForTiret_CreatesImplicitLetterWhenMissing()
        {
            var article = new Article();
            var paragraph = new Paragraph { Article = article, Parent = article };
            var point = new Point { Article = article, Paragraph = paragraph, Parent = paragraph };
            var builder = new LetterBuilder();

            var ensured = builder.EnsureForTiret(point, paragraph, article, null);

            Assert.True(ensured.CreatedImplicit);
            Assert.Single(point.Letters);
        }
    }

    public class TiretBuilderTests
    {
        [Fact]
        public void Build_AssignsIndexNumber()
        {
            var article = new Article();
            var paragraph = new Paragraph { Article = article, Parent = article };
            var point = new Point { Article = article, Paragraph = paragraph, Parent = paragraph };
            var letter = new Letter { Article = article, Paragraph = paragraph, Point = point, Parent = point };
            var builder = new TiretBuilder();

            var tiret = builder.Build(new TiretBuildInput(letter, point, paragraph, article, "\u2013 Tekst", 2));

            Assert.Equal("2", tiret.Number?.Value);
        }
    }
}
