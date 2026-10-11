using Basira.App.Abstractions;
using Basira.App.Diagnostics;
using Basira.Core.Common;
using Basira.Core.Localization;
using Basira.Core.Logging;
using Basira.Core.Speech;
using Basira.Core.Text;

namespace Basira.App.Orchestration;

/// <summary>
/// Local reading, no AI: read the window, say-all from here, OCR, "where am I", and moving through what Basira said
/// (previous/next sentence and paragraph, repeat). While a say-all speaks, the command layer stays open for the arrows.
/// </summary>
internal sealed class ReadingController
{
    private const int RepeatTwiceMs = 1500;
    private static readonly TimeSpan ReadTimeout = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan OcrTimeout = TimeSpan.FromSeconds(15);

    private readonly AppServices _app;
    private readonly Narrator _narrator;
    private long _lastRepeatAt = long.MinValue;
    private int _readId;
    private int _currentReadUtterance = -1;

    public ReadingController(AppServices app, Narrator narrator)
    {
        _app = app;
        _narrator = narrator;
    }

    public Task ReadWindowAsync(CancellationToken ct) =>
        ReadAsync(ct2 => _app.Screen!.ReadTextAsync(ReadScope.Window, ct2), ReadTimeout, "read_window", ct);

    /// <summary>Say-all of the document or window from the current point.</summary>
    public Task ReadFromHereAsync(CancellationToken ct) =>
        ReadAsync(ct2 => _app.Screen!.ReadTextAsync(ReadScope.Document, ct2), ReadTimeout, "read_document", ct);

    public Task OcrWindowAsync(CancellationToken ct) =>
        ReadAsync(ct2 => _app.Screen!.OcrForegroundAsync(ct2), OcrTimeout, "ocr_window", ct);

    public async Task WhereAmIAsync(CancellationToken ct)
    {
        if (_app.Screen is null)
        {
            _app.SayNow(AppPhrase.FeatureUnavailable);
            return;
        }

        string? line = await Bounded(ct2 => _app.Screen.DescribeFocusAsync(_app.UiLang, ct2), ReadTimeout, "where_am_i", ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(line))
        {
            _app.SayNow(StringKey.NothingToRead);
            return;
        }

        _app.Speech.Speak(line, UtteranceKind.System, ScriptSegmenter.DominantLanguage(line, _app.UiLang), interrupt: true);
    }

    public void PreviousSentence() => SpeakFrom(_app.Speech.History.Previous());

    public void NextSentence() => SpeakFrom(_app.Speech.History.Next());

    public void PreviousParagraph() => SpeakFrom(_app.Speech.History.PreviousParagraph());

    public void NextParagraph() => SpeakFrom(_app.Speech.History.NextParagraph());

    /// <summary>Once: the last sentence. Twice within 1.5 s: the whole last answer (or last utterance).</summary>
    public void Repeat()
    {
        long now = _app.Clock.NowMs;
        bool twice = now - Interlocked.Exchange(ref _lastRepeatAt, now) <= RepeatTwiceMs;
        SpeechHistory history = _app.Speech.History;
        IReadOnlyList<SpeechItem> items;
        if (twice)
        {
            IReadOnlyList<SpeechItem> answer = _narrator.LastAnswerItems();
            items = answer.Count > 0 && history.LastSentence()?.Kind == UtteranceKind.Answer ? answer : history.LastUtterance();
            Interlocked.Exchange(ref _lastRepeatAt, long.MinValue);
        }
        else
        {
            items = history.LastSentence() is { } last ? [last] : [];
        }

        if (items.Count == 0)
        {
            _app.SayNow(StringKey.NothingToRepeat);
            return;
        }

        _app.Speech.SpeakItems(items, LangOf(items[0]), interrupt: true);
    }

    /// <summary>Within a say-all, continue reading from the item; otherwise speak just that sentence.</summary>
    private void SpeakFrom(SpeechItem? item)
    {
        if (item is null)
        {
            _app.Speech.PlayEarcon(Earcon.Error);
            return;
        }

        IReadOnlyList<SpeechItem> items = item.UtteranceId == Volatile.Read(ref _currentReadUtterance)
            ? _app.Speech.History.From(item.Index).Where(i => i.UtteranceId == item.UtteranceId).ToArray()
            : [item];

        if (items.Count > 1)
        {
            int id = Interlocked.Increment(ref _readId);
            _app.Input.SetReading(true);
            _ = _app.Speech.SpeakItemsAsync(items, LangOf(item), CancellationToken.None, interrupt: true)
                .ContinueWith(_ => EndReading(id), TaskScheduler.Default);
        }
        else
        {
            _app.Speech.SpeakItems(items, LangOf(item), interrupt: true);
        }
    }

    private Lang LangOf(SpeechItem item) => ScriptSegmenter.DominantLanguage(item.Text, _app.UiLang);

    private async Task ReadAsync(Func<CancellationToken, Task<string?>> read, TimeSpan timeout, string what, CancellationToken ct)
    {
        if (_app.Screen is null)
        {
            _app.SayNow(AppPhrase.FeatureUnavailable);
            return;
        }

        string? text = await Bounded(read, timeout, what, ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(text))
        {
            _app.SayNow(StringKey.NothingToRead);
            return;
        }

        int id = Interlocked.Increment(ref _readId);
        Lang lang = ScriptSegmenter.DominantLanguage(text, _app.UiLang);
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        IReadOnlyList<int> indices = _app.Speech.Enqueue(text, UtteranceKind.Reading, lang, interrupt: true, VerbalizeMode.Normal, done);
        if (indices.Count == 0)
        {
            return;
        }

        SpeechItem? first = _app.Speech.History.From(indices[0]).FirstOrDefault();
        Volatile.Write(ref _currentReadUtterance, first?.UtteranceId ?? -1);
        _app.Input.SetReading(true);
        _app.Log.Event(LogKind.Speech, tool: what);
        try
        {
            // Completes when the last sentence was spoken or the queue was stopped.
            await done.Task.WaitAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            EndReading(id);
        }
    }

    private void EndReading(int id)
    {
        if (Volatile.Read(ref _readId) == id)
        {
            _app.Input.SetReading(false);
        }
    }

    private async Task<string?> Bounded(Func<CancellationToken, Task<string?>> read, TimeSpan timeout, string what, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeout);
        try
        {
            return await read(cts.Token).WaitAsync(cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            _app.Log.Error(what + "_timeout", LogKind.Speech, LogOutcome.TimedOut);
            return null;
        }
        catch (Exception e) when (e is not OperationCanceledException and not OutOfMemoryException)
        {
            _app.Log.Failure(what, e);
            return null;
        }
    }
}
