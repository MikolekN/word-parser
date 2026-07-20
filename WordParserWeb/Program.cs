using DocumentFormat.OpenXml.Packaging;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Caching.Memory;
using Serilog;
using WordParserCore;
using WordParserCore.Exceptions;
using WordParserWeb;

// Limit rozmiaru przesyłanego dokumentu — akty prawne (DOCX/PDF/TXT) mieszczą się z dużym zapasem.
const long MaxUploadBytes = 64L * 1024 * 1024;

var builder = WebApplication.CreateBuilder(args);

LoggerConfig.ConfigureLogger();

builder.Services.AddMemoryCache(o => o.SizeLimit = 256L * 1024 * 1024);
builder.Services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = MaxUploadBytes);
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = MaxUploadBytes);

var app = builder.Build();

HtmlTemplateStore.Initialize(app.Environment.ContentRootPath);

// Defense-in-depth: bez zgadywania typu treści przez przeglądarkę (wszystko serwujemy jako text/html).
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    await next();
});

static IResult Html(string html) => Results.Content(html, "text/html; charset=utf-8");
static string ForcedViewKey(string token) => $"forced-view:{token}";

app.MapGet("/", () => Html(HtmlDocumentRenderer.RenderUploadForm(null)));

app.MapPost("/parse", async (IFormFile? documentFile, IMemoryCache cache) =>
{
    if (documentFile == null || documentFile.Length == 0)
    {
        return Html(HtmlDocumentRenderer.RenderUploadForm("Nie wybrano pliku (obsługiwane formaty: DOCX, PDF, TXT)."));
    }

    try
    {
        // Parsowanie ze strumienia w pamięci — bez pliku tymczasowego na dysku (decyzja planu).
        // Pojemność z góry: bez podwajania bufora przy kopiowaniu (limit rozmiaru pilnuje Kestrel).
        using var buffer = new MemoryStream(capacity: (int)documentFile.Length);
        await documentFile.CopyToAsync(buffer);
        buffer.Position = 0;

        // AlwaysParse: jedno przejście potoku obsługuje oba widoki — akt (dokument) oraz
        // nie-akt (raport klasyfikacji + zbuforowany widok „Parsuj mimo wszystko" bez ponownego uploadu).
        ParseResult result;
        string? forcedParseError = null;
        try
        {
            result = LegalDocumentParser.Parse(buffer, documentFile.FileName,
                new ParseOptions { Policy = ParsePolicy.AlwaysParse });
        }
        catch (ParsingException ex) when (ex is not ScannedPdfException and not UnsupportedDocumentFormatException)
        {
            // Budowa modelu (zwłaszcza nie-aktu) może się nie powieść — raport klasyfikacji nadal się należy.
            buffer.Position = 0;
            result = LegalDocumentParser.Parse(buffer, documentFile.FileName,
                new ParseOptions { Policy = ParsePolicy.ClassifyOnly });
            forcedParseError = ex.Message;
        }

        if (result.Classification.IsLegalAct && result.Document is not null)
        {
            return Html(HtmlDocumentRenderer.RenderDocument(result.Document, documentFile.FileName));
        }

        string? forcedToken = null;
        if (result.Document is not null)
        {
            forcedToken = Guid.NewGuid().ToString("N");
            string forcedHtml = HtmlDocumentRenderer.RenderDocument(result.Document, documentFile.FileName);
            cache.Set(ForcedViewKey(forcedToken), forcedHtml, new MemoryCacheEntryOptions
            {
                SlidingExpiration = TimeSpan.FromMinutes(15),
                // SizeLimit cache jest w bajtach — string trzyma 2 bajty na znak.
                Size = (long)forcedHtml.Length * sizeof(char),
            });
        }

        return Html(HtmlDocumentRenderer.RenderClassificationReport(result, documentFile.FileName, forcedToken, forcedParseError));
    }
    catch (ScannedPdfException ex)
    {
        Log.Warning(ex, "Odrzucono PDF bez uzytecznej warstwy tekstowej.");
        return Html(HtmlDocumentRenderer.RenderUploadForm(ex.Message));
    }
    catch (UnsupportedDocumentFormatException ex)
    {
        Log.Warning(ex, "Odrzucono dokument o nieobslugiwanym formacie.");
        return Html(HtmlDocumentRenderer.RenderUploadForm(ex.Message));
    }
    catch (OpenXmlPackageException ex)
    {
        Log.Warning(ex, "Uszkodzony pakiet DOCX.");
        return Html(HtmlDocumentRenderer.RenderUploadForm("Plik nie jest poprawnym dokumentem Word (DOCX)."));
    }
    catch (ParsingException ex)
    {
        // Domyka też ścieżkę retry: gdy ClassifyOnly ponowi wyjątek adaptera (np. pakiet OPC
        // bez word/document.xml), użytkownik ma dostać komunikat, nie HTTP 500.
        Log.Warning(ex, "Nie udalo sie przetworzyc dokumentu.");
        return Html(HtmlDocumentRenderer.RenderUploadForm($"Nie udało się przetworzyć dokumentu: {ex.Message}"));
    }
    catch (IOException ioEx)
    {
        Log.Error(ioEx, "Blad wejscia/wyjscia podczas przetwarzania pliku.");
        return Html(HtmlDocumentRenderer.RenderUploadForm("Nie udalo sie odczytac pliku. Sprobuj ponownie."));
    }
}).DisableAntiforgery();

app.MapGet("/parse/forced/{token}", (string token, IMemoryCache cache) =>
    cache.TryGetValue(ForcedViewKey(token), out string? html) && html is not null
        ? Html(html)
        : Html(HtmlDocumentRenderer.RenderUploadForm("Widok wymuszonego parsowania wygasł — prześlij plik ponownie.")));

app.Run();
