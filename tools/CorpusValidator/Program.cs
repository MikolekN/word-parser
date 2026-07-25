using System.Text;
using Saga.Model;
using Saga.Core.Ingest;
using Saga.Core.Services.Classify.Document;

// ============================================================================
// CorpusValidator — walidacja DocumentClassifier na oznakowanym korpusie DOCX.
//
// Ground truth = nazwa folderu kategorii (USTAWY / ROZPORZADZENIA / OBWIESZCZENIA)
// w układzie <root>/<rok>/<KATEGORIA>/*.docx. Dla każdego pliku uruchamia
// DocxBlockReader -> DocumentClassifier i zestawia predykcję z etykietą folderu.
// Wypisuje dokładność wg kategorii, macierz pomyłek, flagi kontekstowe
// (IsConsolidatedText / IsAmending), przykłady błędów oraz zapisuje raport TXT + CSV.
//
// Użycie:
//   dotnet run --project tools/CorpusValidator [-- <root-korpusu> [<sciezka-raportu.txt>]]
// Domyślny korpus (gitignore'owany, lokalny): DocRepo/Akty ogłoszone (szczotki)
// ============================================================================

var corpusRoot = args.Length > 0
    ? args[0]
    : "DocRepo/Akty ogłoszone (szczotki)";
var reportPath = args.Length > 1 ? args[1] : "corpus-report.txt";
var csvPath = System.IO.Path.ChangeExtension(reportPath, ".csv");

if (!System.IO.Directory.Exists(corpusRoot))
{
    Console.Error.WriteLine($"Nie znaleziono katalogu korpusu: {corpusRoot}");
    Console.Error.WriteLine("Podaj ścieżkę jako pierwszy argument, np.:");
    Console.Error.WriteLine("  dotnet run --project tools/CorpusValidator -- \"DocRepo/Akty ogłoszone (szczotki)\"");
    return 1;
}

// folder kategorii -> oczekiwany "bucket"
static string GroundTruthBucket(string categoryFolder) => categoryFolder.ToUpperInvariant() switch
{
    "USTAWY" => "USTAWA",
    "ROZPORZADZENIA" => "ROZPORZADZENIE",
    "OBWIESZCZENIA" => "OBWIESZCZENIE",
    _ => "?",
};

// przewidziany ActType -> "bucket" do porównania z ground truth
static string PredictedBucket(DocumentClassificationResult r)
{
    if (!r.IsLegalAct || r.ActType is null) return "NIE-AKT";
    return r.ActType switch
    {
        LegalActType.Statute or LegalActType.AmendingStatute => "USTAWA",
        LegalActType.Regulation => "ROZPORZADZENIE",
        LegalActType.Announcement => "OBWIESZCZENIE",
        LegalActType.Resolution => "UCHWALA",
        LegalActType.ExecutiveOrder => "ZARZADZENIE",
        LegalActType.LocalLegalAct => "AKT-MIEJSC",
        LegalActType.Code => "KODEKS",
        _ => r.ActType.ToString()!,
    };
}

var reader = new DocxBlockReader();
var classifier = new DocumentClassifier();

var buckets = new[] { "USTAWA", "ROZPORZADZENIE", "OBWIESZCZENIE", "UCHWALA", "ZARZADZENIE", "AKT-MIEJSC", "KODEKS", "NIE-AKT" };

// confusion[groundTruth][predicted]
var confusion = new Dictionary<string, Dictionary<string, int>>();
foreach (var gt in new[] { "USTAWA", "ROZPORZADZENIE", "OBWIESZCZENIE" })
    confusion[gt] = buckets.ToDictionary(b => b, _ => 0);

var perCatTotal = new Dictionary<string, int>();
var perCatCorrect = new Dictionary<string, int>();
var perCatErrors = new Dictionary<string, int>();          // błędy odczytu docx
var perCatConsolidated = new Dictionary<string, int>();    // IsConsolidatedText
var perCatAmending = new Dictionary<string, int>();        // IsAmending
var confSum = new Dictionary<string, long>();

var misclass = new List<(string bucket, string file, string pred, int conf, bool cons, bool amend, string just)>();
var readErrors = new List<(string file, string err)>();
var csv = new StringBuilder("year,category,file,groundTruth,predicted,actType,isAct,confidence,isConsolidated,isAmending,blockCount\n");

var years = new[] { "2024", "2025", "2026" };
var categories = new[] { "USTAWY", "ROZPORZADZENIA", "OBWIESZCZENIA" };

int processed = 0;
foreach (var year in years)
{
    foreach (var cat in categories)
    {
        var dir = System.IO.Path.Combine(corpusRoot, year, cat);
        if (!System.IO.Directory.Exists(dir)) continue;
        var gt = GroundTruthBucket(cat);
        perCatTotal.TryAdd(gt, 0);
        perCatCorrect.TryAdd(gt, 0);
        perCatErrors.TryAdd(gt, 0);
        perCatConsolidated.TryAdd(gt, 0);
        perCatAmending.TryAdd(gt, 0);
        confSum.TryAdd(gt, 0);

        foreach (var file in System.IO.Directory.EnumerateFiles(dir, "*.docx").OrderBy(f => f))
        {
            processed++;
            if (processed % 200 == 0)
                Console.Error.WriteLine($"  ...{processed} plików");

            perCatTotal[gt]++;
            try
            {
                using var fs = System.IO.File.OpenRead(file);
                var blocks = reader.ReadBlocks(fs);
                var result = classifier.Classify(blocks);
                var pred = PredictedBucket(result);

                confusion[gt][pred] = confusion[gt].GetValueOrDefault(pred) + 1;
                confSum[gt] += result.Confidence;
                if (result.IsConsolidatedText) perCatConsolidated[gt]++;
                if (result.IsAmending) perCatAmending[gt]++;

                if (pred == gt) perCatCorrect[gt]++;
                else if (misclass.Count(m => m.bucket == gt) < 15)
                    misclass.Add((gt, System.IO.Path.GetFileName(file), pred, result.Confidence,
                        result.IsConsolidatedText, result.IsAmending,
                        result.Justification.Length > 120 ? result.Justification[..120] : result.Justification));

                csv.Append($"{year},{cat},\"{System.IO.Path.GetFileName(file)}\",{gt},{pred},{result.ActType},{result.IsLegalAct},{result.Confidence},{result.IsConsolidatedText},{result.IsAmending},{blocks.Count}\n");
            }
            catch (Exception ex)
            {
                perCatErrors[gt]++;
                if (readErrors.Count < 30)
                    readErrors.Add((System.IO.Path.GetFileName(file), ex.GetType().Name + ": " + ex.Message));
                csv.Append($"{year},{cat},\"{System.IO.Path.GetFileName(file)}\",{gt},READ-ERROR,,,,,,,\n");
            }
        }
    }
}

var sb = new StringBuilder();
void W(string s = "") { sb.AppendLine(s); Console.WriteLine(s); }

W("================================================================");
W(" WALIDACJA DocumentClassifier NA KORPUSIE AKTÓW OGŁOSZONYCH");
W("================================================================");
W($"Korpus: {corpusRoot}");
W($"Plików przetworzonych: {processed}");
W();

W("--- Dokładność wg kategorii (ground truth = folder) ---");
W($"{"Kategoria",-16} {"Plików",7} {"Trafień",8} {"Błędów odczytu",15} {"Dokładność",11} {"Śr. pewność",12}");
int totAll = 0, totCorrect = 0, totErr = 0;
foreach (var gt in new[] { "USTAWA", "ROZPORZADZENIE", "OBWIESZCZENIE" })
{
    var tot = perCatTotal.GetValueOrDefault(gt);
    var cor = perCatCorrect.GetValueOrDefault(gt);
    var err = perCatErrors.GetValueOrDefault(gt);
    var okReads = tot - err;
    var acc = okReads > 0 ? 100.0 * cor / okReads : 0;
    var avgConf = okReads > 0 ? (double)confSum.GetValueOrDefault(gt) / okReads : 0;
    totAll += tot; totCorrect += cor; totErr += err;
    W($"{gt,-16} {tot,7} {cor,8} {err,15} {acc,10:F1}% {avgConf,11:F1}");
}
var okReadsAll = totAll - totErr;
W($"{"RAZEM",-16} {totAll,7} {totCorrect,8} {totErr,15} {(okReadsAll > 0 ? 100.0 * totCorrect / okReadsAll : 0),10:F1}%");
W();

W("--- Macierz pomyłek (wiersz = prawda, kolumna = predykcja) ---");
var cols = new[] { "USTAWA", "ROZPORZADZENIE", "OBWIESZCZENIE", "UCHWALA", "ZARZADZENIE", "AKT-MIEJSC", "KODEKS", "NIE-AKT" };
W($"{"prawda\\pred",-16}" + string.Concat(cols.Select(c => c.Length > 8 ? c[..8].PadLeft(9) : c.PadLeft(9))));
foreach (var gt in new[] { "USTAWA", "ROZPORZADZENIE", "OBWIESZCZENIE" })
{
    var row = confusion[gt];
    W($"{gt,-16}" + string.Concat(cols.Select(c => row.GetValueOrDefault(c).ToString().PadLeft(9))));
}
W();

W("--- Flagi kontekstowe ---");
foreach (var gt in new[] { "USTAWA", "ROZPORZADZENIE", "OBWIESZCZENIE" })
    W($"{gt,-16} IsConsolidatedText={perCatConsolidated.GetValueOrDefault(gt),5}  IsAmending={perCatAmending.GetValueOrDefault(gt),5}");
W();

if (readErrors.Count > 0)
{
    W($"--- Błędy odczytu DOCX (pierwsze {readErrors.Count}) ---");
    foreach (var (file, err) in readErrors) W($"  {file}: {err}");
    W();
}

W("--- Przykłady błędnej klasyfikacji (do 15 na kategorię) ---");
foreach (var gt in new[] { "USTAWA", "ROZPORZADZENIE", "OBWIESZCZENIE" })
{
    var items = misclass.Where(m => m.bucket == gt).ToList();
    if (items.Count == 0) continue;
    W($"  [{gt}] — {items.Count} próbek:");
    foreach (var m in items)
        W($"    {m.file,-30} -> {m.pred,-14} conf={m.conf,3} cons={m.cons,-5} amend={m.amend,-5} | {m.just}");
    W();
}

System.IO.File.WriteAllText(reportPath, sb.ToString());
System.IO.File.WriteAllText(csvPath, csv.ToString());
Console.Error.WriteLine($"\nRaport: {reportPath}");
Console.Error.WriteLine($"CSV:    {csvPath}");
return 0;
