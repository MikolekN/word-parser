using DocumentFormat.OpenXml.Packaging;
using Saga.Model;
using Saga.Model.EditorialUnits;
using Serilog;
using Saga.Core;
using Saga.Core.Exceptions;
using Saga.Core.Ingest;
using Saga.Core.Services.Converters;

namespace Saga.Cli
{
    class Program
    {
        // Kody wyjścia: 0 = OK; 1 = błąd wejścia/parsowania; 2 = dokument nierozpoznany jako akt prawny (bez --force).
        private const int ExitOk = 0;
        private const int ExitError = 1;
        private const int ExitNotLegalAct = 2;

        static int Main(string[] args)
        {
            LoggerConfig.ConfigureLogger();

            try
            {
                return Run(args);
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        private static int Run(string[] args)
        {
            var cli = CliArguments.Parse(args, out string? argumentError);
            if (argumentError != null || cli is null)
            {
                if (argumentError != null)
                {
                    Console.WriteLine(argumentError);
                }
                PrintUsage();
                return ExitError;
            }

            if (!File.Exists(cli.FilePath))
            {
                Console.WriteLine($"Plik nie istnieje: {cli.FilePath}");
                return ExitError;
            }

            string filePath = cli.FilePath;
            if (cli.LegacyDocxMode)
            {
                // Tryb legacy --docx: zachowana dotychczasowa kopia zapasowa obok pliku
                // (historycznie parser modyfikował dokument). Nowe ścieżki czytają read-only, bez kopii.
                var backupPath = CreateLegacyBackup(filePath);
                if (backupPath is null)
                {
                    return ExitError;
                }
                filePath = backupPath;
            }

            var options = new ParseOptions
            {
                // Tryb legacy nigdy nie klasyfikował — parsuje zawsze (nie łamiemy istniejących skryptów).
                Policy = cli.Force || cli.LegacyDocxMode ? ParsePolicy.AlwaysParse : ParsePolicy.ParseWhenLegalAct,
                ForcedFormat = cli.Format ?? (cli.LegacyDocxMode ? SourceFormat.Docx : null),
            };

            ParseResult result;
            try
            {
                result = LegalDocumentParser.Parse(filePath, options);
            }
            catch (ScannedPdfException ex)
            {
                Console.WriteLine($"PDF bez użytecznej warstwy tekstowej (skan?): {ex.Message}");
                return ExitError;
            }
            catch (UnsupportedDocumentFormatException ex)
            {
                Console.WriteLine(ex.Message);
                return ExitError;
            }
            catch (OpenXmlPackageException ex)
            {
                Console.WriteLine($"Plik nie jest poprawnym dokumentem Word (DOCX): {ex.Message}");
                return ExitError;
            }
            catch (ParsingException ex)
            {
                Console.WriteLine($"Błąd parsowania: {ex.Message}");
                return ExitError;
            }
            catch (IOException ex)
            {
                Console.WriteLine($"Błąd odczytu pliku: {ex.Message}");
                return ExitError;
            }
            catch (UnauthorizedAccessException ex)
            {
                Console.WriteLine($"Brak uprawnień do odczytu pliku: {ex.Message}");
                return ExitError;
            }

            // Tryb legacy zachowuje dotychczasowy format stdout (skrypty diffujące/grepujące
            // wyjście --docx) — raport klasyfikacji drukują tylko nowe wywołania.
            if (!cli.LegacyDocxMode)
            {
                PrintClassificationReport(result);
            }

            if (result.Document is null)
            {
                Console.WriteLine();
                Console.WriteLine("Dokument nie został sparsowany. Użyj --force, aby sparsować mimo wszystko.");
                return ExitNotLegalAct;
            }

            if (!cli.LegacyDocxMode)
            {
                Console.WriteLine();
            }
            PrintDocument(result.Document);

            if (cli.DumpPath != null)
            {
                return WriteDump(result.Document, cli.DumpPath);
            }

            return ExitOk;
        }

        private static void PrintUsage()
        {
            Console.WriteLine("Użycie:");
            Console.WriteLine("  saga <plik> [--format docx|pdf|txt] [--force] [--dump <plik.xml>]");
            Console.WriteLine("  saga --docx <plik> [--dump <plik.xml>]   (tryb legacy: kopia zapasowa + parsowanie bez klasyfikacji)");
            Console.WriteLine();
            Console.WriteLine("Kody wyjścia: 0 = OK; 1 = błąd; 2 = dokument nierozpoznany jako akt prawny (bez --force).");
        }

        /// <summary>Kopia zapasowa w trybie legacy --docx; zwraca ścieżkę kopii albo null przy błędzie.</summary>
        private static string? CreateLegacyBackup(string filePath)
        {
            string directoryName = Path.GetDirectoryName(filePath) ?? string.Empty;

            string backupFileName = Path.GetFileNameWithoutExtension(filePath) + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + Path.GetExtension(filePath);
            string backupFilePath = Path.Combine(directoryName, backupFileName);

            try
            {
                // overwrite: dwa uruchomienia w tej samej sekundzie (np. skrypt diffujący --dump przed/po)
                // dają identyczną nazwę kopii — bez nadpisania drugie kończyłoby się IOException
                File.Copy(filePath, backupFilePath, true);
                return backupFilePath;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Console.WriteLine($"Nie udało się utworzyć kopii zapasowej '{backupFilePath}': {ex.Message}");
                return null;
            }
        }

        private static int WriteDump(LegalDocument document, string dumpPath)
        {
            try
            {
                File.WriteAllText(dumpPath, CanonicalDtoXmlSerializer.Serialize(document));
                Console.WriteLine($"Zapisano kanoniczny zrzut modelu: {dumpPath}");
                return ExitOk;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DirectoryNotFoundException)
            {
                Console.WriteLine($"Nie udało się zapisać zrzutu modelu do '{dumpPath}': {ex.Message}");
                return ExitError;
            }
        }

        // ============================================================
        // Raport klasyfikacji
        // ============================================================

        private static void PrintClassificationReport(ParseResult result)
        {
            var c = result.Classification;

            Console.WriteLine("── Klasyfikacja dokumentu ──");
            Console.WriteLine($"Format źródłowy: {FormatLabel(result.SourceFormat)}, bloków: {result.BlockCount}");
            Console.WriteLine(c.IsLegalAct
                ? $"Rozpoznano akt prawny: {c.ActType?.ToFriendlyString() ?? "rodzaj nieokreślony"} (pewność {c.Confidence}/100)"
                : $"Nie rozpoznano aktu prawnego (pewność {c.Confidence}/100)");

            if (c.IsConsolidatedText)
            {
                Console.WriteLine("Tekst jednolity: tak");
            }
            if (c.IsAmending)
            {
                Console.WriteLine("Akt zmieniający: tak");
            }

            Console.WriteLine(c.Justification);

            const int maxSignals = 8;
            foreach (var signal in c.Signals.Take(maxSignals))
            {
                string evidence = string.IsNullOrEmpty(signal.MatchedText) ? string.Empty : $" — „{signal.MatchedText}”";
                Console.WriteLine($"  [{signal.Score,3}] {signal.Description}{evidence}");
            }
            if (c.Signals.Count > maxSignals)
            {
                Console.WriteLine($"  … oraz {c.Signals.Count - maxSignals} dalszych sygnałów.");
            }
        }

        private static string FormatLabel(SourceFormat format) => format switch
        {
            SourceFormat.Docx => "DOCX",
            SourceFormat.Pdf => "PDF",
            SourceFormat.PlainText => "TXT",
            _ => "nieznany",
        };

        // ============================================================
        // Wydruk modelu dokumentu
        // ============================================================

        private static void PrintDocument(LegalDocument document)
        {
            Console.WriteLine($"{document.Type.ToFriendlyString().ToUpper()}: {document.Title} ({document.SourceJournal})");

            bool isFirst = true;
            foreach (var article in document.Articles)
            {
                if (!isFirst)
                {
                    Console.WriteLine();
                }

                PrintArticle(article);
                isFirst = false;
            }
        }

        private static void PrintArticle(Article article)
        {
            // Nie pokazujmy treści artykułu - jest nią treść pierwszego ustępu
            // Zamiast tego pokażmy informację o tym, czy artykuł jest nowelizujący i ewentualnie jego publikator (Dz. U.)
            Console.WriteLine($"  [{article.Id}] " + (article.IsAmending ? "artykuł zmieniający akt: " + article.Journals.FirstOrDefault()?.ToString() : string.Empty));

            foreach (var paragraph in article.Paragraphs)
            {
                PrintParagraph(paragraph);
            }
        }

        private static void PrintParagraph(Saga.Model.EditorialUnits.Paragraph paragraph)
        {
            PrintEntityLine(paragraph, "    ");
            PrintCommonParts(paragraph.CommonParts, "      ", paragraph);

            foreach (var point in paragraph.Points)
            {
                PrintPoint(point);
            }

            PrintAmendments(paragraph, "      ");
        }

        private static void PrintPoint(Point point)
        {
            PrintEntityLine(point, "      ");
            PrintCommonParts(point.CommonParts, "        ", point);

            foreach (var letter in point.Letters)
            {
                PrintLetter(letter);
            }

            PrintAmendments(point, "        ");
        }

        private static void PrintLetter(Letter letter)
        {
            PrintEntityLine(letter, "        ");
            PrintCommonParts(letter.CommonParts, "          ", letter);

            foreach (var tiret in letter.Tirets)
            {
                PrintTiret(tiret);
            }

            PrintAmendments(letter, "          ");
        }

        private static void PrintTiret(Tiret tiret)
        {
            PrintEntityLine(tiret, "          ");

            foreach (var nestedTiret in tiret.Tirets)
            {
                PrintTiret(nestedTiret);
            }

            PrintAmendments(tiret, "            ");
        }

        private static void PrintEntityLine(BaseEntity entity, string indent)
        {
            // Gdy encja ma wiele segmentow, wyswietl je rozdzielone
            if (entity is IHasTextSegments hasSegments && hasSegments.TextSegments.Count > 1)
            {
                Console.WriteLine($"{indent}[{entity.Id}]");
                foreach (var segment in hasSegments.TextSegments)
                {
                    var roleTag = !string.IsNullOrEmpty(segment.Role) ? $" ({segment.Role})" : string.Empty;
                    Console.WriteLine($"{indent}  zd. {segment.Order}: {segment.Text}{roleTag}");
                }
            }
            else
            {
                string contentPreview = GetContentPreview(entity.ContentText, 48);
                if (string.IsNullOrWhiteSpace(contentPreview))
                {
                    Console.WriteLine($"{indent}[{entity.Id}]");
                }
                else
                {
                    Console.WriteLine($"{indent}[{entity.Id}] {contentPreview}");
                }
            }

            if (entity.ValidationMessages.Count == 0)
            {
                return;
            }

            foreach (var message in entity.ValidationMessages)
            {
                Console.WriteLine($"{indent}{message}");
            }
        }

        private static void PrintCommonParts(List<CommonPart> commonParts, string indent, BaseEntity parent)
        {
            foreach (var cp in commonParts)
            {
                if (cp.Type == CommonPartType.Intro)
                {
                    // Intro: nie duplikujemy treści, tylko pokazujemy powiązanie z segmentem rodzica
                    var segmentInfo = cp.SourceSegmentOrder.HasValue
                        ? $"segment {cp.SourceSegmentOrder} z [{parent.Id}]"
                        : $"[{parent.Id}]";
                    Console.WriteLine($"{indent}├─ wpr. do wyl. - {segmentInfo}");
                }
                else
                {
                    // WrapUp: pokazujemy treść (to osobny akapit)
                    string preview = GetContentPreview(cp.ContentText, 48);
                    Console.WriteLine($"{indent}└─ cz. wsp. {preview}");
                }
            }
        }

        private static void PrintAmendments(BaseEntity entity, string indent)
        {
            if (entity is not IHasAmendments { Amendment: { } amendment })
            {
                return;
            }

            var opLabel = amendment.OperationType switch
            {
                AmendmentOperationType.Repeal => "uchylenie",
                AmendmentOperationType.Insertion => "dodanie",
                AmendmentOperationType.Modification => "zmiana brzmienia",
                AmendmentOperationType.Error => "błąd",
                _ => "nieznany"
            };

            var targetAct = amendment.TargetLegalAct;
            var targetActStr = targetAct.Positions.Count > 0
                ? $"DU.{targetAct.Year}.{string.Join(",", targetAct.Positions)}"
                : "brak publikatora";

            Console.WriteLine($"{indent}╔═ {opLabel} w akcie: {targetActStr}");

            foreach (var target in amendment.Targets)
            {
                Console.WriteLine($"{indent}║  Cel: {target}");
            }

            if (amendment.Content != null)
            {
                PrintAmendmentContent(amendment.Content, indent);
            }

            if (amendment.EffectiveDate.HasValue)
            {
                Console.WriteLine($"{indent}║  Wejście w życie: {amendment.EffectiveDate.Value:yyyy-MM-dd}");
            }

            Console.WriteLine($"{indent}╚══════════════════════════════════════");
        }

        private static void PrintAmendmentContent(AmendmentContent content, string indent)
        {
            string cIndent = indent + "║  ";

            if (!string.IsNullOrEmpty(content.PlainText))
            {
                Console.WriteLine($"{cIndent}Treść: {GetContentPreview(content.PlainText, 60)}");
                return;
            }

            // Drukuj hierarchiczną treść nowelizacji
            foreach (var article in content.Articles)
            {
                Console.WriteLine($"{cIndent}[{article.Id}]");
                foreach (var paragraph in article.Paragraphs)
                {
                    PrintAmendmentEntity(paragraph, cIndent + "  ");
                    foreach (var point in paragraph.Points)
                    {
                        PrintAmendmentEntity(point, cIndent + "    ");
                        foreach (var letter in point.Letters)
                        {
                            PrintAmendmentEntity(letter, cIndent + "      ");
                            foreach (var tiret in letter.Tirets)
                            {
                                PrintAmendmentTiret(tiret, cIndent + "        ");
                            }
                        }
                    }
                }
            }

            foreach (var paragraph in content.Paragraphs)
            {
                PrintAmendmentEntity(paragraph, cIndent);
                foreach (var point in paragraph.Points)
                {
                    PrintAmendmentEntity(point, cIndent + "  ");
                }
            }

            foreach (var point in content.Points)
            {
                PrintAmendmentEntity(point, cIndent);
                foreach (var letter in point.Letters)
                {
                    PrintAmendmentEntity(letter, cIndent + "  ");
                }
            }

            foreach (var letter in content.Letters)
            {
                PrintAmendmentEntity(letter, cIndent);
                foreach (var tiret in letter.Tirets)
                {
                    PrintAmendmentTiret(tiret, cIndent + "  ");
                }
            }

            foreach (var tiret in content.Tirets)
            {
                PrintAmendmentTiret(tiret, cIndent);
            }

            foreach (var cp in content.CommonParts)
            {
                var cpLabel = cp.Type == CommonPartType.Intro ? "wpr. do wyl." : "cz. wsp.";
                Console.WriteLine($"{cIndent}{cpLabel}: {GetContentPreview(cp.ContentText, 48)}");
            }
        }

        private static void PrintAmendmentEntity(BaseEntity entity, string indent)
        {
            string preview = GetContentPreview(entity.ContentText, 48);
            if (string.IsNullOrWhiteSpace(preview))
            {
                Console.WriteLine($"{indent}[{entity.Id}]");
            }
            else
            {
                Console.WriteLine($"{indent}[{entity.Id}] {preview}");
            }
        }

        private static void PrintAmendmentTiret(Tiret tiret, string indent)
        {
            PrintAmendmentEntity(tiret, indent);
            foreach (var nested in tiret.Tirets)
            {
                PrintAmendmentTiret(nested, indent + "  ");
            }
        }

        private static string GetContentPreview(string content, int maxLength)
        {
            if (string.IsNullOrEmpty(content))
            {
                return string.Empty;
            }

            return content.Length <= maxLength ? content : content.Substring(0, maxLength);
        }
    }

    /// <summary>Argumenty CLI. Formy: „&lt;plik&gt; [--format …] [--force] [--dump …]" oraz legacy „--docx &lt;plik&gt;".</summary>
    sealed class CliArguments
    {
        public required string FilePath { get; init; }
        public bool LegacyDocxMode { get; init; }
        public bool Force { get; init; }
        public SourceFormat? Format { get; init; }
        public string? DumpPath { get; init; }

        public static CliArguments? Parse(string[] args, out string? error)
        {
            error = null;

            string? filePath = null;
            bool legacyDocx = false;
            bool force = false;
            SourceFormat? format = null;
            string? dumpPath = null;

            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--docx":
                        legacyDocx = true;
                        break;

                    case "--force":
                        force = true;
                        break;

                    case "--format":
                        if (++i >= args.Length)
                        {
                            error = "Brak wartości po --format (oczekiwane: docx, pdf lub txt).";
                            return null;
                        }
                        format = args[i].ToLowerInvariant() switch
                        {
                            "docx" => SourceFormat.Docx,
                            "pdf" => SourceFormat.Pdf,
                            "txt" => SourceFormat.PlainText,
                            _ => null,
                        };
                        if (format is null)
                        {
                            error = $"Nieznany format '{args[i]}' (oczekiwane: docx, pdf lub txt).";
                            return null;
                        }
                        break;

                    case "--dump":
                        if (++i >= args.Length)
                        {
                            error = "Brak ścieżki pliku po --dump.";
                            return null;
                        }
                        dumpPath = args[i];
                        break;

                    default:
                        if (args[i].StartsWith("--", StringComparison.Ordinal))
                        {
                            error = $"Nieznany przełącznik: {args[i]}";
                            return null;
                        }
                        if (filePath != null)
                        {
                            error = $"Nadmiarowy argument: {args[i]} (plik został już wskazany: {filePath}).";
                            return null;
                        }
                        filePath = args[i];
                        break;
                }
            }

            if (filePath is null)
            {
                error = args.Length == 0 ? null : "Nie wskazano pliku do sparsowania.";
                return null;
            }

            return new CliArguments
            {
                FilePath = filePath,
                LegacyDocxMode = legacyDocx,
                Force = force,
                Format = format,
                DumpPath = dumpPath,
            };
        }
    }
}
