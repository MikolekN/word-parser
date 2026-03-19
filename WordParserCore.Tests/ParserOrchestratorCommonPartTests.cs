using ModelDto;
using ModelDto.SystematizingUnits;
using WordParserCore.Services.Parsing;
using Xunit;
using DocumentFormat.OpenXml.Wordprocessing;
using DocumentFormat.OpenXml;
using System.Linq;

namespace WordParserCore.Tests
{
	public class ParserOrchestratorCommonPartTests
	{
		private ParsingContext CreateContext()
		{
			var document = new LegalDocument
			{
				Type = LegalActType.Statute,
				SourceJournal = new JournalInfo { Year = 2024, Positions = { 1 } }
			};
			var subchapter = new Subchapter();
			return new ParsingContext(document, subchapter);
		}

		private Paragraph CreateParagraph(string text, string? styleId = null)
		{
			var paragraph = new Paragraph();
			var run = new Run(new Text(text));
			paragraph.Append(run);

			if (styleId != null)
			{
				paragraph.ParagraphProperties = new ParagraphProperties
				{
					ParagraphStyleId = new ParagraphStyleId { Val = styleId }
				};
			}

			return paragraph;
		}

		[Fact]
		public void ProcessParagraph_WrapUpPointStyle_AttachesToParagraph()
		{
			var orchestrator = new ParserOrchestrator();
			var context = CreateContext();

			var article = CreateParagraph("Art. 1. Podreczniki dopuszczone do uzytku szkolnego do:",
				"ARTartustawynprozporzdzenia");
			var point1 = CreateParagraph("1) ksztalcenia w zawodzie,", "PKTpunkt");
			var point2 = CreateParagraph("2) ksztalcenia specjalnego,", "PKTpunkt");
			var point3 = CreateParagraph("3) ksztalcenia uczniow...", "PKTpunkt");
			var wrapUp = CreateParagraph("- zachowuja dopuszczenie do uzytku szkolnego.",
				"CZWSPPKTczwsplnapunktw");

			orchestrator.ProcessParagraph(article, context);
			orchestrator.ProcessParagraph(point1, context);
			orchestrator.ProcessParagraph(point2, context);
			orchestrator.ProcessParagraph(point3, context);
			orchestrator.ProcessParagraph(wrapUp, context);

			var paragraph = context.CurrentParagraph;
			Assert.NotNull(paragraph);

			var wrapUpPart = paragraph.CommonParts.Single(cp => cp.Type == CommonPartType.WrapUp);
			Assert.Equal("zachowuja dopuszczenie do uzytku szkolnego.", wrapUpPart.ContentText);
		}
	}
}
