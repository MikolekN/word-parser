using System;
using ModelDto;
using ModelDto.EditorialUnits;
using WordParserCore.Helpers;
using WordParserCore.Services.Parsing;
using Xunit;

namespace WordParserCore.Tests
{
	public class AmendmentCollectorTests
	{
		// ============================================================
		// Stan poczatkowy
		// ============================================================

		[Fact]
		public void NewCollector_IsNotCollecting()
		{
			var collector = new AmendmentCollector();
			Assert.False(collector.IsCollecting);
			Assert.Equal(0, collector.Count);
			Assert.Null(collector.Owner);
			Assert.Null(collector.Target);
		}

		// ============================================================
		// Begin
		// ============================================================

		[Fact]
		public void Begin_SetsOwnerAndTarget()
		{
			var collector = new AmendmentCollector();
			var owner = CreateParagraph("1");
			var target = new StructuralAmendmentReference
			{
				Structure = new StructuralReference(),
				RawText = "w art. 5 ust. 2"
			};

			collector.Begin(owner, target);

			Assert.True(collector.IsCollecting);
			Assert.Same(owner, collector.Owner);
			Assert.Same(target, collector.Target);
			Assert.Equal(0, collector.Count);
		}

		[Fact]
		public void Begin_WithoutTarget_SetsOwnerOnly()
		{
			var collector = new AmendmentCollector();
			var owner = CreateParagraph("1");

			collector.Begin(owner);

			Assert.True(collector.IsCollecting);
			Assert.Same(owner, collector.Owner);
			Assert.Null(collector.Target);
		}

		[Fact]
		public void Begin_NullOwner_Throws()
		{
			var collector = new AmendmentCollector();
			Assert.Throws<ArgumentNullException>(() => collector.Begin(null!));
		}

		[Fact]
		public void Begin_WhileAlreadyCollecting_ResetsAndStartsNew()
		{
			var collector = new AmendmentCollector();
			var owner1 = CreateParagraph("1");
			var owner2 = CreateParagraph("2");

			collector.Begin(owner1);
			collector.AddParagraph("tresc 1", "ZUSTzmustartykuempunktem");

			// Begin z nowym ownerem — resetuje poprzedni stan
			collector.Begin(owner2);

			Assert.Same(owner2, collector.Owner);
			Assert.Equal(0, collector.Count);
		}

		// ============================================================
		// AddParagraph
		// ============================================================

		[Fact]
		public void AddParagraph_IncrementsCount()
		{
			var collector = new AmendmentCollector();
			var owner = CreateParagraph("1");
			collector.Begin(owner);

			collector.AddParagraph("1. Treść ustępu.", "ZUSTzmustartykuempunktem");
			collector.AddParagraph("1) punkt pierwszy;", "ZPKTzmpktartykuempunktem");

			Assert.Equal(2, collector.Count);
		}

		[Fact]
		public void AddParagraph_PreservesOrder()
		{
			var collector = new AmendmentCollector();
			collector.Begin(CreateParagraph("1"));

			collector.AddParagraph("akapit 1", "style1");
			collector.AddParagraph("akapit 2", "style2");
			collector.AddParagraph("akapit 3", null);

			Assert.Equal(3, collector.Paragraphs.Count);
			Assert.Equal("akapit 1", collector.Paragraphs[0].Text);
			Assert.Equal("akapit 2", collector.Paragraphs[1].Text);
			Assert.Equal("akapit 3", collector.Paragraphs[2].Text);
		}

		[Fact]
		public void AddParagraph_DecodesStyleInfo()
		{
			var collector = new AmendmentCollector();
			collector.Begin(CreateParagraph("1"));

			// Styl znany z mapy — powinien byc zdekodowany
			collector.AddParagraph("1. Treść.", "ZUSTzmustartykuempunktem");

			var para = collector.Paragraphs[0];
			Assert.NotNull(para.StyleInfo);
			Assert.Equal(AmendmentInstrument.ArticleOrPoint, para.StyleInfo.Instrument);
			Assert.Equal(AmendmentTargetKind.Paragraph, para.StyleInfo.TargetKind);
		}

		[Fact]
		public void AddParagraph_UnknownStyle_StyleInfoIsNull()
		{
			var collector = new AmendmentCollector();
			collector.Begin(CreateParagraph("1"));

			collector.AddParagraph("jakiś tekst", "NieznanyStyl123");

			Assert.Null(collector.Paragraphs[0].StyleInfo);
		}

		[Fact]
		public void AddParagraph_NullStyle_StyleInfoIsNull()
		{
			var collector = new AmendmentCollector();
			collector.Begin(CreateParagraph("1"));

			collector.AddParagraph("tekst bez stylu", null);

			Assert.Null(collector.Paragraphs[0].StyleInfo);
			Assert.Null(collector.Paragraphs[0].StyleId);
		}

		// ============================================================
		// Reset
		// ============================================================

		[Fact]
		public void Reset_ClearsEverything()
		{
			var collector = new AmendmentCollector();
			collector.Begin(CreateParagraph("1"), new StructuralAmendmentReference
			{
				Structure = new StructuralReference(),
				RawText = "cel"
			});
			collector.AddParagraph("a", "s1");
			collector.AddParagraph("b", "s2");

			collector.Reset();

			Assert.False(collector.IsCollecting);
			Assert.Equal(0, collector.Count);
			Assert.Null(collector.Owner);
			Assert.Null(collector.Target);
			Assert.Empty(collector.Paragraphs);
		}

		[Fact]
		public void Reset_OnEmptyCollector_NoCrash()
		{
			var collector = new AmendmentCollector();
			collector.Reset(); // nie powinno rzucic wyjatku
			Assert.False(collector.IsCollecting);
		}

		// ============================================================
		// IsCollecting — rozne scenariusze
		// ============================================================

		[Fact]
		public void IsCollecting_TrueWhenOwnerSet_EvenWithoutParagraphs()
		{
			var collector = new AmendmentCollector();
			collector.Begin(CreateParagraph("1"));

			Assert.True(collector.IsCollecting);
			Assert.Equal(0, collector.Count);
		}

		// ============================================================
		// Helpers
		// ============================================================

		private static Paragraph CreateParagraph(string number)
		{
			return new Paragraph
			{
				Number = new EntityNumber { Value = number },
				ContentText = $"Treść ustępu {number}."
			};
		}
	}
}
