using System.Text.RegularExpressions;

namespace Saga.Core.Helpers
{
	/// <summary>
	/// Rozpoznaje oznaczenia dzienników urzędowych wg § 162 ust. 2 ZTP.
    ///
    /// Wspomaga podział parsowanego tekstu na zdania poprzez pomijanie kropek pojawiających
    ///  się w oznaczeniach (kropka w oznaczeniu nie oznacza końca zdania).
	/// </summary>
	internal static class JournalDesignationRecognizer
	{
		/// <summary>
		/// Człon skrótu utworzony z sylaby nazwy własnej i zakończony kropką — § 162 ust. 2 pkt 5.
		/// Przykłady: „Fin.", „Zdr.", „Wewn.", „Sprawiedl.", „Maz.".
		/// </summary>
		private const string NameSyllable = @"\p{Lu}\p{L}{0,8}\s*\.";

		/// <summary>„Min." + sylaby nazwy ministra — § 162 ust. 2 pkt 5</summary>
		private const string MinistryJournal = @"[Mm]in\s*\.(?:\s*" + NameSyllable + @")*";

		/// <summary>
		/// Skrót nazwy wielkimi literami bez kropek („NBP", „GUS", „UOKiK"),
        /// albo dla nazwy dwuwyrazowej „Urz."/„Kom." + sylaba drugiego wyrazu.
        /// § 162 ust. 2 pkt 7
		/// </summary>
		private const string CentralOfficeJournal =
			@"(?:(?:[Uu]rz|[Kk]om)\s*\.(?:\s*" + NameSyllable + @")*|\p{Lu}{2,}\p{L}*)";

		/// <summary>
		/// „Woj." + urzędowa nazwa województwa w pełnym brzmieniu („Mazowieckiego")
        /// albo skrócona jak w pkt 5 („Maz.").
        /// § 162 ust. 2 pkt 10
		/// </summary>
		private const string VoivodeshipJournal =
			@"[Ww]oj\s*\.(?:\s*(?:" + NameSyllable + @"|\p{Lu}\p{L}+))?";

		/// <summary>§ 162 ust. 2 pkt 3a-3c: dzienniki unijne i Wspólnot Europejskich.</summary>
		private const string EuropeanJournal = @"UE(?:\s+Polskie\s+wydanie\s+specjalne)?|WE";

		/// <summary>Wydawca dziennika urzędowego wg § 162 ust. 2 pkt 5, 7 i 10.</summary>
		private const string JournalIssuer =
			"(?:" + MinistryJournal + "|" + VoivodeshipJournal + "|" + CentralOfficeJournal + ")";

		/// <summary>
		/// § 162 ust. 2 pkt 3a-3c i 5-10: „Dz. Urz." z oznaczeniem wydawcy. Pkt 6, 8 i 9 dopuszczają
		/// dziennik wspólny, w którym wydawcy są łączeni spójnikiem „i". Oznaczenie wydawcy jest
		/// opcjonalne, żeby kropka po „Urz." była objęta wetem także przy skrócie spoza § 162.
		/// </summary>
		private const string OfficialGazetteJournal =
			@"\b[Dd]z\s*\.\s*[Uu]rz\s*\.(?:\s*(?:" + EuropeanJournal
			+ "|" + JournalIssuer + @"(?:\s*i\s*" + JournalIssuer + @")*))?";

		/// <summary>
		/// Komplet oznaczeń dzienników urzędowych wg § 162 ust. 2 ZTP.
		/// </summary>
		internal static readonly Regex Pattern = new(
			OfficialGazetteJournal
			+ @"|\b[Dd]z\s*\.\s*[Uu]\s*\."
			+ @"|\b[Mm]\s*\.\s*[Ss]\s*\.\s*[Gg]\s*\."
			+ @"|\b[Mm]\s*\.\s*[Pp]\s*\.",
			RegexOptions.Compiled);

		/// <summary>Wyszukuje wszystkie oznaczenia dzienników w tekście.</summary>
		public static MatchCollection FindAll(string text) => Pattern.Matches(text);

		/// <summary>Sprawdza czy pozycja `index` leży wewnątrz któregoś z oznaczeń.</summary>
		public static bool Covers(MatchCollection designations, int index)
		{
			foreach (Match designation in designations)
			{
				if (index < designation.Index)
					break;

				if (index < designation.Index + designation.Length)
					return true;
			}

			return false;
		}
	}
}
