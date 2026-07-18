using ModelDto;
using ModelDto.EditorialUnits;
using Serilog;
using WordParserCore.Helpers;
using WordParserCore.Ingest;
using WordParserCore.Services.Classify;
using WordParserCore.Services.Parsing.Builders;

namespace WordParserCore.Services.Parsing
{
	/// <summary>
	/// Buduje encje domenowe (Article, Paragraph, Point, Letter, Tiret)
	/// na podstawie wyników klasyfikacji akapitu. Odpowiada za:
	/// - budowanie encji i resetowanie podpoziomów w kontekście,
	/// - transfer kar numeracyjnych z ClassificationResult jako ValidationMessage,
	/// - aktualizację pozycji strukturalnej (CurrentStructuralReference),
	/// - wykrywanie celów nowelizacji w treści encji,
	/// - obsługę akapitów wrap-up (CZ_WSP_*).
	/// </summary>
	internal sealed class StructureProcessor
	{
		private readonly ArticleBuilder _articleBuilder = new();
		private readonly ParagraphBuilder _paragraphBuilder = new();
		private readonly PointBuilder _pointBuilder = new();
		private readonly LetterBuilder _letterBuilder = new();
		private readonly TiretBuilder _tiretBuilder = new();
		private readonly SystematizingUnitBuilder _systematizingUnitBuilder = new();
		private readonly JournalReferenceService _journalReferenceService = new();

		/// <summary>
		/// Obsługuje akapit wrap-up (zamknięcie listy przez półpauzę przed pozycją nadrzędną).
		/// Wywoływana z Process() gdy classification.Kind == WrapUp.
		/// </summary>
		private static bool TryHandleWrapUp(ParsingContext context, string text, string? styleId)
		{
			if (!TryGetWrapUpTarget(styleId, out var targetKind))
				return false;

			if (!IsWrapUpByText(text))
			{
				Log.Warning(
					"WrapUp pominiety: styl={StyleId}, brak polpauzy w tekscie: {Text}",
					styleId,
					text);
				return true;
			}

			BaseEntity? parent = targetKind switch
			{
				AmendmentTargetKind.Point  => context.CurrentParagraph,
				AmendmentTargetKind.Letter => context.CurrentPoint,
				AmendmentTargetKind.Tiret  => context.CurrentLetter,
				_ => null
			};

			if (parent == null)
			{
				Log.Warning(
					"WrapUp pominiety: brak rodzica dla {TargetKind} (styl={StyleId})",
					targetKind,
					styleId);
				return true;
			}

			var hasListItems = targetKind switch
			{
				AmendmentTargetKind.Point  => context.CurrentParagraph?.Points.Count > 0,
				AmendmentTargetKind.Letter => context.CurrentPoint?.Letters.Count > 0,
				AmendmentTargetKind.Tiret  => context.CurrentLetter?.Tirets.Count > 0,
				_ => false
			};

			if (!hasListItems)
			{
				Log.Warning(
					"WrapUp pominiety: brak elementow listy dla {TargetKind} (styl={StyleId})",
					targetKind,
					styleId);
				return true;
			}

			var attached = ParsingFactories.AttachWrapUpCommonPart(parent, text);
			Log.Debug(attached
				? "WrapUp dodany dla {ParentId}"
				: "WrapUp pominiety (duplikat lub pusty tekst) dla {ParentId}",
				parent.Id);

			return true;
		}

		/// <summary>
		/// Przetwarza akapit: buduje encję domenową na podstawie klasyfikacji,
		/// aktualizuje pozycję strukturalną i wykrywa cele nowelizacji.
		/// Zwraca true jeśli encja została zbudowana (wykrywanie triggera powinno nastąpić po powrocie).
		/// </summary>
		public bool Process(ParsingContext context, ClassificationResult classification, string text,
			string? sourceStyleId = null, BlockLayoutInfo? layout = null)
		{
			// Tytuł jednostki systematyzacyjnej (drugi wiersz wzorca dwuwierszowego, § 60):
			// pierwszy akapit po nagłówku jednostki, jeśli sam nie jest jednostką, opisuje ją.
			if (context.PendingHeadingUnit is { } pendingUnit)
			{
				context.PendingHeadingUnit = null;
				if (classification.Kind == ParagraphKind.Unknown && IsHeadingCandidate(text))
				{
					pendingUnit.Heading = text.Trim();
					return true;
				}
				// brak tytułu — kontynuuj normalną obsługę bieżącego akapitu
			}

			// Jednostki systematyzacyjne (§ 60-62) — mogą wystąpić przed pierwszym artykułem.
			if (IsSystematizing(classification.Kind))
			{
				HandleSystematizingUnit(context, classification, text, sourceStyleId);
				return true;
			}

			if (classification.Kind == ParagraphKind.Article)
			{
				// Pierwszy artykuł zamyka strefę tytułową — metadane dalej nie są zbierane.
				context.Metadata.Seal();

				var result = _articleBuilder.Build(new ArticleBuildInput(context.Subchapter, text));
				ValidationReporter.AddClassificationWarning(result.Article, classification, "ART");
				context.CurrentArticle = result.Article;
				context.CurrentParagraph = result.Paragraph;
				context.CurrentPoint = null;
				context.CurrentLetter = null;
				context.ClearTiretStack();

				UpdateStructuralReference(context, result.Article);
				if (result.Paragraph != null)
				{
					if (!result.Paragraph.IsImplicit)
						UpdateStructuralReference(context, result.Paragraph);
					DetectAmendmentTargets(context, result.Paragraph);
				}
				_journalReferenceService.ParseJournalReferences(result.Article);
				return true;
			}

			// Nierozpoznany — inference-first, potem diagnostyka
			if (classification.Kind == ParagraphKind.Unknown)
			{
				HandleUnknown(context, text, sourceStyleId, layout);
				return true; // zawsze "skonsumowany"
			}

			if (context.CurrentArticle == null)
			{
				Log.Warning("Akapit {Kind} poza kontekstem artykułu (metadane lub pre-art.) — pominięty. Styl: {StyleId}",
					classification.Kind, sourceStyleId);
				return false;
			}

			switch (classification.Kind)
			{
				case ParagraphKind.Paragraph:
					context.CurrentParagraph = _paragraphBuilder.Build(
						new ParagraphBuildInput(context.CurrentArticle, context.CurrentParagraph, text));
					ValidationReporter.AddClassificationWarning(context.CurrentParagraph, classification, "UST");
					context.CurrentPoint = null;
					context.CurrentLetter = null;
					context.ClearTiretStack();

					UpdateStructuralReference(context, context.CurrentParagraph);
					DetectAmendmentTargets(context, context.CurrentParagraph);
					return true;

				case ParagraphKind.Point:
					var ensuredParagraph = _paragraphBuilder.EnsureForPoint(context.CurrentArticle, context.CurrentParagraph);
					context.CurrentParagraph = ensuredParagraph.Paragraph;

					// Wiąż intro z segmentem rodzica przed dodaniem pierwszego punktu
					if (context.CurrentParagraph.Points.Count == 0)
						ParsingFactories.AttachIntroCommonPart(context.CurrentParagraph);

					context.CurrentPoint = _pointBuilder.Build(
						new PointBuildInput(context.CurrentParagraph, context.CurrentArticle, text));
					ValidationReporter.AddClassificationWarning(context.CurrentPoint, classification, "PKT");
					context.CurrentLetter = null;
					context.ClearTiretStack();

					UpdateStructuralReference(context, context.CurrentPoint);
					DetectAmendmentTargets(context, context.CurrentPoint);
					return true;

				case ParagraphKind.Letter:
					var ensuredPoint = _pointBuilder.EnsureForLetter(context.CurrentParagraph, context.CurrentArticle, context.CurrentPoint);
					context.CurrentPoint = ensuredPoint.Point;
					if (ensuredPoint.CreatedImplicit)
					{
						ValidationReporter.AddValidationMessage(context.CurrentPoint, ValidationLevel.Warning,
							"Brak jawnego punktu; utworzono niejawny punkt na podstawie struktury.");
					}

					// Wiąż intro z segmentem rodzica przed dodaniem pierwszej litery
					if (context.CurrentPoint.Letters.Count == 0)
						ParsingFactories.AttachIntroCommonPart(context.CurrentPoint);

					context.CurrentLetter = _letterBuilder.Build(
						new LetterBuildInput(context.CurrentPoint, context.CurrentParagraph, context.CurrentArticle, text));
					ValidationReporter.AddClassificationWarning(context.CurrentLetter, classification, "LIT");
					context.ClearTiretStack();

					UpdateStructuralReference(context, context.CurrentLetter);
					DetectAmendmentTargets(context, context.CurrentLetter);
					return true;

				case ParagraphKind.Tiret:
					var ensuredPointForTiret = _pointBuilder.EnsureForLetter(context.CurrentParagraph, context.CurrentArticle, context.CurrentPoint);
					context.CurrentPoint = ensuredPointForTiret.Point;
					if (ensuredPointForTiret.CreatedImplicit)
					{
						ValidationReporter.AddValidationMessage(context.CurrentPoint, ValidationLevel.Warning,
							"Brak jawnego punktu; utworzono niejawny punkt na podstawie struktury.");
					}

					var ensuredLetter = _letterBuilder.EnsureForTiret(
						context.CurrentPoint, context.CurrentParagraph, context.CurrentArticle, context.CurrentLetter);
					context.CurrentLetter = ensuredLetter.Letter;
					if (ensuredLetter.CreatedImplicit)
					{
						ValidationReporter.AddValidationMessage(context.CurrentLetter, ValidationLevel.Warning,
							"Brak jawnej litery; utworzono niejawna litere na podstawie struktury.");
					}

					// Wiąż intro z segmentem rodzica przed dodaniem pierwszego tiretu
					if (context.CurrentLetter.Tirets.Count == 0)
						ParsingFactories.AttachIntroCommonPart(context.CurrentLetter);

					var tiretDepth = GetTiretDepth(sourceStyleId, layout, context);

					// Skroc stos do glebokosci depth-1 (usun tirety glebsze lub rowne)
					context.PopTiretsToDepth(tiretDepth);

					// Straznik: przy glebokosci > 1 rodzic musi istniec; inaczej degradacja do poziomu 1
					// (np. 2TIR/wciecie bez poprzedzajacego tiretu — zamiast wyjatku).
					var parentTiret = tiretDepth > 1 && context.TiretStack.Count > 0 ? context.TiretStack[^1] : null;

					// Intro wspolna - tylko dla pierwszego dziecka na danym poziomie
					if (parentTiret != null && parentTiret.Tirets.Count == 0)
						ParsingFactories.AttachIntroCommonPart(parentTiret);

					var tiretIndex = parentTiret != null
						? parentTiret.Tirets.Count + 1
						: context.CurrentLetter.Tirets.Count + 1;

					var tiret = _tiretBuilder.Build(new TiretBuildInput(
						context.CurrentLetter, context.CurrentPoint, context.CurrentParagraph,
						context.CurrentArticle, text, tiretIndex, parentTiret));
					ValidationReporter.AddClassificationWarning(tiret, classification, "TIR");

					// Gdy zagniezdzenie wywnioskowano z wciecia (a nie ze stylu 2TIR/3TIR) — audyt decyzji (§ 58 ZTP).
					if (parentTiret != null && !StyleEncodesTiretDepth(sourceStyleId))
						ValidationReporter.AddValidationMessage(tiret, ValidationLevel.Info,
							$"Glebokosc tiretu ({tiretDepth}) ustalona z wciecia (§ 58 ZTP).");
					// Degradacja: sklasyfikowano na poziom > 1, ale brak tiretu nadrzednego — slad diagnostyczny.
					else if (tiretDepth > 1 && parentTiret == null)
						ValidationReporter.AddValidationMessage(tiret, ValidationLevel.Warning,
							$"Tiret sklasyfikowany na poziom {tiretDepth}, ale brak tiretu nadrzednego — umieszczono na poziomie 1.");

					// Tirety o glebokosci ustalonej ze STYLU nie wnosza wciecia jako punktu odniesienia dla
					// wnioskowania (ich wciecie moze byc null lub sprzeczne z narzucona stylem glebokoscia) —
					// inaczej mieszanie trybu stylowego i wcieciowego w obrebie litery falszowaloby zagniezdzenie.
					context.PushTiret(tiret, StyleDecidesTiretDepth(sourceStyleId) ? null : layout?.LeftIndentTwips);

					UpdateStructuralReference(context, tiret);
					DetectAmendmentTargets(context, tiret);
					return true;

				case ParagraphKind.WrapUp:
					TryHandleWrapUp(context, text, sourceStyleId);
					return true;

				default:
					Log.Warning("Nieobsługiwany ParagraphKind {Kind} w switch — pominięty (styl={StyleId})",
						classification.Kind, sourceStyleId);
					return false;
			}
		}

		// ============================================================
		// Metody pomocnicze
		// ============================================================

		/// <summary>
		/// Obsługuje akapit Unknown: najpierw próbuje wywnioskować rodzaj z treści,
		/// a gdy brak wzorca — dołącza ValidationMessage do najgłębszej aktywnej encji.
		/// </summary>
		private void HandleUnknown(ParsingContext context, string text, string? sourceStyleId, BlockLayoutInfo? layout)
		{
			// Krok 1: wywnioskuj z treści (ochrona przed edge cases LayeredClassifier)
			if (context.CurrentArticle != null)
			{
				var inferredKind = TryInferKindFromText(text);
				if (inferredKind != ParagraphKind.Unknown)
				{
					Log.Warning("Akapit Unknown — wywnioskowany jako {Kind} z wzorca tekstowego " +
						"(styl: {StyleId}): {Text}",
						inferredKind, sourceStyleId,
						text.Length > 60 ? text[..60] + "…" : text);

					var rescued = new ClassificationResult
					{
						Kind       = inferredKind,
						Confidence = 50,
						Penalties  =
						[
							new ClassificationPenalty
							{
								Reason = "Wywnioskowano z wzorca tekstowego po braku klasyfikacji",
								Value  = 50,
							},
						],
					};
					Process(context, rescued, text, sourceStyleId, layout);
					return;
				}
			}

			// Krok 2: brak wzorca — diagnostyka bez encji
			if (context.CurrentArticle == null)
			{
				// Strefa tytułowa (§ 16-19): spróbuj wychwycić metadane aktu (rodzaj/data/przedmiot).
				if (context.Metadata.Observe(text))
					return;

				Log.Warning("Akapit Unknown przed pierwszym artykułem (metadane) — pominięty. " +
					"Styl: {StyleId}, Tekst: {Text}",
					sourceStyleId, text.Length > 80 ? text[..80] + "…" : text);
				return;
			}

			var deepest = (BaseEntity?)context.CurrentLetter
				?? (BaseEntity?)context.CurrentPoint
				?? (BaseEntity?)context.CurrentParagraph
				?? (BaseEntity?)context.CurrentArticle;

			deepest?.ValidationMessages.Add(new ValidationMessage(
				ValidationLevel.Warning,
				$"Pominięty akapit nierozpoznany (styl: '{sourceStyleId ?? "brak"}')." +
				$" Tekst: '{(text.Length > 80 ? text[..80] + "…" : text)}'"));

			Log.Warning("Akapit Unknown pominięty — brak dopasowania do wzorca. " +
				"Styl: {StyleId}, Tekst: {Text}",
				sourceStyleId, text.Length > 80 ? text[..80] + "…" : text);
		}

		private static ParagraphKind TryInferKindFromText(string text)
		{
			if (ParagraphClassifier.IsParagraphByText(text)) return ParagraphKind.Paragraph;
			if (ParagraphClassifier.IsPointByText(text))     return ParagraphKind.Point;
			if (ParagraphClassifier.IsLetterByText(text))    return ParagraphKind.Letter;
			if (ParagraphClassifier.IsTiretByText(text))     return ParagraphKind.Tiret;
			return ParagraphKind.Unknown;
		}

		// ============================================================
		// Jednostki systematyzacyjne (§ 60-62 ZTP)
		// ============================================================

		private static bool IsSystematizing(ParagraphKind kind) => kind is
			ParagraphKind.PartUnit or ParagraphKind.BookUnit or ParagraphKind.TitleUnit or
			ParagraphKind.DivisionUnit or ParagraphKind.ChapterUnit or ParagraphKind.SubchapterUnit;

		private void HandleSystematizingUnit(ParsingContext context, ClassificationResult classification,
			string text, string? sourceStyleId)
		{
			var number = ParagraphClassifier.ParseSystematizingNumber(classification.Kind, text);
			if (number == null)
			{
				// MatchSystematizingRegex waliduje numer, więc tu nie powinniśmy trafić — defensywnie.
				Log.Warning("Jednostka systematyzacyjna {Kind} bez rozpoznanego numeru (styl={StyleId}): {Text}",
					classification.Kind, sourceStyleId, text);
				return;
			}

			_systematizingUnitBuilder.Enter(context, classification.Kind, number);
			Log.Debug("Wejście w jednostkę systematyzacyjną {Kind} {Number}", classification.Kind, number.Value);
		}

		/// <summary>
		/// Czy akapit to prawdopodobny tytuł jednostki (§ 60): krótki, rozpoczyna się wielką literą
		/// i NIE kończy się kropką (tytuł jednostki nie ma kropki końcowej; zdanie treści ma) — chroni
		/// przed pochłonięciem realnej treści jako tytułu jednostki bez tytułu.
		/// </summary>
		private static bool IsHeadingCandidate(string text)
		{
			var t = text.Trim();
			if (t.Length == 0 || t.Length > 120)
				return false;
			if (!char.IsLetter(t[0]) || !char.IsUpper(t[0]))
				return false;
			return t[^1] != '.';
		}

		/// <summary>
		/// Aktualizuje bieżącą pozycję strukturalną w kontekście na podstawie
		/// numeru zbudowanej encji. Ustawienie poziomu resetuje podrzędne
		/// (np. WithArticle zeruje ust/pkt/lit/tir), aby zachować spójne eId.
		/// </summary>
		private static void UpdateStructuralReference(ParsingContext context, BaseEntity entity)
		{
			var numberValue = entity.Number?.Value;
			if (string.IsNullOrEmpty(numberValue))
				return;

			context.CurrentStructuralReference = entity.UnitType switch
			{
				UnitType.Article   => context.CurrentStructuralReference.WithArticle(numberValue),
				UnitType.Paragraph => context.CurrentStructuralReference.WithParagraph(numberValue),
				UnitType.Point     => context.CurrentStructuralReference.WithPoint(numberValue),
				UnitType.Letter    => context.CurrentStructuralReference.WithLetter(numberValue),
				UnitType.Tiret     => context.CurrentStructuralReference.WithTiret(numberValue),
				_                  => context.CurrentStructuralReference
			};
		}

		/// <summary>
		/// Wykrywa cele nowelizacji w treści encji implementującej IHasAmendments.
		/// Parsuje wzorce takie jak "w art. 5", "po ust. 2" itp.
		/// Kontekst jest dziedziczony z encji nadrzędnych (kumulacja referencji rozproszonych po poziomach).
		/// </summary>
		private static void DetectAmendmentTargets(ParsingContext context, BaseEntity entity)
		{
			if (entity is not IHasAmendments)
				return;

			if (string.IsNullOrWhiteSpace(entity.ContentText))
				return;

			// Zacznij od kontekstu rodzica (jeśli istnieje wykryty cel w encji nadrzędnej)
			var parentRef = FindParentAmendmentTargetReference(context, entity);
			var targetRef = parentRef?.Clone() ?? new StructuralReference();
			context.ReferenceService.UpdateLegalReference(targetRef, entity.ContentText);

			// Sprawdź czy wykryto jakikolwiek cel nowelizacji
			if (targetRef.Article == null && targetRef.Paragraph == null &&
				targetRef.Point == null && targetRef.Letter == null && targetRef.Tiret == null)
			{
				return;
			}

			var amendmentRef = new StructuralAmendmentReference
			{
				Structure = targetRef,
				RawText = entity.ContentText
			};

			context.DetectedAmendmentTargets[entity.Guid] = amendmentRef;

			Log.Debug("Wykryto cel nowelizacji w {UnitType} [{EntityId}]: {AmendmentTarget}",
				entity.UnitType, entity.Id, amendmentRef);
		}

		/// <summary>
		/// Przeszukuje encje nadrzędne w hierarchii (Parent), szukając wcześniej
		/// wykrytego celu nowelizacji, którego kontekst może być odziedziczony.
		/// </summary>
		private static StructuralReference? FindParentAmendmentTargetReference(ParsingContext context, BaseEntity entity)
		{
			var current = entity.Parent;
			while (current != null)
			{
				if (context.DetectedAmendmentTargets.TryGetValue(current.Guid, out var parentTarget))
					return parentTarget.Structure;
				current = current.Parent;
			}
			return null;
		}

		/// <summary>Tolerancja wciecia (twips, ~6 pt) przy porownywaniu poziomow tiretow (§ 58 ZTP).</summary>
		private const int TiretIndentTolerance = 120;

		/// <summary>Maks. glebokosc tiretu odwzorowana w modelu/stylach (TIR/2TIR/3TIR).</summary>
		private const int MaxTiretDepth = 3;

		/// <summary>
		/// Wyznacza glebokosc tiretu. Priorytet:
		/// 1) styl jawnie kodujacy glebokosc (2TIR/3TIR/TIR) — dokumenty szablonowe niezmienione;
		/// 2) wciecie lewe bloku wzgledem tiretow otwartych na stosie (§ 58 ZTP) — dla dokumentow bezstylowych z ukladem;
		/// 3) brak sygnalu (np. czysty TXT) — domyslnie poziom 1 (zachowanie dotychczasowe).
		/// </summary>
		private static int GetTiretDepth(string? styleId, BlockLayoutInfo? layout, ParsingContext context)
		{
			if (!string.IsNullOrEmpty(styleId))
			{
				if (styleId.StartsWith("3TIR", StringComparison.OrdinalIgnoreCase)) return 3;
				if (styleId.StartsWith("2TIR", StringComparison.OrdinalIgnoreCase)) return 2;
				if (styleId.StartsWith("TIR", StringComparison.OrdinalIgnoreCase)) return 1;
			}

			if (layout?.LeftIndentTwips is int curIndent)
			{
				var inferred = InferTiretDepthFromIndent(curIndent, context.OpenTiretIndents);
				if (inferred != null) return inferred.Value;
			}

			return 1;
		}

		/// <summary>Czy styl jawnie koduje NIEZEROWA glebokosc zagniezdzenia tiretu (2TIR/3TIR).</summary>
		private static bool StyleEncodesTiretDepth(string? styleId) =>
			!string.IsNullOrEmpty(styleId) &&
			(styleId.StartsWith("2TIR", StringComparison.OrdinalIgnoreCase) ||
			 styleId.StartsWith("3TIR", StringComparison.OrdinalIgnoreCase));

		/// <summary>Czy to glebokosc tiretu rozstrzyga STYL (TIR/2TIR/3TIR), a nie wciecie.</summary>
		private static bool StyleDecidesTiretDepth(string? styleId) =>
			(!string.IsNullOrEmpty(styleId) && styleId.StartsWith("TIR", StringComparison.OrdinalIgnoreCase))
			|| StyleEncodesTiretDepth(styleId);

		/// <summary>
		/// Wnioskuje glebokosc tiretu z wciecia lewego wzgledem wciec tiretow otwartych na stosie.
		/// Wieksze wciecie (o > tolerancje) niz najglebszy ZNANY punkt odniesienia = dziecko biezacego szczytu
		/// stosu; wciecie w tolerancji = rodzenstwo tego poziomu; mniejsze = rodzenstwo plytszego poziomu.
		/// Zwraca null, gdy brak punktu odniesienia (stos pusty lub same wciecia null) — wtedy wywolujacy
		/// przyjmuje poziom 1. UWAGA (ograniczenie ekstrakcji ukladu): LeftIndentTwips pochodzi wylacznie
		/// z bezposredniego w:left akapitu (DocxBlockReader), nie z wciec dziedziczonych ze stylu/numeracji;
		/// tiret z wcieciem dziedziczonym trafia tu jako null i domyslnie ląduje na poziomie 1 (Etap 9/PDF: geometria).
		/// </summary>
		private static int? InferTiretDepthFromIndent(int curIndent, IReadOnlyList<int?> openIndents)
		{
			int deepest = openIndents.Count - 1;
			while (deepest >= 0 && openIndents[deepest] == null) deepest--;
			if (deepest < 0) return null;

			int refIndent = openIndents[deepest]!.Value;

			// Glebiej niz najglebszy ZNANY punkt odniesienia → dziecko RZECZYWISTEGO szczytu stosu.
			// Glebokosc liczymy od pelnej wysokosci stosu (openIndents.Count), nie od poziomu odniesienia,
			// bo miedzy nim a szczytem moga stac tirety o wcieciu null (styl 2TIR/3TIR) — inaczej zaniżalibysmy.
			if (curIndent > refIndent + TiretIndentTolerance)
				return System.Math.Min(openIndents.Count + 1, MaxTiretDepth);

			// W przeciwnym razie znajdz najglebszy poziom, ktorego wciecie nie jest wieksze od biezacego
			// (w tolerancji) — to poziom-rodzenstwo biezacego tiretu.
			for (int d = deepest + 1; d >= 1; d--)
			{
				var ind = openIndents[d - 1];
				if (ind == null) continue;
				if (curIndent >= ind.Value - TiretIndentTolerance)
					return d;
			}
			return 1;
		}

		private static bool IsWrapUpByText(string text)
		{
			if (string.IsNullOrWhiteSpace(text))
				return false;

			var trimmed = text.TrimStart();
			if (trimmed.Length < 2)
				return false;

			var first = trimmed[0];
			if (first != '\u2013' && first != '-')
				return false;

			return char.IsWhiteSpace(trimmed[1]);
		}

		private static bool TryGetWrapUpTarget(string? styleId, out AmendmentTargetKind targetKind)
		{
			targetKind = AmendmentTargetKind.Unknown;
			if (!StyleLibraryMapper.TryGetStyleInfo(styleId, out var info) || info == null)
				return false;

			var name = info.DisplayName;
			if (name.StartsWith("CZ_WSP_PKT \u2013", StringComparison.OrdinalIgnoreCase))
			{
				targetKind = AmendmentTargetKind.Point;
				return true;
			}

			if (name.StartsWith("CZ_WSP_LIT \u2013", StringComparison.OrdinalIgnoreCase))
			{
				targetKind = AmendmentTargetKind.Letter;
				return true;
			}

			if (name.StartsWith("CZ_WSP_TIR \u2013", StringComparison.OrdinalIgnoreCase))
			{
				targetKind = AmendmentTargetKind.Tiret;
				return true;
			}

			return false;
		}
	}
}
