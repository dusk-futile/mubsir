using System.Diagnostics;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Mubsir.Core.Agent;
using Mubsir.Core.Config;
using Mubsir.Core.Logging;

namespace Mubsir.App.Brain;

/// <summary>Gemini as the brain, so Mubsir can run free. Its free use isn't counted in the month's spending.</summary>
internal sealed class GeminiBrain(SecretStore secrets, SettingsProvider settings, IEventLog log) : IBrain, IDisposable
{
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(90);
    private static readonly TimeSpan[] WaitsAfterLimit = [TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15)];
    private readonly HttpClient _http = new(new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(5) }) { Timeout = Timeout.InfiniteTimeSpan };

    public IBrainSession StartSession(BrainSessionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return new Session(this, options);
    }

    public void Dispose() => _http.Dispose();

    private sealed class Session(GeminiBrain owner, BrainSessionOptions options) : IBrainSession
    {
        private readonly JsonArray _contents = [];
        private readonly Dictionary<string, string> _names = new(StringComparer.Ordinal);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public async IAsyncEnumerable<BrainEvent> SendAsync(BrainInput input, [EnumeratorCancellation] CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(input);
            string? key = owner.Key();
            if (key is null)
            {
                yield return new BrainFailed(BrainErrorKind.NoApiKey, "no_api_key");
                yield break;
            }

            int mark = _contents.Count;
            _contents.Add(input switch
            {
                UserTurn turn => GeminiWire.UserTurn(turn),
                ToolResults results => GeminiWire.ToolAnswers(results.Results, _names),
                _ => throw new ArgumentOutOfRangeException(nameof(input)),
            });
            GeminiWire.Trim(_contents);

            string model = GeminiPrompt.SafeModelId(owner.Settings().GeminiBrainModel) ?? MubsirSettings.DefaultGeminiBrainModel;
            string url = GeminiEars.Endpoint + model + ":streamGenerateContent?alt=sse";
            using var limit = CancellationTokenSource.CreateLinkedTokenSource(ct);
            limit.CancelAfter(RequestTimeout);

            var clock = Stopwatch.StartNew();
            HttpResponseMessage? response = null;
            BrainFailed? failed = null;
            for (int attempt = 0; response is null && failed is null; attempt++)
            {
                try
                {
                    using var request = new HttpRequestMessage(HttpMethod.Post, url);
                    request.Headers.TryAddWithoutValidation("x-goog-api-key", key);
                    request.Content = new StringContent(GeminiWire.Request(options, _contents), Encoding.UTF8, new MediaTypeHeaderValue("application/json"));
                    HttpResponseMessage sent = await owner._http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, limit.Token).ConfigureAwait(false);
                    if (sent.IsSuccessStatusCode)
                    {
                        response = sent;
                        break;
                    }

                    int status = (int)sent.StatusCode;
                    string body = await sent.Content.ReadAsStringAsync(limit.Token).ConfigureAwait(false);
                    sent.Dispose();
                    if (status is 429 or 503 && attempt < WaitsAfterLimit.Length)
                    {
                        await Task.Delay(WaitsAfterLimit[attempt], limit.Token).ConfigureAwait(false);
                        continue;
                    }

                    failed = new BrainFailed(GeminiWire.KindOf(status, body), "gemini_" + status);
                }
                catch (HttpRequestException)
                {
                    failed = new BrainFailed(BrainErrorKind.Network, "gemini_network");
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    failed = new BrainFailed(BrainErrorKind.Network, "gemini_timeout");
                }
            }

            if (failed is not null)
            {
                Rollback(mark);
                owner.Log.Fail(LogKind.BrainRequest, failed.Code, latencyMs: clock.ElapsedMilliseconds);
                yield return failed;
                yield break;
            }

            var parts = new JsonArray();
            var calls = new List<ToolCall>();
            string? finish = null;
            string? blocked = null;
            bool cut = false;
            using (response)
            {
                using Stream stream = await response!.Content.ReadAsStreamAsync(limit.Token).ConfigureAwait(false);
                using var reader = new StreamReader(stream, Encoding.UTF8);
                while (true)
                {
                    string? line = null;
                    try
                    {
                        line = await reader.ReadLineAsync(limit.Token).ConfigureAwait(false);
                    }
                    catch (Exception e) when (e is IOException or HttpRequestException || (e is OperationCanceledException && !ct.IsCancellationRequested))
                    {
                        cut = true;
                    }

                    if (line is null)
                    {
                        break;
                    }

                    if (!line.StartsWith("data:", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    GeminiWire.ChunkResult chunk;
                    try
                    {
                        chunk = GeminiWire.Chunk(line[5..].Trim(), () => "g" + Guid.NewGuid().ToString("N"));
                    }
                    catch (Exception e) when (e is JsonException or InvalidOperationException)
                    {
                        cut = true;
                        break;
                    }

                    foreach (JsonNode? part in chunk.Parts)
                    {
                        parts.Add(part?.DeepClone());
                    }

                    calls.AddRange(chunk.Calls);
                    finish = chunk.FinishReason ?? finish;
                    blocked = chunk.BlockReason ?? blocked;
                    if (chunk.Text is { } text)
                    {
                        yield return new TextDelta(text);
                    }
                }
            }

            if (blocked is not null || finish is "SAFETY" or "RECITATION" or "PROHIBITED_CONTENT" or "SPII" or "BLOCKLIST")
            {
                Rollback(mark);
                yield return new BrainFailed(BrainErrorKind.Refusal, "gemini_blocked");
                yield break;
            }

            if (cut || finish is null || parts.Count == 0)
            {
                Rollback(mark);
                owner.Log.Fail(LogKind.BrainRequest, cut ? "gemini_stream_cut" : "gemini_empty", latencyMs: clock.ElapsedMilliseconds);
                yield return new BrainFailed(BrainErrorKind.Network, "stream_cut");
                yield break;
            }

            _contents.Add(new JsonObject { ["role"] = "model", ["parts"] = parts });
            owner.Log.Ok(LogKind.BrainRequest, model, clock.ElapsedMilliseconds);
            foreach (ToolCall call in calls)
            {
                _names[call.Id] = call.Name;
                yield return new ToolCallRequested(call);
            }

            yield return new TurnCompleted(calls.Count > 0 ? "tool_use" : "end_turn");
        }

        private void Rollback(int mark)
        {
            while (_contents.Count > mark)
            {
                _contents.RemoveAt(_contents.Count - 1);
            }
        }
    }

    private IEventLog Log => log;

    private MubsirSettings Settings() => settings.Settings;

    private string? Key()
    {
        string? key = secrets.Get(SecretNames.Gemini);
        return string.IsNullOrWhiteSpace(key) ? null : key.Trim();
    }
}

/// <summary>Claude if there's a key for it, otherwise the free Gemini.</summary>
internal sealed class BrainRouter(IBrain claude, IBrain gemini, SecretStore secrets) : IBrain
{
    public IBrainSession StartSession(BrainSessionOptions options) =>
        secrets.Has(SecretNames.Anthropic) ? claude.StartSession(options) : gemini.StartSession(options);
}
