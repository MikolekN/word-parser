namespace Saga.Core.Services.Classify
{
	public enum ParagraphKind
	{
		// Jednostki redakcyjne
		Article,
		Paragraph,
		Point,
		Letter,
		Tiret,
		WrapUp,

		// Jednostki systematyzacyjne (§ 60-62 ZTP)
		PartUnit,        // Część
		BookUnit,        // Księga
		TitleUnit,       // Tytuł
		DivisionUnit,    // Dział
		ChapterUnit,     // Rozdział
		SubchapterUnit,  // Oddział
		UnitHeading,     // tytuł jednostki systematyzacyjnej (drugi wiersz wzorca dwuwierszowego)

		Unknown
	}
}
