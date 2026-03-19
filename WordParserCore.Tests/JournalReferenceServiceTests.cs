using System;
using System.Linq;
using ModelDto.EditorialUnits;
using WordParserCore.Services;
using Xunit;

namespace WordParserCore.Tests
{
	public class JournalReferenceServiceTests
	{
		private readonly JournalReferenceService _service = new();

		[Fact]
		public void ParseJournalReferences_UsesEffectiveDateWhenYearMissing()
		{
			var article = new Article
			{
				EffectiveDate = new DateTime(2022, 1, 1),
				ContentText = "Art. 1. W ustawie z dnia 1 stycznia 2022 r. (Dz. U. poz. 5)"
			};

			_service.ParseJournalReferences(article);

			Assert.Single(article.Journals);
			Assert.Equal(2022, article.Journals[0].Year);
			Assert.Equal(new[] { 5 }, article.Journals[0].Positions);
		}

		[Fact]
		public void ParseJournalReferences_MergesPositionsForSameYear()
		{
			var article = new Article
			{
				EffectiveDate = new DateTime(2020, 1, 1),
				ContentText = "Art. 2. W ustawie z dnia 1 stycznia 2020 r. (Dz. U. z 2020 r. poz. 10 i 12; Dz. U. z 2020 r. poz. 12, 15)"
			};

			_service.ParseJournalReferences(article);

			Assert.Single(article.Journals);
			var journal = article.Journals[0];
			Assert.Equal(2020, journal.Year);
			Assert.Equal(new[] { 10, 12, 15 }, journal.Positions);
		}

		[Fact]
		public void ParseJournalReferences_CapturesMultipleYears()
		{
			var article = new Article
			{
				EffectiveDate = new DateTime(2019, 1, 1),
				ContentText = "Art. 3. W ustawie z dnia 1 stycznia 2019 r. (Dz. U. z 2020 r. poz. 10, 12; Dz. U. z 2021 r. poz. 3)"
			};

			_service.ParseJournalReferences(article);

			Assert.Equal(2, article.Journals.Count);
			Assert.Contains(article.Journals, j => j.Year == 2020 && j.Positions.SequenceEqual(new[] { 10, 12 }));
			Assert.Contains(article.Journals, j => j.Year == 2021 && j.Positions.SequenceEqual(new[] { 3 }));
		}

		[Fact]
		public void ParseJournalReferences_UsesLongestSourceString()
		{
			var article = new Article
			{
				EffectiveDate = new DateTime(2020, 1, 1),
				ContentText = "Art. 4. W ustawie z dnia 1 stycznia 2020 r. (Dz. U. z 2020 r. poz. 10; Dz. U. z 2020 r. poz. 10 i 12)"
			};

			_service.ParseJournalReferences(article);

			var journal = Assert.Single(article.Journals);
			Assert.Equal("Dz. U. z 2020 r. poz. 10 i 12", journal.SourceString);
		}

		[Fact]
		public void ParseJournalReferences_IgnoresNonAmendingArticles()
		{
			var article = new Article
			{
				EffectiveDate = new DateTime(2024, 1, 1),
				ContentText = "Art. 72. W przypadku wnioskow (Dz. U. z 2022 r. poz. 902)"
			};

			_service.ParseJournalReferences(article);

			Assert.Empty(article.Journals);
		}
	}
}
