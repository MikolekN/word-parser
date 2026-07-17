using System.Collections.Generic;
using System.Text;
using ModelDto;
using ModelDto.EditorialUnits;

namespace WordParserCore.Tests
{
	/// <summary>
	/// Opisuje model dokumentu jako kanoniczny, tekstowy zrzut STRUKTURY + NUMERÓW + TREŚCI,
	/// z pominięciem pól proweniencyjnych (Guid, znaczniki czasu) i diagnostyki (pewność,
	/// komunikaty walidacji) — te ostatnie różnią się między ścieżką stylową a bezstylową.
	/// Dwa modele są równoważne, gdy ich opisy są identyczne.
	/// </summary>
	internal static class ModelEquivalenceComparer
	{
		public static string Describe(LegalDocument document)
		{
			var sb = new StringBuilder();
			foreach (var article in document.Articles)
			{
				sb.AppendLine($"art {Num(article.Number)} | {article.ContentText}");
				foreach (var paragraph in article.Paragraphs)
				{
					var ustLabel = paragraph.IsImplicit ? "*" : Num(paragraph.Number);
					sb.AppendLine($"  ust {ustLabel} | {paragraph.ContentText}");
					foreach (var point in paragraph.Points)
					{
						sb.AppendLine($"    pkt {Num(point.Number)} | {point.ContentText}");
						foreach (var letter in point.Letters)
						{
							sb.AppendLine($"      lit {Num(letter.Number)} | {letter.ContentText}");
							DescribeTirets(sb, letter.Tirets, depth: 4);
						}
					}
				}
			}
			return sb.ToString();
		}

		private static void DescribeTirets(StringBuilder sb, List<Tiret> tirets, int depth)
		{
			var indent = new string(' ', depth * 2);
			foreach (var tiret in tirets)
			{
				sb.AppendLine($"{indent}tir {Num(tiret.Number)} | {tiret.ContentText}");
				DescribeTirets(sb, tiret.Tirets, depth + 1);
			}
		}

		private static string Num(EntityNumber? number)
			=> string.IsNullOrEmpty(number?.Value) ? "-" : number!.Value;
	}
}
