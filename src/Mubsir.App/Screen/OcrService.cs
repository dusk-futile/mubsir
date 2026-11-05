using Mubsir.Core.Text;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;

namespace Mubsir.App.Screen;

internal sealed class OcrService
{
    private static readonly string[] PreferredLanguages = ["ar-SA", "ar-EG"];

    private readonly object _gate = new();
    private OcrEngine? _engine;
    private OcrEngine? _latin;
    private bool _tried;

    public static int MaxImageDimension
    {
        get
        {
            try
            {
                return (int)OcrEngine.MaxImageDimension;
            }
            catch (Exception)
            {
                return 2600;
            }
        }
    }

    public bool IsAvailable => Engine() is not null;

    /// <summary>Without this, an Arabic scan comes out as nonsense from another language's engine.</summary>
    public static bool HasArabic
    {
        get
        {
            try
            {
                return OcrEngine.AvailableRecognizerLanguages.Any(l => l.LanguageTag.StartsWith("ar", StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception e) when (e is not OutOfMemoryException)
            {
                return false;
            }
        }
    }

    public async Task<string?> RecognizeAsync(SoftwareBitmap bitmap, CancellationToken ct)
    {
        var engine = Engine();
        if (engine is null)
        {
            return null;
        }

        var lines = ToLines(await engine.RecognizeAsync(bitmap).AsTask(ct).ConfigureAwait(false));
        var latin = LatinEngine();
        if (latin is not null && (lines.Count == 0 || OcrRepair.HasLatin(lines)))
        {
            var latinLines = ToLines(await latin.RecognizeAsync(bitmap).AsTask(ct).ConfigureAwait(false));
            return Text(OcrRepair.Merge(lines, latinLines.SelectMany(l => l).ToArray()));
        }

        return Text(string.Join('\n', lines.OrderBy(l => l.Min(w => w.Y)).Select(OcrLineOrder.ToLogicalLine).Where(t => t.Length > 0)));
    }

    private static string? Text(string text) => string.IsNullOrWhiteSpace(text) ? null : text;

    private static List<IReadOnlyList<OcrWordBox>> ToLines(OcrResult? result)
    {
        var lines = new List<IReadOnlyList<OcrWordBox>>();
        if (result?.Lines is null)
        {
            return lines;
        }

        foreach (var line in result.Lines)
        {
            var words = new List<OcrWordBox>(line.Words.Count);
            foreach (var w in line.Words)
            {
                var r = w.BoundingRect;
                if (!string.IsNullOrWhiteSpace(w.Text))
                {
                    words.Add(new OcrWordBox(w.Text, r.X, r.Y, r.Width, r.Height));
                }
            }

            if (words.Count > 0)
            {
                lines.Add(words);
            }
        }

        return lines;
    }

    private OcrEngine? Engine()
    {
        lock (_gate)
        {
            if (_tried)
            {
                return _engine;
            }

            _tried = true;
            foreach (var tag in PreferredLanguages)
            {
                _engine = TryCreate(tag);
                if (_engine is not null)
                {
                    _latin = CreateLatin();
                    return _engine;
                }
            }

            try
            {
                _engine = OcrEngine.TryCreateFromUserProfileLanguages();
            }
            catch (Exception)
            {
                _engine = null;
            }

            return _engine ??= TryCreate("en-US");
        }
    }

    /// <summary>A second engine for English on the page. It's only there when the first one is Arabic.</summary>
    private OcrEngine? LatinEngine()
    {
        Engine();
        lock (_gate)
        {
            return _latin;
        }
    }

    private static OcrEngine? CreateLatin()
    {
        var engine = TryCreate("en-US") ?? TryCreate("en-GB");
        if (engine is not null)
        {
            return engine;
        }

        try
        {
            foreach (var language in OcrEngine.AvailableRecognizerLanguages)
            {
                if (!language.LanguageTag.StartsWith("ar", StringComparison.OrdinalIgnoreCase) && TryCreate(language.LanguageTag) is { } other)
                {
                    return other;
                }
            }
        }
        catch (Exception)
        {
        }

        return null;
    }

    private static OcrEngine? TryCreate(string tag)
    {
        try
        {
            var language = new Language(tag);
            return OcrEngine.IsLanguageSupported(language) ? OcrEngine.TryCreateFromLanguage(language) : null;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
