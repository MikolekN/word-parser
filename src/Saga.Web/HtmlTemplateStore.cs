using System.Text.Encodings.Web;
using Serilog;

namespace WordParserWeb;

static class HtmlTemplateStore
{
    private static readonly HtmlEncoder Encoder = HtmlEncoder.Default;
    private static string? _layoutTemplate;
    private static string? _styles;
    private static string? _scripts;
    private static string _contentRoot = string.Empty;

    public static void Initialize(string contentRoot)
    {
        _contentRoot = contentRoot;
    }

    public static string RenderPage(string title, string body)
    {
        string template = GetLayoutTemplate();
        string styles = GetStyles();
        string scripts = GetScripts();

        return template
            .Replace("{{title}}", Encoder.Encode(title))
            .Replace("{{styles}}", styles)
            .Replace("{{scripts}}", scripts)
            .Replace("{{body}}", body);
    }

    private static string GetLayoutTemplate()
    {
        if (_layoutTemplate != null)
        {
            return _layoutTemplate;
        }

        string path = Path.Combine(_contentRoot, "Templates", "layout.html");
        try
        {
            _layoutTemplate = File.ReadAllText(path);
        }
        catch (IOException ioEx)
        {
            Log.Error(ioEx, "Nie udalo sie wczytac layout.html z {Path}", path);
            _layoutTemplate = "<html><head><title>{{title}}</title><style>{{styles}}</style></head><body>{{body}}<script>{{scripts}}</script></body></html>";
        }
        catch (UnauthorizedAccessException uaEx)
        {
            Log.Error(uaEx, "Brak dostepu do layout.html z {Path}", path);
            _layoutTemplate = "<html><head><title>{{title}}</title><style>{{styles}}</style></head><body>{{body}}<script>{{scripts}}</script></body></html>";
        }

        return _layoutTemplate;
    }

    private static string GetStyles()
    {
        if (_styles != null)
        {
            return _styles;
        }

        string path = Path.Combine(_contentRoot, "Templates", "styles.css");
        try
        {
            _styles = File.ReadAllText(path);
        }
        catch (IOException ioEx)
        {
            Log.Error(ioEx, "Nie udalo sie wczytac styles.css z {Path}", path);
            _styles = "body { font-family: serif; }";
        }
        catch (UnauthorizedAccessException uaEx)
        {
            Log.Error(uaEx, "Brak dostepu do styles.css z {Path}", path);
            _styles = "body { font-family: serif; }";
        }

        return _styles;
    }

    private static string GetScripts()
    {
        if (_scripts != null)
        {
            return _scripts;
        }

        string path = Path.Combine(_contentRoot, "Templates", "scripts.js");
        try
        {
            _scripts = File.ReadAllText(path);
        }
        catch (IOException ioEx)
        {
            Log.Error(ioEx, "Nie udalo sie wczytac scripts.js z {Path}", path);
            _scripts = string.Empty;
        }
        catch (UnauthorizedAccessException uaEx)
        {
            Log.Error(uaEx, "Brak dostepu do scripts.js z {Path}", path);
            _scripts = string.Empty;
        }

        return _scripts;
    }
}
