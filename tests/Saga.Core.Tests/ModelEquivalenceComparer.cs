using System.Collections.Generic;
using System.Linq;
using System.Text;
using ModelDto;
using ModelDto.EditorialUnits;

namespace WordParserCore.Tests
{
	/// <summary>
	/// Opisuje model dokumentu jako kanoniczny, tekstowy zrzut STRUKTURY + NUMERÓW + TREŚCI
	/// (wraz z nowelizacjami: operacja, typ obiektu, cele, treść), z pominięciem pól
	/// proweniencyjnych (Guid, znaczniki czasu) i diagnostyki (pewność, komunikaty walidacji) —
	/// te ostatnie różnią się między ścieżką stylową a bezstylową.
	/// Dwa modele są równoważne, gdy ich opisy są identyczne.
	/// </summary>
	internal static class ModelEquivalenceComparer
	{
		public static string Describe(LegalDocument document)
		{
			var sb = new StringBuilder();
			foreach (var article in document.Articles)
			{
				// eId (a nie sam numer) — koduje ścieżkę jednostek systematyzacyjnych (rozd_1__art_5),
				// więc rozjazd umiejscowienia artykułu między ścieżkami jest widoczny dla ekwiwalencji.
				sb.AppendLine($"art {Num(article.Number)} eId={article.Id} | {article.ContentText}");
				DescribeParagraphs(sb, article.Paragraphs, depth: 1);
			}
			return sb.ToString();
		}

		private static void DescribeParagraphs(StringBuilder sb, List<Paragraph> paragraphs, int depth)
		{
			var indent = new string(' ', depth * 2);
			foreach (var paragraph in paragraphs)
			{
				var ustLabel = paragraph.IsImplicit ? "*" : Num(paragraph.Number);
				sb.AppendLine($"{indent}ust {ustLabel} | {paragraph.ContentText}");
				DescribeCommonParts(sb, paragraph.CommonParts, depth + 1);
				DescribeAmendment(sb, paragraph.Amendment, depth + 1);
				DescribePoints(sb, paragraph.Points, depth + 1);
			}
		}

		private static void DescribePoints(StringBuilder sb, List<Point> points, int depth)
		{
			var indent = new string(' ', depth * 2);
			foreach (var point in points)
			{
				sb.AppendLine($"{indent}pkt {Num(point.Number)} | {point.ContentText}");
				DescribeCommonParts(sb, point.CommonParts, depth + 1);
				DescribeAmendment(sb, point.Amendment, depth + 1);
				DescribeLetters(sb, point.Letters, depth + 1);
			}
		}

		private static void DescribeLetters(StringBuilder sb, List<Letter> letters, int depth)
		{
			var indent = new string(' ', depth * 2);
			foreach (var letter in letters)
			{
				sb.AppendLine($"{indent}lit {Num(letter.Number)} | {letter.ContentText}");
				DescribeCommonParts(sb, letter.CommonParts, depth + 1);
				DescribeAmendment(sb, letter.Amendment, depth + 1);
				DescribeTirets(sb, letter.Tirets, depth + 1);
			}
		}

		/// <summary>
		/// Części wspólne encji (intro/wrapUp) — treść prawna, która żyje WYŁĄCZNIE w CommonParts,
		/// musi uczestniczyć w ekwiwalencji (inaczej zgubiona część wspólna przechodzi niewykryta).
		/// </summary>
		private static void DescribeCommonParts(StringBuilder sb, List<CommonPart> commonParts, int depth)
		{
			var indent = new string(' ', depth * 2);
			foreach (var commonPart in commonParts)
				sb.AppendLine($"{indent}czwsp {commonPart.Type} | {commonPart.ContentText}");
		}

		private static void DescribeTirets(StringBuilder sb, List<Tiret> tirets, int depth)
		{
			var indent = new string(' ', depth * 2);
			foreach (var tiret in tirets)
			{
				sb.AppendLine($"{indent}tir {Num(tiret.Number)} | {tiret.ContentText}");
				DescribeAmendment(sb, tiret.Amendment, depth + 1);
				DescribeTirets(sb, tiret.Tirets, depth + 1);
			}
		}

		/// <summary>
		/// Nowelizacja przypisana do encji: operacja, typ obiektu, cele, publikator i treść
		/// (rekurencyjnie — jednostki treści nowelizacji tym samym formatem co ustawa matka).
		/// </summary>
		private static void DescribeAmendment(StringBuilder sb, Amendment? amendment, int depth)
		{
			if (amendment == null)
				return;

			var indent = new string(' ', depth * 2);
			var targets = string.Join("; ", amendment.Targets.Select(t => t.ToString()));
			var journal = amendment.TargetLegalAct;
			var journalDesc = (journal.Year is > 0) || journal.Positions.Count > 0
				? $"{journal.Year ?? 0}/{string.Join(",", journal.Positions)}"
				: "-";
			var effective = amendment.EffectiveDate?.ToString("yyyy-MM-dd") ?? "-";
			sb.AppendLine($"{indent}AMENDMENT {amendment.OperationType} obj={amendment.Content?.ObjectType.ToString() ?? "brak"} " +
				$"targets=[{targets}] journal={journalDesc} effective={effective}");

			if (amendment.Content == null)
				return;

			if (!string.IsNullOrEmpty(amendment.Content.PlainText))
				sb.AppendLine($"{indent}  plaintext | {amendment.Content.PlainText}");

			foreach (var article in amendment.Content.Articles)
			{
				sb.AppendLine($"{indent}  art {Num(article.Number)} | {article.ContentText}");
				DescribeParagraphs(sb, article.Paragraphs, depth + 2);
			}
			DescribeParagraphs(sb, amendment.Content.Paragraphs, depth + 1);
			DescribePoints(sb, amendment.Content.Points, depth + 1);
			DescribeLetters(sb, amendment.Content.Letters, depth + 1);
			DescribeTirets(sb, amendment.Content.Tirets, depth + 1);
			foreach (var commonPart in amendment.Content.CommonParts)
				sb.AppendLine($"{indent}  czwsp | {commonPart.ContentText}");
		}

		private static string Num(EntityNumber? number)
			=> string.IsNullOrEmpty(number?.Value) ? "-" : number!.Value;
	}
}
