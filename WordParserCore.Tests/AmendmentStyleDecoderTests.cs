using System.Linq;
using WordParserCore;
using WordParserCore.Helpers;
using Xunit;

namespace WordParserCore.Tests
{
	public class AmendmentStyleDecoderTests
	{
		// ============================================================
		// Statyczna mapa — liczba wpisow i spojnosc
		// ============================================================

		[Fact]
		public void AmendmentStyleInfoMap_Has168Entries()
		{
			Assert.Equal(168, StyleLibraryMapper.AmendmentStyleInfoMap.Count);
		}

		[Fact]
		public void AmendmentStyleInfoMap_AllKeysExistInStyleMap()
		{
			foreach (var key in StyleLibraryMapper.AmendmentStyleInfoMap.Keys)
			{
				Assert.True(
					StyleLibraryMapper.StyleMap.ContainsKey(key),
					$"Klucz '{key}' z AmendmentStyleInfoMap nie istnieje w StyleMap");
			}
		}

		[Fact]
		public void AmendmentStyleInfoMap_AllEntriesHaveNonEmptyShortCodeAndDisplayName()
		{
			foreach (var (key, info) in StyleLibraryMapper.AmendmentStyleInfoMap)
			{
				Assert.False(string.IsNullOrWhiteSpace(info.ShortCode),
					$"ShortCode jest pusty dla klucza '{key}'");
				Assert.False(string.IsNullOrWhiteSpace(info.DisplayName),
					$"DisplayName jest pusty dla klucza '{key}'");
			}
		}

		// ============================================================
		// Statyczna mapa — rozklad instrumentow
		// ============================================================

		[Fact]
		public void AmendmentStyleInfoMap_ContainsAllInstrumentTypes()
		{
			var instruments = StyleLibraryMapper.AmendmentStyleInfoMap.Values
				.Select(i => i.Instrument)
				.Distinct()
				.ToHashSet();

			Assert.Contains(AmendmentInstrument.ArticleOrPoint, instruments);
			Assert.Contains(AmendmentInstrument.Letter, instruments);
			Assert.Contains(AmendmentInstrument.Tiret, instruments);
			Assert.Contains(AmendmentInstrument.DoubleTiret, instruments);
			Assert.Contains(AmendmentInstrument.Nested, instruments);
		}

		// ============================================================
		// TryGetAmendmentStyleInfo — lookup statyczny
		// ============================================================

		[Theory]
		[InlineData(null)]
		[InlineData("")]
		[InlineData("   ")]
		public void TryGetAmendmentStyleInfo_NullOrEmpty_ReturnsNull(string? styleId)
		{
			Assert.Null(StyleLibraryMapper.TryGetAmendmentStyleInfo(styleId));
		}

		[Fact]
		public void TryGetAmendmentStyleInfo_NonAmendmentStyleId_ReturnsNull()
		{
			// ART to styl zwykly, nie nowelizacyjny
			Assert.Null(StyleLibraryMapper.TryGetAmendmentStyleInfo("ARTartustawynprozporzdzenia"));
		}

		[Fact]
		public void TryGetAmendmentStyleInfo_UnknownStyleId_ReturnsNull()
		{
			Assert.Null(StyleLibraryMapper.TryGetAmendmentStyleInfo("nieistniejacystyl"));
		}

		// ============================================================
		// TryGetAmendmentStyleInfo — konkretne wpisy (spot-check)
		// ============================================================

		[Fact]
		public void TryGetAmendmentStyleInfo_Z_UST_ReturnsCorrectInfo()
		{
			var info = StyleLibraryMapper.TryGetAmendmentStyleInfo("ZUSTzmustartykuempunktem");
			Assert.NotNull(info);
			Assert.Equal(AmendmentInstrument.ArticleOrPoint, info.Instrument);
			Assert.Equal(AmendmentTargetKind.Paragraph, info.TargetKind);
			Assert.Null(info.CommonPartOf);
			Assert.Null(info.ParentContext);
			Assert.Equal("Z/UST(§)", info.ShortCode);
		}

		[Fact]
		public void TryGetAmendmentStyleInfo_Z_TIR_w_LIT_ReturnsCorrectInfo()
		{
			var info = StyleLibraryMapper.TryGetAmendmentStyleInfo("ZTIRwLITzmtirwlitartykuempunktem");
			Assert.NotNull(info);
			Assert.Equal(AmendmentInstrument.ArticleOrPoint, info.Instrument);
			Assert.Equal(AmendmentTargetKind.Tiret, info.TargetKind);
			Assert.Null(info.CommonPartOf);
			Assert.Equal(AmendmentTargetKind.Letter, info.ParentContext);
			Assert.Equal("Z/TIR_w_LIT", info.ShortCode);
		}

		[Fact]
		public void TryGetAmendmentStyleInfo_Z_CZ_WSP_PKT_ReturnsCommonPartInfo()
		{
			var info = StyleLibraryMapper.TryGetAmendmentStyleInfo("ZCZWSPPKTzmczciwsppktartykuempunktem");
			Assert.NotNull(info);
			Assert.Equal(AmendmentTargetKind.CommonPart, info.TargetKind);
			Assert.Equal(AmendmentTargetKind.Point, info.CommonPartOf);
		}

		[Fact]
		public void TryGetAmendmentStyleInfo_ZZ_ART_ReturnsNestedInfo()
		{
			var info = StyleLibraryMapper.TryGetAmendmentStyleInfo("ZZARTzmianazmart");
			Assert.NotNull(info);
			Assert.Equal(AmendmentInstrument.Nested, info.Instrument);
			Assert.Equal(AmendmentTargetKind.Article, info.TargetKind);
			Assert.Equal("ZZ/ART(§)", info.ShortCode);
		}

		[Fact]
		public void TryGetAmendmentStyleInfo_Z_LIT_PKT_ReturnsLetterInstrumentPointTarget()
		{
			var info = StyleLibraryMapper.TryGetAmendmentStyleInfo("ZLITPKTzmpktliter");
			Assert.NotNull(info);
			Assert.Equal(AmendmentInstrument.Letter, info.Instrument);
			Assert.Equal(AmendmentTargetKind.Point, info.TargetKind);
		}

		[Fact]
		public void TryGetAmendmentStyleInfo_Z_2TIR_CZ_WSP_TIR_w_PKT_ReturnsComplexInfo()
		{
			var info = StyleLibraryMapper.TryGetAmendmentStyleInfo("Z2TIRCZWSPTIRwPKTzmczciwsptirwpktpodwjnymtiret");
			Assert.NotNull(info);
			Assert.Equal(AmendmentInstrument.DoubleTiret, info.Instrument);
			Assert.Equal(AmendmentTargetKind.CommonPart, info.TargetKind);
			Assert.Equal(AmendmentTargetKind.Tiret, info.CommonPartOf);
			Assert.Equal(AmendmentTargetKind.Point, info.ParentContext);
		}

		// ============================================================
		// DecodeByStyleId — fasada (deleguje do mapy statycznej)
		// ============================================================

		[Theory]
		[InlineData("ZUSTzmustartykuempunktem", AmendmentInstrument.ArticleOrPoint, AmendmentTargetKind.Paragraph)]
		[InlineData("ZLITPKTzmpktliter", AmendmentInstrument.Letter, AmendmentTargetKind.Point)]
		[InlineData("ZTIRLITzmlittiret", AmendmentInstrument.Tiret, AmendmentTargetKind.Letter)]
		[InlineData("ZZUSTzmianazmust", AmendmentInstrument.Nested, AmendmentTargetKind.Paragraph)]
		public void DecodeByStyleId_UsesStaticMap(string styleId, AmendmentInstrument expectedInstrument, AmendmentTargetKind expectedTarget)
		{
			var result = AmendmentStyleDecoder.DecodeByStyleId(styleId);
			Assert.NotNull(result);
			Assert.Equal(expectedInstrument, result.Instrument);
			Assert.Equal(expectedTarget, result.TargetKind);
		}

		[Fact]
		public void DecodeByStyleId_UnknownStyleId_ReturnsNull()
		{
			Assert.Null(AmendmentStyleDecoder.DecodeByStyleId("ARTartustawynprozporzdzenia"));
		}

		[Fact]
		public void DecodeByStyleId_Null_ReturnsNull()
		{
			Assert.Null(AmendmentStyleDecoder.DecodeByStyleId(null));
		}
	}
}
