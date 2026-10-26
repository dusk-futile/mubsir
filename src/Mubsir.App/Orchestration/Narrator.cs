using System.Text;
using Mubsir.Core.Agent;
using Mubsir.Core.Common;
using Mubsir.Core.Speech;
using Mubsir.Core.Text;

namespace Mubsir.App.Orchestration;

internal sealed class Narrator : INarrator
{
    private readonly object _gate = new();
    private readonly AppServices _app;
    private readonly StringBuilder _buffer = new();
    private readonly List<int> _answerIndices = [];
    private Lang _lang;
    private bool _aiEarconPlayed;

    public Narrator(AppServices app)
    {
        _app = app;
        _lang = app.UiLang;
    }

    public event Action? FirstOutput;

    public bool HasOutput { get; private set; }

    public void BeginTask(Lang lang)
    {
        lock (_gate)
        {
            _lang = lang;
            _buffer.Clear();
            _aiEarconPlayed = false;
            HasOutput = false;
            _answerIndices.Clear();
        }
    }

    public IReadOnlyList<SpeechItem> LastAnswerItems()
    {
        int[] indices;
        lock (_gate)
        {
            indices = _answerIndices.ToArray();
        }

        if (indices.Length == 0)
        {
            return [];
        }

        var wanted = new HashSet<int>(indices);
        return _app.Speech.History.From(indices[0]).Where(i => wanted.Contains(i.Index)).ToArray();
    }

    public void Narrate(string text, NarrationKind kind)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        Lang lang;
        lock (_gate)
        {
            lang = _lang;
        }

        try
        {
            if (kind == NarrationKind.Warning)
            {
                _app.Speech.PlayEarcon(Earcon.Error);
            }

            UtteranceKind utterance = kind == NarrationKind.Step ? UtteranceKind.Narration : UtteranceKind.System;
            _app.Speech.Speak(text, utterance, ScriptSegmenter.DominantLanguage(text, lang));
            MarkOutput();
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            _app.Log.Failure("narrate", e);
        }
    }

    public void AnswerDelta(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        List<string> ready;
        lock (_gate)
        {
            _buffer.Append(text);
            ready = TakeCompleteSentences(flushAll: false);
        }

        SpeakAnswer(ready);
    }

    public void AnswerEnd()
    {
        List<string> rest;
        lock (_gate)
        {
            rest = TakeCompleteSentences(flushAll: true);
        }

        SpeakAnswer(rest);
    }

    public void Play(Earcon earcon) => _app.Speech.PlayEarcon(earcon);

    private List<string> TakeCompleteSentences(bool flushAll)
    {
        var result = new List<string>();
        string pending = _buffer.ToString();
        if (pending.Trim().Length == 0)
        {
            if (flushAll)
            {
                _buffer.Clear();
            }

            return result;
        }

        IReadOnlyList<string> sentences = SentenceSplitter.Split(pending);
        if (flushAll)
        {
            result.AddRange(sentences);
            _buffer.Clear();
            return result;
        }

        bool lastComplete = pending.EndsWith('\n');
        int complete = lastComplete ? sentences.Count : sentences.Count - 1;
        if (complete <= 0)
        {
            return result;
        }

        for (int i = 0; i < complete; i++)
        {
            result.Add(sentences[i]);
        }

        if (lastComplete)
        {
            _buffer.Clear();
        }
        else
        {
            string last = sentences[^1];
            int at = pending.LastIndexOf(last, StringComparison.Ordinal);
            _buffer.Clear().Append(at >= 0 ? pending[at..] : last);
        }

        return result;
    }

    private void SpeakAnswer(List<string> sentences)
    {
        if (sentences.Count == 0)
        {
            return;
        }

        try
        {
            Lang lang;
            bool playEarcon;
            lock (_gate)
            {
                lang = _lang;
                playEarcon = !_aiEarconPlayed;
                _aiEarconPlayed = true;
            }

            if (playEarcon)
            {
                _app.Speech.PlayEarcon(Earcon.AiContent);
            }

            foreach (string sentence in sentences)
            {
                if (string.IsNullOrWhiteSpace(sentence))
                {
                    continue;
                }

                IReadOnlyList<int> indices = _app.Speech.Enqueue(sentence, UtteranceKind.Answer, lang, interrupt: false, VerbalizeMode.Normal, done: null);
                lock (_gate)
                {
                    _answerIndices.AddRange(indices);
                }
            }

            MarkOutput();
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            _app.Log.Failure("answer_speak", e);
        }
    }

    private void MarkOutput()
    {
        bool first;
        lock (_gate)
        {
            first = !HasOutput;
            HasOutput = true;
        }

        if (first)
        {
            FirstOutput?.Invoke();
        }
    }
}
