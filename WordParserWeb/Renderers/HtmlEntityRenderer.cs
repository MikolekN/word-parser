using System.Text;
using System.Text.Encodings.Web;
using ModelDto;
using ModelDto.EditorialUnits;

namespace WordParserWeb;

static class HtmlEntityRenderer
{
    private static readonly HtmlEncoder Encoder = HtmlEncoder.Default;

    public static void RenderArticle(StringBuilder sb, Article article, int depth)
    {
        string amendingInfo = article.IsAmending
            ? $" artykul zmieniajacy akt: {Encoder.Encode(article.Journals.FirstOrDefault()?.ToString() ?? string.Empty)}"
            : string.Empty;

        AppendLine(sb, depth, $"{BuildEntityLead(article)}{amendingInfo}", article.Id,
            entityType: "article", dataMeta: MetadataSerializer.Serialize(article));

        for (int index = 0; index < article.Paragraphs.Count; index++)
        {
            var paragraph = article.Paragraphs[index];
            string? articlePrefix = null;

            if (index == 0 && article.Paragraphs.Count > 1 && !paragraph.IsImplicit)
            {
                articlePrefix = BuildArticlePrefixLabel(article);
            }

            RenderParagraph(sb, paragraph, depth + 1, articlePrefix);
        }
    }

    private static void RenderParagraph(StringBuilder sb, Paragraph paragraph, int depth, string? leadPrefix)
    {
        RenderEntityLine(sb, paragraph, depth, leadPrefix);

        foreach (var point in paragraph.Points)
        {
            RenderPoint(sb, point, depth + 1);
        }

        RenderCommonParts(sb, paragraph.CommonParts, paragraph, depth + 1);
        RenderAmendments(sb, paragraph, depth + 1);
    }

    private static void RenderPoint(StringBuilder sb, Point point, int depth)
    {
        RenderEntityLine(sb, point, depth, null);

        foreach (var letter in point.Letters)
        {
            RenderLetter(sb, letter, depth + 1);
        }

        RenderCommonParts(sb, point.CommonParts, point, depth + 1);
        RenderAmendments(sb, point, depth + 1);
    }

    private static void RenderLetter(StringBuilder sb, Letter letter, int depth)
    {
        RenderEntityLine(sb, letter, depth, null);

        foreach (var tiret in letter.Tirets)
        {
            RenderTiret(sb, tiret, depth + 1);
        }

        RenderCommonParts(sb, letter.CommonParts, letter, depth + 1);
        RenderAmendments(sb, letter, depth + 1);
    }

    private static void RenderTiret(StringBuilder sb, Tiret tiret, int depth)
    {
        RenderEntityLine(sb, tiret, depth, null);

        foreach (var nestedTiret in tiret.Tirets)
        {
            RenderTiret(sb, nestedTiret, depth + 1);
        }

        RenderAmendments(sb, tiret, depth + 1);
    }

    private static void RenderEntityLine(StringBuilder sb, BaseEntity entity, int depth, string? leadPrefix)
    {
        string lead = BuildEntityLead(entity);
        if (!string.IsNullOrWhiteSpace(leadPrefix))
        {
            lead = string.IsNullOrWhiteSpace(lead) ? leadPrefix : $"{leadPrefix} {lead}";
        }
        string title = entity.Id;
        string entityType = entity.UnitType.ToString().ToLowerInvariant();
        string dataMeta = MetadataSerializer.Serialize(entity);
        var hasSegments = entity as IHasTextSegments;

        if (hasSegments != null && hasSegments.TextSegments.Count > 1)
        {
            AppendLine(sb, depth, lead, title, entityType: entityType, dataMeta: dataMeta);

            foreach (var segment in hasSegments.TextSegments)
            {
                string roleTag = !string.IsNullOrEmpty(segment.Role) ? $" ({Encoder.Encode(segment.Role)})" : string.Empty;
                string segmentLine = $"<span class=\"segment\">zd. {segment.Order}: {Encoder.Encode(segment.Text)}{roleTag}</span>";
                AppendLine(sb, depth + 1, segmentLine, title);
            }
        }
        else
        {
            string contentPreview = GetContentPreview(entity.ContentText, 1024);
            if (string.IsNullOrWhiteSpace(contentPreview))
            {
                AppendLine(sb, depth, lead, title, null, entityType, dataMeta);
            }
            else
            {
                string lineContent = string.IsNullOrWhiteSpace(lead)
                    ? Encoder.Encode(contentPreview)
                    : $"{lead} {Encoder.Encode(contentPreview)}";
                AppendLine(sb, depth, lineContent, title, null, entityType, dataMeta);
            }
        }

        foreach (var message in entity.ValidationMessages)
        {
            AppendLine(sb, depth + 1, $"<span class=\"validation\">{Encoder.Encode(message.ToString())}</span>", title);
        }
    }

    private static void RenderCommonParts(StringBuilder sb, List<CommonPart> commonParts, BaseEntity parent, int depth)
    {
        foreach (var cp in commonParts)
        {
            if (cp.Type == CommonPartType.Intro)
            {
                continue;
            }

            string preview = GetContentPreview(cp.ContentText, 120);
            AppendLine(sb, depth, $"<span class=\"common-part\">– {Encoder.Encode(preview)}</span>", null, "wrapup-linked");
        }
    }

    private static void RenderAmendments(StringBuilder sb, BaseEntity entity, int depth)
    {
        if (entity is not IHasAmendments { Amendment: { } amendment })
        {
            return;
        }

        string opLabel = amendment.OperationType switch
        {
            AmendmentOperationType.Repeal => "uchylenie",
            AmendmentOperationType.Insertion => "dodanie",
            AmendmentOperationType.Modification => "zmiana brzmienia",
            AmendmentOperationType.Error => "blad",
            _ => "nieznany"
        };

        string targetActStr = amendment.TargetLegalAct.Positions.Count > 0
            ? $"DU.{amendment.TargetLegalAct.Year}.{string.Join(",", amendment.TargetLegalAct.Positions)}"
            : "brak publikatora";

        bool isRepeal = amendment.OperationType == AmendmentOperationType.Repeal;
        bool isInsertionOrModification = amendment.OperationType is AmendmentOperationType.Insertion or AmendmentOperationType.Modification;
        string? tooltip = null;
        string? contentClass = null;

        if (isRepeal)
        {
            AppendLine(
                sb,
                depth,
                $"<span class=\"amendment\">{opLabel} w akcie: {Encoder.Encode(targetActStr)}</span>",
                null,
                "amendment-repeal");

            foreach (var target in amendment.Targets)
            {
                AppendLine(sb, depth + 1, $"<span class=\"amendment-item\">Cel: {Encoder.Encode(target.ToString())}</span>");
            }
        }
        else if (isInsertionOrModification)
        {
            tooltip = BuildAmendmentTooltip(opLabel, targetActStr, amendment.Targets);
            contentClass = "amendment-positive";
        }

        if (amendment.Content != null)
        {
            RenderAmendmentContent(sb, amendment.Content, depth + 1, tooltip, contentClass);
        }

        if (amendment.EffectiveDate.HasValue)
        {
            AppendLine(sb, depth + 1, $"<span class=\"amendment-item\">Wejscie w zycie: {amendment.EffectiveDate.Value:yyyy-MM-dd}</span>");
        }
    }

    private static void RenderAmendmentContent(StringBuilder sb, AmendmentContent content, int depth, string? tooltip, string? extraClass)
    {
        if (!string.IsNullOrEmpty(content.PlainText))
        {
            AppendLine(
                sb,
                depth,
                $"<span class=\"amendment-item\">Tresc: {Encoder.Encode(GetContentPreview(content.PlainText, 160))}</span>",
                tooltip,
                extraClass);
            return;
        }

        foreach (var article in content.Articles)
        {
            string articleMeta = MetadataSerializer.Serialize(article, isAmendmentContent: true);
            AppendLine(sb, depth, BuildEntityLead(article), tooltip ?? article.Id, extraClass, "article", articleMeta);
            foreach (var paragraph in article.Paragraphs)
            {
                RenderAmendmentEntity(sb, paragraph, depth + 1, tooltip, extraClass);
                foreach (var point in paragraph.Points)
                {
                    RenderAmendmentEntity(sb, point, depth + 2, tooltip, extraClass);
                    foreach (var letter in point.Letters)
                    {
                        RenderAmendmentEntity(sb, letter, depth + 3, tooltip, extraClass);
                        foreach (var tiret in letter.Tirets)
                        {
                            RenderAmendmentTiret(sb, tiret, depth + 4, tooltip, extraClass);
                        }
                    }
                }
            }
        }

        foreach (var paragraph in content.Paragraphs)
        {
            RenderAmendmentEntity(sb, paragraph, depth, tooltip, extraClass);
            foreach (var point in paragraph.Points)
            {
                RenderAmendmentEntity(sb, point, depth + 1, tooltip, extraClass);
            }
        }

        foreach (var point in content.Points)
        {
            RenderAmendmentEntity(sb, point, depth, tooltip, extraClass);
            foreach (var letter in point.Letters)
            {
                RenderAmendmentEntity(sb, letter, depth + 1, tooltip, extraClass);
            }
        }

        foreach (var letter in content.Letters)
        {
            RenderAmendmentEntity(sb, letter, depth, tooltip, extraClass);
            foreach (var tiret in letter.Tirets)
            {
                RenderAmendmentTiret(sb, tiret, depth + 1, tooltip, extraClass);
            }
        }

        foreach (var tiret in content.Tirets)
        {
            RenderAmendmentTiret(sb, tiret, depth, tooltip, extraClass);
        }

        foreach (var cp in content.CommonParts)
        {
            string cpLabel = cp.Type == CommonPartType.Intro ? "wpr. do wyl." : "cz. wsp.";
            AppendLine(
                sb,
                depth,
                $"<span class=\"common-part\">{cpLabel}: {Encoder.Encode(GetContentPreview(cp.ContentText, 120))}</span>",
                tooltip,
                extraClass);
        }
    }

    private static void RenderAmendmentEntity(StringBuilder sb, BaseEntity entity, int depth, string? tooltip, string? extraClass)
    {
        string preview = GetContentPreview(entity.ContentText, 120);
        string lead = BuildEntityLead(entity);
        string title = entity.Id;
        string entityType = entity.UnitType.ToString().ToLowerInvariant();
        string dataMeta = MetadataSerializer.Serialize(entity, isAmendmentContent: true);

        if (string.IsNullOrWhiteSpace(preview))
        {
            AppendLine(sb, depth, lead, tooltip ?? title, extraClass, entityType, dataMeta);
        }
        else
        {
            string lineContent = string.IsNullOrWhiteSpace(lead)
                ? Encoder.Encode(preview)
                : $"{lead} {Encoder.Encode(preview)}";
            AppendLine(sb, depth, lineContent, tooltip ?? title, extraClass, entityType, dataMeta);
        }
    }

    private static void RenderAmendmentTiret(StringBuilder sb, Tiret tiret, int depth, string? tooltip, string? extraClass)
    {
        RenderAmendmentEntity(sb, tiret, depth, tooltip, extraClass);
        foreach (var nested in tiret.Tirets)
        {
            RenderAmendmentTiret(sb, nested, depth + 1, tooltip, extraClass);
        }
    }

    internal static void AppendLine(StringBuilder sb, int depth, string html,
        string? title = null, string? extraClass = null,
        string? entityType = null, string? dataMeta = null)
    {
        int margin = Math.Clamp(depth, 0, 10) * 18;
        string titleAttr = string.IsNullOrWhiteSpace(title) ? string.Empty : $" title=\"{Encoder.Encode(title)}\"";

        var classes = new List<string> { "line" };
        if (!string.IsNullOrWhiteSpace(extraClass)) classes.Add(extraClass);
        if (!string.IsNullOrWhiteSpace(entityType))
        {
            classes.Add("entity");
            classes.Add($"entity-{entityType}");
        }

        string classAttr = string.Join(" ", classes);
        string dataMetaAttr = !string.IsNullOrWhiteSpace(dataMeta)
            ? $" data-meta=\"{Encoder.Encode(dataMeta)}\""
            : string.Empty;

        sb.AppendLine($"<div class=\"{classAttr}\" style=\"margin-left: {margin}px\"{titleAttr}{dataMetaAttr}>{html}</div>");
    }

    private static string BuildEntityLead(BaseEntity entity)
    {
        if (entity is Article)
        {
            return $"<span class=\"entity-id\">[{Encoder.Encode(entity.Id)}]</span>";
        }

        if (entity is Paragraph paragraph)
        {
            string label = paragraph.IsImplicit
                ? BuildImplicitParagraphLabel(paragraph)
                : BuildNumberLabel(paragraph.Number?.Value, ".");
            return WrapPrefix(label);
        }

        if (entity is Point point)
        {
            return WrapPrefix(BuildNumberLabel(point.Number?.Value, ")"));
        }

        if (entity is Letter letter)
        {
            return WrapPrefix(BuildNumberLabel(letter.Number?.Value, ")"));
        }

        if (entity is Tiret)
        {
            return WrapPrefix("–");
        }

        return WrapPrefix(entity.DisplayLabel);
    }

    private static string? BuildArticlePrefixLabel(Article article)
    {
        string? articleNumber = article.Number?.Value;
        if (string.IsNullOrWhiteSpace(articleNumber))
        {
            return null;
        }

        return WrapPrefix($"Art. {articleNumber}.");
    }

    private static string BuildImplicitParagraphLabel(Paragraph paragraph)
    {
        string? articleNumber = paragraph.Article?.Number?.Value;
        if (string.IsNullOrWhiteSpace(articleNumber) && paragraph.Parent is Article parentArticle)
        {
            articleNumber = parentArticle.Number?.Value;
        }

        return string.IsNullOrWhiteSpace(articleNumber) ? "Art." : $"Art. {articleNumber}";
    }

    private static string BuildNumberLabel(string? number, string suffix)
    {
        if (string.IsNullOrWhiteSpace(number))
        {
            return string.Empty;
        }

        return $"{number}{suffix}";
    }

    private static string WrapPrefix(string label)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            return string.Empty;
        }

        return $"<span class=\"entity-prefix\">{Encoder.Encode(label)}</span>";
    }

    private static string BuildAmendmentTooltip(string opLabel, string targetAct, IReadOnlyList<StructuralAmendmentReference> targets)
    {
        string targetInfo = targets.Count > 0
            ? string.Join("; ", targets.Select(target => target.ToString()))
            : "brak";

        return $"Nowelizacja: {opLabel} w akcie: {targetAct}. Cele: {targetInfo}.";
    }

    internal static string GetContentPreview(string content, int maxLength)
    {
        if (string.IsNullOrEmpty(content))
        {
            return string.Empty;
        }

        return content.Length <= maxLength ? content : content[..maxLength];
    }
}
