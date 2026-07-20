using System;
using System.IO;
using WordParserCore.Services.Converters;
using Xunit;

namespace WordParserCore.Tests
{
	/// <summary>
	/// Testy snapshotowe (golden) pełnego modelu dokumentu referencyjnego doc001.docx.
	/// Siatka bezpieczeństwa dla przebudowy potoku: każda zmiana zachowania parsera
	/// na ścieżce DOCX+szablon musi być widoczna jako diff snapshotu.
	/// Dokument źródłowy (DocRepo/) i golden (Artifacts/) są lokalne, niewersjonowane.
	///
	/// Regeneracja goldenu (po świadomej, udokumentowanej zmianie zachowania):
	///   UPDATE_SNAPSHOTS=1 dotnet test --filter "FullyQualifiedName~LegalDocumentSnapshotTests"
	/// </summary>
	public class LegalDocumentSnapshotTests
	{
		private static string ParseReferenceActToCanonicalXml()
		{
			var tempPath = TestFiles.CreateTemporaryCopy(TestFiles.GetReferenceDocPath("doc001.docx"));
			try
			{
				// AlwaysParse: siatka regresyjna PARSERA ma być niezależna od progów klasyfikatora —
				// zmiana punktacji klasyfikacji nie może wyzerować snapshotu (Document = null).
				var result = LegalDocumentParser.Parse(tempPath, new ParseOptions { Policy = ParsePolicy.AlwaysParse });
				return CanonicalDtoXmlSerializer.Serialize(result.Document!);
			}
			finally
			{
				File.Delete(tempPath);
			}
		}

		[Fact]
		public void Doc001_CanonicalXml_MatchesSnapshot()
		{
			var actual = ParseReferenceActToCanonicalXml();
			var snapshotPath = TestFiles.GetArtifactPath("doc001.snapshot.xml");

			if (Environment.GetEnvironmentVariable("UPDATE_SNAPSHOTS") == "1")
			{
				File.WriteAllText(snapshotPath, actual);
				return;
			}

			Assert.True(File.Exists(snapshotPath),
				$"Brak pliku snapshotu: {snapshotPath}. Wygeneruj go przez UPDATE_SNAPSHOTS=1 i zrewiduj przed commitem.");

			// Normalizacja końców linii: golden mógł przejść przez konwersję EOL (git/edytor),
			// a serializer emituje wyłącznie '\n'.
			var expected = File.ReadAllText(snapshotPath).Replace("\r\n", "\n");
			Assert.Equal(expected, actual);
		}

		[Fact]
		public void Doc001_Parse_IsDeterministic()
		{
			var first = ParseReferenceActToCanonicalXml();
			var second = ParseReferenceActToCanonicalXml();

			Assert.Equal(first, second);
		}
	}
}
