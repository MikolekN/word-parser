using System;
using System.Collections.Generic;

namespace Saga.Core.Helpers
{
	/// <summary>
	/// Konwersja oznaczeń jednostek systematyzacyjnych (§ 60 ZTP) na wartość liczbową:
	/// cyfry rzymskie (CZĘŚĆ/KSIĘGA/TYTUŁ/DZIAŁ) oraz liczebniki porządkowe słowne
	/// („część pierwsza"). Cyfry arabskie (Rozdział/Oddział) parsuje wywołujący bezpośrednio.
	/// </summary>
	internal static class RomanNumeralConverter
	{
		private static readonly Dictionary<char, int> RomanDigits = new()
		{
			['I'] = 1, ['V'] = 5, ['X'] = 10, ['L'] = 50, ['C'] = 100, ['D'] = 500, ['M'] = 1000,
		};

		private static readonly Dictionary<string, int> WordOrdinals = new(StringComparer.OrdinalIgnoreCase)
		{
			["pierwsza"] = 1, ["druga"] = 2, ["trzecia"] = 3, ["czwarta"] = 4, ["piąta"] = 5,
			["szósta"] = 6, ["siódma"] = 7, ["ósma"] = 8, ["dziewiąta"] = 9, ["dziesiąta"] = 10,
			["jedenasta"] = 11, ["dwunasta"] = 12,
		};

		/// <summary>
		/// Parsuje cyfrę rzymską (I, IV, XII…) na liczbę. Zwraca null dla pustego lub niepoprawnego wejścia.
		/// Waliduje kanoniczność (odtworzenie w tył musi dać identyczny zapis), by odrzucić „IIII"/„VX".
		/// </summary>
		public static int? RomanToInt(string? roman)
		{
			if (string.IsNullOrWhiteSpace(roman))
				return null;

			var s = roman.Trim().ToUpperInvariant();
			int total = 0, prev = 0;
			foreach (var c in s)
			{
				if (!RomanDigits.TryGetValue(c, out var value))
					return null;
				total += value <= prev ? value : value - 2 * prev;
				prev = value;
			}

			return total > 0 && ToRoman(total) == s ? total : null;
		}

		/// <summary>Parsuje liczebnik porządkowy słowny („pierwsza", „druga"…) na liczbę; null gdy nierozpoznany.</summary>
		public static int? WordOrdinalToInt(string? word) =>
			!string.IsNullOrWhiteSpace(word) && WordOrdinals.TryGetValue(word.Trim(), out var n) ? n : null;

		/// <summary>Rozpoznaje numer jednostki wyższej: najpierw cyfra rzymska, potem liczebnik słowny.</summary>
		public static int? Parse(string? token) => RomanToInt(token) ?? WordOrdinalToInt(token);

		/// <summary>Zamienia liczbę na kanoniczną cyfrę rzymską (do normalizacji eId jednostek wyższych).</summary>
		internal static string ToRoman(int number)
		{
			if (number <= 0 || number >= 4000)
				return string.Empty;

			(int Value, string Symbol)[] map =
			{
				(1000, "M"), (900, "CM"), (500, "D"), (400, "CD"), (100, "C"), (90, "XC"),
				(50, "L"), (40, "XL"), (10, "X"), (9, "IX"), (5, "V"), (4, "IV"), (1, "I"),
			};

			var sb = new System.Text.StringBuilder();
			foreach (var (value, symbol) in map)
				while (number >= value)
				{
					sb.Append(symbol);
					number -= value;
				}

			return sb.ToString();
		}
	}
}
