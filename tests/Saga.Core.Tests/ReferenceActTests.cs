using System;
using System.IO;
using System.Linq;
using Saga.Core;
using Xunit;

namespace Saga.Core.Tests
{
	public class ReferenceActTests
	{
		private static string GetTemporaryCopyPath(string fileName)
			=> TestFiles.CreateTemporaryCopy(TestFiles.GetReferenceDocPath(fileName));

		// AlwaysParse: testy referencyjne parsera niezależne od progów klasyfikatora dokumentu.
		private static Saga.Model.LegalDocument ParseDocument(string tempPath)
			=> LegalDocumentParser.Parse(tempPath, new ParseOptions { Policy = ParsePolicy.AlwaysParse }).Document!;

		[Fact]
		public void ReferenceAct_DefaultPolicy_IsRecognizedAsLegalAct()
		{
			// Strażnik fałszywego negatywu na REALNYM akcie: zmiana punktacji/progów klasyfikatora,
			// która odrzuciłaby doc001 przy domyślnej polityce (Document=null, CLI exit 2),
			// musi być widoczna w siatce — testy syntetyczne tego nie łapią.
			var tempPath = GetTemporaryCopyPath("doc001.docx");
			try
			{
				var result = LegalDocumentParser.Parse(tempPath);

				Assert.True(result.Classification.IsLegalAct,
					$"doc001 nierozpoznany jako akt: {result.Classification.Justification}");
				Assert.NotNull(result.Document);
			}
			finally
			{
				File.Delete(tempPath);
			}
		}

		[Fact]
		public void ReferenceAct_ContainsAtLeastOneArticle()
		{
			var tempPath = GetTemporaryCopyPath("doc001.docx");
			try
			{
				var document = ParseDocument(tempPath);

				Assert.True(document.Articles.Any());
			}
			finally
			{
				File.Delete(tempPath);
			}
		}

		[Fact]
		public void ReferenceAct_Point8_ContainsLetterAndTiretNumbers()
		{
			var tempPath = GetTemporaryCopyPath("doc001.docx");
			try
			{
				var document = ParseDocument(tempPath);

				var paragraph = document.Articles
					.SelectMany(a => a.Paragraphs)
					.FirstOrDefault(p => p.Points.Any(pt => pt.Number?.Value == "8"));

				Assert.NotNull(paragraph);

				var point = paragraph!.Points.First(p => p.Number?.Value == "8");
				var letter = point.Letters.First(l => l.Tirets.Any());
				var tiret = letter.Tirets.First();

				var paragraphNumber = paragraph.Number?.Value;
				var pointNumber = point.Number?.Value;
				var letterNumber = letter.Number?.Value;
				var tiretNumber = tiret.Number?.Value;

				Assert.True(string.IsNullOrWhiteSpace(paragraphNumber) || paragraphNumber == "1",
					$"Unexpected paragraph number. Paragraph text: '{paragraph.ContentText}', Number: '{paragraphNumber}'");
				Assert.True(!string.IsNullOrWhiteSpace(pointNumber),
					$"Missing point number. Point text: '{point.ContentText}'");
				Assert.True(!string.IsNullOrWhiteSpace(letterNumber),
					$"Missing letter number. Letter text: '{letter.ContentText}'");
				Assert.True(!string.IsNullOrWhiteSpace(tiretNumber),
					$"Missing tiret number. Tiret text: '{tiret.ContentText}'");
			}
			finally
			{
				File.Delete(tempPath);
			}
		}
	}
}
