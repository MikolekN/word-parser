using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using ModelDto;
using ModelDto.EditorialUnits;
using ModelDto.SystematizingUnits;

#nullable enable

namespace WordParserCore.Services.Converters
{
	/// <summary>
	/// Kanoniczna, deterministyczna serializacja LegalDocument do XML.
	/// Przeznaczenie: testy snapshotowe (golden) i diagnostyczny zrzut CLI (--dump).
	/// Celowo pomija pola niedeterministyczne (Guid, CreatedAt/UpdatedAt, znaczniki czasu
	/// komunikatów walidacji) — dwa przebiegi parsera na tym samym pliku muszą dawać
	/// identyczny bajt-w-bajt wynik.
	/// </summary>
	public static class CanonicalDtoXmlSerializer
	{
		public static string Serialize(LegalDocument document)
		{
			var root = new XElement("legalDocument",
				new XAttribute("type", document.Type.ToString()));

			if (!string.IsNullOrEmpty(document.Title))
				root.Add(new XAttribute("title", document.Title));

			AddJournalElement(root, "sourceJournal", document.SourceJournal);

			root.Add(SerializePart(document.RootPart));

			return ToCanonicalString(root);
		}

		// ============================================================
		// Jednostki systematyzacyjne
		// ============================================================

		private static XElement SerializePart(Part part)
		{
			var el = CreateUnitElement("part", part, part.Heading, part.IsImplicit);
			foreach (var book in part.Books)
				el.Add(SerializeBook(book));
			return el;
		}

		private static XElement SerializeBook(Book book)
		{
			var el = CreateUnitElement("book", book, book.Heading, book.IsImplicit);
			foreach (var title in book.Titles)
				el.Add(SerializeTitle(title));
			return el;
		}

		private static XElement SerializeTitle(Title title)
		{
			var el = CreateUnitElement("title", title, title.Heading, title.IsImplicit);
			foreach (var division in title.Divisions)
				el.Add(SerializeDivision(division));
			return el;
		}

		private static XElement SerializeDivision(Division division)
		{
			var el = CreateUnitElement("division", division, division.Heading, division.IsImplicit);
			foreach (var chapter in division.Chapters)
				el.Add(SerializeChapter(chapter));
			return el;
		}

		private static XElement SerializeChapter(Chapter chapter)
		{
			var el = CreateUnitElement("chapter", chapter, chapter.Heading, chapter.IsImplicit);
			foreach (var subchapter in chapter.Subchapters)
				el.Add(SerializeSubchapter(subchapter));
			return el;
		}

		private static XElement SerializeSubchapter(Subchapter subchapter)
		{
			var el = CreateUnitElement("subchapter", subchapter, subchapter.Heading, subchapter.IsImplicit);
			foreach (var article in subchapter.Articles)
				el.Add(SerializeArticle(article));
			return el;
		}

		private static XElement CreateUnitElement(string name, BaseEntity entity, string heading, bool isImplicit)
		{
			var el = new XElement(name);
			if (isImplicit)
				el.Add(new XAttribute("implicit", true));
			AddEntityCore(el, entity);
			if (!string.IsNullOrEmpty(heading))
				el.Add(new XAttribute("heading", heading));
			AddContentAndDiagnostics(el, entity);
			return el;
		}

		// ============================================================
		// Jednostki redakcyjne
		// ============================================================

		private static XElement SerializeArticle(Article article)
		{
			var el = new XElement("article");
			AddEntityCore(el, article);

			foreach (var journal in article.Journals)
				AddJournalElement(el, "journal", journal);

			AddContentAndDiagnostics(el, article);

			foreach (var paragraph in article.Paragraphs)
				el.Add(SerializeParagraph(paragraph));

			return el;
		}

		private static XElement SerializeParagraph(Paragraph paragraph)
		{
			var el = new XElement("paragraph");
			if (paragraph.IsImplicit)
				el.Add(new XAttribute("implicit", true));
			AddEntityCore(el, paragraph);
			if (!string.IsNullOrEmpty(paragraph.Role))
				el.Add(new XAttribute("role", paragraph.Role));

			AddContentAndDiagnostics(el, paragraph);
			AddTextSegments(el, paragraph.TextSegments);
			AddCommonParts(el, paragraph.CommonParts);

			foreach (var point in paragraph.Points)
				el.Add(SerializePoint(point));

			AddAmendment(el, paragraph.Amendment);
			return el;
		}

		private static XElement SerializePoint(Point point)
		{
			var el = new XElement("point");
			AddEntityCore(el, point);

			AddContentAndDiagnostics(el, point);
			AddTextSegments(el, point.TextSegments);
			AddCommonParts(el, point.CommonParts);

			foreach (var letter in point.Letters)
				el.Add(SerializeLetter(letter));

			AddAmendment(el, point.Amendment);
			return el;
		}

		private static XElement SerializeLetter(Letter letter)
		{
			var el = new XElement("letter");
			AddEntityCore(el, letter);

			AddContentAndDiagnostics(el, letter);
			AddTextSegments(el, letter.TextSegments);
			AddCommonParts(el, letter.CommonParts);

			foreach (var tiret in letter.Tirets)
				el.Add(SerializeTiret(tiret));

			AddAmendment(el, letter.Amendment);
			return el;
		}

		private static XElement SerializeTiret(Tiret tiret)
		{
			var el = new XElement("tiret");
			AddEntityCore(el, tiret);

			AddContentAndDiagnostics(el, tiret);
			AddTextSegments(el, tiret.TextSegments);

			foreach (var nested in tiret.Tirets)
				el.Add(SerializeTiret(nested));

			AddAmendment(el, tiret.Amendment);
			return el;
		}

		private static XElement SerializeCommonPart(CommonPart commonPart)
		{
			var el = new XElement("commonPart",
				new XAttribute("type", commonPart.Type.ToString()));
			AddEntityCore(el, commonPart);

			if (!string.IsNullOrEmpty(commonPart.ParentEId))
				el.Add(new XAttribute("parentEId", commonPart.ParentEId));
			if (commonPart.SourceSegmentOrder.HasValue)
				el.Add(new XAttribute("segmentOrder", commonPart.SourceSegmentOrder.Value));

			AddContentAndDiagnostics(el, commonPart);
			return el;
		}

		// ============================================================
		// Nowelizacje
		// ============================================================

		private static void AddAmendment(XElement parent, Amendment? amendment)
		{
			if (amendment == null)
				return;

			var el = new XElement("amendment",
				new XAttribute("operation", amendment.OperationType.ToString()));

			if (amendment.EffectiveDate.HasValue)
				el.Add(new XAttribute("effectiveDate", amendment.EffectiveDate.Value.ToString("yyyy-MM-dd")));

			AddJournalElement(el, "targetAct", amendment.TargetLegalAct);

			foreach (var target in amendment.Targets)
			{
				var targetEl = new XElement("target", target.ToString());
				if (!string.IsNullOrEmpty(target.RawText))
					targetEl.Add(new XAttribute("raw", target.RawText));
				el.Add(targetEl);
			}

			if (amendment.Content != null)
				el.Add(SerializeAmendmentContent(amendment.Content));

			parent.Add(el);
		}

		private static XElement SerializeAmendmentContent(AmendmentContent content)
		{
			var el = new XElement("amendmentContent",
				new XAttribute("objectType", content.ObjectType.ToString()));

			if (!string.IsNullOrEmpty(content.PlainText))
				el.Add(new XElement("plainText", content.PlainText));

			foreach (var article in content.Articles)
				el.Add(SerializeArticle(article));
			foreach (var paragraph in content.Paragraphs)
				el.Add(SerializeParagraph(paragraph));
			foreach (var point in content.Points)
				el.Add(SerializePoint(point));
			foreach (var letter in content.Letters)
				el.Add(SerializeLetter(letter));
			foreach (var tiret in content.Tirets)
				el.Add(SerializeTiret(tiret));
			foreach (var commonPart in content.CommonParts)
				el.Add(SerializeCommonPart(commonPart));

			return el;
		}

		// ============================================================
		// Elementy wspólne
		// ============================================================

		/// <summary>
		/// Atrybuty wspólne encji: eId, numer (z rozbiciem) i data wejścia w życie.
		/// Pomija Guid oraz wartości domyślne (determinizm i zwięzłość snapshotu).
		/// </summary>
		private static void AddEntityCore(XElement el, BaseEntity entity)
		{
			var eId = entity.Id;
			if (!string.IsNullOrEmpty(eId))
				el.Add(new XAttribute("eId", eId));

			if (entity.Number != null)
			{
				if (!string.IsNullOrEmpty(entity.Number.Value))
					el.Add(new XAttribute("number", entity.Number.Value));
				if (!string.IsNullOrEmpty(entity.Number.RawValue) && entity.Number.RawValue != entity.Number.Value)
					el.Add(new XAttribute("rawNumber", entity.Number.RawValue));
				// Komponenty numeru serializowane osobno — snapshot musi widzieć regresje
				// parsowania numeru (NumericPart zasila walidację ciągłości numeracji)
				if (entity.Number.NumericPart != 0)
					el.Add(new XAttribute("numeric", entity.Number.NumericPart));
				if (!string.IsNullOrEmpty(entity.Number.LexicalPart))
					el.Add(new XAttribute("lexical", entity.Number.LexicalPart));
				if (!string.IsNullOrEmpty(entity.Number.Superscript))
					el.Add(new XAttribute("superscript", entity.Number.Superscript));
			}

			if (entity.EffectiveDate != default)
				el.Add(new XAttribute("effectiveDate", entity.EffectiveDate.ToString("yyyy-MM-dd")));
		}

		private static void AddContentAndDiagnostics(XElement el, BaseEntity entity)
		{
			if (!string.IsNullOrEmpty(entity.ContentText))
				el.Add(new XElement("content", entity.ContentText));

			foreach (var message in entity.ValidationMessages)
			{
				el.Add(new XElement("validation",
					new XAttribute("level", message.Level.ToString()),
					message.Message));
			}
		}

		private static void AddTextSegments(XElement el, List<TextSegment> segments)
		{
			foreach (var segment in segments)
			{
				var segmentEl = new XElement("segment",
					new XAttribute("order", segment.Order),
					new XAttribute("type", segment.Type.ToString()));
				if (!string.IsNullOrEmpty(segment.Role))
					segmentEl.Add(new XAttribute("role", segment.Role));
				segmentEl.Add(segment.Text);
				el.Add(segmentEl);
			}
		}

		private static void AddCommonParts(XElement el, List<CommonPart> commonParts)
		{
			foreach (var commonPart in commonParts)
				el.Add(SerializeCommonPart(commonPart));
		}

		private static void AddJournalElement(XElement parent, string name, JournalInfo? journal)
		{
			if (journal == null)
				return;
			if (journal.Year == 0 && journal.Positions.Count == 0 && string.IsNullOrEmpty(journal.SourceString))
				return;

			var el = new XElement(name, new XAttribute("year", journal.Year));
			if (journal.Positions.Count > 0)
				el.Add(new XAttribute("positions", string.Join(",", journal.Positions)));
			if (!string.IsNullOrEmpty(journal.SourceString))
				el.Add(new XAttribute("source", journal.SourceString));
			parent.Add(el);
		}

		/// <summary>
		/// Zapis z wymuszonym "\n" — XmlWriter domyślnie używa "\r\n",
		/// co psułoby porównania bajt-w-bajt między systemami.
		/// </summary>
		private static string ToCanonicalString(XElement root)
		{
			var settings = new XmlWriterSettings
			{
				Indent = true,
				IndentChars = "  ",
				NewLineChars = "\n",
				OmitXmlDeclaration = true,
				Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
			};

			var builder = new StringBuilder();
			using (var writer = XmlWriter.Create(builder, settings))
			{
				root.Save(writer);
			}

			builder.Append('\n');
			return builder.ToString();
		}
	}
}
