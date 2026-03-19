using ModelDto;
using ModelDto.EditorialUnits;
using ModelDto.SystematizingUnits;
using Xunit;

namespace WordParserCore.Tests
{
    public class EIdTests
    {
        [Fact]
        public void EId_SkipsImplicitSystematizingUnits()
        {
            var part = new Part { IsImplicit = true };
            var book = new Book { IsImplicit = true, Parent = part };
            var title = new Title { IsImplicit = true, Parent = book };
            var division = new Division { IsImplicit = true, Parent = title };
            var chapter = new Chapter { IsImplicit = true, Parent = division };
            var subchapter = new Subchapter { IsImplicit = true, Parent = chapter };

            var article = new Article
            {
                Parent = subchapter,
                Number = new EntityNumber { Value = "5", NumericPart = 5 }
            };

            var paragraph = new Paragraph
            {
                Parent = article,
                Article = article,
                Number = new EntityNumber { Value = "1", NumericPart = 1 }
            };

            Assert.Equal("art_5__ust_1", paragraph.Id);
        }
    }
}