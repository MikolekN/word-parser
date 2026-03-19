using System;
using System.IO;
using System.Linq;
using WordParserCore;
using Xunit;

namespace WordParserCore.Tests
{
    public class ReferenceActTests
    {
        private static string GetReferenceDocPath(string fileName)
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "DocRepo")))
            {
                dir = dir.Parent;
            }

            if (dir == null)
            {
                throw new DirectoryNotFoundException("DocRepo directory not found from test output path.");
            }

            return Path.Combine(dir.FullName, "DocRepo", fileName);
        }

        private static string GetTemporaryCopyPath(string fileName)
        {
            var sourcePath = GetReferenceDocPath(fileName);
            var tempPath = Path.Combine(Path.GetTempPath(), $"WordParserTests_{Guid.NewGuid():N}_{fileName}");
            File.Copy(sourcePath, tempPath, true);
            return tempPath;
        }

        [Fact]
        public void ReferenceAct_ContainsAtLeastOneArticle()
        {
            var tempPath = GetTemporaryCopyPath("doc001.docx");
            try
            {
                var document = LegalDocumentParser.Parse(tempPath);

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
                var document = LegalDocumentParser.Parse(tempPath);

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
