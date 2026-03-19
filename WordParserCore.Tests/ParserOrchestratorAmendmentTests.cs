using ModelDto;
using ModelDto.SystematizingUnits;
using WordParserCore.Services.Parsing;
using Xunit;
using DocumentFormat.OpenXml.Wordprocessing;
using DocumentFormat.OpenXml;
using System.Linq;

namespace WordParserCore.Tests
{
	/// <summary>
	/// Testy dla orkiestratora parsowania w kontekście nowelizacji.
	/// Logika oparta na stylach: Z/... = nowelizacja,
	/// ART/UST/PKT/LIT/TIR = ustawa matka, brak stylu + trigger = nowelizacja.
	/// </summary>
	public class ParserOrchestratorAmendmentTests
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
		public void ProcessParagraph_WithAmendmentStyleZ_SetsInsideAmendment()
		{
			var orchestrator = new ParserOrchestrator();
			var context = CreateContext();
			// Realny styleId: Z/UST(§) => ZUSTzmustartykuempunktem
			var paragraph = CreateParagraph("2. Stosowanie wyłączenia określonego w ust. 1 nie może...", "ZUSTzmustartykuempunktem");

			orchestrator.ProcessParagraph(paragraph, context);

			Assert.True(context.InsideAmendment);
			Assert.Null(context.CurrentArticle); // Nie powinien utworzyć artykułu
		}

		[Fact]
		public void ProcessParagraph_TriggerPhrase_ProcessedNormally_TriggerSetAfter()
		{
			var orchestrator = new ParserOrchestrator();
			var context = CreateContext();
			var article = CreateParagraph("Art. 1. W ustawie wprowadza się zmiany:", "ART");
			var triggerParagraph = CreateParagraph("a) w ust. 2 pkt 3 otrzymuje brzmienie:", "LIT");

			orchestrator.ProcessParagraph(article, context);
			orchestrator.ProcessParagraph(triggerParagraph, context);

			// Akapit z triggerem przetworzony normalnie (litera a utworzona)
			Assert.False(context.InsideAmendment);
			Assert.True(context.AmendmentTriggerDetected);
			Assert.NotNull(context.CurrentArticle?.Paragraphs.FirstOrDefault()?.Points.FirstOrDefault()?.Letters.FirstOrDefault());
		}

		[Fact]
		public void ProcessParagraph_AfterTrigger_UnstyledParagraph_EntersAmendment()
		{
			var orchestrator = new ParserOrchestrator();
			var context = CreateContext();
			var article = CreateParagraph("Art. 1. Wprowadza się zmiany:", "ART");
			var trigger = CreateParagraph("1) art. 5 otrzymuje brzmienie:", "PKT");
			var amendmentContent = CreateParagraph("2. Stosowanie wyłączenia nie może...", null); // Bez stylu

			orchestrator.ProcessParagraph(article, context);
			orchestrator.ProcessParagraph(trigger, context);
			Assert.True(context.AmendmentTriggerDetected);
			Assert.False(context.InsideAmendment);

			orchestrator.ProcessParagraph(amendmentContent, context);

			Assert.True(context.InsideAmendment); // Brak stylu + trigger → nowelizacja
		}

		[Fact]
		public void ProcessParagraph_AfterTrigger_StyledParagraph_DoesNotEnterAmendment()
		{
			var orchestrator = new ParserOrchestrator();
			var context = CreateContext();
			var article = CreateParagraph("Art. 1. Wprowadza się zmiany:", "ART");
			var trigger = CreateParagraph("1) art. 5 otrzymuje brzmienie:", "PKT");
			var nextParentLaw = CreateParagraph("2) w art. 7 dodaje się ust. 3 w brzmieniu:", "PKT");

			orchestrator.ProcessParagraph(article, context);
			orchestrator.ProcessParagraph(trigger, context);
			Assert.True(context.AmendmentTriggerDetected);

			orchestrator.ProcessParagraph(nextParentLaw, context);

			// Styl PKT → ustawa matka, nie nowelizacja. Nowy trigger ustawiony.
			Assert.False(context.InsideAmendment);
			Assert.True(context.AmendmentTriggerDetected); // Nowy trigger z "w brzmieniu:"
			Assert.Equal("2", context.CurrentPoint?.Number?.Value);
		}

		[Fact]
		public void ProcessParagraph_InsideAmendment_StyledParagraph_ExitsAmendment()
		{
			var orchestrator = new ParserOrchestrator();
			var context = CreateContext();
			var article = CreateParagraph("Art. 1. Wprowadza się zmiany:", "ART");
			var trigger = CreateParagraph("1) art. 5 otrzymuje brzmienie:", "PKT");
			var amendmentContent1 = CreateParagraph("Treść nowelizacji", null);
			var amendmentContent2 = CreateParagraph("2. Dalszą treść nowelizacji", null);
			var parentLawResumes = CreateParagraph("2) w art. 7 dodaje się ust. 3:", "PKT");

			orchestrator.ProcessParagraph(article, context);
			orchestrator.ProcessParagraph(trigger, context);
			orchestrator.ProcessParagraph(amendmentContent1, context);
			Assert.True(context.InsideAmendment);

			orchestrator.ProcessParagraph(amendmentContent2, context);
			Assert.True(context.InsideAmendment); // Dalej w nowelizacji

			orchestrator.ProcessParagraph(parentLawResumes, context);

			// Styl PKT → wyjście z nowelizacji, przetworzenie punktu 2
			Assert.False(context.InsideAmendment);
			Assert.Equal("2", context.CurrentPoint?.Number?.Value);
		}

		[Fact]
		public void ProcessParagraph_InsideAmendment_UnstyledTriggerPoint_EndsAmendmentAndProcessesPoint()
		{
			var orchestrator = new ParserOrchestrator();
			var context = CreateContext();
			var article = CreateParagraph("Art. 2. W ustawie wprowadza sie zmiany:", "ART");
			var point6 = CreateParagraph("6) w art. 132e w ust. 1 pkt 4 otrzymuje brzmienie:", "PKT");
			var amendmentContent1 = CreateParagraph("1. Uczniowie szkol podstawowych i szkol ponadpodstawowych", null);
			var amendmentContent2 = CreateParagraph("4) zwrot oplat zwiazanych z nauka dzieci w szkole", null);
			var point7 = CreateParagraph("7)\tw art. 166 ust. 1 otrzymuje brzmienie:", "Normalny");

			orchestrator.ProcessParagraph(article, context);
			orchestrator.ProcessParagraph(point6, context);
			orchestrator.ProcessParagraph(amendmentContent1, context);
			Assert.True(context.InsideAmendment);

			orchestrator.ProcessParagraph(amendmentContent2, context);
			Assert.True(context.InsideAmendment);

			orchestrator.ProcessParagraph(point7, context);

			Assert.False(context.InsideAmendment);
			Assert.Equal("7", context.CurrentPoint?.Number?.Value);
		}

		[Fact]
		public void ProcessParagraph_LongAmendment_AllUnstyledSkipped()
		{
			// Symulacja: "po art. 5 dodaje się art. 5a-5g w brzmieniu:"
			// po czym następuje wiele akapitów treści nowelizacji bez stylu
			var orchestrator = new ParserOrchestrator();
			var context = CreateContext();
			var article = CreateParagraph("Art. 1. W ustawie wprowadza się zmiany:", "ART");
			var trigger = CreateParagraph("3) po art. 5 dodaje się art. 5a-5g w brzmieniu:", "PKT");

			orchestrator.ProcessParagraph(article, context);
			orchestrator.ProcessParagraph(trigger, context);

			// Wiele akapitów nowelizacji bez stylu
			for (int i = 0; i < 20; i++)
			{
				var content = CreateParagraph($"{i + 1}. Treść artykułu nowelizacji nr {i}", null);
				orchestrator.ProcessParagraph(content, context);
				Assert.True(context.InsideAmendment, $"Akapit {i} powinien być w nowelizacji");
			}

			// Powrót do ustawy matki ze stylem
			var nextPoint = CreateParagraph("4) po art. 7 dodaje się art. 7a w brzmieniu:", "PKT");
			orchestrator.ProcessParagraph(nextPoint, context);

			Assert.False(context.InsideAmendment);
			Assert.Equal("4", context.CurrentPoint?.Number?.Value);
		}

		[Fact]
		public void ProcessParagraph_ZStyleAmendment_ExitOnParentLawStyle()
		{
			var orchestrator = new ParserOrchestrator();
			var context = CreateContext();
			var article = CreateParagraph("Art. 1. Wprowadza się zmiany:", "ART");
			// Realne styleId: Z/ART(§) => ZARTzmartartykuempunktem, Z/UST(§) => ZUSTzmustartykuempunktem
			var zContent1 = CreateParagraph("Art. 5a. Treść", "ZARTzmartartykuempunktem");
			var zContent2 = CreateParagraph("1. Ustęp w nowelizacji", "ZUSTzmustartykuempunktem");
			var parentLaw = CreateParagraph("2) w art. 7:", "PKT");

			orchestrator.ProcessParagraph(article, context);
			orchestrator.ProcessParagraph(zContent1, context);
			Assert.True(context.InsideAmendment);

			orchestrator.ProcessParagraph(zContent2, context);
			Assert.True(context.InsideAmendment);

			orchestrator.ProcessParagraph(parentLaw, context);
			Assert.False(context.InsideAmendment);
			Assert.Equal("2", context.CurrentPoint?.Number?.Value);
		}

		[Fact]
		public void ProcessParagraph_WithInWording_SetsAmendmentTrigger()
		{
			var orchestrator = new ParserOrchestrator();
			var context = CreateContext();
			var article = CreateParagraph("Art. 1. Zmiany:", "ART");
			var trigger = CreateParagraph("2) w art. 11 dodaje się ust. 3 w brzmieniu:", "PKT");

			orchestrator.ProcessParagraph(article, context);
			orchestrator.ProcessParagraph(trigger, context);

			Assert.True(context.AmendmentTriggerDetected);
			Assert.False(context.InsideAmendment);
		}

		[Fact]
		public void ProcessParagraph_AmendmentTarget_InheritsArticleFromParentPoint()
		{
			// Scenariusz: pkt mowi "w art. 1:", lit mowi "w ust. 2 pkt 3 otrzymuje brzmienie:"
			// Cel nowelizacji lit_a powinien zawierac pelna sciezke: art. 1 | ust. 2 | pkt 3
			var orchestrator = new ParserOrchestrator();
			var context = CreateContext();

			var article = CreateParagraph("Art. 1. W ustawie z dnia 16 września 2011 r. wprowadza się zmiany:", "ART");
			var point = CreateParagraph("1) w art. 1:", "PKT");
			var letter = CreateParagraph("a) w ust. 2 pkt 3 otrzymuje brzmienie:", "LIT");

			orchestrator.ProcessParagraph(article, context);
			orchestrator.ProcessParagraph(point, context);
			orchestrator.ProcessParagraph(letter, context);

			var letterEntity = context.CurrentLetter;
			Assert.NotNull(letterEntity);

			Assert.True(context.DetectedAmendmentTargets.ContainsKey(letterEntity!.Guid),
				"Litera powinna mieć wykryty cel nowelizacji");

			var target = context.DetectedAmendmentTargets[letterEntity.Guid];

			Assert.Equal("1", target.Structure.Article);   // odziedziczone z pkt_1
			Assert.Equal("2", target.Structure.Paragraph); // z lit_a
			Assert.Equal("3", target.Structure.Point);     // z lit_a
		}

		[Fact]
		public void ProcessParagraph_AmendmentTarget_SiblingLetters_InheritIndependently()
		{
			// Scenariusz: pkt "w art. 1:", lit_a "ust. 2 pkt 3 otrzymuje brzmienie:",
			// lit_b "ust. 4 otrzymuje brzmienie:" — kazda litera dziedziczy art. 1 osobno
			var orchestrator = new ParserOrchestrator();
			var context = CreateContext();

			var article = CreateParagraph("Art. 1. W ustawie wprowadza się zmiany:", "ART");
			var point = CreateParagraph("1) w art. 1:", "PKT");
			var letterA = CreateParagraph("a) w ust. 2 pkt 3 otrzymuje brzmienie:", "LIT");
			// Symulacja zakonczenia nowelizacji (powrot do stylu ustawy matki)
			var letterB = CreateParagraph("b) ust. 4 otrzymuje brzmienie:", "LIT");

			orchestrator.ProcessParagraph(article, context);
			orchestrator.ProcessParagraph(point, context);
			orchestrator.ProcessParagraph(letterA, context);
			// AmendmentTrigger ustawiony; nastepny akapit bez stylu wejdzie w nowelizacje
			// Ale lit_b ma styl LIT — zamknie nowelizacje i przetworzy sie normalnie
			orchestrator.ProcessParagraph(letterB, context);

			var letterBEntity = context.CurrentLetter;
			Assert.NotNull(letterBEntity);

			Assert.True(context.DetectedAmendmentTargets.ContainsKey(letterBEntity!.Guid),
				"Litera B powinna mieć wykryty cel nowelizacji");

			var targetB = context.DetectedAmendmentTargets[letterBEntity.Guid];

			Assert.Equal("1", targetB.Structure.Article);   // odziedziczone z pkt_1
			Assert.Equal("4", targetB.Structure.Paragraph); // z lit_b (nie "2" z lit_a!)
			Assert.Null(targetB.Structure.Point);            // lit_b nie wspomina pkt
		}

		[Fact]
		public void ProcessParagraph_ImplicitParagraphWithAmendmentTarget_DetectsTarget()
		{
			var orchestrator = new ParserOrchestrator();
			var context = CreateContext();
			var article = CreateParagraph(
				"Art. 3. W ustawie z dnia 28 lipca 1983 r. w art. 3 pkt 10 otrzymuje brzmienie:",
				"ART");

			orchestrator.ProcessParagraph(article, context);

			var paragraph = context.CurrentParagraph;
			Assert.NotNull(paragraph);
			Assert.True(paragraph!.IsImplicit);
			Assert.True(context.DetectedAmendmentTargets.ContainsKey(paragraph.Guid),
				"Ustep niejawny powinien miec wykryty cel nowelizacji z tresci ogona artykulu");

			var target = context.DetectedAmendmentTargets[paragraph.Guid];
			Assert.Equal("3", target.Structure.Article);
			Assert.Equal("10", target.Structure.Point);
			Assert.Null(target.Structure.Paragraph);
			Assert.Null(target.Structure.Letter);
		}

		// ============================================================
		// Uchylenie (Repeal) — testy
		// ============================================================

		[Fact]
		public void ProcessParagraph_RepealTrigger_CreatesRepealAmendment()
		{
			var orchestrator = new ParserOrchestrator();
			var context = CreateContext();
			var article = CreateParagraph(
				"Art. 1. W ustawie z dnia 22 marca 2018 r. o komornikach wprowadza się zmiany:", "ART");
			var repealPoint = CreateParagraph("1) w art. 11 w ust. 4 uchyla się pkt 4;", "PKT");

			orchestrator.ProcessParagraph(article, context);
			orchestrator.ProcessParagraph(repealPoint, context);

			var point = context.CurrentPoint;
			Assert.NotNull(point);
			Assert.NotNull(point!.Amendment);
			Assert.Equal(AmendmentOperationType.Repeal, point.Amendment!.OperationType);
			Assert.Null(point.Amendment.Content);
			Assert.Single(point.Amendment.Targets);
			Assert.Equal("11", point.Amendment.Targets[0].Structure.Article);
			Assert.Equal("4", point.Amendment.Targets[0].Structure.Paragraph);
			Assert.Equal("4", point.Amendment.Targets[0].Structure.Point);
		}

		[Fact]
		public void ProcessParagraph_RepealTrigger_DoesNotSetAmendmentTriggerOrInsideAmendment()
		{
			var orchestrator = new ParserOrchestrator();
			var context = CreateContext();
			var article = CreateParagraph("Art. 1. W ustawie wprowadza się zmiany:", "ART");
			var repealPoint = CreateParagraph("1) w art. 11 w ust. 4 uchyla się pkt 4;", "PKT");

			orchestrator.ProcessParagraph(article, context);
			orchestrator.ProcessParagraph(repealPoint, context);

			Assert.False(context.AmendmentTriggerDetected,
				"Uchylenie nie powinno ustawiać triggera — nie ma treści do zebrania");
			Assert.False(context.InsideAmendment,
				"Uchylenie nie powinno włączać trybu nowelizacji — jest samowystarczalne");
		}

		[Fact]
		public void ProcessParagraph_RepealFollowedByModification_BothHandledCorrectly()
		{
			// Scenariusz: pkt 1 uchyla, pkt 2 zmienia brzmienie
			var orchestrator = new ParserOrchestrator();
			var context = CreateContext();
			var article = CreateParagraph(
				"Art. 1. W ustawie z dnia 22 marca 2018 r. o komornikach wprowadza się zmiany:", "ART");
			var repealPoint = CreateParagraph("1) w art. 11 w ust. 4 uchyla się pkt 4;", "PKT");
			var modificationPoint = CreateParagraph("2) art. 16 otrzymuje brzmienie:", "PKT");

			orchestrator.ProcessParagraph(article, context);
			orchestrator.ProcessParagraph(repealPoint, context);
			orchestrator.ProcessParagraph(modificationPoint, context);

			// Uchylenie powinno być na punkcie 1
			var points = context.CurrentArticle?.Paragraphs[0].Points;
			Assert.NotNull(points);
			Assert.True(points!.Count >= 2);

			var pkt1 = points[0];
			Assert.NotNull(pkt1.Amendment);
			Assert.Equal(AmendmentOperationType.Repeal, pkt1.Amendment!.OperationType);

			// Punkt 2 powinien mieć trigger ustawiony (czeka na treść nowelizacji)
			Assert.True(context.AmendmentTriggerDetected);
			Assert.Equal("2", context.CurrentPoint?.Number?.Value);
		}

		[Fact]
		public void ProcessParagraph_RepealInLetter_CreatesRepealAmendment()
		{
			// Scenariusz: pkt "w art. 1:", lit "w ust. 2 uchyla się pkt 3;"
			var orchestrator = new ParserOrchestrator();
			var context = CreateContext();
			var article = CreateParagraph("Art. 1. W ustawie wprowadza się zmiany:", "ART");
			var point = CreateParagraph("1) w art. 1:", "PKT");
			var repealLetter = CreateParagraph("a) w ust. 2 uchyla się pkt 3;", "LIT");

			orchestrator.ProcessParagraph(article, context);
			orchestrator.ProcessParagraph(point, context);
			orchestrator.ProcessParagraph(repealLetter, context);

			var letter = context.CurrentLetter;
			Assert.NotNull(letter);
			Assert.NotNull(letter!.Amendment);
			Assert.Equal(AmendmentOperationType.Repeal, letter.Amendment!.OperationType);
			Assert.Equal("1", letter.Amendment.Targets[0].Structure.Article);
			Assert.Equal("2", letter.Amendment.Targets[0].Structure.Paragraph);
			Assert.Equal("3", letter.Amendment.Targets[0].Structure.Point);
		}

		[Fact]
		public void ProcessParagraph_RepealLinksJournalInfo()
		{
			var orchestrator = new ParserOrchestrator();
			var context = CreateContext(); // sourceJournal = year: 2024, position: 1
			var article = CreateParagraph(
				"Art. 1. W ustawie z dnia 22 marca 2018 r. o komornikach wprowadza się zmiany:", "ART");
			var repealPoint = CreateParagraph("1) w art. 11 uchyla się ust. 4;", "PKT");

			orchestrator.ProcessParagraph(article, context);
			orchestrator.ProcessParagraph(repealPoint, context);

			var point = context.CurrentPoint;
			Assert.NotNull(point?.Amendment);
			Assert.Equal(2024, point!.Amendment!.TargetLegalAct.Year);
			Assert.Contains(1, point.Amendment.TargetLegalAct.Positions);
		}

		// ============================================================
		// Uchylenie podczas aktywnego zbierania — naprawa edge case
		// ============================================================

		[Fact]
		public void ProcessParagraph_RepealDuringActiveCollection_PreviousAmendmentFinalized()
		{
			// Scenariusz: pkt 1 otwiera nowelizację (trigger), wchodzi w Z/* content,
			// pkt 2 ma "uchyla się" — poprzednia nowelizacja powinna zostać sfinalizowana
			var orchestrator = new ParserOrchestrator();
			var context = CreateContext();

			var article = CreateParagraph("Art. 1. W ustawie wprowadza się zmiany:", "ART");
			var point1 = CreateParagraph("1) art. 5 otrzymuje brzmienie:", "PKT");
			var amendContent = CreateParagraph("Art. 5. Nowe brzmienie artykułu.", "ZARTzmartartykuempunktem");
			var repealPoint = CreateParagraph("2) w art. 7 uchyla się ust. 3;", "PKT");

			orchestrator.ProcessParagraph(article, context);
			orchestrator.ProcessParagraph(point1, context);
			Assert.True(context.AmendmentTriggerDetected);

			orchestrator.ProcessParagraph(amendContent, context);
			Assert.True(context.InsideAmendment);

			// Uchylenie podczas aktywnego zbierania — powinno sfinalizować pkt1, potem uchylić
			orchestrator.ProcessParagraph(repealPoint, context);

			Assert.False(context.InsideAmendment);

			// Punkt 1 powinien mieć sfinalizowaną nowelizację (Modification)
			var points = context.CurrentArticle?.Paragraphs[0].Points;
			Assert.NotNull(points);
			var pkt1 = points!.FirstOrDefault(p => p.Number?.Value == "1");
			Assert.NotNull(pkt1);
			Assert.NotNull(pkt1!.Amendment);
			Assert.Equal(AmendmentOperationType.Modification, pkt1.Amendment!.OperationType);

			// Punkt 2 powinien mieć Repeal
			var pkt2 = points.FirstOrDefault(p => p.Number?.Value == "2");
			Assert.NotNull(pkt2);
			Assert.NotNull(pkt2!.Amendment);
			Assert.Equal(AmendmentOperationType.Repeal, pkt2.Amendment!.OperationType);
		}

		[Fact]
		public void ProcessParagraph_RepealDuringActiveCollection_NoPreviousContentLost()
		{
			// Weryfikacja że treść zebranej nowelizacji NIE jest gubiona
			var orchestrator = new ParserOrchestrator();
			var context = CreateContext();

			var article = CreateParagraph("Art. 3. W ustawie wprowadza się zmiany:", "ART");
			var point1 = CreateParagraph("1) art. 10 otrzymuje brzmienie:", "PKT");
			var zContent1 = CreateParagraph("Art. 10. 1. Treść ustępu pierwszego.", "ZARTzmartartykuempunktem");
			var zContent2 = CreateParagraph("2. Treść ustępu drugiego.", "ZUSTzmustartykuempunktem");
			var repealPoint = CreateParagraph("2) w art. 11 uchyla się ust. 5;", "PKT");

			orchestrator.ProcessParagraph(article, context);
			orchestrator.ProcessParagraph(point1, context);
			orchestrator.ProcessParagraph(zContent1, context);
			orchestrator.ProcessParagraph(zContent2, context);
			orchestrator.ProcessParagraph(repealPoint, context);

			// Punkt 1 ma nowelizację z zebraną treścią (2 akapity: Art + Ust)
			var pkt1 = context.CurrentArticle?.Paragraphs[0].Points
				.FirstOrDefault(p => p.Number?.Value == "1");
			Assert.NotNull(pkt1?.Amendment);
			Assert.NotNull(pkt1!.Amendment!.Content);
			Assert.Equal(AmendmentObjectType.Article, pkt1.Amendment.Content!.ObjectType);
		}

		[Fact]
		public void ProcessParagraph_AfterTrigger_StyledContentWithoutOwnTrigger_EntersAmendment()
		{
			// Regresja: treść nowelizacji ze stylem ustawy matki (np. PKT) była błędnie pomijana.
			// Scenariusz z dokumentu: "– pkt 1 otrzymuje brzmienie:" (tir), po czym treść
			// z stylem PKT (nowe brzmienie punktu). Treść powinna być zebrana jako nowelizacja.
			var orchestrator = new ParserOrchestrator();
			var context = CreateContext();

			var article = CreateParagraph("Art. 8. W ustawie wprowadza się zmiany:", "ART");
			var point = CreateParagraph("9) w art. 12:", "PKT");
			var letter = CreateParagraph("b) w ust. 2:", "LIT");
			var tiretTrigger = CreateParagraph("– pkt 1 otrzymuje brzmienie:", null); // tiret bez stylu
			var amendmentContent = CreateParagraph("1) nowa treść punktu pierwszego", "PKT"); // treść z stylem PKT

			orchestrator.ProcessParagraph(article, context);
			orchestrator.ProcessParagraph(point, context);
			orchestrator.ProcessParagraph(letter, context);
			orchestrator.ProcessParagraph(tiretTrigger, context);

			Assert.True(context.AmendmentTriggerDetected, "Tiret z 'otrzymuje brzmienie' powinien ustawić trigger");
			Assert.False(context.InsideAmendment);

			orchestrator.ProcessParagraph(amendmentContent, context);

			// Treść ze stylem PKT (bez własnego zwrotu nowelizacyjnego) → powinna trafić do nowelizacji
			Assert.True(context.InsideAmendment,
				"Akapit ze stylem PKT bez własnego triggera po triggerze nowelizacji powinien wejść w nowelizację");
			Assert.False(context.AmendmentTriggerDetected);
		}

		[Fact]
		public void ProcessParagraph_AfterTrigger_StyledParagraphWithOwnTrigger_TreatedAsParentLaw()
		{
			// Gdy po triggerze pojawia się akapit ze stylem ustawy matki, który SAM zawiera
			// trigger ("w brzmieniu:", "otrzymuje brzmienie:"), to jest nowym elementem ustawy matki —
			// nie treścią nowelizacji poprzedniego triggera. Dotychczasowe zachowanie.
			var orchestrator = new ParserOrchestrator();
			var context = CreateContext();

			var article = CreateParagraph("Art. 1. Wprowadza się zmiany:", "ART");
			var trigger = CreateParagraph("1) art. 5 otrzymuje brzmienie:", "PKT");
			var nextElement = CreateParagraph("2) w art. 7 dodaje się ust. 3 w brzmieniu:", "PKT");

			orchestrator.ProcessParagraph(article, context);
			orchestrator.ProcessParagraph(trigger, context);
			Assert.True(context.AmendmentTriggerDetected);

			orchestrator.ProcessParagraph(nextElement, context);

			// Styl PKT + własny trigger "w brzmieniu:" → nowy element ustawy matki
			Assert.False(context.InsideAmendment);
			Assert.True(context.AmendmentTriggerDetected, "Nowy trigger z 'w brzmieniu:' powinien być ustawiony");
			Assert.Equal("2", context.CurrentPoint?.Number?.Value);
		}

		[Fact]
		public void ProcessParagraph_TwoConsecutiveTirets_EachGetsOwnAmendment()
		{
			// Regresja: dwa tirety w tej samej literze oba mają trigger nowelizacji.
			// Nowelizacja drugiego tiretu nadpisywała nowelizację pierwszego (single-property Amendment),
			// bo GetOwner zwracał CurrentLetter zamiast CurrentTiret.
			var orchestrator = new ParserOrchestrator();
			var context = CreateContext();

			var article = CreateParagraph("Art. 8. W ustawie wprowadza się zmiany:", "ART");
			var point   = CreateParagraph("9) w art. 12:", "PKT");
			var letter  = CreateParagraph("b) w ust. 2:", "LIT");
			// tir_1: trigger
			var tir1         = CreateParagraph("– pkt 1 otrzymuje brzmienie:", null);
			var content1     = CreateParagraph("nowa treść pktu 1", null);
			// tir_2: trigger (powinien domknąć nowelizację tir_1 i otworzyć własną)
			var tir2         = CreateParagraph("– pkt 3 otrzymuje brzmienie:", null);
			var content2     = CreateParagraph("nowa treść pktu 3", null);
			// Kolejny punkt — domknie nowelizację tir_2
			var nextPoint    = CreateParagraph("10) następna zmiana", "PKT");

			orchestrator.ProcessParagraph(article, context);
			orchestrator.ProcessParagraph(point,   context);
			orchestrator.ProcessParagraph(letter,  context);
			orchestrator.ProcessParagraph(tir1,    context);
			orchestrator.ProcessParagraph(content1, context);
			orchestrator.ProcessParagraph(tir2,    context);
			orchestrator.ProcessParagraph(content2, context);
			orchestrator.ProcessParagraph(nextPoint, context);

			var tirets = context.CurrentArticle?.Paragraphs[0].Points[0].Letters[0].Tirets;
			Assert.NotNull(tirets);
			Assert.Equal(2, tirets!.Count);

			var tiret1 = tirets[0];
			var tiret2 = tirets[1];

			Assert.True(tiret1.Amendment != null,
				"tir_1 (pkt 1 otrzymuje brzmienie) powinien mieć własną nowelizację");
			Assert.True(tiret2.Amendment != null,
				"tir_2 (pkt 3 otrzymuje brzmienie) powinien mieć własną nowelizację");

			Assert.True(!ReferenceEquals(tiret1.Amendment, tiret2.Amendment),
				"Nowelizacje obu tiretów muszą być osobnymi obiektami — nie nadpisywać się nawzajem");
		}

		[Fact]
		public void ProcessParagraph_NestedDoubleTirets_EachGetsOwnAmendment()
		{
			var orchestrator = new ParserOrchestrator();
			var context = CreateContext();

			// tir_1 (bez triggera) -> 2tir_1 z triggerem -> tresc -> 2tir_2 z triggerem -> tresc -> tir_2
			var article   = CreateParagraph("Art. 8. W ustawie wprowadza si\u0119 zmiany:", "ART");
			var point     = CreateParagraph("9) w art. 12:", "PKT");
			var letter    = CreateParagraph("b) w ust. 2:", "LIT");
			var tir1      = CreateParagraph("\u2013 punkt 1:", "TIR");
			var two_tir1  = CreateParagraph("\u2013 podpunkt A otrzymuje brzmienie:", "2TIR");
			var content1  = CreateParagraph("nowa tresc podpunktu A", null);
			var two_tir2  = CreateParagraph("\u2013 podpunkt B otrzymuje brzmienie:", "2TIR");
			var content2  = CreateParagraph("nowa tresc podpunktu B", null);
			var tir2      = CreateParagraph("\u2013 punkt 2:", "TIR");

			orchestrator.ProcessParagraph(article,   context);
			orchestrator.ProcessParagraph(point,     context);
			orchestrator.ProcessParagraph(letter,    context);
			orchestrator.ProcessParagraph(tir1,      context);
			orchestrator.ProcessParagraph(two_tir1,  context);
			orchestrator.ProcessParagraph(content1,  context);
			orchestrator.ProcessParagraph(two_tir2,  context);
			orchestrator.ProcessParagraph(content2,  context);
			orchestrator.ProcessParagraph(tir2,      context);

			var tirets = context.CurrentArticle?.Paragraphs[0].Points[0].Letters[0].Tirets;
			Assert.NotNull(tirets);
			Assert.Equal(2, tirets!.Count); // tylko tir_1 i tir_2 bezposrednio pod litera

			var tiret1 = tirets[0];
			Assert.Equal(2, tiret1.Tirets.Count); // 2tir_1 i 2tir_2 zagniezdzone pod tir_1

			var double_tir1 = tiret1.Tirets[0];
			var double_tir2 = tiret1.Tirets[1];

			Assert.True(double_tir1.Amendment != null,
				"2tir_1 (podpunkt A) powinien miec wlasna nowelizacje");
			Assert.True(double_tir2.Amendment != null,
				"2tir_2 (podpunkt B) powinien miec wlasna nowelizacje");
			Assert.True(!ReferenceEquals(double_tir1.Amendment, double_tir2.Amendment),
				"Nowelizacje obu podtiretow musza byc osobnymi obiektami");
		}
	}
}
