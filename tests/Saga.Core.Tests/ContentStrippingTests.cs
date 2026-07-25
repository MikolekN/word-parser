using ModelDto;
using ModelDto.EditorialUnits;
using ModelDto.SystematizingUnits;
using WordParserCore.Services.Parsing;
using WordParserCore.Services.Parsing.Builders;
using Xunit;

namespace WordParserCore.Tests
{
    public class ContentStrippingTests
    {
        // --- StripPrefix ---

        [Fact]
        public void StripParagraphPrefix_RemovesNumberDot()
        {
            var result = ParsingFactories.StripParagraphPrefix("1. Treść ustępu pierwszego.");
            Assert.Equal("Treść ustępu pierwszego.", result);
        }

        [Fact]
        public void StripParagraphPrefix_RemovesAlphanumericNumber()
        {
            var result = ParsingFactories.StripParagraphPrefix("2a. Ustęp z przyrostkiem.");
            Assert.Equal("Ustęp z przyrostkiem.", result);
        }

        [Fact]
        public void StripParagraphPrefix_NoPrefix_ReturnsUnchanged()
        {
            var result = ParsingFactories.StripParagraphPrefix("Brak numeru tutaj.");
            Assert.Equal("Brak numeru tutaj.", result);
        }

        [Fact]
        public void StripPointPrefix_RemovesNumberParen()
        {
            var result = ParsingFactories.StripPointPrefix("1) treść punktu pierwszego");
            Assert.Equal("treść punktu pierwszego", result);
        }

        [Fact]
        public void StripPointPrefix_WithOpeningQuote_RemovesQuoteAndNumber()
        {
            var result = ParsingFactories.StripPointPrefix("\"3) treść punktu");
            Assert.Equal("treść punktu", result);
        }

        [Fact]
        public void StripPointPrefix_NoSpaceAfterParen()
        {
            var result = ParsingFactories.StripPointPrefix("3a)treść");
            Assert.Equal("treść", result);
        }

        [Fact]
        public void StripLetterPrefix_RemovesLetterParen()
        {
            var result = ParsingFactories.StripLetterPrefix("a) treść litery");
            Assert.Equal("treść litery", result);
        }

        [Fact]
        public void StripParagraphPrefix_WithOpeningQuote_RemovesQuoteAndNumber()
        {
            var result = ParsingFactories.StripParagraphPrefix("\u201E3a. treść ustępu");
            Assert.Equal("treść ustępu", result);
        }

        [Fact]
        public void StripTiretPrefix_RemovesEnDash()
        {
            var result = ParsingFactories.StripTiretPrefix("\u2013 treść tiretu");
            Assert.Equal("treść tiretu", result);
        }

        [Fact]
        public void ParsePointNumber_WithOpeningQuote_ReturnsNumber()
        {
            var number = ParsingFactories.ParsePointNumber("\"3) treść punktu");
            Assert.Equal("3", number?.Value);
        }

        [Fact]
        public void ParseParagraphNumber_WithOpeningQuote_ReturnsNumber()
        {
            var number = ParsingFactories.ParseParagraphNumber("\u201E6a. treść ustępu");
            Assert.Equal("6a", number?.Value);
        }

        // --- SplitIntoSentences ---

        [Fact]
        public void SplitIntoSentences_SingleSentence_ReturnsSingleSegment()
        {
            var segments = ParsingFactories.SplitIntoSentences("Przepis stosuje się odpowiednio.");
            Assert.Single(segments);
            Assert.Equal("Przepis stosuje się odpowiednio.", segments[0].Text);
            Assert.Equal(1, segments[0].Order);
            Assert.Equal(TextSegmentType.Sentence, segments[0].Type);
        }

        [Fact]
        public void SplitIntoSentences_TwoSentences_SplitsCorrectly()
        {
            var segments = ParsingFactories.SplitIntoSentences(
                "Przepis wchodzi w życie z dniem ogłoszenia. Minister właściwy wydaje rozporządzenie.");
            Assert.Equal(2, segments.Count);
            Assert.Equal("Przepis wchodzi w życie z dniem ogłoszenia.", segments[0].Text);
            Assert.Equal("Minister właściwy wydaje rozporządzenie.", segments[1].Text);
            Assert.Equal(1, segments[0].Order);
            Assert.Equal(2, segments[1].Order);
        }

        [Fact]
        public void SplitIntoSentences_DotWithoutUppercase_DoesNotSplit()
        {
            // po "ust." jest spacja ale mała litera - nie jest to podział zdania
            var segments = ParsingFactories.SplitIntoSentences("w ust. 2 pkt 3 dodaje się lit. b");
            Assert.Single(segments);
        }

        [Fact]
        public void SplitIntoSentences_EmptyText_ReturnsEmpty()
        {
            var segments = ParsingFactories.SplitIntoSentences("");
            Assert.Empty(segments);
        }

        [Fact]
        public void SplitIntoSentences_ThreeSentences()
        {
            var text = "Zdanie pierwsze. Zdanie drugie. Zdanie trzecie.";
            var segments = ParsingFactories.SplitIntoSentences(text);
            Assert.Equal(3, segments.Count);
            Assert.Equal(3, segments[2].Order);
        }

        // --- BuilderIntegration: ContentText bez numeru + TextSegments ---

        [Fact]
        public void ParagraphBuilder_ContentTextWithoutNumber()
        {
            var article = new Article();
            var builder = new ParagraphBuilder();

            var paragraph = builder.Build(new ParagraphBuildInput(article, null, "1. Treść pierwszego ustępu."));

            Assert.Equal("1", paragraph.Number?.Value);
            Assert.Equal("Treść pierwszego ustępu.", paragraph.ContentText);
            Assert.DoesNotContain("1.", paragraph.ContentText.Split(' ')[0] + ".");
        }

        [Fact]
        public void ParagraphBuilder_CreatesTextSegments()
        {
            var article = new Article();
            var builder = new ParagraphBuilder();

            var paragraph = builder.Build(new ParagraphBuildInput(article, null,
                "1. Przepis pierwszego zdania. Drugie zdanie ustępu."));

            Assert.Equal(2, paragraph.TextSegments.Count);
            Assert.Equal("Przepis pierwszego zdania.", paragraph.TextSegments[0].Text);
            Assert.Equal("Drugie zdanie ustępu.", paragraph.TextSegments[1].Text);
        }

        [Fact]
        public void PointBuilder_ContentTextWithoutNumber()
        {
            var article = new Article();
            var paragraph = new Paragraph { Article = article };
            var builder = new PointBuilder();

            var point = builder.Build(new PointBuildInput(paragraph, article, "1) treść punktu pierwszego"));

            Assert.Equal("1", point.Number?.Value);
            Assert.Equal("treść punktu pierwszego", point.ContentText);
        }

        [Fact]
        public void PointBuilder_CreatesTextSegments()
        {
            var article = new Article();
            var paragraph = new Paragraph { Article = article };
            var builder = new PointBuilder();

            var point = builder.Build(new PointBuildInput(paragraph, article,
                "1) Zdanie pierwsze punktu. Zdanie drugie punktu."));

            Assert.Equal(2, point.TextSegments.Count);
        }

        [Fact]
        public void LetterBuilder_ContentTextWithoutNumber()
        {
            var article = new Article();
            var paragraph = new Paragraph { Article = article };
            var point = new Point { Article = article, Paragraph = paragraph };
            var builder = new LetterBuilder();

            var letter = builder.Build(new LetterBuildInput(point, paragraph, article, "a) treść litery a"));

            Assert.Equal("a", letter.Number?.Value);
            Assert.Equal("treść litery a", letter.ContentText);
        }

        [Fact]
        public void TiretBuilder_ContentTextWithoutPrefix()
        {
            var article = new Article();
            var paragraph = new Paragraph { Article = article };
            var point = new Point { Article = article, Paragraph = paragraph };
            var letter = new Letter { Article = article, Paragraph = paragraph, Point = point };
            var builder = new TiretBuilder();

            var tiret = builder.Build(new TiretBuildInput(letter, point, paragraph, article,
                "\u2013 treść tiretu pierwszego", 1));

            Assert.Equal("treść tiretu pierwszego", tiret.ContentText);
            Assert.Single(tiret.TextSegments);
        }

        [Fact]
        public void ArticleBuilder_ParagraphFromTail_ContentWithoutNumber()
        {
            var builder = new ArticleBuilder();
            var subchapter = new Subchapter();

            var result = builder.Build(new ArticleBuildInput(subchapter, "Art. 5. 1. Treść ustępu."));

            Assert.Equal("1", result.Paragraph.Number?.Value);
            Assert.Equal("Treść ustępu.", result.Paragraph.ContentText);
            Assert.Single(result.Paragraph.TextSegments);
        }

        [Fact]
        public void ArticleBuilder_ImplicitParagraph_ContentWithoutArtPrefix()
        {
            var builder = new ArticleBuilder();
            var subchapter = new Subchapter();

            var result = builder.Build(new ArticleBuildInput(subchapter, "Art. 2. Treść artykułu."));

            Assert.True(result.Paragraph.IsImplicit);
            Assert.Equal("Treść artykułu.", result.Paragraph.ContentText);
            Assert.Single(result.Paragraph.TextSegments);
        }

        [Fact]
        public void ParagraphBuilder_ReusesImplicit_ContentWithoutNumber()
        {
            var article = new Article();
            var implicitParagraph = ParsingFactories.CreateImplicitParagraph(article);
            article.Paragraphs.Add(implicitParagraph);
            var builder = new ParagraphBuilder();

            var result = builder.Build(new ParagraphBuildInput(article, implicitParagraph, "1. Treść."));

            Assert.Same(implicitParagraph, result);
            Assert.Equal("1", result.Number?.Value);
            Assert.Equal("Treść.", result.ContentText);
            Assert.False(result.IsImplicit);
        }
    }
}
