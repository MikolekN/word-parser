using System.Linq;
using Saga.Model;
using Saga.Model.SystematizingUnits;
using Serilog;
using Saga.Core.Services.Classify;

namespace Saga.Core.Services.Parsing.Builders
{
	/// <summary>
	/// Buduje jednostki systematyzacyjne (Część → Księga → Tytuł → Dział → Rozdział → Oddział; § 60-62 ZTP)
	/// i utrzymuje bieżącą ścieżkę w kontekście. Pierwsza jawna jednostka danego poziomu (o ile jej poddrzewo
	/// nie zawiera jeszcze artykułów) przejmuje istniejący węzeł niejawny (IsImplicit=false); kolejna tworzy
	/// jednostkę-rodzeństwo z własnym świeżym łańcuchem niejawnym w dół do oddziału. Artykuły dołączane są do
	/// bieżącego oddziału (ctx.Subchapter), więc aktualizacja ścieżki przekierowuje kolejne artykuły.
	///
	/// Ograniczenie modelu: RootPart jest pojedynczy — wiele Części (np. „CZĘŚĆ OGÓLNA/SZCZEGÓLNA") nie jest
	/// reprezentowalnych, druga „CZĘŚĆ" nadpisuje numer części głównej (z ostrzeżeniem).
	/// </summary>
	internal sealed class SystematizingUnitBuilder
	{
		public void Enter(ParsingContext ctx, ParagraphKind kind, EntityNumber number)
		{
			ISystematizingUnit? entered = kind switch
			{
				ParagraphKind.PartUnit       => EnterPart(ctx, number),
				ParagraphKind.BookUnit       => EnterBook(ctx, number),
				ParagraphKind.TitleUnit      => EnterTitle(ctx, number),
				ParagraphKind.DivisionUnit   => EnterDivision(ctx, number),
				ParagraphKind.ChapterUnit    => EnterChapter(ctx, number),
				ParagraphKind.SubchapterUnit => EnterSubchapter(ctx, number),
				_ => null,
			};
			if (entered == null)
				return;

			// Następny akapit (jeśli to tytuł) opisze tę jednostkę (wzorzec dwuwierszowy, § 60).
			ctx.PendingHeadingUnit = entered;

			// Nowa jednostka systematyzacyjna zaczyna świeże jednostki redakcyjne.
			ctx.CurrentArticle = null;
			ctx.CurrentParagraph = null;
			ctx.CurrentPoint = null;
			ctx.CurrentLetter = null;
			ctx.ClearTiretStack();
		}

		private static ISystematizingUnit? EnterPart(ParsingContext ctx, EntityNumber number)
		{
			// Model ma pojedynczy RootPart — części-rodzeństwa nie są reprezentowalne. Przejmujemy węzeł
			// tylko gdy niejawny i bez artykułów pod nim (inaczej ujawnienie zmieniłoby eId wcześniejszych
			// artykułów). W przeciwnym razie CZĘŚĆ jest pomijana z ostrzeżeniem.
			if (!CanClaim(ctx.CurrentPart))
			{
				Log.Warning("CZĘŚĆ ({Number}) pominięta — RootPart zajęty lub zawiera już artykuły " +
					"(pojedyncza część w modelu).", number.Value);
				return null;
			}

			Claim(ctx.CurrentPart, number);
			return ctx.CurrentPart;
		}

		private static ISystematizingUnit EnterBook(ParsingContext ctx, EntityNumber number)
		{
			if (CanClaim(ctx.CurrentBook))
			{
				Claim(ctx.CurrentBook, number);
				return ctx.CurrentBook;
			}

			var book = new Book { IsImplicit = false, Number = number, Parent = ctx.CurrentPart };
			ctx.CurrentPart.Books.Add(book);
			ctx.CurrentBook = book;
			ctx.CurrentTitle = AddImplicit(new Title(), book, book.Titles);
			ctx.CurrentDivision = AddImplicit(new Division(), ctx.CurrentTitle, ctx.CurrentTitle.Divisions);
			ctx.CurrentChapter = AddImplicit(new Chapter(), ctx.CurrentDivision, ctx.CurrentDivision.Chapters);
			ctx.Subchapter = AddImplicit(new Subchapter(), ctx.CurrentChapter, ctx.CurrentChapter.Subchapters);
			return book;
		}

		private static ISystematizingUnit EnterTitle(ParsingContext ctx, EntityNumber number)
		{
			if (CanClaim(ctx.CurrentTitle))
			{
				Claim(ctx.CurrentTitle, number);
				return ctx.CurrentTitle;
			}

			var title = new Title { IsImplicit = false, Number = number, Parent = ctx.CurrentBook };
			ctx.CurrentBook.Titles.Add(title);
			ctx.CurrentTitle = title;
			ctx.CurrentDivision = AddImplicit(new Division(), title, title.Divisions);
			ctx.CurrentChapter = AddImplicit(new Chapter(), ctx.CurrentDivision, ctx.CurrentDivision.Chapters);
			ctx.Subchapter = AddImplicit(new Subchapter(), ctx.CurrentChapter, ctx.CurrentChapter.Subchapters);
			return title;
		}

		private static ISystematizingUnit EnterDivision(ParsingContext ctx, EntityNumber number)
		{
			if (CanClaim(ctx.CurrentDivision))
			{
				Claim(ctx.CurrentDivision, number);
				return ctx.CurrentDivision;
			}

			var division = new Division { IsImplicit = false, Number = number, Parent = ctx.CurrentTitle };
			ctx.CurrentTitle.Divisions.Add(division);
			ctx.CurrentDivision = division;
			ctx.CurrentChapter = AddImplicit(new Chapter(), division, division.Chapters);
			ctx.Subchapter = AddImplicit(new Subchapter(), ctx.CurrentChapter, ctx.CurrentChapter.Subchapters);
			return division;
		}

		private static ISystematizingUnit EnterChapter(ParsingContext ctx, EntityNumber number)
		{
			if (CanClaim(ctx.CurrentChapter))
			{
				Claim(ctx.CurrentChapter, number);
				return ctx.CurrentChapter;
			}

			var chapter = new Chapter { IsImplicit = false, Number = number, Parent = ctx.CurrentDivision };
			ctx.CurrentDivision.Chapters.Add(chapter);
			ctx.CurrentChapter = chapter;
			ctx.Subchapter = AddImplicit(new Subchapter(), chapter, chapter.Subchapters);
			return chapter;
		}

		private static ISystematizingUnit EnterSubchapter(ParsingContext ctx, EntityNumber number)
		{
			if (CanClaim(ctx.Subchapter))
			{
				Claim(ctx.Subchapter, number);
				return ctx.Subchapter;
			}

			var subchapter = new Subchapter { IsImplicit = false, Number = number, Parent = ctx.CurrentChapter };
			ctx.CurrentChapter.Subchapters.Add(subchapter);
			ctx.Subchapter = subchapter;
			return subchapter;
		}

		/// <summary>Można przejąć węzeł niejawny tylko gdy jego poddrzewo nie zawiera jeszcze artykułów
		/// (inaczej uczynienie go jawnym reparentowałoby wcześniejsze artykuły i zmieniałoby ich eId).</summary>
		private static bool CanClaim(ISystematizingUnit unit) => unit.IsImplicit && !HasArticles(unit);

		private static void Claim(ISystematizingUnit unit, EntityNumber number)
		{
			unit.IsImplicit = false;
			((BaseEntity)unit).Number = number;
		}

		private static T AddImplicit<T>(T unit, BaseEntity parent, System.Collections.Generic.List<T> siblings)
			where T : BaseEntity
		{
			unit.Parent = parent;
			siblings.Add(unit);
			return unit;
		}

		private static bool HasArticles(ISystematizingUnit unit) => unit switch
		{
			Subchapter s => s.Articles.Count > 0,
			Chapter c    => c.Subchapters.Any(HasArticles),
			Division d   => d.Chapters.Any(HasArticles),
			Title t      => t.Divisions.Any(HasArticles),
			Book b       => b.Titles.Any(HasArticles),
			Part p       => p.Books.Any(HasArticles),
			_            => false,
		};
	}
}
