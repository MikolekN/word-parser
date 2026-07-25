using System;
using System.Collections.Generic;
using Saga.Model;
using Saga.Core.Services.Classify.Document;

namespace Saga.Core.Services.Parsing
{
	/// <summary>
	/// Zbiera metadane aktu ze strefy tytułowej — akapitów sprzed pierwszego artykułu (§ 16-19, § 102, § 120 ZTP):
	/// rodzaj (nagłówek USTAWA/ROZPORZĄDZENIE/OBWIESZCZENIE/UCHWAŁA/ZARZĄDZENIE), organ wydający, datę i przedmiot.
	/// Karmiony z <see cref="StructureProcessor"/> akapitami nierozpoznanymi (Unknown) przed pierwszym artykułem.
	///
	/// Strefa tytułowa jest OGRANICZONA i kolektor pieczętuje się SAM (niezależnie od sygnału artykułu):
	/// - rusza dopiero na nagłówku rodzaju (proza przed nim jest ignorowana);
	/// - po nagłówku przyjmuje wyłącznie organ / datę / przedmiot; przedmiot (§ 18-19) jest komponentem
	///   końcowym tytułu, więc go domyka;
	/// - drugi nagłówek rodzaju (np. „USTAWA" w załączniku tekstu jednolitego, § 102) NIE jest scalany —
	///   domyka strefę i wraca do wywołującego (należy do treści/załącznika);
	/// - pierwszy wiersz spoza strefy tytułowej (np. „Na podstawie art. …", „§ 1. …") również domyka.
	/// To ogranicza okno zbierania także dla aktów opartych na „§" (rozporządzenia/uchwały), gdzie żaden wiersz
	/// nie jest klasyfikowany jako artykuł i sygnał <see cref="Seal"/> ze <see cref="StructureProcessor"/> nie nadchodzi.
	///
	/// Tytuł składany jest WERBATIM z rozpoznanych wierszy (zasada „nie przeinaczyć"); datę wydobywa dodatkowo
	/// w postaci strukturalnej (<see cref="LegalDocument.ActDate"/>).
	/// </summary>
	public sealed class DocumentMetadataCollector
	{
		private static readonly Dictionary<string, int> Months = new(StringComparer.OrdinalIgnoreCase)
		{
			["stycznia"] = 1, ["lutego"] = 2, ["marca"] = 3, ["kwietnia"] = 4, ["maja"] = 5, ["czerwca"] = 6,
			["lipca"] = 7, ["sierpnia"] = 8, ["września"] = 9, ["października"] = 10, ["listopada"] = 11, ["grudnia"] = 12,
		};

		private readonly List<string> _titleParts = new();
		private DateTime? _actDate;
		private bool _kindSeen;
		private bool _sealed;

		/// <summary>Liczba zebranych wierszy metadanych (diagnostyka/testy).</summary>
		public int CollectedLineCount => _titleParts.Count;

		/// <summary>
		/// Obserwuje nierozpoznany akapit sprzed pierwszego artykułu. Zwraca true, gdy wiersz uznano
		/// za metadaną (wywołujący pomija wtedy ostrzeżenie „pominięty akapit nierozpoznany").
		/// </summary>
		public bool Observe(string text)
		{
			if (_sealed || string.IsNullOrWhiteSpace(text))
				return false;

			var t = text.Trim();

			if (!_kindSeen)
			{
				// Strefa tytułowa rusza dopiero na nagłówku rodzaju — inaczej proza sprzed aktu trafiłaby do tytułu.
				if (IsKindHeader(t))
				{
					_kindSeen = true;
					_titleParts.Add(t);
					return true;
				}
				return false;
			}

			// Drugi nagłówek rodzaju (np. „USTAWA" w załączniku tekstu jednolitego) — nie scalaj dwóch aktów.
			if (IsKindHeader(t))
			{
				Seal();
				return false;
			}

			// Organ wydający w osobnym wierszu (§ 120 ust. 4 — rozporządzenie; § 102 — „Marszałka Sejmu…").
			if (IsIssuingOrgan(t))
			{
				_titleParts.Add(t);
				return true;
			}

			// Data aktu (§ 17): „z dnia D miesiąca RRRR r."
			var dm = ZtpPatterns.ActDateLinePattern.Match(t);
			if (dm.Success)
			{
				_titleParts.Add(t);
				TryCaptureDate(dm.Groups["day"].Value, dm.Groups["month"].Value, dm.Groups["year"].Value);
				return true;
			}

			// Przedmiot aktu (§ 18-19 / § 120 ust. 6) — komponent końcowy tytułu; domyka strefę tytułową.
			if (ZtpPatterns.ActSubjectPattern.IsMatch(t))
			{
				_titleParts.Add(t);
				Seal();
				return true;
			}

			// Pierwszy wiersz spoza strefy tytułowej kończy zbieranie (ogranicza okno także dla aktów „§").
			Seal();
			return false;
		}

		/// <summary>Pieczętuje kolektor — dalsze <see cref="Observe"/> zwracają false. Idempotentne.</summary>
		public void Seal() => _sealed = true;

		/// <summary>
		/// Zapisuje zebrane metadane do dokumentu (przy finalizacji parsowania). Nie nadpisuje wartości
		/// już ustawionych (np. przez ścieżkę stylową/inny kanał).
		/// </summary>
		public void ApplyTo(LegalDocument document)
		{
			if (_titleParts.Count > 0 && string.IsNullOrEmpty(document.Title))
				document.Title = string.Join(" ", _titleParts);

			if (_actDate.HasValue && !document.ActDate.HasValue)
				document.ActDate = _actDate;
		}

		private void TryCaptureDate(string day, string month, string year)
		{
			if (_actDate.HasValue)
				return;
			if (!Months.TryGetValue(month, out var mo) ||
			    !int.TryParse(day, out var d) ||
			    !int.TryParse(year, out var y))
				return;
			try
			{
				_actDate = new DateTime(y, mo, d);
			}
			catch (ArgumentOutOfRangeException)
			{
				// Niepoprawna data (np. 31 lutego) — pomijamy datę strukturalną, wiersz zostaje w tytule.
			}
		}

		private static bool IsKindHeader(string t) =>
			ZtpPatterns.StatuteHeaderPattern.IsMatch(t) ||
			ZtpPatterns.RegulationHeaderPattern.IsMatch(t) ||
			ZtpPatterns.AnnouncementHeaderPattern.IsMatch(t) ||
			ZtpPatterns.ResolutionHeaderPattern.IsMatch(t) ||
			ZtpPatterns.OrderHeaderPattern.IsMatch(t);

		private static bool IsIssuingOrgan(string t) =>
			ZtpPatterns.IssuingOrganLinePattern.IsMatch(t) ||
			ZtpPatterns.MarshalOfSejmIssuerPattern.IsMatch(t);
	}
}
