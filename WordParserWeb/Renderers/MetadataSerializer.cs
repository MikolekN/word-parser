using System.Text.Json.Nodes;
using ModelDto;
using ModelDto.EditorialUnits;

namespace WordParserWeb;

static class MetadataSerializer
{
    public static string Serialize(BaseEntity entity, bool isAmendmentContent = false)
    {
        var obj = new JsonObject();

        obj["eid"] = entity.Id;
        obj["unitType"] = entity.UnitType.ToString();
        obj["unitTypeLabel"] = GetPolishLabel(entity.UnitType);
        if (isAmendmentContent) obj["isAmendmentContent"] = true;

        if (entity.Number != null)
        {
            obj["number"] = entity.Number.Value;
            if (entity.Number.NumericPart != 0)
                obj["numericPart"] = entity.Number.NumericPart;
            if (!string.IsNullOrEmpty(entity.Number.LexicalPart))
                obj["lexicalPart"] = entity.Number.LexicalPart;
            if (!string.IsNullOrEmpty(entity.Number.Superscript))
                obj["superscript"] = entity.Number.Superscript;
        }

        if (!string.IsNullOrEmpty(entity.ContentText))
        {
            obj["contentText"] = entity.ContentText.Length > 300
                ? entity.ContentText[..300] + "…"
                : entity.ContentText;
        }

        if (entity.EffectiveDate != default)
            obj["effectiveDate"] = entity.EffectiveDate.ToString("yyyy-MM-dd");

        if (entity.ValidationMessages.Count > 0)
        {
            var vmArray = new JsonArray();
            foreach (var vm in entity.ValidationMessages)
            {
                vmArray.Add(new JsonObject
                {
                    ["level"] = vm.Level.ToString(),
                    ["message"] = vm.Message
                });
            }
            obj["validationMessages"] = vmArray;
        }

        if (entity is IHasAmendments { Amendment: { } amendment })
            obj["amendment"] = BuildAmendmentNode(amendment);

        if (entity is IHasCommonParts hasCommonParts)
        {
            var introTexts = new System.Collections.Generic.List<string>();
            foreach (var cp in hasCommonParts.CommonParts)
            {
                if (cp.Type == CommonPartType.Intro && !string.IsNullOrWhiteSpace(cp.ContentText))
                    introTexts.Add(cp.ContentText);
            }
            if (introTexts.Count > 0)
            {
                string combined = string.Join(" ", introTexts);
                obj["introText"] = combined.Length > 300 ? combined[..300] + "…" : combined;
            }
        }

        switch (entity)
        {
            case Article article:
                obj["isAmending"] = article.IsAmending;
                if (article.Journals.Count > 0)
                {
                    var jArray = new JsonArray();
                    foreach (var j in article.Journals) jArray.Add(j.ToString());
                    obj["journals"] = jArray;
                }
                obj["paragraphsCount"] = article.Paragraphs.Count;
                break;

            case Paragraph paragraph:
                obj["isImplicit"] = paragraph.IsImplicit;
                if (!string.IsNullOrEmpty(paragraph.Role))
                    obj["role"] = paragraph.Role;
                obj["textSegmentsCount"] = paragraph.TextSegments.Count;
                obj["commonPartsCount"] = paragraph.CommonParts.Count;
                break;

            case Point point:
                obj["textSegmentsCount"] = point.TextSegments.Count;
                obj["commonPartsCount"] = point.CommonParts.Count;
                obj["hasAmendment"] = point.Amendment != null;
                break;

            case Letter letter:
                obj["textSegmentsCount"] = letter.TextSegments.Count;
                obj["commonPartsCount"] = letter.CommonParts.Count;
                obj["hasAmendment"] = letter.Amendment != null;
                break;

            case Tiret tiret:
                obj["textSegmentsCount"] = tiret.TextSegments.Count;
                obj["hasAmendment"] = tiret.Amendment != null;
                break;
        }

        return obj.ToJsonString();
    }

    private static JsonObject BuildAmendmentNode(Amendment amendment)
    {
        string targetAct = amendment.TargetLegalAct.Positions.Count > 0
            ? $"DU.{amendment.TargetLegalAct.Year}.{string.Join(",", amendment.TargetLegalAct.Positions)}"
            : "brak publikatora";

        string operationTypeLabel = amendment.OperationType switch
        {
            AmendmentOperationType.Repeal => "uchylenie",
            AmendmentOperationType.Insertion => "dodanie",
            AmendmentOperationType.Modification => "zmiana brzmienia",
            AmendmentOperationType.Error => "błąd",
            _ => "nieznany"
        };

        var node = new JsonObject
        {
            ["operationType"] = amendment.OperationType.ToString(),
            ["operationTypeLabel"] = operationTypeLabel,
            ["targetAct"] = targetAct
        };

        if (amendment.Targets.Count > 0)
        {
            var targetsArray = new JsonArray();
            foreach (var t in amendment.Targets) targetsArray.Add(t.ToString());
            node["targets"] = targetsArray;
        }

        if (amendment.EffectiveDate.HasValue)
            node["effectiveDate"] = amendment.EffectiveDate.Value.ToString("yyyy-MM-dd");

        return node;
    }

    private static string GetPolishLabel(UnitType unitType) => unitType switch
    {
        UnitType.Article => "Artykuł",
        UnitType.Paragraph => "Ustęp",
        UnitType.Point => "Punkt",
        UnitType.Letter => "Litera",
        UnitType.Tiret => "Tiret",
        UnitType.CommonPart => "Część wspólna",
        UnitType.Part => "Część",
        UnitType.Book => "Księga",
        UnitType.Title => "Tytuł",
        UnitType.Division => "Dział",
        UnitType.Chapter => "Rozdział",
        UnitType.Subchapter => "Oddział",
        _ => unitType.ToString()
    };
}
