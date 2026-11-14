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

/// <summary>
/// Any AI service that speaks the common chat format, at the address the person typed in settings: OpenRouter, a
/// router such as OmniRoute, or a model on this PC. Its cost isn't counted, because its prices aren't known.
/// </summary>
internal sealed class CustomBrain(SecretStore secrets, SettingsProvider settings, IEventLog log) : IBrain, IDisposable
{
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(90);
    private readonly HttpClient _http = new(new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(5) }) { Timeout = Timeout.InfiniteTimeSpan };

    public IBrainSession StartSession(BrainSessionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return new Session(this, options);
    }

    public void Dispose() => _http.Dispose();

    private sealed class Session(CustomBrain owner, BrainSessionOptions options) : IBrainSession
    {
        // ponytail: every turn of the task is sent each time (old screen snapshots shortened), and the answer isn't
        // streamed. A task ends after 15 steps; drop old turns and stream if tasks grow or the first word feels late.
        private readonly JsonArray _messages = [];

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public async IAsyncEnumerable<BrainEvent> SendAsync(BrainInput input, [EnumeratorCancellation] CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(input);
            int mark = _messages.Count;
            switch (input)
            {
                case UserTurn turn:
                    _messages.Add((JsonNode)OpenAiWire.UserTurn(turn));
                    break;
                case ToolResults results:
                    foreach (JsonObject answered in OpenAiWire.ToolAnswers(results.Results))
                    {
                        _messages.Add((JsonNode)answered);
                    }

                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(input));
            }

            OpenAiWire.ShortenOldSnapshots(_messages);
            (OpenAiWire.Answer? answer, BrainFailed? failed) = await owner.AskAsync(options, _messages, ct).ConfigureAwait(false);
            if (answer is null)
            {
                while (_messages.Count > mark)
                {
                    _messages.RemoveAt(_messages.Count - 1);
                }

                yield return failed ?? new BrainFailed(BrainErrorKind.Other, "custom_failed");
                yield break;
            }

            _messages.Add((JsonNode)answer.Message);
            if (answer.Text is { } text)
            {
                yield return new TextDelta(text);
            }

            foreach (ToolCall call in answer.Calls)
            {
                yield return new ToolCallRequested(call);
            }

            yield return new TurnCompleted(answer.Calls.Count > 0 ? "tool_use" : "end_turn");
        }
    }

    private async Task<(OpenAiWire.Answer? Answer, BrainFailed? Failed)> AskAsync(BrainSessionOptions options, JsonArray messages, CancellationToken ct)
    {
        MubsirSettings s = settings.Settings;
        if (OpenAiWire.Endpoint(s.CustomBrainUrl) is not { } endpoint || string.IsNullOrWhiteSpace(s.CustomBrainModel))
        {
            return (null, new BrainFailed(BrainErrorKind.NoApiKey, "custom_not_set"));
        }

        var clock = Stopwatch.StartNew();
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limit.CancelAfter(RequestTimeout);
        BrainFailed Fail(BrainErrorKind kind, string code)
        {
            log.Fail(LogKind.BrainRequest, code, latencyMs: clock.ElapsedMilliseconds);
            return new BrainFailed(kind, code);
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
            if (secrets.Get(SecretNames.Custom) is { Length: > 0 } key)
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key.Trim());
            }

            request.Content = new StringContent(OpenAiWire.Request(options, messages, s.CustomBrainModel), Encoding.UTF8, new MediaTypeHeaderValue("application/json"));
            using HttpResponseMessage response = await _http.SendAsync(request, limit.Token).ConfigureAwait(false);
            string body = await response.Content.ReadAsStringAsync(limit.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                int status = (int)response.StatusCode;
                return (null, Fail(GeminiWire.KindOf(status, null), "custom_" + status));
            }

            OpenAiWire.Answer answer = OpenAiWire.Parse(body, () => "c" + Guid.NewGuid().ToString("N"));
            if (answer.Text is null && answer.Calls.Count == 0)
            {
                return (null, Fail(BrainErrorKind.Network, "custom_empty"));
            }

            log.Ok(LogKind.BrainRequest, "custom", clock.ElapsedMilliseconds);
            return (answer, null);
        }
        catch (HttpRequestException)
        {
            return (null, Fail(BrainErrorKind.Network, "custom_network"));
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return (null, Fail(BrainErrorKind.Network, "custom_timeout"));
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or FormatException)
        {
            return (null, Fail(BrainErrorKind.Other, "custom_bad_answer"));
        }
    }
}
