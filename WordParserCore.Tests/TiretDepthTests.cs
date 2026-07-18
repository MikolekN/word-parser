using System.Linq;
using ModelDto;
using WordParserCore.Ingest;
using WordParserCore.Services.Parsing;
using Xunit;

namespace WordParserCore.Tests
{
	/// <summary>
	/// Testy głębokości tiretu (Etap 7a, § 58 ZTP): priorytet stylu 2TIR/3TIR, wnioskowanie zagnieżdżenia
	/// z wcięcia lewego bloku dla dokumentów bezstylowych z układem oraz domyślny poziom 1 bez sygnału (TXT).
	/// </summary>
	public class TiretDepthTests
	{
		private static ParsingContext ConnectedContext()
		{
			var document = new LegalDocument { Type = LegalActType.Statute };
			var subchapter = document.RootPart.Books[0].Titles[0].Divisions[0].Chapters[0].Subchapters[0];
			return new ParsingContext(document, subchapter);
		}

		private static DocumentBlock Blk(string text, string? styleId = null, int? indentTwips = null) => new()
		{
			Text = text,
			StyleId = styleId,
			Layout = (styleId == null && indentTwips == null) ? null
				: new BlockLayoutInfo { LeftIndentTwips = indentTwips },
			Source = new BlockSourceLocation { BlockIndex = null },
		};

		private static void Feed(ParserOrchestrator orch, ParsingContext ctx, params DocumentBlock[] blocks)
		{
			foreach (var b in blocks)
				orch.ProcessBlock(b, ctx);
		}

		private static void FeedArticleLetterScaffold(ParserOrchestrator orch, ParsingContext ctx)
		{
			Feed(orch, ctx,
				Blk("Art. 1. Tresc wprowadzajaca."),
				Blk("1) punkt pierwszy:"),
				Blk("a) litera a:"));
		}

		[Fact]
		public void IndentInference_NestsDeeperTiret_AndReturnsToShallowerSibling()
		{
			var orch = new ParserOrchestrator();
			var ctx = ConnectedContext();
			FeedArticleLetterScaffold(orch, ctx);

			// Wcięcia: 1000 (poziom 1), 1000 (rodzeństwo), 1400 (głębiej → poziom 2), 1000 (powrót do poziomu 1).
			Feed(orch, ctx,
				Blk("– tiret pierwszy,", indentTwips: 1000),
				Blk("– tiret drugi,", indentTwips: 1000),
				Blk("– tiret glebszy,", indentTwips: 1400),
				Blk("– tiret powrotny,", indentTwips: 1000));

			var letter = ctx.CurrentLetter!;
			Assert.Equal(3, letter.Tirets.Count);            // tir1, tir2, tir4 na poziomie 1
			Assert.Empty(letter.Tirets[0].Tirets);
			Assert.Single(letter.Tirets[1].Tirets);          // tir3 zagnieżdżony pod tir2
			Assert.Empty(letter.Tirets[2].Tirets);
		}

		[Fact]
		public void IndentInferredNesting_AttachesInfoValidationMessage()
		{
			var orch = new ParserOrchestrator();
			var ctx = ConnectedContext();
			FeedArticleLetterScaffold(orch, ctx);

			Feed(orch, ctx,
				Blk("– tiret pierwszy,", indentTwips: 1000),
				Blk("– tiret glebszy,", indentTwips: 1400));

			var nested = ctx.CurrentLetter!.Tirets[0].Tirets.Single();
			Assert.Contains(nested.ValidationMessages,
				m => m.Level == ValidationLevel.Info && m.Message.Contains("wciecia"));
		}

		[Fact]
		public void NoLayout_AllTiretsStayFlatAtDepthOne()
		{
			// Czysty tekst (brak układu) — zachowanie dotychczasowe: wszystkie tirety na poziomie 1.
			var orch = new ParserOrchestrator();
			var ctx = ConnectedContext();
			FeedArticleLetterScaffold(orch, ctx);

			Feed(orch, ctx,
				Blk("– tiret pierwszy,"),
				Blk("– tiret drugi,"),
				Blk("– tiret trzeci,"));

			var letter = ctx.CurrentLetter!;
			Assert.Equal(3, letter.Tirets.Count);
			Assert.All(letter.Tirets, t => Assert.Empty(t.Tirets));
		}

		[Fact]
		public void StyleDepth_WinsOverContradictingIndent()
		{
			// Styl 2TIR zagnieżdża mimo MNIEJSZEGO wcięcia — styl ma priorytet (dokumenty szablonowe).
			var orch = new ParserOrchestrator();
			var ctx = ConnectedContext();
			FeedArticleLetterScaffold(orch, ctx);

			Feed(orch, ctx,
				Blk("– tiret poziomu 1,", styleId: "TIR", indentTwips: 2000),
				Blk("– tiret poziomu 2,", styleId: "2TIR", indentTwips: 100));

			var letter = ctx.CurrentLetter!;
			Assert.Single(letter.Tirets);
			var nested = letter.Tirets[0].Tirets.Single();
			// Zagnieżdżenie ze STYLU nie dopisuje diagnostyki „z wciecia".
			Assert.DoesNotContain(nested.ValidationMessages,
				m => m.Level == ValidationLevel.Info && m.Message.Contains("wciecia"));
		}

		[Fact]
		public void StyleDepthGreaterThanOne_WithoutParent_DegradesToLevel1_WithWarning()
		{
			// Straznik: 2TIR jako pierwszy tiret (pusty stos) — brak rodzica; umieszczenie na poziomie 1 ze śladem.
			var orch = new ParserOrchestrator();
			var ctx = ConnectedContext();
			FeedArticleLetterScaffold(orch, ctx);

			Feed(orch, ctx, Blk("– tiret bez rodzica,", styleId: "2TIR", indentTwips: 100));

			var tiret = Assert.Single(ctx.CurrentLetter!.Tirets);
			Assert.Empty(tiret.Tirets);
			Assert.Contains(tiret.ValidationMessages,
				m => m.Level == ValidationLevel.Warning && m.Message.Contains("brak tiretu nadrzednego"));
		}
	}
}
