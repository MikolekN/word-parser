using Saga.Core.Services.Classify;
using Xunit;

namespace Saga.Core.Tests
{
	/// <summary>
	/// Testy CHARAKTERYZUJĄCE obecne zachowanie klasyfikatora dla akapitów BEZ stylu szablonu
	/// (styleId = null albo styl nierozpoznany, np. "Normal" z DOCX bez szablonu).
	///
	/// Część utrwalonych tu zachowań to ZNANE LUKI gałęzi bezstylowej (rejestr: docs/backlog.md,
	/// sekcja 1) — nie błędy do „naprawienia przy okazji". Każda zmiana wyniku wymaga jawnej
	/// aktualizacji tego pliku z uzasadnieniem ZTP w opisie PR.
	/// </summary>
	public class ParagraphClassifierStylelessTests
	{
		private static ClassificationResult Classify(string text, string? styleId = null)
			=> new ParagraphClassifier().Classify(new ClassificationInput(text, styleId));

		// ============================================================
		// Ścieżka "tylko regex" — działa dziś poprawnie (Confidence 90)
		// ============================================================

		[Theory]
		[InlineData("Art. 5. Treść artykułu", ParagraphKind.Article)]
		[InlineData("2. Treść ustępu", ParagraphKind.Paragraph)]
		[InlineData("2a. Treść ustępu dodanego", ParagraphKind.Paragraph)]
		[InlineData("3) treść punktu;", ParagraphKind.Point)]
		[InlineData("12a) treść punktu;", ParagraphKind.Point)]
		[InlineData("a) treść litery,", ParagraphKind.Letter)]
		[InlineData("za) treść litery po wyczerpaniu alfabetu,", ParagraphKind.Letter)]
		[InlineData("- treść tiretu,", ParagraphKind.Tiret)]
		public void Classify_WithoutStyle_RegexOnlyPath_ReturnsKindWithStyleAbsentPenalty(
			string text, ParagraphKind expected)
		{
			var result = Classify(text);

			Assert.Equal(expected, result.Kind);
			Assert.Equal(90, result.Confidence); // 100 - StyleAbsentPenalty(10)
		}

		[Fact]
		public void Classify_EnDashTiret_WithoutStyle_ReturnsTiret()
		{
			// Półpauza – bez sanityzacji (w potoku Sanitize normalizuje ją do '-')
			var result = Classify("– treść tiretu,");

			Assert.Equal(ParagraphKind.Tiret, result.Kind);
			Assert.Equal(90, result.Confidence);
		}

		[Fact]
		public void Classify_QuotedTiret_WithoutStyle_ReturnsTiret()
		{
			// Cytowany tiret w treści nowelizacji („– …") — CLAUDE.md: wzorce muszą
			// obsługiwać opcjonalny prefiks cudzysłowu dla treści nowelizacji
			var result = Classify("„– treść tiretu w nowelizacji,");

			Assert.Equal(ParagraphKind.Tiret, result.Kind);
		}

		[Fact]
		public void Classify_UnrecognizedStyleId_BehavesLikeNoStyle()
		{
			// DOCX bez szablonu: styl "Normal" nie występuje w StyleLibraryMapper
			var result = Classify("2. Treść ustępu", "Normal");

			Assert.Equal(ParagraphKind.Paragraph, result.Kind);
			Assert.Equal(90, result.Confidence);
		}

		// ============================================================
		// ZNANE LUKI gałęzi bezstylowej — utrwalone świadomie
		// ============================================================

		[Fact]
		public void Classify_WrapUpLikeText_WithoutStyle_IsMisclassifiedAsTiret()
		{
			// UWAGA: dokumentuje obecne BŁĘDNE zachowanie — część wspólna wyliczenia
			// ("– przepisy stosuje się odpowiednio.") bez stylu CZ_WSP_* wpada w TiretPattern.
			// Luka świadomie odłożona — powód i warunki domknięcia: docs/backlog.md, sekcja 1.
			var result = Classify("- przepisy stosuje się odpowiednio.");

			Assert.Equal(ParagraphKind.Tiret, result.Kind);
		}

		[Theory]
		[InlineData("Rozdział 1", ParagraphKind.ChapterUnit)]
		[InlineData("Oddział 2", ParagraphKind.SubchapterUnit)]
		[InlineData("DZIAŁ II", ParagraphKind.DivisionUnit)]
		[InlineData("TYTUŁ III", ParagraphKind.TitleUnit)]
		[InlineData("KSIĘGA PIERWSZA", ParagraphKind.BookUnit)]
		[InlineData("CZĘŚĆ I", ParagraphKind.PartUnit)]
		public void Classify_SystematizingUnit_WithoutStyle_IsRecognized(string text, ParagraphKind expected)
		{
			// Etap 6b: jednostki systematyzacyjne (§ 60-62 ZTP) rozpoznawane z treści bez stylu
			// (Rozdział/Oddział — cyfra arabska; Część/Księga/Tytuł/Dział — rzymska lub liczebnik słowny).
			var result = Classify(text);

			Assert.Equal(expected, result.Kind);
		}

		[Fact]
		public void Classify_NamedPartWithoutNumber_StaysUnknown()
		{
			// Ograniczenie: część nazwana bez numeru („CZĘŚĆ OGÓLNA") nie jest rozpoznawana jako jednostka —
			// brak numeru rzymskiego/słownego. (Wielo-częściowe akry i części nazwane — poza zakresem 6b.)
			var result = Classify("CZĘŚĆ OGÓLNA");

			Assert.Equal(ParagraphKind.Unknown, result.Kind);
		}

		[Theory]
		[InlineData("USTAWA")]
		[InlineData("ROZPORZĄDZENIE")]
		[InlineData("z dnia 12 marca 2024 r.")]
		[InlineData("o zmianie ustawy o komornikach sądowych")]
		public void Classify_ActMetadata_WithoutStyle_IsUnknown(string text)
		{
			// UWAGA: dokumentuje obecną LUKĘ — metadane aktu (tytuł, data, rodzaj; § 16-19 ZTP)
			// bez stylów TYTUŁ_AKTU/DATA_AKTU/OZN_RODZ_AKTU są nierozpoznawane.
			// Zmiana planowana w Etapie 7 (DocumentMetadataCollector).
			var result = Classify(text);

			Assert.Equal(ParagraphKind.Unknown, result.Kind);
			Assert.Equal(1, result.Confidence);
		}

		[Fact]
		public void Classify_QuotedAmendmentContent_WithoutStyle_IsNotFlaggedAsAmendment()
		{
			// UWAGA: dokumentuje obecną LUKĘ — treść nowelizacji („Art. 5. …") bez stylu Z/*
			// klasyfikuje się jak zwykły artykuł aktu i NIE dostaje IsAmendmentContent.
			// Granice nowelizacji wyznaczają dziś wyłącznie triggery tekstowe w AmendmentStateManager.
			// Zmiana planowana w Etapie 8 (QuoteBalanceTracker).
			var result = Classify("„Art. 5. Nowe brzmienie artykułu.");

			Assert.Equal(ParagraphKind.Article, result.Kind);
			Assert.False(result.IsAmendmentContent);
		}

		[Fact]
		public void Classify_EmptyishText_WithoutStyle_IsUnknown()
		{
			var result = Classify("Zwykły tekst narracyjny bez oznaczenia jednostki.");

			Assert.Equal(ParagraphKind.Unknown, result.Kind);
			Assert.Equal(1, result.Confidence);
		}
	}
}
