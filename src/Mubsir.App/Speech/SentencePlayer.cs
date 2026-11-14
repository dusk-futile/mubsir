using Mubsir.App.Abstractions;
using Mubsir.Core.Common;
using Mubsir.Core.Logging;
using Windows.Media.Core;
using Windows.Media.Playback;

namespace Mubsir.App.Speech;

/// <summary>A voice that says one sentence at a time. If it can't, the next voice along says it.</summary>
internal interface ISentenceVoice
{
    bool CanSpeak(bool isBook, Lang lang);

    /// <summary>True if the sentence was heard, or was stopped by the user. False if another voice should say it.</summary>
    Task<bool> SpeakAsync(string text, Lang lang, int rate, Action started, bool isBook);

    /// <summary>Starts preparing the next sentence so there's less of a pause.</summary>
    void GetReady(string text, Lang lang, int rate, bool isBook);

    void Stop();
}

/// <summary>
/// Plays one sentence at a time through a single Windows media player that stays open, so the next sentence
/// starts without the pause of setting up a new player. Shared by every voice. Safe to call from any thread.
/// </summary>
internal sealed class SentencePlayer(IEventLog log, string name) : IDisposable
{
    private readonly object _gate = new();
    private MediaPlayer? _player;
    private TaskCompletionSource<bool>? _playing;
    private bool _disposed;

    /// <summary>
    /// Plays the audio and returns when it ends: true if it was heard or stopped by the user, false if it
    /// couldn't play (another voice should say it). <paramref name="started"/> runs as playback begins.
    /// </summary>
    public async Task<bool> PlayAsync(byte[] audio, string contentType, Action started, TimeSpan limit, CancellationToken stopped)
    {
        ArgumentNullException.ThrowIfNull(audio);
        ArgumentNullException.ThrowIfNull(started);
        var done = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var memory = new MemoryStream(audio, writable: false);
        using var stream = memory.AsRandomAccessStream();
        using var source = MediaSource.CreateFromStream(stream, contentType);

        // Handlers belong to this sentence only, so a late event from an earlier one can't end this one.
        void Ended(MediaPlayer sender, object args) => done.TrySetResult(true);
        void Failed(MediaPlayer sender, MediaPlayerFailedEventArgs args)
        {
            log.Fail(LogKind.Speech, name + "_play_" + args.Error);
            done.TrySetResult(false);
        }

        MediaPlayer player;
        lock (_gate)
        {
            if (stopped.IsCancellationRequested || _disposed)
            {
                return true;
            }

            player = _player ??= new MediaPlayer { AutoPlay = false, AudioCategory = MediaPlayerAudioCategory.Speech };
            _playing?.TrySetResult(true);
            _playing = done;
            player.MediaEnded += Ended;
            player.MediaFailed += Failed;

            // Under the lock, so Stop can't come between choosing the sentence and starting it.
            player.Source = source;
            started();
            player.Play();
        }

        try
        {
            using CancellationTokenRegistration onStop = stopped.Register(static d => ((TaskCompletionSource<bool>)d!).TrySetResult(true), done);
            Task finished = await Task.WhenAny(done.Task, Task.Delay(limit)).ConfigureAwait(false);
            if (finished != done.Task)
            {
                log.Error(name + "_play_timeout", LogKind.Speech, LogOutcome.TimedOut);
                return false;
            }

            return await done.Task.ConfigureAwait(false);
        }
        finally
        {
            lock (_gate)
            {
                player.MediaEnded -= Ended;
                player.MediaFailed -= Failed;
                if (ReferenceEquals(_playing, done))
                {
                    _playing = null;
                    Quiet(player);
                }
            }
        }
    }

    /// <summary>Silences the sentence playing now. The player stays open for the next one.</summary>
    public void Stop()
    {
        lock (_gate)
        {
            _playing?.TrySetResult(true);
            _playing = null;
            if (_player is { } player)
            {
                Quiet(player);
            }
        }
    }

    public void Dispose()
    {
        MediaPlayer? player;
        lock (_gate)
        {
            _disposed = true;
            _playing?.TrySetResult(true);
            _playing = null;
            player = _player;
            _player = null;
        }

        try
        {
            player?.Pause();
            player?.Dispose();
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            log.Fail(LogKind.Speech, name + "_dispose_" + MetaLog.Code(e));
        }
    }

    private void Quiet(MediaPlayer player)
    {
        try
        {
            player.Pause();
            player.Source = null;
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            log.Fail(LogKind.Speech, name + "_stop_" + MetaLog.Code(e));
        }
    }
}
