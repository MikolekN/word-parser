using System;
using Saga.Model;
using Saga.Core.Ingest;
using Saga.Core.Services.Parsing;
using Xunit;

namespace Saga.Core.Tests
{
	/// <summary>
	/// Testy zbierania metadanych aktu ze strefy tytułowej (Etap 7b, § 16-19 ZTP): rodzaj/data/przedmiot →
	/// <see cref="LegalDocument.Title"/> / <see cref="LegalDocument.ActDate"/>. Sprawdza wierność brzmienia
	/// (bez przeinaczeń), wydobycie daty, ignorowanie prozy sprzed nagłówka i zapieczętowanie na artykule.
	/// </summary>
	public class DocumentMetadataCollectorTests
	{
		// ============================================================
		// Bezpośrednio na kolektorze
		// ============================================================

		[Fact]
		public void Collects_KindDateSubject_IntoVerbatimTitleAndStructuredDate()
		{
			var c = new DocumentMetadataCollector();
			Assert.True(c.Observe("USTAWA"));
			Assert.True(c.Observe("z dnia 1 marca 2024 r."));
			Assert.True(c.Observe("o ochronie konkurencji i konsumentów"));

			var doc = new LegalDocument();
			c.ApplyTo(doc);

			Assert.Equal("USTAWA z dnia 1 marca 2024 r. o ochronie konkurencji i konsumentów", doc.Title);
			Assert.Equal(new DateTime(2024, 3, 1), doc.ActDate);
		}

		[Fact]
		public void ProseBeforeKindHeader_IsIgnored()
		{
			var c = new DocumentMetadataCollector();
			Assert.False(c.Observe("Dziennik Ustaw Rzeczypospolitej Polskiej"));
			Assert.False(c.Observe("o czymś tam")); // przedmiot bez wcześniejszego nagłówka rodzaju — pomijany
			Assert.Equal(0, c.CollectedLineCount);

			var doc = new LegalDocument();
			c.ApplyTo(doc);
			Assert.Equal(string.Empty, doc.Title);
			Assert.Null(doc.ActDate);
		}

		[Fact]
		public void AfterSeal_ObserveReturnsFalse()
		{
			var c = new DocumentMetadataCollector();
			Assert.True(c.Observe("USTAWA"));
			c.Seal();
			Assert.False(c.Observe("z dnia 1 marca 2024 r."));
		}

		[Fact]
		public void InvalidCalendarDate_KeepsLineInTitle_ButLeavesActDateNull()
		{
			var c = new DocumentMetadataCollector();
			c.Observe("USTAWA");
			Assert.True(c.Observe("z dnia 31 lutego 2024 r.")); // wiersz-daty rozpoznany, ale 31 lutego nie istnieje
			c.Observe("o rzeczy niemożliwej");

			var doc = new LegalDocument();
			c.ApplyTo(doc);
			Assert.Contains("z dnia 31 lutego 2024 r.", doc.Title);
			Assert.Null(doc.ActDate);
		}

		[Fact]
		public void ApplyTo_DoesNotOverwriteExistingTitleOrDate()
		{
			var c = new DocumentMetadataCollector();
			c.Observe("USTAWA");
			c.Observe("z dnia 1 marca 2024 r.");

			var doc = new LegalDocument { Title = "Wcześniejszy tytuł", ActDate = new DateTime(2000, 1, 1) };
			c.ApplyTo(doc);

			Assert.Equal("Wcześniejszy tytuł", doc.Title);
			Assert.Equal(new DateTime(2000, 1, 1), doc.ActDate);
		}

		// ============================================================
		// Integracja przez orkiestrator (strefa tytułowa przed pierwszym artykułem)
		// ============================================================

		private static (ParserOrchestrator orch, ParsingContext ctx) NewPipeline()
		{
			var document = new LegalDocument { Type = LegalActType.Statute };
			var subchapter = document.RootPart.Books[0].Titles[0].Divisions[0].Chapters[0].Subchapters[0];
			return (new ParserOrchestrator(), new ParsingContext(document, subchapter));
		}

		private static DocumentBlock Blk(string text) => new()
		{
			Text = text,
			Source = new BlockSourceLocation { BlockIndex = null },
		};

		[Fact]
		public void Pipeline_TitleZoneBeforeFirstArticle_PopulatesDocumentMetadata()
		{
			var (orch, ctx) = NewPipeline();

			orch.ProcessBlock(Blk("USTAWA"), ctx);
			orch.ProcessBlock(Blk("z dnia 6 września 2001 r."), ctx);
			orch.ProcessBlock(Blk("o dostępie do informacji publicznej"), ctx);
			orch.ProcessBlock(Blk("Art. 1. Informacja publiczna podlega udostępnieniu."), ctx);
			orch.Finalize(ctx);

			Assert.Equal("USTAWA z dnia 6 września 2001 r. o dostępie do informacji publicznej", ctx.Document.Title);
			Assert.Equal(new DateTime(2001, 9, 6), ctx.Document.ActDate);
			Assert.Single(ctx.Document.Articles);
		}

		[Fact]
		public void Pipeline_NoTitleZone_LeavesMetadataEmpty()
		{
			var (orch, ctx) = NewPipeline();

			orch.ProcessBlock(Blk("Art. 1. Sama treść, bez strefy tytułowej."), ctx);
			orch.Finalize(ctx);

			Assert.Equal(string.Empty, ctx.Document.Title);
			Assert.Null(ctx.Document.ActDate);
		}

		[Fact]
		public void Pipeline_KindHeaderAfterFirstArticle_IsNotCollected()
		{
			// Po pierwszym artykule kolektor jest zapieczętowany — „USTAWA" w treści nie tworzy tytułu.
			var (orch, ctx) = NewPipeline();

			orch.ProcessBlock(Blk("Art. 1. Treść artykułu."), ctx);
			orch.ProcessBlock(Blk("USTAWA"), ctx);
			orch.ProcessBlock(Blk("z dnia 1 marca 2024 r."), ctx);
			orch.Finalize(ctx);

			Assert.Equal(string.Empty, ctx.Document.Title);
			Assert.Null(ctx.Document.ActDate);
		}

		[Fact]
		public void Pipeline_SectionBasedActWithoutArticle_DoesNotGlueBodyProseIntoTitle()
		{
			// Regresja: akt oparty na „§" nie tworzy artykułu (sygnał Seal ze StructureProcessora nie nadchodzi),
			// więc kolektor domyka strefę tytułową sam — pierwszy wiersz spoza niej („§ 1. …") ją kończy,
			// a późniejsza proza pasująca do wzorca przedmiotu („o ile …") NIE trafia do tytułu.
			var (orch, ctx) = NewPipeline();

			orch.ProcessBlock(Blk("ROZPORZĄDZENIE"), ctx);
			orch.ProcessBlock(Blk("z dnia 1 marca 2024 r."), ctx);
			orch.ProcessBlock(Blk("§ 1. Przepis ogólny."), ctx);
			orch.ProcessBlock(Blk("o ile przepisy odrębne nie stanowią inaczej."), ctx);
			orch.Finalize(ctx);

			Assert.Equal("ROZPORZĄDZENIE z dnia 1 marca 2024 r.", ctx.Document.Title);
			Assert.DoesNotContain("o ile", ctx.Document.Title);
			Assert.Empty(ctx.Document.Articles);
		}

		[Fact]
		public void Collects_IssuingOrganOnSeparateLine()
		{
			// § 120 ust. 4: organ wydający rozporządzenie w osobnym wierszu jest częścią tytułu (nie gubić).
			var c = new DocumentMetadataCollector();
			c.Observe("ROZPORZĄDZENIE");
			Assert.True(c.Observe("MINISTRA ZDROWIA"));
			c.Observe("z dnia 5 stycznia 2020 r.");
			c.Observe("w sprawie badań lekarskich");

			var doc = new LegalDocument();
			c.ApplyTo(doc);

			Assert.Equal("ROZPORZĄDZENIE MINISTRA ZDROWIA z dnia 5 stycznia 2020 r. w sprawie badań lekarskich", doc.Title);
			Assert.Equal(new DateTime(2020, 1, 5), doc.ActDate);
		}

		[Fact]
		public void SecondKindHeader_SealsWithoutMerging()
		{
			// Tekst jednolity: „USTAWA" w załączniku nie może zostać doklejona do tytułu obwieszczenia
			// ani nadpisać jego daty datą aktu ogłaszanego.
			var c = new DocumentMetadataCollector();
			c.Observe("OBWIESZCZENIE MARSZAŁKA SEJMU RZECZYPOSPOLITEJ POLSKIEJ");
			c.Observe("z dnia 5 stycznia 2020 r.");
			Assert.False(c.Observe("USTAWA"));
			Assert.False(c.Observe("z dnia 20 lipca 2000 r."));

			var doc = new LegalDocument();
			c.ApplyTo(doc);

			Assert.DoesNotContain("USTAWA", doc.Title);
			Assert.DoesNotContain("20 lipca 2000", doc.Title);
			Assert.Equal(new DateTime(2020, 1, 5), doc.ActDate);
		}
	}
}
