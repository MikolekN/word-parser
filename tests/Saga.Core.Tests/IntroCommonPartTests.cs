using ModelDto;
using ModelDto.EditorialUnits;
using WordParserCore.Services.Parsing;
using Xunit;

namespace WordParserCore.Tests
{
    /// <summary>
    /// Testy wiazania CommonPart Intro z segmentem tekstu rodzica
    /// (logika AttachIntroCommonPart).
    /// </summary>
    public class IntroCommonPartTests
    {
        [Fact]
        public void AttachIntro_SingleSegment_WholeSegmentBecomesIntro()
        {
            // Arrange: ustep z jednym zdaniem
            var paragraph = new Paragraph
            {
                ContentText = "W ustawie wprowadza się następujące zmiany:"
            };
            ParsingFactories.SetContentAndSegments(paragraph, paragraph.ContentText);

            // Act
            ParsingFactories.AttachIntroCommonPart(paragraph);

            // Assert
            Assert.Single(paragraph.CommonParts);
            var intro = paragraph.CommonParts[0];
            Assert.Equal(CommonPartType.Intro, intro.Type);
            Assert.Equal(1, intro.SourceSegmentOrder);
            Assert.Equal(paragraph.ContentText, intro.ContentText);
            Assert.Equal("ListIntro", paragraph.TextSegments[0].Role);
            Assert.Same(paragraph, intro.Parent);
        }

        [Fact]
        public void AttachIntro_MultipleSegments_LastSegmentBecomesIntro()
        {
            // Arrange: punkt z dwoma zdaniami
            var point = new Point
            {
                ContentText = "Zmienia się regulamin. W regulaminie wprowadza się zmiany:"
            };
            ParsingFactories.SetContentAndSegments(point, point.ContentText);

            // Pre-check: powinny byc 2 segmenty
            Assert.Equal(2, point.TextSegments.Count);

            // Act
            ParsingFactories.AttachIntroCommonPart(point);

            // Assert
            Assert.Single(point.CommonParts);
            var intro = point.CommonParts[0];
            Assert.Equal(CommonPartType.Intro, intro.Type);
            Assert.Equal(2, intro.SourceSegmentOrder);
            Assert.Equal(point.TextSegments[1].Text, intro.ContentText);
            Assert.Null(point.TextSegments[0].Role); // pierwsze zdanie nie jest intro
            Assert.Equal("ListIntro", point.TextSegments[1].Role);
        }

        [Fact]
        public void AttachIntro_NoSegments_DoesNothing()
        {
            // Arrange: ustep bez tekstu (implicit)
            var paragraph = new Paragraph { IsImplicit = true, ContentText = string.Empty };

            // Act
            ParsingFactories.AttachIntroCommonPart(paragraph);

            // Assert
            Assert.Empty(paragraph.CommonParts);
        }

        [Fact]
        public void AttachIntro_CalledTwice_DoesNotDuplicate()
        {
            // Arrange
            var paragraph = new Paragraph
            {
                ContentText = "Wprowadza się zmiany:"
            };
            ParsingFactories.SetContentAndSegments(paragraph, paragraph.ContentText);

            // Act
            ParsingFactories.AttachIntroCommonPart(paragraph);
            ParsingFactories.AttachIntroCommonPart(paragraph);

            // Assert: tylko jeden CommonPart
            Assert.Single(paragraph.CommonParts);
        }

        [Fact]
        public void AttachIntro_Letter_WorksForTiretParent()
        {
            // Arrange: litera z jednym zdaniem jako intro dla tiretow
            var letter = new Letter
            {
                ContentText = "obejmuje następujące elementy:"
            };
            ParsingFactories.SetContentAndSegments(letter, letter.ContentText);

            // Act
            ParsingFactories.AttachIntroCommonPart(letter);

            // Assert
            Assert.Single(letter.CommonParts);
            var intro = letter.CommonParts[0];
            Assert.Equal(CommonPartType.Intro, intro.Type);
            Assert.Same(letter, intro.Parent);
        }

        [Fact]
        public void AttachIntro_SetsCorrectParentEId()
        {
            // Arrange: ustep z ustawionym rodzicem (artykulem) dla poprawnego eId
            var article = new Article { Number = new EntityNumber { Value = "10" } };
            var paragraph = new Paragraph
            {
                Parent = article,
                Article = article,
                Number = new EntityNumber { Value = "2" },
                ContentText = "W ustawie wprowadza się zmiany:"
            };
            ParsingFactories.SetContentAndSegments(paragraph, paragraph.ContentText);

            // Act
            ParsingFactories.AttachIntroCommonPart(paragraph);

            // Assert
            var intro = paragraph.CommonParts[0];
            Assert.Equal("art_10__ust_2", intro.ParentEId);
            // eId intro powinien byc art_10__ust_2__intro
            Assert.Equal("art_10__ust_2__intro", intro.Id);
        }

        [Fact]
        public void AttachWrapUp_StripsTiretPrefixAndAddsCommonPart()
        {
            // Arrange: ustep z rodzicem dla poprawnego eId
            var article = new Article { Number = new EntityNumber { Value = "293" } };
            var paragraph = new Paragraph
            {
                Parent = article,
                Article = article,
                Number = new EntityNumber { Value = "1" }
            };

            // Act
            var added = ParsingFactories.AttachWrapUpCommonPart(paragraph, "- zachowuja dopuszczenie.");

            // Assert
            Assert.True(added);
            Assert.Single(paragraph.CommonParts);
            var wrapUp = paragraph.CommonParts[0];
            Assert.Equal(CommonPartType.WrapUp, wrapUp.Type);
            Assert.Equal("zachowuja dopuszczenie.", wrapUp.ContentText);
            Assert.Equal("art_293__ust_1__wrapUp", wrapUp.Id);
        }
    }
}
