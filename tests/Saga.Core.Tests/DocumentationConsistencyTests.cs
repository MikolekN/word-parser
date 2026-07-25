using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace Saga.Core.Tests
{
	/// <summary>
	/// Testy pilnujące, że dokumentacja mówi prawdę o kodzie. Dokumentacja dryfuje cicho —
	/// przeniesiony plik albo zmieniona nazwa klasy nie psuje builda, więc bez testu rozjazd
	/// wychodzi dopiero wtedy, gdy ktoś (albo agent) podejmie decyzję na podstawie nieprawdy.
	///
	/// Zakres celowo ograniczony do rzeczy sprawdzalnych mechanicznie: linki, ścieżki plików
	/// i kompletność indeksu ADR. Zgodności treści z kodem żaden test nie sprawdzi.
	/// </summary>
	public class DocumentationConsistencyTests
	{
		private static readonly string Root = TestFiles.GetRepositoryRoot();

		/// <summary>Linki markdown: [tekst](sciezka) — bez linków zewnętrznych i samych zakotwiczeń.</summary>
		private static readonly Regex MarkdownLinkPattern =
			new(@"\]\((?<target>[^)\s]+)\)", RegexOptions.Compiled);

		/// <summary>
		/// Ścieżki plików źródłowych — także BEZ backticków, bo w blokach ```bash pojawiają się
		/// w poleceniach (`dotnet build src/Saga.Core/Saga.Core.csproj`). Wymagamy prefiksu
		/// znanego katalogu, żeby nie łapać fragmentów prozy.
		/// </summary>
		/// Uwaga na kolejność w alternatywie rozszerzeń: „cs" przed „csproj" ucięłoby ścieżkę
		/// do pliku projektu w połowie i zgłaszało nieistniejący plik.
		private static readonly Regex SourcePathPattern =
			new(@"(?<path>(?:src|tests|tools|schema|docs)/[A-Za-z0-9_.\-/]+\.(?:csproj|sln|cs))\b", RegexOptions.Compiled);

		/// <summary>
		/// Wszystkie pliki źródłowe repozytorium jako ścieżki relatywne z separatorem '/'.
		/// Bez katalogów build/lokalnych — te nie są przedmiotem dokumentacji.
		/// </summary>
		private static readonly Lazy<string[]> RepositoryFiles = new(() =>
		{
			string[] excluded = { "/bin/", "/obj/", "/.git/", "/schema-pg/", "/DocRepo/", "/node_modules/" };
			return Directory.EnumerateFiles(Root, "*", SearchOption.AllDirectories)
				.Select(path => Path.GetRelativePath(Root, path).Replace(Path.DirectorySeparatorChar, '/'))
				.Where(path => !excluded.Any(fragment => $"/{path}".Contains(fragment, StringComparison.Ordinal)))
				.ToArray();
		});

		/// <summary>
		/// Czy dokumentacja wskazuje na istniejący plik. Ścieżka może być podana względem korzenia
		/// repozytorium albo względem katalogu projektu (konwencja docs/parsing-flow.md), dlatego
		/// dopuszczamy dopasowanie po sufiksie — nadal wyłapuje plik usunięty, przeniesiony
		/// do innego projektu albo literówkę w nazwie.
		/// </summary>
		private static bool RepositoryContains(string documentedPath)
			=> File.Exists(Path.Combine(Root, documentedPath))
			   || RepositoryFiles.Value.Any(path =>
				   path.EndsWith($"/{documentedPath}", StringComparison.Ordinal));

		/// <summary>
		/// Cała dokumentacja wersjonowana — nie tylko docs/, bo README narzędzi w tools/ też
		/// odwołuje się do plików projektu (i przy migracji nazw właśnie tam został nieaktualny ślad).
		/// Pomijamy docs/internal/ (pliki robocze, gitignorowane) oraz katalogi build.
		/// </summary>
		private static IEnumerable<string> VersionedMarkdownFiles()
		{
			string[] excluded =
			{
				$"{Path.DirectorySeparatorChar}internal{Path.DirectorySeparatorChar}",
				$"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
				$"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
				$"{Path.DirectorySeparatorChar}schema-pg{Path.DirectorySeparatorChar}",
				$"{Path.DirectorySeparatorChar}DocRepo{Path.DirectorySeparatorChar}",
				$"{Path.DirectorySeparatorChar}.claude{Path.DirectorySeparatorChar}",
				$"{Path.DirectorySeparatorChar}.devcontainer{Path.DirectorySeparatorChar}",
			};

			return Directory.EnumerateFiles(Root, "*.md", SearchOption.AllDirectories)
				.Where(file => !excluded.Any(fragment => file.Contains(fragment, StringComparison.Ordinal)));
		}

		public static TheoryData<string> MarkdownFiles()
		{
			var data = new TheoryData<string>();
			foreach (var file in VersionedMarkdownFiles())
			{
				data.Add(Path.GetRelativePath(Root, file));
			}
			return data;
		}

		[Theory]
		[MemberData(nameof(MarkdownFiles))]
		public void MarkdownLinks_PointToExistingFiles(string relativeDocPath)
		{
			var docPath = Path.Combine(Root, relativeDocPath);
			var docDirectory = Path.GetDirectoryName(docPath)!;
			var broken = new List<string>();

			foreach (Match match in MarkdownLinkPattern.Matches(File.ReadAllText(docPath)))
			{
				var target = match.Groups["target"].Value;

				// linki zewnętrzne i odnośniki wewnątrz dokumentu nie wskazują na pliki
				if (target.StartsWith("http", StringComparison.OrdinalIgnoreCase) ||
				    target.StartsWith('#') ||
				    target.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}

				// obcięcie zakotwiczenia: plik.cs#L42 → plik.cs
				var pathPart = target.Split('#')[0];
				if (pathPart.Length == 0)
				{
					continue;
				}

				var resolved = Path.GetFullPath(Path.Combine(docDirectory, Uri.UnescapeDataString(pathPart)));
				if (!File.Exists(resolved) && !Directory.Exists(resolved))
				{
					broken.Add($"{target} → {Path.GetRelativePath(Root, resolved)}");
				}
			}

			Assert.True(broken.Count == 0,
				$"{relativeDocPath}: linki prowadzące w nicość:{Environment.NewLine}  " +
				string.Join($"{Environment.NewLine}  ", broken));
		}

		[Theory]
		[MemberData(nameof(MarkdownFiles))]
		public void DocumentedSourcePaths_PointToExistingFiles(string relativeDocPath)
		{
			var docPath = Path.Combine(Root, relativeDocPath);
			var missing = new List<string>();

			foreach (Match match in SourcePathPattern.Matches(File.ReadAllText(docPath)))
			{
				var path = match.Groups["path"].Value;
				if (!RepositoryContains(path))
				{
					missing.Add(path);
				}
			}

			Assert.True(missing.Count == 0,
				$"{relativeDocPath}: ścieżki plików źródłowych, których nie ma w repozytorium:{Environment.NewLine}  " +
				string.Join($"{Environment.NewLine}  ", missing.Distinct()));
		}

		[Fact]
		public void AdrIndex_ListsEveryDecisionRecord()
		{
			var adrDirectory = Path.Combine(Root, "docs", "adr");
			var indexContent = File.ReadAllText(Path.Combine(adrDirectory, "README.md"));

			var records = Directory.EnumerateFiles(adrDirectory, "*.md")
				.Select(Path.GetFileName)
				.Where(name => name != "README.md" && name != "0000-template.md")
				.OrderBy(name => name, StringComparer.Ordinal)
				.ToList();

			Assert.NotEmpty(records);

			var unlisted = records.Where(name => !indexContent.Contains(name!, StringComparison.Ordinal)).ToList();
			Assert.True(unlisted.Count == 0,
				"ADR bez wpisu w docs/adr/README.md: " + string.Join(", ", unlisted));
		}

		[Fact]
		public void AdrRecords_DeclareStatusAndDate()
		{
			var adrDirectory = Path.Combine(Root, "docs", "adr");
			var withoutHeader = new List<string>();

			foreach (var file in Directory.EnumerateFiles(adrDirectory, "*.md")
				         .Where(f => !Path.GetFileName(f).Equals("README.md", StringComparison.Ordinal)))
			{
				var content = File.ReadAllText(file);
				if (!content.Contains("- Status:", StringComparison.Ordinal) ||
				    !content.Contains("- Data:", StringComparison.Ordinal))
				{
					withoutHeader.Add(Path.GetFileName(file));
				}
			}

			Assert.True(withoutHeader.Count == 0,
				"ADR bez nagłówka Status/Data: " + string.Join(", ", withoutHeader));
		}
	}
}
