using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Mubsir.App.Audio;
using Mubsir.Core.Agent;
using Mubsir.Core.Cost;
using Mubsir.Core.Logging;
using Mubsir.Core.Setup;

namespace Mubsir.App.Brain;

/// <summary>Speech to text with the same Fish Audio key as the voice. It gets no language hint, so mixed sentences stay mixed.</summary>
internal sealed class FishEars(SecretStore secrets, IEventLog log, CostLedger? ledger = null, Action? onCostRecorded = null, HttpMessageHandler? handler = null) : IDisposable
{
    private const string Endpoint = "https://api.fish.audio/v1/asr";
    private const string Model = "transcribe-1-pro";
    public static readonly TimeSpan CallTimeout = TimeSpan.FromSeconds(20);

    private readonly HttpClient _http = new(handler ?? new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(5) }) { Timeout = Timeout.InfiniteTimeSpan };

    public async Task<EarsResult> TranscribeAsync(byte[] wav, CancellationToken ct)
    {
        string? key = secrets.Get(SecretNames.Fish);
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new EarsException(BrainErrorKind.NoApiKey, "no_api_key");
        }

        var clock = Stopwatch.StartNew();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(CallTimeout);
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key.Trim());
            request.Headers.TryAddWithoutValidation("model", Model);
            using var form = new MultipartFormDataContent();
            var audio = new ByteArrayContent(wav);
            audio.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
            form.Add(audio, "audio", "speech.wav");
            form.Add(new StringContent("false"), "tag_audio_events");
            form.Add(new StringContent("false"), "diarize");
            request.Content = form;
            using HttpResponseMessage response = await _http.SendAsync(request, timeout.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                throw response.StatusCode == HttpStatusCode.PaymentRequired
                    ? new EarsException(BrainErrorKind.Auth, "http_402")
                    : GeminiEars.MapStatus(response.StatusCode);
            }

            // Hearing at $0.36 per hour of audio
            TimeSpan duration = AudioMath.GetWavDuration(wav);
            ledger?.AddFishHearing(duration);
            onCostRecorded?.Invoke();

            await using Stream body = await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false);
            using JsonDocument json = await JsonDocument.ParseAsync(body, cancellationToken: timeout.Token).ConfigureAwait(false);
            string text = json.RootElement.TryGetProperty("text", out JsonElement t) ? t.GetString() ?? string.Empty : string.Empty;
            log.Write(new LogEvent(DateTimeOffset.UtcNow, LogKind.Transcribe, LogOutcome.Ok, Tool: "fish", LatencyMs: clock.ElapsedMilliseconds));
            return new EarsResult(text.Trim(), null, null, null);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new EarsException(BrainErrorKind.Overloaded, "timeout");
        }
        catch (Exception e) when (e is HttpRequestException or JsonException)
        {
            throw new EarsException(BrainErrorKind.Other, MetaLog.Code(e));
        }
    }

    public void Dispose() => _http.Dispose();
}

/// <summary>Hearing that keeps working if one service fails: it tries whichever keys exist, one after the other.</summary>
internal sealed class EarsRouter(GeminiEars gemini, FishEars fish, SecretStore secrets, SettingsProvider settings, IEventLog log) : IDisposable
{
    public bool HasAnyKey => secrets.Has(SecretNames.Gemini) || secrets.Has(SecretNames.Fish);

    public async Task<EarsResult> TranscribeAsync(byte[] wav, EarsHints hints, CancellationToken ct)
    {
        if (settings.Settings.PrivateMode)
        {
            throw new EarsException(BrainErrorKind.Other, "private_mode");
        }

        // The chosen service first; the other one only if the first fails.
        IReadOnlyList<EarsChoice> order = Providers.Ears(settings.Settings.Ears, secrets.Held());
        if (order.Count == 0)
        {
            return await gemini.TranscribeAsync(wav, hints, ct).ConfigureAwait(false);
        }

        for (int i = 0; ; i++)
        {
            try
            {
                return order[i] == EarsChoice.Fish
                    ? await fish.TranscribeAsync(wav, ct).ConfigureAwait(false)
                    : await gemini.TranscribeAsync(wav, hints, ct).ConfigureAwait(false);
            }
            catch (EarsException e) when (i + 1 < order.Count && e.Code != "private_mode")
            {
                log.Fail(LogKind.Transcribe, "fallback_" + order[i + 1].ToString().ToLowerInvariant() + "_" + e.Code);
            }
        }
    }

    public void Dispose()
    {
        gemini.Dispose();
        fish.Dispose();
    }
}
