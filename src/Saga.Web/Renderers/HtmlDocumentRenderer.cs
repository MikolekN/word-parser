using System.Text;
using System.Text.Encodings.Web;
using Saga.Model;
using Saga.Core;
using Saga.Core.Ingest;

namespace Saga.Web;

static class HtmlDocumentRenderer
{
    private static readonly HtmlEncoder Encoder = HtmlEncoder.Default;

    public static string RenderUploadForm(string? errorMessage)
    {
        var sb = new StringBuilder();

        sb.AppendLine("<section class=\"panel\">");
        sb.AppendLine("  <h1>SAGA</h1>");
        sb.AppendLine("  <p class=\"lead\">Prześlij dokument (DOCX, PDF z warstwą tekstową lub TXT), aby zobaczyć wynik klasyfikacji i parsowania.</p>");

        if (!string.IsNullOrWhiteSpace(errorMessage))
        {
            sb.AppendLine($"  <div class=\"error\">{Encoder.Encode(errorMessage)}</div>");
        }

        sb.AppendLine("  <form method=\"post\" enctype=\"multipart/form-data\" action=\"/parse\">");
        sb.AppendLine("    <input type=\"file\" name=\"documentFile\" accept=\".docx,.pdf,.txt\" required />");
        sb.AppendLine("    <button type=\"submit\">Parsuj dokument</button>");
        sb.AppendLine("  </form>");
        sb.AppendLine("</section>");

        return WrapPage("Saga.Web", sb.ToString());
    }

    public static string RenderDocument(LegalDocument document, string fileName)
    {
        var sb = new StringBuilder();

        sb.AppendLine("<section class=\"panel\">");
        sb.AppendLine("  <a class=\"back\" href=\"/\">&#8592; Wroc do formularza</a>");
        sb.AppendLine("  <h1>Wynik parsowania</h1>");
        sb.AppendLine($"  <div class=\"meta\">Plik: <strong>{Encoder.Encode(fileName)}</strong></div>");
        sb.AppendLine($"  <div class=\"doc-title\">{Encoder.Encode(document.Type.ToFriendlyString().ToUpper())}: {Encoder.Encode(document.Title)} ({Encoder.Encode(document.SourceJournal?.ToString() ?? "brak")})</div>");
        AppendClassificationBanner(sb, document.Classification);
        sb.AppendLine("</section>");

        sb.AppendLine("<div class=\"two-col-layout\">");
        sb.AppendLine("  <div class=\"doc-col\">");
        sb.AppendLine("    <section class=\"panel\">");

        bool isFirst = true;
        foreach (var article in document.Articles)
        {
            if (!isFirst)
            {
                sb.AppendLine("      <div class=\"gap\"></div>");
            }

            HtmlEntityRenderer.RenderArticle(sb, article, 0);
            isFirst = false;
        }

        sb.AppendLine("    </section>");
        sb.AppendLine("  </div>");
        sb.AppendLine("  <div class=\"meta-col\" id=\"meta-panel\">");
        sb.AppendLine("    <div class=\"meta-placeholder\">Kliknij element, aby zobaczyc metadane</div>");
        sb.AppendLine("  </div>");
        sb.AppendLine("</div>");

        return WrapPage("Wynik parsowania", sb.ToString());
    }

    /// <summary>
    /// Raport klasyfikacji — strona pokazywana, gdy dokument nie został rozpoznany jako akt
    /// (albo gdy budowa modelu się nie powiodła). Przycisk „Parsuj mimo wszystko" prowadzi do
    /// zbuforowanego widoku wymuszonego parsowania (token), bez ponownego przesyłania pliku.
    /// </summary>
    public static string RenderClassificationReport(ParseResult result, string fileName, string? forcedToken, string? forcedParseError)
    {
        var classification = result.Classification;
        var sb = new StringBuilder();

        sb.AppendLine("<section class=\"panel\">");
        sb.AppendLine("  <a class=\"back\" href=\"/\">&#8592; Wroc do formularza</a>");
        sb.AppendLine("  <h1>Raport klasyfikacji</h1>");
        sb.AppendLine($"  <div class=\"meta\">Plik: <strong>{Encoder.Encode(fileName)}</strong> &middot; format: {FormatLabel(result.SourceFormat)} &middot; bloków: {result.BlockCount}</div>");
        AppendClassificationBanner(sb, classification);

        if (forcedToken != null)
        {
            sb.AppendLine($"  <a class=\"force-button\" href=\"/parse/forced/{Encoder.Encode(forcedToken)}\">Parsuj mimo wszystko</a>");
        }
        if (forcedParseError != null)
        {
            sb.AppendLine($"  <div class=\"error\">Próba zbudowania modelu nie powiodła się: {Encoder.Encode(forcedParseError)}</div>");
        }

        if (classification.Signals.Count > 0)
        {
            sb.AppendLine("  <h2>Sygnały klasyfikacyjne</h2>");
            sb.AppendLine("  <table class=\"signals\">");
            sb.AppendLine("    <tr><th>Waga</th><th>Opis</th><th>Dowód</th></tr>");
            foreach (var signal in classification.Signals)
            {
                sb.AppendLine("    <tr>");
                sb.AppendLine($"      <td>{signal.Score}</td>");
                sb.AppendLine($"      <td>{Encoder.Encode(signal.Description)}</td>");
                sb.AppendLine($"      <td class=\"evidence\">{Encoder.Encode(signal.MatchedText)}</td>");
                sb.AppendLine("    </tr>");
            }
            sb.AppendLine("  </table>");
        }

        sb.AppendLine("</section>");

        return WrapPage("Raport klasyfikacji", sb.ToString());
    }

    /// <summary>Banner z werdyktem klasyfikacji; pomijany, gdy klasyfikacja nie była wykonana.</summary>
    private static void AppendClassificationBanner(StringBuilder sb, DocumentClassificationResult? classification)
    {
        if (classification is null)
        {
            return;
        }

        string cssClass = classification.IsLegalAct ? "classification-banner" : "classification-banner warn";
        sb.AppendLine($"  <div class=\"{cssClass}\">");

        if (classification.IsLegalAct)
        {
            string flags = string.Empty;
            if (classification.IsConsolidatedText)
            {
                flags += " &middot; tekst jednolity";
            }
            if (classification.IsAmending)
            {
                flags += " &middot; akt zmieniający";
            }
            sb.AppendLine($"    Rozpoznano: <strong>{Encoder.Encode(classification.ActType?.ToFriendlyString() ?? "akt prawny")}</strong> (pewność {classification.Confidence}/100){flags}");
        }
        else
        {
            sb.AppendLine($"    <strong>Dokument nie został rozpoznany jako akt prawny</strong> (pewność {classification.Confidence}/100)");
        }

        sb.AppendLine($"    <div class=\"justification\">{Encoder.Encode(classification.Justification)}</div>");
        sb.AppendLine("  </div>");
    }

    private static string FormatLabel(SourceFormat format) => format switch
    {
        SourceFormat.Docx => "DOCX",
        SourceFormat.Pdf => "PDF",
        SourceFormat.PlainText => "TXT",
        _ => "nieznany",
    };

    private static string WrapPage(string title, string body)
    {
        return HtmlTemplateStore.RenderPage(title, body);
    }
}
