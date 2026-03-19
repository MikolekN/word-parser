using ModelDto;
using WordParserCore.Services;
using WordParserCore.Services.Parsing;
using WordParserCore.Services.Parsing.Builders;
using ModelDto.EditorialUnits;
using ModelDto.SystematizingUnits;
using Xunit;

namespace WordParserCore.Tests
{
    public class LegalReferenceServiceTests
    {
        private readonly LegalReferenceService _service = new();

        [Fact]
        public void UpdateLegalReference_DetectsArticle()
        {
            var reference = new StructuralReference();

            _service.UpdateLegalReference(reference, "w art. 5 dodaje się pkt 3");

            Assert.Equal("5", reference.Article);
        }

        [Fact]
        public void UpdateLegalReference_DetectsParagraph()
        {
            var reference = new StructuralReference();

            _service.UpdateLegalReference(reference, "w ust. 2 pkt 1 otrzymuje brzmienie");

            Assert.Equal("2", reference.Paragraph);
        }

        [Fact]
        public void UpdateLegalReference_DetectsPoint()
        {
            var reference = new StructuralReference();

            _service.UpdateLegalReference(reference, "po pkt 3a dodaje się pkt 3b");

            Assert.Equal("3a", reference.Point);
        }

        [Fact]
        public void UpdateLegalReference_DetectsLetter()
        {
            var reference = new StructuralReference();

            _service.UpdateLegalReference(reference, "w lit. b tiret drugie");

            Assert.Equal("b", reference.Letter);
        }

        [Fact]
        public void UpdateLegalReference_DetectsMultipleLevels()
        {
            var reference = new StructuralReference();

            _service.UpdateLegalReference(reference, "w art. 10 w ust. 3 pkt 2 otrzymuje brzmienie");

            Assert.Equal("10", reference.Article);
            Assert.Equal("3", reference.Paragraph);
            Assert.Equal("2", reference.Point);
        }

        [Fact]
        public void UpdateLegalReference_DoesNotOverrideExistingValues()
        {
            var reference = new StructuralReference();
            reference.Article = "7";

            _service.UpdateLegalReference(reference, "w art. 5 ust. 2 dodaje się pkt 3");

            Assert.Equal("7", reference.Article); // nie nadpisane
            Assert.Equal("2", reference.Paragraph); // wykryte z tekstu
        }

        [Fact]
        public void UpdateLegalReference_NoMatchReturnsEmptyReference()
        {
            var reference = new StructuralReference();

            _service.UpdateLegalReference(reference, "Przepis stosuje się odpowiednio.");

            Assert.Null(reference.Article);
            Assert.Null(reference.Paragraph);
            Assert.Null(reference.Point);
            Assert.Null(reference.Letter);
        }

        [Fact]
        public void GetContext_BuildsParentChainText()
        {
            var article = new Article { ContentText = "Art. 5." };
            var paragraph = new Paragraph { Parent = article, ContentText = "1. Tekst ustępu" };
            var point = new Point { Parent = paragraph, ContentText = "1) tekst punktu" };

            var context = _service.GetContext(point);

            Assert.Contains("Tekst ustępu", context);
            Assert.Contains("tekst punktu", context);
            // Article jest granicą - nie jest włączany
            Assert.DoesNotContain("Art. 5", context);
        }
    }

    public class OrchestratorAmendmentIntegrationTests
    {
        [Fact]
        public void ProcessParagraph_UpdatesStructuralReference_ForArticle()
        {
            var document = new LegalDocument();
            var subchapter = new Subchapter();
            var context = new ParsingContext(document, subchapter);
            var orchestrator = new ParserOrchestrator();

            var paragraph = CreateWordParagraph("Art. 5. Treść artykułu");
            orchestrator.ProcessParagraph(paragraph, context);

            Assert.Equal("5", context.CurrentStructuralReference.Article);
        }

        [Fact]
        public void ProcessParagraph_UpdatesStructuralReference_ForParagraph()
        {
            var document = new LegalDocument();
            var subchapter = new Subchapter();
            var context = new ParsingContext(document, subchapter);
            var orchestrator = new ParserOrchestrator();

            orchestrator.ProcessParagraph(CreateWordParagraph("Art. 3. 1. Pierwszy ustęp"), context);
            orchestrator.ProcessParagraph(CreateWordParagraph("2. Drugi ustęp"), context);

            Assert.Equal("3", context.CurrentStructuralReference.Article);
            Assert.Equal("2", context.CurrentStructuralReference.Paragraph);
        }

        [Fact]
        public void ProcessParagraph_UpdatesStructuralReference_ForPoint()
        {
            var document = new LegalDocument();
            var subchapter = new Subchapter();
            var context = new ParsingContext(document, subchapter);
            var orchestrator = new ParserOrchestrator();

            orchestrator.ProcessParagraph(CreateWordParagraph("Art. 1. 1. Ustęp pierwszy"), context);
            orchestrator.ProcessParagraph(CreateWordParagraph("1) punkt pierwszy"), context);

            Assert.Equal("1", context.CurrentStructuralReference.Article);
            Assert.Equal("1", context.CurrentStructuralReference.Paragraph);
            Assert.Equal("1", context.CurrentStructuralReference.Point);
        }

        [Fact]
        public void ProcessParagraph_NewArticle_ResetsChildReferences()
        {
            var document = new LegalDocument();
            var subchapter = new Subchapter();
            var context = new ParsingContext(document, subchapter);
            var orchestrator = new ParserOrchestrator();

            orchestrator.ProcessParagraph(CreateWordParagraph("Art. 1. 1. Ustęp"), context);
            orchestrator.ProcessParagraph(CreateWordParagraph("1) punkt"), context);

            // Nowy artykuł - reset dzieci
            orchestrator.ProcessParagraph(CreateWordParagraph("Art. 2. Treść"), context);

            Assert.Equal("2", context.CurrentStructuralReference.Article);
            Assert.Null(context.CurrentStructuralReference.Point);
        }

        [Fact]
        public void ProcessParagraph_DetectsAmendmentTargets_InParagraphContent()
        {
            var document = new LegalDocument();
            var subchapter = new Subchapter();
            var context = new ParsingContext(document, subchapter);
            var orchestrator = new ParserOrchestrator();

            orchestrator.ProcessParagraph(
                CreateWordParagraph("Art. 1. 1. W art. 10 ust. 3 dodaje się pkt 5"), context);

            // Paragraf z treścią nowelizacyjną powinien mieć wykryty cel
            Assert.NotNull(context.CurrentParagraph);
            Assert.True(context.DetectedAmendmentTargets.ContainsKey(context.CurrentParagraph.Guid));

            var target = context.DetectedAmendmentTargets[context.CurrentParagraph.Guid];
            Assert.Equal("10", target.Structure.Article);
            Assert.Equal("3", target.Structure.Paragraph);
        }

        [Fact]
        public void ProcessParagraph_NoAmendmentTargets_ForNormalContent()
        {
            var document = new LegalDocument();
            var subchapter = new Subchapter();
            var context = new ParsingContext(document, subchapter);
            var orchestrator = new ParserOrchestrator();

            orchestrator.ProcessParagraph(
                CreateWordParagraph("Art. 1. 1. Przepis stosuje się odpowiednio."), context);

            Assert.NotNull(context.CurrentParagraph);
            Assert.False(context.DetectedAmendmentTargets.ContainsKey(context.CurrentParagraph.Guid));
        }

        /// <summary>
        /// Tworzy minimalny akapit Wordowy z podanym tekstem (do testów).
        /// </summary>
        private static DocumentFormat.OpenXml.Wordprocessing.Paragraph CreateWordParagraph(string text)
        {
            var paragraph = new DocumentFormat.OpenXml.Wordprocessing.Paragraph();
            var run = new DocumentFormat.OpenXml.Wordprocessing.Run();
            run.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Text(text));
            paragraph.AppendChild(run);
            return paragraph;
        }
    }
}
