using System.IO.Packaging;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using ModelDto;
using ModelDto.EditorialUnits;
using Serilog;
using WordParserCore;

namespace WordParser
{
    class Program
    {
        static void Main(string[] args)
        {
            LoggerConfig.ConfigureLogger();

            try
            {
            if (args.Length < 2)
            {
                Console.WriteLine("Użycie: WordParser --docx <nazwa-pliku>");
                return;
            }

            string option = args[0];
            string filePath = args[1];

            if (!File.Exists(filePath))
            {
                Console.WriteLine("Plik nie istnieje.");
                return;
            } else
            {
                string directoryName = Path.GetDirectoryName(filePath) ?? string.Empty;
                if (directoryName == null)
                {
                    Console.WriteLine("Nie można uzyskać katalogu z podanej ścieżki pliku.");
                    return;
                }
                string backupDirectory = directoryName;

                string backupFileName = Path.GetFileNameWithoutExtension(filePath) + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + Path.GetExtension(filePath);
                string backupFilePath = Path.Combine(backupDirectory, backupFileName);

                File.Copy(filePath, backupFilePath);
                filePath = backupFilePath;

                if (option == "--docx")
                {
                    var document = LegalDocumentParser.Parse(filePath);
                    PrintDocument(document);
                }
            
                // using (WordprocessingDocument wordDoc = WordprocessingDocument.Open(filePath, true))
                // {
                //     wordDoc.CompressionOption = CompressionOption.Maximum;
                    
                //     {
                //         var legalAct = new WordParserCore.LegalAct(wordDoc);

                //         // Usuwanie komentarzy autora 'System'
                //         legalAct.CommentManager.RemoveSystemComments();

                //         if (option == "--hyperlinks")
                //         {
                //             int commentCount = legalAct.DocumentProcessor.ParseHyperlinks();
                //             Console.WriteLine($"Liczba dodanych komentarzy: {commentCount}");
                //         }
                //         else if (option == "--formatting")
                //         {
                //             legalAct.DocumentProcessor.CleanParagraphProperties();
                //             legalAct.DocumentProcessor.MergeRuns();
                //             legalAct.DocumentProcessor.MergeTexts();
                //         }
                //         else if (option == "--generatexml")
                //         {
                //             legalAct.XmlGenerator.Generate();
                //             //legalAct.SaveAmendmentList();
                //             legalAct.CommentManager.CommentErrors(legalAct);
                //         }
                //         else if (option == "--createAmendmentsTable")
                //         {
                //             using var stream = legalAct.XlsxGenerator.GenerateXlsx();
                //             string xlsxFileName = Path.GetFileNameWithoutExtension(filePath) + "_amendments.xlsx";
                //             string xlsxFilePath = Path.Combine(directoryName, xlsxFileName);

                //             using (var fileStream = File.Create(xlsxFilePath))
                //             {
                //                 stream.CopyTo(fileStream);
                //             }

                //             Console.WriteLine($"Utworzono plik: {xlsxFilePath}");
                //         }
                //         else
                //         {
                //             Console.WriteLine("Nieznany przełącznik. Użycie: WordParser --hyperlinks|--formatting <nazwa-pliku>");
                //         }
                //         string newFileName = Path.GetFileName(filePath);
                        
                //         string copiesDirectory = Path.Combine(directoryName, "kopie");
                //         if (!Directory.Exists(copiesDirectory))
                //         {
                //             Directory.CreateDirectory(copiesDirectory);
                //         }
                //         string newFilePath = Path.Combine(copiesDirectory, newFileName);

                //         // Zapisz dokument pod nową nazwą
                //         legalAct.SaveAs(newFilePath);
                //     }
                // }
                // System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                // {
                //     FileName = filePath,
                //     UseShellExecute = true
                // });
            }
            //Console.ReadLine(); 
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

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

        private static void PrintParagraph(ModelDto.EditorialUnits.Paragraph paragraph)
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
}
