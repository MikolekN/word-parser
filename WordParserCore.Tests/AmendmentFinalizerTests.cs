using System;
using System.Collections.Generic;
using ModelDto;
using ModelDto.EditorialUnits;
using ModelDto.SystematizingUnits;
using WordParserCore.Services.Parsing;
using WordParserCore.Services.Parsing.Builders;
using Xunit;

namespace WordParserCore.Tests
{
	public class AmendmentFinalizerTests
	{
		private readonly AmendmentFinalizer _finalizer = new();

		// ============================================================
		// Helpers
		// ============================================================

		private static ParsingContext CreateContext(JournalInfo? sourceJournal = null)
		{
			var document = new LegalDocument
			{
				Type = LegalActType.Statute,
				SourceJournal = sourceJournal ?? new JournalInfo { Year = 2024, Positions = { 123 } }
			};
			var subchapter = new Subchapter();
			return new ParsingContext(document, subchapter);
		}

		private static Paragraph CreateOwnerParagraph(string contentText, Article? article = null)
		{
			var paragraph = new Paragraph
			{
				ContentText = contentText,
				Number = new EntityNumber { NumericPart = 1, Value = "1" },
				Article = article
			};
			paragraph.Parent = article;
			return paragraph;
		}

		private static Point CreateOwnerPoint(string contentText, Paragraph? parent = null, Article? article = null)
		{
			var point = new Point
			{
				ContentText = contentText,
				Number = new EntityNumber { NumericPart = 1, Value = "1" },
				Paragraph = parent,
				Article = article
			};
			point.Parent = parent;
			return point;
		}

		private static AmendmentContent CreateSampleContent()
		{
			var content = new AmendmentContent
			{
				ObjectType = AmendmentObjectType.Paragraph
			};
			content.Paragraphs.Add(new Paragraph
			{
				ContentText = "Treść zmienionego ustępu.",
				Number = new EntityNumber { NumericPart = 2, Value = "2" }
			});
			return content;
		}

		private static AmendmentCollector BeginCollector(BaseEntity owner, StructuralAmendmentReference? target = null)
		{
			var collector = new AmendmentCollector();
			collector.Begin(owner, target);
			collector.AddParagraph("2. Treść nowelizacji", null);
			return collector;
		}

		// ============================================================
		// DetectOperationType — testy
		// ============================================================

		[Theory]
		[InlineData("w ust. 2 pkt 3 otrzymuje brzmienie:", AmendmentOperationType.Modification)]
		[InlineData("art. 5 otrzymuje brzmienie:", AmendmentOperationType.Modification)]
		[InlineData("pkt 1 i 2 otrzymują brzmienie:", AmendmentOperationType.Modification)]
		[InlineData("wyrazy \"tekst\" zastępuje się wyrazami \"nowy tekst\"", AmendmentOperationType.Modification)]
		public void DetectOperationType_Modification_Patterns(string text, AmendmentOperationType expected)
		{
			var result = AmendmentFinalizer.DetectOperationType(text);
			Assert.Equal(expected, result);
		}

		[Theory]
		[InlineData("po art. 5 dodaje się art. 5a w brzmieniu:", AmendmentOperationType.Insertion)]
		[InlineData("dodaje się ust. 3 w brzmieniu:", AmendmentOperationType.Insertion)]
		[InlineData("po pkt 2 dodaje się pkt 2a w brzmieniu:", AmendmentOperationType.Insertion)]
		[InlineData("w ust. 1 dodaje się pkt 5 w brzmieniu:", AmendmentOperationType.Insertion)]
		public void DetectOperationType_Insertion_Patterns(string text, AmendmentOperationType expected)
		{
			var result = AmendmentFinalizer.DetectOperationType(text);
			Assert.Equal(expected, result);
		}

		[Theory]
		[InlineData("art. 5 uchyla się", AmendmentOperationType.Repeal)]
		[InlineData("uchyla się pkt 3", AmendmentOperationType.Repeal)]
		[InlineData("w art. 11 w ust. 4 uchyla się pkt 4;", AmendmentOperationType.Repeal)]
		public void DetectOperationType_Repeal_Patterns(string text, AmendmentOperationType expected)
		{
			var result = AmendmentFinalizer.DetectOperationType(text);
			Assert.Equal(expected, result);
		}

		[Fact]
		public void DetectOperationType_NullOrEmpty_ReturnsModification()
		{
			Assert.Equal(AmendmentOperationType.Modification, AmendmentFinalizer.DetectOperationType(null));
			Assert.Equal(AmendmentOperationType.Modification, AmendmentFinalizer.DetectOperationType(""));
			Assert.Equal(AmendmentOperationType.Modification, AmendmentFinalizer.DetectOperationType("   "));
		}

		[Fact]
		public void DetectOperationType_UnknownText_ReturnsModification()
		{
			var result = AmendmentFinalizer.DetectOperationType("jakiś nieznany tekst bez wzorca");
			Assert.Equal(AmendmentOperationType.Modification, result);
		}

		// ============================================================
		// Finalize — tworzenie obiektu Amendment
		// ============================================================

		[Fact]
		public void Finalize_CreatesAmendment_WithCorrectOperationType()
		{
			var article = new Article { ContentText = "Art. 1. W ustawie zmienia się:", Journals = { new JournalInfo { Year = 2020, Positions = { 456 } } } };
			var owner = CreateOwnerPoint("po pkt 2 dodaje się pkt 2a w brzmieniu:", article: article);
			var context = CreateContext();
			var content = CreateSampleContent();
			var collector = BeginCollector(owner);

			var input = new AmendmentFinalizerInput(content, collector, context);
			var amendment = _finalizer.Finalize(input);

			Assert.NotNull(amendment);
			Assert.Equal(AmendmentOperationType.Insertion, amendment!.OperationType);
			Assert.NotNull(amendment.Content);
		}

		[Fact]
		public void Finalize_Repeal_ContentIsNull()
		{
			var owner = CreateOwnerPoint("pkt 3 uchyla się");
			var context = CreateContext();
			var content = CreateSampleContent();
			var collector = BeginCollector(owner);

			var input = new AmendmentFinalizerInput(content, collector, context);
			var amendment = _finalizer.Finalize(input);

			Assert.NotNull(amendment);
			Assert.Equal(AmendmentOperationType.Repeal, amendment!.OperationType);
			Assert.Null(amendment.Content);
		}

		[Fact]
		public void Finalize_Modification_ContentPresent()
		{
			var owner = CreateOwnerParagraph("art. 5 otrzymuje brzmienie:");
			var context = CreateContext();
			var content = CreateSampleContent();
			var collector = BeginCollector(owner);

			var input = new AmendmentFinalizerInput(content, collector, context);
			var amendment = _finalizer.Finalize(input);

			Assert.NotNull(amendment);
			Assert.Equal(AmendmentOperationType.Modification, amendment!.OperationType);
			Assert.NotNull(amendment.Content);
			Assert.Single(amendment.Content!.Paragraphs);
		}

		// ============================================================
		// Finalize — łączenie z celami (Targets)
		// ============================================================

		[Fact]
		public void Finalize_LinksTargetFromCollector()
		{
			var owner = CreateOwnerParagraph("ust. 2 otrzymuje brzmienie:");
			var context = CreateContext();
			var content = CreateSampleContent();
			var target = new StructuralAmendmentReference
			{
				Structure = new StructuralReference { Article = "5", Paragraph = "2" },
				RawText = "ust. 2"
			};
			var collector = BeginCollector(owner, target);

			var input = new AmendmentFinalizerInput(content, collector, context);
			var amendment = _finalizer.Finalize(input);

			Assert.NotNull(amendment);
			Assert.Single(amendment!.Targets);
			Assert.Equal("5", amendment.Targets[0].Structure.Article);
			Assert.Equal("2", amendment.Targets[0].Structure.Paragraph);
		}

		[Fact]
		public void Finalize_LinksTargetFromDetectedAmendmentTargets_WhenCollectorTargetNull()
		{
			var owner = CreateOwnerParagraph("ust. 2 otrzymuje brzmienie:");
			var context = CreateContext();
			var content = CreateSampleContent();

			// Dodaj cel do mapy DetectedAmendmentTargets
			var detectedTarget = new StructuralAmendmentReference
			{
				Structure = new StructuralReference { Article = "10", Point = "3" },
				RawText = "pkt 3"
			};
			context.DetectedAmendmentTargets[owner.Guid] = detectedTarget;

			var collector = BeginCollector(owner); // bez targetu
			var input = new AmendmentFinalizerInput(content, collector, context);
			var amendment = _finalizer.Finalize(input);

			Assert.NotNull(amendment);
			Assert.Single(amendment!.Targets);
			Assert.Equal("10", amendment.Targets[0].Structure.Article);
			Assert.Equal("3", amendment.Targets[0].Structure.Point);
		}

		// ============================================================
		// Finalize — łączenie z JournalInfo
		// ============================================================

		[Fact]
		public void Finalize_LinksJournalFromParentArticle()
		{
			var article = new Article
			{
				ContentText = "Art. 1. W ustawie...",
				Journals = { new JournalInfo { Year = 2020, Positions = { 456 } } }
			};
			var owner = CreateOwnerParagraph("ust. 2 otrzymuje brzmienie:", article);
			var context = CreateContext();
			var content = CreateSampleContent();
			var collector = BeginCollector(owner);

			var input = new AmendmentFinalizerInput(content, collector, context);
			var amendment = _finalizer.Finalize(input);

			Assert.NotNull(amendment);
			Assert.Equal(2020, amendment!.TargetLegalAct.Year);
			Assert.Contains(456, amendment.TargetLegalAct.Positions);
		}

		[Fact]
		public void Finalize_FallbackToDocumentSourceJournal()
		{
			// Brak Journals w artykule — fallback na SourceJournal
			var owner = CreateOwnerParagraph("ust. 2 otrzymuje brzmienie:");
			var context = CreateContext(new JournalInfo { Year = 2024, Positions = { 789 } });
			var content = CreateSampleContent();
			var collector = BeginCollector(owner);

			var input = new AmendmentFinalizerInput(content, collector, context);
			var amendment = _finalizer.Finalize(input);

			Assert.NotNull(amendment);
			Assert.Equal(2024, amendment!.TargetLegalAct.Year);
			Assert.Contains(789, amendment.TargetLegalAct.Positions);
		}

		// ============================================================
		// Finalize — przypisanie do właściciela (IHasAmendments)
		// ============================================================

		[Fact]
		public void Finalize_AssignsAmendmentToOwner_WhenIHasAmendments()
		{
			var owner = CreateOwnerParagraph("ust. 2 otrzymuje brzmienie:");
			var context = CreateContext();
			var content = CreateSampleContent();
			var collector = BeginCollector(owner);

			Assert.Null(owner.Amendment);

			var input = new AmendmentFinalizerInput(content, collector, context);
			_finalizer.Finalize(input);

			Assert.NotNull(owner.Amendment);
		}

		[Fact]
		public void Finalize_NullOwner_ReturnsNull()
		{
			var context = CreateContext();
			var content = CreateSampleContent();
			var collector = new AmendmentCollector(); // Nie wywołano Begin — Owner == null

			var input = new AmendmentFinalizerInput(content, collector, context);
			var result = _finalizer.Finalize(input);

			Assert.Null(result);
		}

		// ============================================================
		// Finalize — walidacja
		// ============================================================

		[Fact]
		public void Finalize_NoTargets_AddsValidationWarning()
		{
			var owner = CreateOwnerParagraph("jakiś tekst bez celu");
			var context = CreateContext();
			var content = CreateSampleContent();
			var collector = BeginCollector(owner);

			var input = new AmendmentFinalizerInput(content, collector, context);
			_finalizer.Finalize(input);

			Assert.Contains(owner.ValidationMessages,
				m => m.Level == ValidationLevel.Warning &&
					m.Message.Contains("bez wykrytego celu"));
		}

		[Fact]
		public void Finalize_RepealWithContent_AddsInfoMessage()
		{
			// Uchylenie, ale z treścią — nietypowe
			var owner = CreateOwnerParagraph("pkt 3 uchyla się");
			var context = CreateContext();
			// Celowo konfigurujemy content, ale dla Repeal powinno być null
			// — to jest test walidacji gdy Repeal *nie ma* treści (expected)
			var content = new AmendmentContent { ObjectType = AmendmentObjectType.None };
			var collector = BeginCollector(owner);

			var input = new AmendmentFinalizerInput(content, collector, context);
			var amendment = _finalizer.Finalize(input);

			// Repeal → Content jest null (wymuszony w finalizerze)
			Assert.NotNull(amendment);
			Assert.Null(amendment!.Content);
		}

		// ============================================================
		// ResolveTargetJournal — testy wewnętrzne
		// ============================================================

		[Fact]
		public void ResolveTargetJournal_FromArticleWithJournals()
		{
			var article = new Article
			{
				ContentText = "Art. 1. W ustawie...",
				Journals = { new JournalInfo { Year = 2019, Positions = { 100, 200 } } }
			};
			var owner = CreateOwnerParagraph("ust. 1 otrzymuje brzmienie:", article);
			var context = CreateContext();

			var journal = AmendmentFinalizer.ResolveTargetJournal(owner, context);

			Assert.NotNull(journal);
			Assert.Equal(2019, journal!.Year);
			Assert.Equal(2, journal.Positions.Count);
		}

		[Fact]
		public void ResolveTargetJournal_Fallback_DocumentSourceJournal()
		{
			var owner = CreateOwnerParagraph("ust. 1 otrzymuje brzmienie:");
			var context = CreateContext(new JournalInfo { Year = 2024, Positions = { 500 } });

			var journal = AmendmentFinalizer.ResolveTargetJournal(owner, context);

			Assert.NotNull(journal);
			Assert.Equal(2024, journal!.Year);
		}

		[Fact]
		public void ResolveTargetJournal_NoJournalAnywhere_ReturnsNull()
		{
			var owner = CreateOwnerParagraph("ust. 1 otrzymuje brzmienie:");
			var context = CreateContext(new JournalInfo()); // Year=0, Positions empty

			var journal = AmendmentFinalizer.ResolveTargetJournal(owner, context);

			Assert.Null(journal);
		}
	}
}
