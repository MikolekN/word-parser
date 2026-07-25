using System;
using System.IO;
using System.Linq;
using System.Text;
using ModelDto;
using WordParserCore;
using WordParserCore.Ingest;
using WordParserCore.Ingest.Pdf;
using Xunit;

namespace WordParserCore.Tests
{
	/// <summary>
	/// Testy strażnicze decyzji architektonicznych z docs/adr/. Każdy pilnuje jednej decyzji,
	/// której odwrócenie nie zepsułoby builda — przez co dałoby się je wprowadzić niezauważenie.
	/// Czerwony test tutaj znaczy: albo zmiana jest niezamierzona, albo trzeba wystawić nowy ADR
	/// ze statusem Supersedes i świadomie usunąć asercję.
	///
	/// Decyzje pokryte testami stojącymi w innych miejscach (nie duplikujemy ich tutaj) są
	/// wskazane w sekcji „Weryfikacja" odpowiedniego ADR.
	/// </summary>
	public class ArchitectureDecisionTests
	{
		// ============================================================
		// ADR-0001 — reprezentacja pośrednia nie wycieka do ModelDto
		// ============================================================

		[Fact]
		public void Adr0001_ModelDto_DoesNotDependOnParserOrIntermediateRepresentation()
		{
			var modelDtoDirectory = Path.Combine(TestFiles.GetRepositoryRoot(), "ModelDto");

			var leaking = Directory.EnumerateFiles(modelDtoDirectory, "*.cs", SearchOption.AllDirectories)
				.Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") &&
				               !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
				.Where(file =>
				{
					var content = File.ReadAllText(file);
					return content.Contains("using WordParserCore", StringComparison.Ordinal) ||
					       content.Contains("DocumentBlock", StringComparison.Ordinal) ||
					       content.Contains("DocumentFormat.OpenXml", StringComparison.Ordinal);
				})
				.Select(Path.GetFileName)
				.ToList();

			Assert.True(leaking.Count == 0,
				"ModelDto musi zostać czystym modelem wyjściowym (ADR-0001). Pliki sięgające " +
				"do parsera, IR albo OpenXml: " + string.Join(", ", leaking));
		}

		// ============================================================
		// ADR-0002 — adaptery bezstylowe nie zgadują styleId
		// ============================================================

		[Fact]
		public void Adr0002_PdfBlocks_NeverCarryStyleId()
		{
			var pdf = new TestPdfBuilder().AddPage()
				.AddText("USTAWA", 57, 760)
				.AddText("z dnia 5 marca 2024 r.", 57, 740)
				.AddText("o ochronie danych osobowych", 57, 720)
				.AddText("Art. 1. Ustawa reguluje ochronę danych osobowych.", 57, 700)
				.AddText("Art. 2. Organem właściwym jest Prezes Urzędu.", 57, 680)
				.AddText("Art. 3. Ustawa wchodzi w życie po upływie 14 dni od dnia ogłoszenia.", 57, 660)
				.Build();

			using var stream = new MemoryStream(pdf);
			var blocks = new PdfBlockReader().ReadBlocks(stream);

			Assert.NotEmpty(blocks);
			// Styl zgadnięty z treści awansowałby heurystykę do rangi sygnału szablonu (ADR-0002).
			Assert.All(blocks, block => Assert.Null(block.StyleId));
		}

		// ============================================================
		// ADR-0004 — brak zależności PDF na licencji AGPL
		// ============================================================

		[Fact]
		public void Adr0004_NoAgplPdfLibraryReferenced()
		{
			var repositoryRoot = TestFiles.GetRepositoryRoot();
			string[] agplPackages = { "itext", "iTextSharp" };

			var offending = Directory.EnumerateFiles(repositoryRoot, "*.csproj", SearchOption.AllDirectories)
				.Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
				.Select(file => (Name: Path.GetFileName(file), Content: File.ReadAllText(file)))
				.Where(project => agplPackages.Any(package =>
					project.Content.Contains($"\"{package}", StringComparison.OrdinalIgnoreCase)))
				.Select(project => project.Name)
				.ToList();

			Assert.True(offending.Count == 0,
				"Biblioteka PDF na licencji AGPL rozciągnęłaby wymóg udostępnienia źródeł na usługę " +
				"sieciową RCL (ADR-0004). Projekty z taką zależnością: " + string.Join(", ", offending));
		}

		// ============================================================
		// ADR-0007 — klasyfikacja nie mutuje LegalDocument.Type
		// ============================================================

		[Fact]
		public void Adr0007_RecognizedActKind_DoesNotOverwriteDocumentType()
		{
			// Rozporządzenie rozpoznawane przez klasyfikator (por. DocumentClassifierTests.Classify_Regulation_IsRecognized).
			string[] regulationLines =
			{
				"ROZPORZĄDZENIE MINISTRA FINANSÓW",
				"z dnia 12 czerwca 2023 r.",
				"w sprawie szczegółowych zasad rachunkowości",
				"Na podstawie art. 50 ust. 1 ustawy z dnia 29 września 1994 r. o rachunkowości zarządza się, co następuje:",
				"§ 1. Rozporządzenie określa zasady rachunkowości.",
				"§ 2. Ilekroć w rozporządzeniu jest mowa o jednostce, rozumie się przez to podmiot.",
				"§ 3. Rozporządzenie wchodzi w życie z dniem 1 stycznia 2024 r.",
			};

			using var stream = new MemoryStream(new UTF8Encoding(false).GetBytes(string.Join("\n", regulationLines)));

			var result = LegalDocumentParser.Parse(stream, "rozporzadzenie.txt");

			// Klasyfikacja rozpoznaje rodzaj…
			Assert.Equal(LegalActType.Regulation, result.Classification.ActType);
			Assert.NotNull(result.Document);

			// …ale Type pozostaje deklaracją wywołującego, nie obserwacją potoku (ADR-0007).
			Assert.Equal(LegalActType.Statute, result.Document!.Type);
			Assert.Same(result.Classification, result.Document.Classification);
		}
	}
}
