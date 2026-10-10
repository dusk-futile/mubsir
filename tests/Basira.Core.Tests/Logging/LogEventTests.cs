using Basira.Core.Logging;

namespace Basira.Core.Tests.Logging;

public class LogEventTests
{
    private static readonly DateTimeOffset At = new(2026, 10, 11, 8, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("open_app")]
    [InlineData("invoke")]
    [InlineData("UIA.Button")]
    [InlineData("http:429")]
    [InlineData("release_to_first_audio.p50")]
    [InlineData("E-TIMEOUT")]
    [InlineData("a")]
    [InlineData("ControlType.Edit")]
    public void ShortIdentifiersAreKept(string id)
    {
        var e = new LogEvent(At, LogKind.ToolCall, Tool: id, ControlType: id, ErrorCode: id, Metric: id);

        Assert.Equal(id, e.Tool);
        Assert.Equal(id, e.ControlType);
        Assert.Equal(id, e.ErrorCode);
        Assert.Equal(id, e.Metric);
        Assert.True(LogIdentifier.IsValid(id));
    }

    public static TheoryData<string> Content => new()
    {
        // Sentences and transcripts
        "send the money to Ahmed",
        "hello world",
        "password123 is my password",
        "line1\nline2",
        "open_app\n",
        " open_app",
        // URLs, domains and paths
        "https://bank.example.com/login",
        "http://x.y",
        "bank.example.com",
        "nbe.com.eg",
        "www.example",
        "mail.google.com",
        "C:\\Users\\Omar\\Documents\\cv.docx",
        "/home/omar/notes.txt",
        "report.pdf",
        "cv.DOCX",
        "photo.jpg",
        "setup.exe",
        "someone@example",
        // Arabic and other scripts, emoji
        "أرسل المال إلى أحمد",
        "زر_الإرسال",
        "open_تطبيق",
        "кнопка",
        "send😀",
        // Too long
        new string('a', 65),
        // Lookalike characters outside ASCII
        "оpen_app", // Cyrillic o
        "open\u200Bapp",
        "ｏｐｅｎ",
    };

    [Theory]
    [MemberData(nameof(Content))]
    public void ContentCannotBeLoggedThroughIdentifierFields(string content)
    {
        var e = new LogEvent(At, LogKind.Error, LogOutcome.Failed, Tool: content, ControlType: content, ErrorCode: content, Metric: content);

        Assert.Equal(LogIdentifier.Invalid, e.Tool);
        Assert.Equal(LogIdentifier.Invalid, e.ControlType);
        Assert.Equal(LogIdentifier.Invalid, e.ErrorCode);
        Assert.Equal(LogIdentifier.Invalid, e.Metric);

        string json = JsonlEventLog.ToJson(e);
        Assert.DoesNotContain(content, json);
    }

    [Fact]
    public void ExactlySixtyFourCharactersIsAllowed()
    {
        string id = new('x', 64);

        Assert.Equal(id, new LogEvent(At, LogKind.Metric, Metric: id).Metric);
    }

    [Fact]
    public void EmptyStringIsInvalid_NullStaysNull()
    {
        var e = new LogEvent(At, LogKind.ToolCall, Tool: "", ControlType: null);

        Assert.Equal(LogIdentifier.Invalid, e.Tool);
        Assert.Null(e.ControlType);
        Assert.Null(LogIdentifier.Sanitize(null));
        Assert.False(LogIdentifier.IsValid(null));
        Assert.False(LogIdentifier.IsValid(""));
    }

    [Fact]
    public void WithExpressionsAreSanitizedToo()
    {
        var e = new LogEvent(At, LogKind.ToolCall, Tool: "type_text");

        LogEvent changed = e with { Tool = "typed: my secret text", ErrorCode = "https://x.example/a", Metric = "ok_metric" };

        Assert.Equal(LogIdentifier.Invalid, changed.Tool);
        Assert.Equal(LogIdentifier.Invalid, changed.ErrorCode);
        Assert.Equal("ok_metric", changed.Metric);
        Assert.Equal("type_text", e.Tool);
    }

    [Fact]
    public void ObjectInitializersAreSanitizedToo()
    {
        var e = new LogEvent(At, LogKind.ToolCall) { ControlType = "اسم الزر" };

        Assert.Equal(LogIdentifier.Invalid, e.ControlType);
    }

    [Fact]
    public void NonFiniteValuesAreDropped()
    {
        Assert.Null(new LogEvent(At, LogKind.Metric, Metric: "m", Value: double.NaN).Value);
        Assert.Null(new LogEvent(At, LogKind.Metric, Metric: "m", Value: double.PositiveInfinity).Value);
        Assert.Equal(12.5, new LogEvent(At, LogKind.Metric, Metric: "m", Value: 12.5).Value);
    }

    [Fact]
    public void HasNoFreeTextField()
    {
        // By design: every string property of LogEvent is one of the validated identifier fields.
        string[] stringProps = typeof(LogEvent).GetProperties()
            .Where(p => p.PropertyType == typeof(string))
            .Select(p => p.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(["ControlType", "ErrorCode", "Metric", "Tool"], stringProps);
    }

    [Fact]
    public void DefaultsAreOkWithNoOptionalFields()
    {
        var e = new LogEvent(At, LogKind.Startup);

        Assert.Equal(LogOutcome.Ok, e.Outcome);
        Assert.Null(e.Tool);
        Assert.Null(e.LatencyMs);
        Assert.Null(e.CostUsd);
        Assert.Equal("""{"at":"2026-10-11T08:00:00+00:00","kind":"Startup","outcome":"Ok"}""", JsonlEventLog.ToJson(e));
    }
}
