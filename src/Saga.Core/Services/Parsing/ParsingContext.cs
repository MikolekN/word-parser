using Saga.Model;
using Saga.Model.SystematizingUnits;
using DtoArticle = Saga.Model.EditorialUnits.Article;
using DtoLetter = Saga.Model.EditorialUnits.Letter;
using DtoParagraph = Saga.Model.EditorialUnits.Paragraph;
using DtoPoint = Saga.Model.EditorialUnits.Point;
using DtoTiret = Saga.Model.EditorialUnits.Tiret;

namespace Saga.Core.Services.Parsing
{
	/// <summary>
	/// Kontekst parsowania przechowujacy aktualny stan drzewa encji
	/// oraz biezaca pozycje strukturalna w hierarchii jednostek redakcyjnych.
	/// </summary>
	public sealed class ParsingContext
	{
		public ParsingContext(LegalDocument document, Subchapter subchapter)
		{
			Document = document;
			Subchapter = subchapter;

			// Bieżąca ścieżka jednostek systematyzujących z kanonicznego (zawsze spójnego) drzewa dokumentu.
			// Dla realnego parsowania Subchapter pokrywa się z CurrentChapter.Subchapters[0].
			CurrentPart = document.RootPart;
			CurrentBook = CurrentPart.Books[0];
			CurrentTitle = CurrentBook.Titles[0];
			CurrentDivision = CurrentTitle.Divisions[0];
			CurrentChapter = CurrentDivision.Chapters[0];
		}

		public LegalDocument Document { get; }

		/// <summary>Bieżący oddział — miejsce dołączania artykułów. Aktualizowany przez SystematizingUnitBuilder.</summary>
		public Subchapter Subchapter { get; internal set; }

		// Bieżąca ścieżka jednostek systematyzujących (Część → … → Rozdział). Aktualizowana przy wejściu
		// w jawną jednostkę; służy do dołączania jednostek-rodzeństwa na właściwym poziomie.
		public Part CurrentPart { get; internal set; }
		public Book CurrentBook { get; internal set; }
		public Title CurrentTitle { get; internal set; }
		public Division CurrentDivision { get; internal set; }
		public Chapter CurrentChapter { get; internal set; }

		/// <summary>Jednostka systematyzacyjna oczekująca na tytuł (drugi wiersz wzorca dwuwierszowego, § 60).</summary>
		public ISystematizingUnit? PendingHeadingUnit { get; set; }

		public DtoArticle? CurrentArticle { get; set; }
		public DtoParagraph? CurrentParagraph { get; set; }
		public DtoPoint? CurrentPoint { get; set; }
		public DtoLetter? CurrentLetter { get; set; }

		/// <summary>
		/// Stos aktywnych tiretow wg glebokosci (0 = brak, 1 = TIR, 2 = 2TIR, 3 = 3TIR).
		/// Ostatni element to biezacy wlasciciel nowelizacji dla poziomu tiret.
		/// Czyszczony przy kazdym wejsciu na poziom Letter/Point/Paragraph/Article.
		/// </summary>
		public List<DtoTiret> TiretStack { get; } = new();

		/// <summary>Skrot: najglebszy aktywny tiret (lub null).</summary>
		public DtoTiret? CurrentTiret => TiretStack.Count > 0 ? TiretStack[^1] : null;

		/// <summary>
		/// Wciecia lewe (twips) tiretow otwartych na stosie <see cref="TiretStack"/> — lista rownolegla,
		/// zawsze tej samej dlugosci. null gdy blok nie niosl ukladu (np. TXT). Zasila wnioskowanie
		/// glebokosci tiretu z wciecia (§ 58 ZTP), gdy styl 2TIR/3TIR nie rozstrzyga. Utrzymywana wylacznie
		/// przez <see cref="PushTiret"/>/<see cref="PopTiretsToDepth"/>/<see cref="ClearTiretStack"/>.
		/// </summary>
		public List<int?> OpenTiretIndents { get; } = new();

		/// <summary>Dodaje tiret na stos wraz z jego wcieciem lewym (utrzymuje synchronizacje obu list).</summary>
		public void PushTiret(DtoTiret tiret, int? leftIndentTwips)
		{
			TiretStack.Add(tiret);
			OpenTiretIndents.Add(leftIndentTwips);
		}

		/// <summary>Zdejmuje ze stosu tirety o glebokosci >= depth (pozostawia depth-1 poziomow).</summary>
		public void PopTiretsToDepth(int depth)
		{
			while (TiretStack.Count >= depth && TiretStack.Count > 0)
			{
				TiretStack.RemoveAt(TiretStack.Count - 1);
				OpenTiretIndents.RemoveAt(OpenTiretIndents.Count - 1);
			}
		}

		/// <summary>Czysci caly stos tiretow (wejscie na poziom Letter/Point/Paragraph/Article/jednostke systematyzacyjna).</summary>
		public void ClearTiretStack()
		{
			TiretStack.Clear();
			OpenTiretIndents.Clear();
		}

		/// <summary>Kolektor metadanych aktu (rodzaj/data/przedmiot) ze strefy tytulowej (§ 16-19 ZTP).</summary>
		public DocumentMetadataCollector Metadata { get; } = new();

		/// <summary>
		/// Serwis do budowania i aktualizacji referencji strukturalnych
		/// w kontekscie nowelizacji.
		/// </summary>
		public LegalReferenceService ReferenceService { get; } = new();

		/// <summary>
		/// Biezaca pozycja strukturalna w hierarchii jednostek redakcyjnych
		/// (art. -> ust. -> pkt -> lit. -> tiret). Aktualizowana przez orkiestrator
		/// po kazdym zbudowaniu encji.
		/// </summary>
		public StructuralReference CurrentStructuralReference { get; set; } = new();

		/// <summary>
		/// Wykryte cele nowelizacji w tresci jednostek redakcyjnych.
		/// Klucz: Guid encji, Wartosc: wykryty cel (referencja strukturalna z RawText).
		/// Wypelniane przez orkiestrator podczas parsowania encji IHasAmendments.
		/// </summary>
		public Dictionary<Guid, StructuralAmendmentReference> DetectedAmendmentTargets { get; } = new();

		/// <summary>
		/// Czy aktualnie przetwarza akapity bedace trescia nowelizacji.
		/// Ustawiane na true gdy:
		/// - napotkano akapit ze stylem Z/... (Z/UST, Z/ART, Z/PKT itd.)
		/// - po triggerze ("otrzymuje brzmienie:") napotkano akapit bez stylu ustawy matki
		/// Resetowane gdy napotkano akapit z rozpoznanym stylem ustawy matki (ART, UST, PKT, LIT, TIR).
		/// </summary>
		public bool InsideAmendment { get; set; }

		/// <summary>
		/// Czy przetworzony wlasnie akapit zawieral zwrot rozpoczynajacy nowelizacje
		/// ("otrzymuje brzmienie:", "w brzmieniu:"). Ustawiane PO przetworzeniu
		/// akapitu, sprawdzane PRZED przetworzeniem nastepnego.
		/// </summary>
		public bool AmendmentTriggerDetected { get; set; }

		/// <summary>
		/// Bufor akapitow biezacej nowelizacji. Zbiera akapity od wejscia w tryb
		/// nowelizacji az do powrotu do stylu ustawy matki, po czym deleguje
		/// do AmendmentBuilder (iteracja 2).
		/// </summary>
		public AmendmentCollector AmendmentCollector { get; } = new();

		/// <summary>
		/// Encja, na ktorej wykryto ostatni trigger nowelizacji.
		/// Przechowywana tymczasowo do momentu rozpoczecia zbierania
		/// (Begin) w AmendmentCollector.
		/// </summary>
		public BaseEntity? AmendmentOwner { get; set; }

	}
}
