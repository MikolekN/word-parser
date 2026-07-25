using System;
using System.IO;

namespace WordParserCore.Tests
{
	/// <summary>
	/// Wspólne ścieżki plików testowych. Dokumenty referencyjne (DOCX) leżą
	/// w lokalnym, niewersjonowanym DocRepo/; goldeny snapshotowe w lokalnym
	/// WordParserCore.Tests/Artifacts/ (też niewersjonowane — decyzja projektu:
	/// dokumenty robocze i artefakty nie trafiają do repozytorium).
	/// </summary>
	internal static class TestFiles
	{
		/// <summary>
		/// Korzeń projektu testowego (katalog z WordParserCore.Tests.csproj),
		/// odnajdywany od katalogu wyjściowego testów w górę.
		/// </summary>
		public static string GetTestProjectRoot()
		{
			var dir = new DirectoryInfo(AppContext.BaseDirectory);
			while (dir != null && !File.Exists(Path.Combine(dir.FullName, "WordParserCore.Tests.csproj")))
			{
				dir = dir.Parent;
			}

			if (dir == null)
			{
				throw new DirectoryNotFoundException(
					"Nie znaleziono katalogu projektu testowego (WordParserCore.Tests.csproj) powyżej katalogu wyjściowego testów.");
			}

			return dir.FullName;
		}

		public static string GetArtifactPath(string fileName)
			=> Path.Combine(GetTestProjectRoot(), "Artifacts", fileName);

		/// <summary>
		/// Korzeń repozytorium (katalog z WordParser.sln) — potrzebny testom weryfikującym
		/// dokumentację, która odwołuje się do plików w innych projektach.
		/// </summary>
		public static string GetRepositoryRoot()
		{
			var dir = new DirectoryInfo(AppContext.BaseDirectory);
			while (dir != null && !File.Exists(Path.Combine(dir.FullName, "WordParser.sln")))
			{
				dir = dir.Parent;
			}

			if (dir == null)
			{
				throw new DirectoryNotFoundException(
					"Nie znaleziono korzenia repozytorium (WordParser.sln) powyżej katalogu wyjściowego testów.");
			}

			return dir.FullName;
		}

		/// <summary>
		/// Ścieżka dokumentu referencyjnego w lokalnym repozytorium dokumentów (DocRepo/).
		/// </summary>
		public static string GetReferenceDocPath(string fileName)
		{
			var dir = new DirectoryInfo(AppContext.BaseDirectory);
			while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "DocRepo")))
			{
				dir = dir.Parent;
			}

			if (dir == null)
			{
				throw new DirectoryNotFoundException(
					"Nie znaleziono katalogu DocRepo powyżej katalogu testów — dokumenty referencyjne są lokalne (niewersjonowane).");
			}

			return Path.Combine(dir.FullName, "DocRepo", fileName);
		}

		/// <summary>
		/// Kopia tymczasowa artefaktu — testy nie dotykają plików w drzewie źródłowym.
		/// </summary>
		public static string CreateTemporaryCopy(string sourcePath)
		{
			var tempPath = Path.Combine(Path.GetTempPath(),
				$"WordParserTests_{Guid.NewGuid():N}_{Path.GetFileName(sourcePath)}");
			File.Copy(sourcePath, tempPath, true);
			return tempPath;
		}
	}
}
