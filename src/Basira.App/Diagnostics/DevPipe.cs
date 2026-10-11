using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using Basira.Core.Input;
using Basira.Core.Logging;
using Basira.Core.Router;

namespace Basira.App.Diagnostics;

/// <summary>What the dev pipe can drive.</summary>
internal sealed record DevPipeHandlers(
    Func<KeyEvent, KeyDecision> InjectKey,
    Func<string, Task> SubmitRequest,
    Func<LocalCommand, string?, Task> RunCommand,
    Func<string> State);

/// <summary>
/// Dev builds only (<c>--dev</c>): a named pipe <c>\\.\pipe\basira-dev</c>, restricted to the current user, that takes
/// one JSON object per line and answers with one JSON line, so scenarios can be scripted from the test PC:
/// <code>
/// {"type":"key","vk":163,"scan":29,"ext":true,"down":true}
/// {"type":"request","text":"..."}
/// {"type":"command","name":"ReadScreen","arg":null}
/// {"type":"state"}
/// </code>
/// Key events are fed into the same state machine as physical keys.
/// </summary>
internal sealed class DevPipe : IDisposable
{
    public const string PipeName = "basira-dev";
    private const int MaxLineChars = 16 * 1024;

    private readonly DevPipeHandlers _handlers;
    private readonly IEventLog _log;
    private readonly CancellationTokenSource _stop = new();
    private Task? _loop;

    public DevPipe(DevPipeHandlers handlers, IEventLog log)
    {
        _handlers = handlers;
        _log = log;
    }

    public void Start() => _loop = Task.Run(AcceptLoopAsync);

    public void Dispose()
    {
        _stop.Cancel();
        try
        {
            _loop?.Wait(TimeSpan.FromSeconds(1));
        }
        catch (AggregateException)
        {
        }

        _stop.Dispose();
    }

    /// <summary>Handles one request line and returns the reply line (pure apart from the handlers; testable).</summary>
    public string HandleLine(string line)
    {
        try
        {
            using JsonDocument doc = JsonDocument.Parse(line);
            JsonElement root = doc.RootElement;
            string type = root.TryGetProperty("type", out JsonElement t) && t.ValueKind == JsonValueKind.String ? t.GetString()! : "";
            switch (type)
            {
                case "key":
                {
                    var e = new KeyEvent(
                        VirtualKey: GetInt(root, "vk"),
                        ScanCode: GetInt(root, "scan"),
                        Extended: GetBool(root, "ext"),
                        IsDown: GetBool(root, "down"),
                        IsInjected: false,
                        TimeMs: Environment.TickCount64);
                    KeyDecision decision = _handlers.InjectKey(e);
                    return Reply(true, ("decision", decision.ToString()));
                }

                case "request":
                {
                    string? text = GetString(root, "text");
                    if (string.IsNullOrWhiteSpace(text))
                    {
                        return Reply(false, ("error", "empty text"));
                    }

                    _ = _handlers.SubmitRequest(text);
                    return Reply(true);
                }

                case "command":
                {
                    if (!CommandGrammar.TryParseName(GetString(root, "name"), out LocalCommand command))
                    {
                        return Reply(false, ("error", "unknown command"));
                    }

                    _ = _handlers.RunCommand(command, GetString(root, "arg"));
                    return Reply(true);
                }

                case "state":
                    return Reply(true, ("state", _handlers.State()));
                default:
                    return Reply(false, ("error", "unknown type"));
            }
        }
        catch (JsonException)
        {
            return Reply(false, ("error", "bad json"));
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            _log.Failure("devpipe_handle", e);
            return Reply(false, ("error", e.GetType().Name));
        }
    }

    private static int GetInt(JsonElement root, string name) =>
        root.TryGetProperty(name, out JsonElement v) && v.TryGetInt32(out int i) ? i : 0;

    private static bool GetBool(JsonElement root, string name) =>
        root.TryGetProperty(name, out JsonElement v) && v.ValueKind == JsonValueKind.True;

    private static string? GetString(JsonElement root, string name) =>
        root.TryGetProperty(name, out JsonElement v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static string Reply(bool ok, params (string Key, string Value)[] fields)
    {
        using var buffer = new MemoryStream(128);
        using (var w = new Utf8JsonWriter(buffer))
        {
            w.WriteStartObject();
            w.WriteBoolean("ok", ok);
            foreach (var (key, value) in fields)
            {
                w.WriteString(key, value);
            }

            w.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    private async Task AcceptLoopAsync()
    {
        while (!_stop.IsCancellationRequested)
        {
            NamedPipeServerStream? pipe = null;
            try
            {
                pipe = new NamedPipeServerStream(
                    PipeName,
                    PipeDirection.InOut,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await pipe.WaitForConnectionAsync(_stop.Token).ConfigureAwait(false);
                _log.Event(LogKind.Startup, tool: "devpipe_client");
                await ServeAsync(pipe).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                _log.Failure("devpipe", e);
                try
                {
                    await Task.Delay(1000, _stop.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
            finally
            {
                if (pipe is not null)
                {
                    await pipe.DisposeAsync().ConfigureAwait(false);
                }
            }
        }
    }

    private async Task ServeAsync(NamedPipeServerStream pipe)
    {
        var utf8 = new UTF8Encoding(false);
        using var reader = new StreamReader(pipe, utf8, false, 4096, leaveOpen: true);
        await using var writer = new StreamWriter(pipe, utf8, 4096, leaveOpen: true) { AutoFlush = true, NewLine = "\n" };
        while (!_stop.IsCancellationRequested && pipe.IsConnected)
        {
            string? line = await reader.ReadLineAsync(_stop.Token).ConfigureAwait(false);
            if (line is null)
            {
                return;
            }

            string reply = line.Length > MaxLineChars
                ? Reply(false, ("error", "line too long"))
                : HandleLine(line);
            await writer.WriteLineAsync(reply.AsMemory(), _stop.Token).ConfigureAwait(false);
        }
    }
}
