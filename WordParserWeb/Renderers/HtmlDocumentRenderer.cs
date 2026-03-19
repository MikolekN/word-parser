using System.Text;
using System.Text.Encodings.Web;
using ModelDto;

namespace WordParserWeb;

static class HtmlDocumentRenderer
{
    private static readonly HtmlEncoder Encoder = HtmlEncoder.Default;

    public static string RenderUploadForm(string? errorMessage)
    {
        var sb = new StringBuilder();

        sb.AppendLine("<section class=\"panel\">");
        sb.AppendLine("  <h1>WordParserWeb</h1>");
        sb.AppendLine("  <p class=\"lead\">Przeslij plik DOCX, aby zobaczyc wynik parsowania w HTML.</p>");

        if (!string.IsNullOrWhiteSpace(errorMessage))
        {
            sb.AppendLine($"  <div class=\"error\">{Encoder.Encode(errorMessage)}</div>");
        }

        sb.AppendLine("  <form method=\"post\" enctype=\"multipart/form-data\" action=\"/parse\">");
        sb.AppendLine("    <input type=\"file\" name=\"docxFile\" accept=\".docx\" required />");
        sb.AppendLine("    <button type=\"submit\">Parsuj dokument</button>");
        sb.AppendLine("  </form>");
        sb.AppendLine("</section>");

        return WrapPage("WordParserWeb", sb.ToString());
    }

    public static string RenderDocument(LegalDocument document, string fileName)
    {
        var sb = new StringBuilder();

        sb.AppendLine("<section class=\"panel\">");
        sb.AppendLine("  <a class=\"back\" href=\"/\">&#8592; Wroc do formularza</a>");
        sb.AppendLine("  <h1>Wynik parsowania</h1>");
        sb.AppendLine($"  <div class=\"meta\">Plik: <strong>{Encoder.Encode(fileName)}</strong></div>");
        sb.AppendLine($"  <div class=\"doc-title\">{Encoder.Encode(document.Type.ToFriendlyString().ToUpper())}: {Encoder.Encode(document.Title)} ({Encoder.Encode(document.SourceJournal?.ToString() ?? "brak")})</div>");
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

    private static string WrapPage(string title, string body)
    {
        return HtmlTemplateStore.RenderPage(title, body);
    }
}
