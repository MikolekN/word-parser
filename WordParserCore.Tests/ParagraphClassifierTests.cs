using WordParserCore.Services.Classify;
using Xunit;

namespace WordParserCore.Tests
{
    public class ParagraphClassifierTests
    {
        private static ClassificationResult Classify(string text, string? styleId)
            => new ParagraphClassifier().Classify(new ClassificationInput(text, styleId));

        [Fact]
        public void Classify_ArticleByText_ReturnsArticle()
        {
            var result = Classify("Art. 3. 1. Tresc", null);

            Assert.Equal(ParagraphKind.Article, result.Kind);
            // Brak stylu → StyleAbsentPenalty, ale Kind poprawny
            Assert.True(result.Confidence < 100);
            Assert.False(result.IsAmendmentContent);
        }

        [Fact]
        public void Classify_ParagraphByStyleAndText_FullConfidence()
        {
            var result = Classify("1. Tresc ustepu", "UST");

            Assert.Equal(ParagraphKind.Paragraph, result.Kind);
            Assert.Equal(100, result.Confidence);
            Assert.Empty(result.Penalties);
        }

        [Fact]
        public void Classify_PointByText_NoSpace_ReturnsPoint()
        {
            var result = Classify("13a)Tekst punktu", null);

            Assert.Equal(ParagraphKind.Point, result.Kind);
            Assert.True(result.Confidence < 100);
        }

        [Fact]
        public void Classify_PointByText_WithOpeningQuote_ReturnsPoint()
        {
            var result = Classify("\"3) Tekst punktu", null);

            Assert.Equal(ParagraphKind.Point, result.Kind);
        }

        [Fact]
        public void Classify_LetterByText_NoSpace_ReturnsLetter()
        {
            var result = Classify("abzz)Tekst litery", null);

            Assert.Equal(ParagraphKind.Letter, result.Kind);
        }

        [Fact]
        public void Classify_TiretByText_ReturnsTiret()
        {
            // Tekst po Sanitize(): \u2013 → '-', \t → ' '
            var result = Classify("- Tekst tiretu", null);

            Assert.Equal(ParagraphKind.Tiret, result.Kind);
        }

        [Fact]
        public void Classify_TiretByStyleAndText_FullConfidence()
        {
            // Tekst jaki trafia z orkiestratora: en-dash+tab po Sanitize() → "- "
            // Przypadek z DocRepo/306960_2737084.docx, art_1__pkt_19__lit_c__tir_1
            var result = Classify("- lit. a otrzymuje brzmienie:", "TIRtiret");

            Assert.Equal(ParagraphKind.Tiret, result.Kind);
            Assert.Equal(100, result.Confidence);
            Assert.Empty(result.Penalties);
        }

        [Fact]
        public void Classify_AmendmentStyleZ_ReturnsAmendmentContentAndKindFromText()
        {
            // Styl nowelizacji + tekst pasujący do Paragraph
            var result = Classify("2. Stosowanie wyłączenia określonego w ust. 1 nie moze...", "ZUSTzmustartykuempunktem");

            Assert.True(result.IsAmendmentContent);
            // Kind ustalany z tekstu (niezależnie od cechy amendment)
            Assert.Equal(ParagraphKind.Paragraph, result.Kind);
        }

        [Fact]
        public void Classify_AmendmentStyleZArt_ReturnsAmendmentContentAndArticleKind()
        {
            var result = Classify("Art. 5. Treść artykułu w nowelizacji.", "ZARTzmartartykuempunktem");

            Assert.True(result.IsAmendmentContent);
            Assert.Equal(ParagraphKind.Article, result.Kind);
        }

        [Fact]
        public void Classify_ZLitStyle_ReturnsAmendmentContent()
        {
            var result = Classify("1) punkt w literze nowelizacji", "ZLITPKTzmpktliter");

            Assert.True(result.IsAmendmentContent);
        }

        [Fact]
        public void Classify_ZTirStyle_ReturnsAmendmentContent()
        {
            var result = Classify("a) litera w tirecie nowelizacji", "ZTIRLITzmlittiret");

            Assert.True(result.IsAmendmentContent);
        }

        [Fact]
        public void Classify_ZZStyle_ReturnsAmendmentContent()
        {
            var result = Classify("Art. 5. Treść", "ZZARTzmianazmart");

            Assert.True(result.IsAmendmentContent);
        }

        [Fact]
        public void Classify_NormalArt_NotAmendment_FullConfidence()
        {
            var result = Classify("Art. 1. Treść", "ARTartustawynprozporzdzenia");

            Assert.False(result.IsAmendmentContent);
            Assert.Equal(ParagraphKind.Article, result.Kind);
            Assert.Equal(100, result.Confidence);
        }

        [Fact]
        public void Classify_ZdanieStyle_NotAmendment()
        {
            var result = Classify("2. Treść ustępu", "ZDANIENASTNOWYWIERSZnpzddrugienowywierszwust");

            Assert.False(result.IsAmendmentContent);
            Assert.Equal(ParagraphKind.Paragraph, result.Kind);
        }

        [Fact]
        public void Classify_NormalStylePointByText_ReturnsPointWithNumberSeven()
        {
            var result = Classify("7)\tw art. 166 ust. 1 otrzymuje brzmienie:", "Normalny");

            Assert.Equal(ParagraphKind.Point, result.Kind);
            // Brak rozpoznanego stylu "Normalny" → kara StyleAbsent
            Assert.True(result.Confidence < 100);
            Assert.True(ParagraphClassifier.PointNumberCapture.IsMatch("7)\tw art. 166 ust. 1 otrzymuje brzmienie:"));
            Assert.Equal("7", ParagraphClassifier.PointNumberCapture.Match("7)\tw art. 166 ust. 1 otrzymuje brzmienie:").Groups[1].Value);
        }
    }
}
