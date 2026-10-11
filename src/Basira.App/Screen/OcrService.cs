using Basira.Core.Text;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;

namespace Basira.App.Screen;

/// <summary>
/// Windows OCR (local, free). Arabic first (ar-SA, then ar-EG), then the user's profile languages, then English.
/// Windows returns Arabic words left to right by position; <see cref="OcrLineOrder"/> restores reading order.
/// </summary>
internal sealed class OcrService
{
    private static readonly string[] PreferredLanguages = ["ar-SA", "ar-EG"];

    private readonly object _gate = new();
    private OcrEngine? _engine;
    private bool _tried;

    /// <summary>Largest image side the engine accepts (falls back to 2600, the documented value).</summary>
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

    /// <summary>True when some OCR language is installed.</summary>
    public bool IsAvailable => Engine() is not null;

    public async Task<string?> RecognizeAsync(SoftwareBitmap bitmap, CancellationToken ct)
    {
        var engine = Engine();
        if (engine is null)
        {
            return null;
        }

        var result = await engine.RecognizeAsync(bitmap).AsTask(ct).ConfigureAwait(false);
        return ToLogicalText(result);
    }

    internal static string? ToLogicalText(OcrResult? result)
    {
        if (result?.Lines is null || result.Lines.Count == 0)
        {
            return null;
        }

        var lines = new List<(double Top, string Text)>(result.Lines.Count);
        foreach (var line in result.Lines)
        {
            var words = new List<OcrWordBox>(line.Words.Count);
            double top = double.MaxValue;
            foreach (var w in line.Words)
            {
                var r = w.BoundingRect;
                words.Add(new OcrWordBox(w.Text, r.X, r.Y, r.Width, r.Height));
                top = Math.Min(top, r.Y);
            }

            var text = OcrLineOrder.ToLogicalLine(words);
            if (!string.IsNullOrWhiteSpace(text))
            {
                lines.Add((top, text));
            }
        }

        return lines.Count == 0 ? null : string.Join('\n', lines.OrderBy(l => l.Top).Select(l => l.Text));
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
