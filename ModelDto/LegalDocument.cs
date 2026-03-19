using System;
using System.Collections.Generic;
using System.Text;
using ModelDto.EditorialUnits;
using ModelDto.SystematizingUnits;

#nullable enable

namespace ModelDto
{
    /// <summary>
    /// Model aktu prawnego - wrapper całej struktury dokumentu legislacyjnego.
    /// 
    /// Zawiera metadane dotyczące całego aktu oraz hierarchię jego struktury.
    /// W konstruktorze tworzona jest minimalna pełna hierarchia od części do oddziału,
    /// a jednostki nieobecne w tekście pozostają jako IsImplicit = true.
    /// 
    /// Przykładowa pełna hierarchia:
    /// Część (Part) → Księga (Book) → Tytuł (Title) → Dział (Division) → 
    /// Rozdział (Chapter) → Oddział (Subchapter) → Artykuł (Article)
    /// 
    /// Struktura w LegalDocument:
    /// LegalDocument
    ///   ├─ Type: Statute/Regulation/Code (determinuje nazewnictwo jednostek)
    ///   ├─ Title: "Ustawa o ochronie pracy"
    ///   ├─ SourceJournal: DzU 2024, poz. 123
    ///   └─ RootSystematizingUnits[]: zawsze zawiera Part (domyślną lub jawną)
    ///       └─ Book → Title → Division → Chapter → Subchapter
    ///           └─ Articles[]: artykuły w oddziale
    ///               └─ Subsections: ustępy
    /// </summary>
    public class LegalDocument
    {
        public LegalDocument()
        {
            var part = new Part { IsImplicit = true };
            var book = new Book { IsImplicit = true, Parent = part };
            var title = new Title { IsImplicit = true, Parent = book };
            var division = new Division { IsImplicit = true, Parent = title };
            var chapter = new Chapter { IsImplicit = true, Parent = division };
            var subchapter = new Subchapter { IsImplicit = true, Parent = chapter };

            chapter.Subchapters.Add(subchapter);
            division.Chapters.Add(chapter);
            title.Divisions.Add(division);
            book.Titles.Add(title);
            part.Books.Add(book);

            RootPart = part;
        }
        /// <summary>
        /// Unikalny identyfikator dokumentu.
        /// </summary>
        public Guid Guid { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Typ aktu prawnego (ustawa, rozporządzenie, kodeks).
        /// Określa konwencje nazewnictwa i struktury jednostek redakcyjnych.
        /// </summary>
        public LegalActType Type { get; set; } = LegalActType.Statute;

        /// <summary>
        /// Pełny tytuł aktu prawnego.
        /// Przykłady: "Kodeks cywilny", "Ustawa z dnia 1 marca 2024 r. o ochronie konkurencji"
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Informacja o źródle publikacji (Dziennik Ustaw, rok, numer pozycji).
        /// Zawiera metadane potrzebne do odwołań w systemie prawnym.
        /// </summary>
        public JournalInfo SourceJournal { get; set; } = new();

        /// <summary>
        /// Pierwsza (najwyższa) jednostka systematyzująca w akcie (Część).
        /// Zawsze zawiera minimalną pełną hierarchię od Części do Oddziału.
        /// Parser oznacza jednostki obecne w tekście jako IsImplicit = false.
        /// </summary>
        public Part RootPart { get; set; } = new();

        /// <summary>
        /// Skrót do wszystkich artykułów w dokumencie (niezależnie od hierarchii).
        /// Przydatne do nawigacji i przeglądania treści.
        /// </summary>
        public IEnumerable<Article> Articles
        {
            get
            {
                foreach (var article in EnumerateArticlesFromUnit(RootPart))
                {
                    yield return article;
                }
            }
        }

        /// <summary>
        /// Data utworzenia dokumentu w systemie.
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        /// <summary>
        /// Data ostatniej modyfikacji dokumentu.
        /// </summary>
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        /// <summary>
        /// Zwraca etykietę wyświetlającą dla głównej jednostki redakcyjnej (artykułu).
        /// Zależy od typu aktu.
        /// </summary>
        public string GetMainUnitLabel() => Type.GetMainUnitLabel();

        /// <summary>
        /// Zwraca etykietę wyświetlającą dla podjednostki (ustępu/paragrafu).
        /// Zależy od typu aktu.
        /// </summary>
        public string GetSubUnitLabel() => Type.GetSubUnitLabel();

        public override string ToString()
        {
            var builder = new StringBuilder();
            builder.Append($"{Type.ToFriendlyString().ToUpper()}: {Title} ({SourceJournal})");

            foreach (var article in Articles)
            {
                builder.AppendLine();
                builder.Append(article.ToString());
            }

            return builder.ToString();
        }

        private static IEnumerable<Article> EnumerateArticlesFromUnit(ISystematizingUnit unit)
        {
            switch (unit)
            {
                case Part part:
                    foreach (var book in part.Books)
                    foreach (var article in EnumerateArticlesFromUnit(book))
                        yield return article;
                    break;
                case Book book:
                    foreach (var title in book.Titles)
                    foreach (var article in EnumerateArticlesFromUnit(title))
                        yield return article;
                    break;
                case Title title:
                    foreach (var division in title.Divisions)
                    foreach (var article in EnumerateArticlesFromUnit(division))
                        yield return article;
                    break;
                case Division division:
                    foreach (var chapter in division.Chapters)
                    foreach (var article in EnumerateArticlesFromUnit(chapter))
                        yield return article;
                    break;
                case Chapter chapter:
                    foreach (var subchapter in chapter.Subchapters)
                    foreach (var article in EnumerateArticlesFromUnit(subchapter))
                        yield return article;
                    break;
                case Subchapter subchapter:
                    foreach (var article in subchapter.Articles)
                        yield return article;
                    break;
            }
        }
    }
}
