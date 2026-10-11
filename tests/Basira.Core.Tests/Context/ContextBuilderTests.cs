using Basira.Core.Context;

namespace Basira.Core.Tests.Context;

public class ContextBuilderTests
{
    private static UiNode Node(int id, string role, string? name = null, params UiNode[] children) =>
        new() { Id = id, Role = role, Name = name, Children = children };

    private static (ScreenContext Ctx, UiNode Focused) YouTube()
    {
        var search = new UiNode { Id = 7, Role = "edit", Name = "Search", Value = "", IsEditable = true, IsFocused = true };
        var root = Node(1, "window", "YouTube - Google Chrome",
            Node(2, "pane", null,
                Node(3, "link", "Home"),
                search,
                Node(9, "button", "Search"),
                new UiNode { Id = 12, Role = "heading", Name = "Recommended", HeadingLevel = 2 }));
        var ctx = new ScreenContext
        {
            AppName = "Google Chrome",
            ProcessName = "chrome.exe",
            WindowTitle = "YouTube",
            Domain = "www.youtube.com",
            IsBrowser = true,
            Root = root,
            Focused = search,
        };
        return (ctx, search);
    }

    [Fact]
    public void BuildsTheCompactFormat()
    {
        var (ctx, _) = YouTube();
        var built = ContextBuilder.Build(ctx);

        var expected = string.Join('\n',
            "App: Google Chrome | Window: \"YouTube\" | Site: youtube.com",
            "Focus: [7] edit \"Search\"",
            "[3] link \"Home\"",
            "[7] edit \"Search\" value=\"\"",
            "[9] button \"Search\"",
            "[12] heading(2) \"Recommended\"");
        Assert.Equal(expected, built.Text);
        Assert.Equal(4, built.ElementCount);
        Assert.False(built.Truncated);
        Assert.Equal(TokenEstimator.Estimate(built.Text), built.EstimatedTokens);
    }

    [Fact]
    public void IdMapResolvesPrintedIds()
    {
        var (ctx, focused) = YouTube();
        var built = ContextBuilder.Build(ctx);
        Assert.Same(focused, built.IdMap[7]);
        Assert.Equal("Home", built.IdMap[3].Name);
        Assert.Equal(new[] { 3, 7, 9, 12 }, built.IdMap.Keys.Order());
    }

    [Fact]
    public void NamedContainersIndentTheirChildren()
    {
        var root = Node(1, "window", "w",
            Node(2, "group", "Toolbar",
                Node(3, "button", "Back"),
                Node(4, "group", null, Node(5, "button", "Forward"))));
        var built = ContextBuilder.Build(new ScreenContext { Root = root });
        Assert.Contains("\n[2] group \"Toolbar\"\n  [3] button \"Back\"\n  [5] button \"Forward\"", built.Text);
    }

    [Fact]
    public void IndentationStopsGrowingAtMaxDepth()
    {
        UiNode leaf = Node(100, "button", "Deep");
        for (int i = 20; i >= 1; i--)
        {
            leaf = Node(i + 1, "group", "G" + i, leaf);
        }

        var built = ContextBuilder.Build(new ScreenContext { Root = Node(1, "window", "w", leaf) });
        var deepLine = built.Text.Split('\n').Single(l => l.Contains("\"Deep\""));
        Assert.Equal(16, deepLine.Length - deepLine.TrimStart().Length);
        Assert.True(built.Text.Split('\n').All(l => l.Length - l.TrimStart().Length <= 16));
    }

    [Fact]
    public void SkipsOffscreenSubtreesAndDisabledUnnamedNodes()
    {
        var root = Node(1, "window", "w",
            new UiNode { Id = 2, Role = "group", Name = "Hidden", IsOffscreen = true, Children = [Node(3, "button", "Inside hidden")] },
            new UiNode { Id = 4, Role = "button", IsEnabled = false },
            new UiNode { Id = 5, Role = "button", Name = "Save", IsEnabled = false },
            Node(6, "button", "Visible"));
        var built = ContextBuilder.Build(new ScreenContext { Root = root });
        Assert.DoesNotContain("Hidden", built.Text);
        Assert.DoesNotContain("Inside hidden", built.Text);
        Assert.DoesNotContain("[4]", built.Text);
        Assert.Contains("[5] button \"Save\" disabled", built.Text);
        Assert.Contains("[6] button \"Visible\"", built.Text);
    }

    [Fact]
    public void PasswordValueIsNeverPrinted()
    {
        var pwd = new UiNode { Id = 5, Role = "edit", Name = "Password", Value = "hunter2-secret", IsPassword = true, IsEditable = true };
        var echo = new UiNode { Id = 6, Role = "edit", Name = "hunter2-secret", Value = "hunter2-secret", IsPassword = true };
        var built = ContextBuilder.Build(new ScreenContext { Root = Node(1, "window", "Login", pwd, echo), Focused = pwd });
        Assert.DoesNotContain("hunter2", built.Text);
        Assert.Contains("[5] edit \"Password\" password value=\"•••\"", built.Text);
        Assert.Contains("[6] edit \"password\" password value=\"•••\"", built.Text);
        Assert.Contains("Focus: [5] edit \"Password\"", built.Text);
    }

    [Fact]
    public void PasswordMaskIsShownEvenWhenEmpty()
    {
        var pwd = new UiNode { Id = 5, Role = "edit", Name = "Password", IsPassword = true };
        var built = ContextBuilder.Build(new ScreenContext { Root = Node(1, "window", "Login", pwd) });
        Assert.Contains("value=\"•••\"", built.Text);
    }

    [Fact]
    public void EditValuesAreRedacted()
    {
        var field = new UiNode { Id = 5, Role = "edit", Name = "Notes", Value = "card 4111 1111 1111 1111 mail a.b@example.com", IsEditable = true };
        var built = ContextBuilder.Build(new ScreenContext { Root = Node(1, "window", "w", field) });
        Assert.Contains("value=\"card [card] mail [email]\"", built.Text);
        Assert.DoesNotContain("4111", built.Text);
    }

    [Fact]
    public void SensitiveFieldValuesAreHidden()
    {
        var otp = new UiNode { Id = 5, Role = "edit", Name = "رمز التحقق", Value = "482913", IsEditable = true };
        var card = new UiNode { Id = 6, Role = "edit", Name = "Card number", Value = "", IsEditable = true };
        var built = ContextBuilder.Build(new ScreenContext { Root = Node(1, "window", "w", otp, card) });
        Assert.DoesNotContain("482913", built.Text);
        Assert.Contains("[5] edit \"رمز التحقق\" value=\"[hidden]\"", built.Text);
        Assert.Contains("[6] edit \"Card number\" value=\"\"", built.Text);
    }

    [Fact]
    public void LongNamesAreTruncatedWithEllipsis()
    {
        var longName = string.Concat(Enumerable.Repeat("word ", 60));
        var built = ContextBuilder.Build(new ScreenContext { Root = Node(1, "window", "w", Node(2, "link", longName)) });
        var line = built.Text.Split('\n').Single(l => l.StartsWith("[2]"));
        var name = line["[2] link \"".Length..^1];
        Assert.EndsWith("…", name);
        Assert.True(name.Length <= ContextBuilder.MaxNameChars);
    }

    [Fact]
    public void NamesAreSingleLineAndQuotesEscaped()
    {
        var built = ContextBuilder.Build(new ScreenContext { Root = Node(1, "window", "w", Node(2, "button", "Say \"hi\"\nnow")) });
        Assert.Contains("[2] button \"Say \\\"hi\\\" now\"", built.Text);
    }

    [Fact]
    public void VisibleTextIsRedactedAndAppendedUnderText()
    {
        var ctx = new ScreenContext
        {
            AppName = "Outlook",
            VisibleText = "Hello Omar,\nYour verification code is 482913.\nCall 01012345678.",
            Root = Node(1, "window", "Inbox", Node(2, "button", "Reply")),
        };
        var built = ContextBuilder.Build(ctx);
        Assert.Contains("\nText:\nHello Omar,\nYour verification code is [code].\nCall [phone].", built.Text);
        Assert.DoesNotContain("482913", built.Text);
        Assert.DoesNotContain("0101234", built.Text);
    }

    [Fact]
    public void VisibleTextCanBeOmitted()
    {
        var ctx = new ScreenContext { VisibleText = "some text", Root = Node(1, "window", "w", Node(2, "button", "OK")) };
        var built = ContextBuilder.Build(ctx, new ContextOptions(IncludeVisibleText: false));
        Assert.DoesNotContain("Text:", built.Text);
        Assert.DoesNotContain("some text", built.Text);
    }

    [Fact]
    public void LongVisibleTextIsCutToItsBudget()
    {
        var text = string.Concat(Enumerable.Repeat("This is a long paragraph of page text. ", 2000));
        var ctx = new ScreenContext { AppName = "Edge", VisibleText = text };
        var built = ContextBuilder.Build(ctx, new ContextOptions(MaxVisibleTextTokens: 100));
        Assert.True(built.Truncated);
        Assert.Contains(ContextBuilder.TruncatedNote, built.Text);
        var textPart = built.Text[(built.Text.IndexOf("Text:\n", StringComparison.Ordinal) + 6)..];
        Assert.True(TokenEstimator.Estimate(textPart) <= 101, $"{TokenEstimator.Estimate(textPart)} tokens");
        Assert.EndsWith("…", textPart);
    }

    [Fact]
    public void CardNumberAtTheCutIsNotHalfLeaked()
    {
        var text = new string('a', 396) + " 4111 1111 1111 1111 tail";
        var built = ContextBuilder.Build(new ScreenContext { VisibleText = text }, new ContextOptions(MaxVisibleTextTokens: 103));
        Assert.DoesNotContain("4111", built.Text);
    }

    [Fact]
    public void ElementCapKeepsFocusedElementAndAncestors()
    {
        var focused = new UiNode { Id = 500, Role = "edit", Name = "Message", IsEditable = true, IsFocused = true };
        var buttons = Enumerable.Range(10, 50).Select(i => Node(i, "button", "B" + i)).ToList();
        var footer = Node(400, "group", "Footer", focused);
        var root = Node(1, "window", "w", buttons.Cast<UiNode>().Append(footer).ToArray());
        var built = ContextBuilder.Build(new ScreenContext { Root = root, Focused = focused }, new ContextOptions(MaxElements: 5));

        Assert.Equal(5, built.ElementCount);
        Assert.True(built.Truncated);
        Assert.Contains(ContextBuilder.TruncatedNote, built.Text);
        Assert.Contains("[400] group \"Footer\"", built.Text);
        Assert.Contains("  [500] edit \"Message\" value=\"\"", built.Text);
        Assert.Same(focused, built.IdMap[500]);
        Assert.Contains("[10] button \"B10\"", built.Text);
        Assert.DoesNotContain("[59]", built.Text);
    }

    [Fact]
    public void ElementCapIsBreadthFirst()
    {
        var groups = Enumerable.Range(1, 3)
            .Select(g => Node(g * 100, "group", "Group " + g, Enumerable.Range(1, 10).Select(b => Node(g * 100 + b, "button", $"B{g}.{b}")).ToArray()))
            .ToArray();
        var built = ContextBuilder.Build(new ScreenContext { Root = Node(1, "window", "w", groups) }, new ContextOptions(MaxElements: 4));
        Assert.Contains("[100] group", built.Text);
        Assert.Contains("[200] group", built.Text);
        Assert.Contains("[300] group", built.Text);
        Assert.Contains("[101] button", built.Text);
        Assert.DoesNotContain("[201]", built.Text);
        Assert.Equal(4, built.ElementCount);
    }

    [Fact]
    public void TokenBudgetCapsElements()
    {
        var buttons = Enumerable.Range(10, 400).Select(i => Node(i, "button", "A fairly long button label number " + i)).ToArray();
        var built = ContextBuilder.Build(new ScreenContext { AppName = "App", Root = Node(1, "window", "w", buttons) }, new ContextOptions(MaxTokens: 300));
        Assert.True(built.Truncated);
        Assert.True(built.EstimatedTokens <= 300, built.EstimatedTokens.ToString());
        Assert.True(built.ElementCount < 400);
    }

    [Fact]
    public void DuplicateAndMissingIdsGetFreshUniqueIds()
    {
        var a = Node(5, "button", "A");
        var b = Node(5, "button", "B");
        var c = Node(0, "button", "C");
        var built = ContextBuilder.Build(new ScreenContext { Root = Node(1, "window", "w", a, b, c) });
        Assert.Equal(3, built.IdMap.Count);
        Assert.Same(a, built.IdMap[5]);
        Assert.Contains(built.IdMap, kv => ReferenceEquals(kv.Value, b) && kv.Key != 5);
        Assert.Contains(built.IdMap, kv => ReferenceEquals(kv.Value, c) && kv.Key > 0);
        foreach (var (id, node) in built.IdMap)
        {
            Assert.Contains($"[{id}] button \"{node.Name}\"", built.Text);
        }
    }

    [Fact]
    public void FocusOutsideTheTreeIsStillListed()
    {
        var focused = new UiNode { Id = 77, Role = "edit", Name = "Address and search bar", IsAddressBar = true };
        var built = ContextBuilder.Build(new ScreenContext { Root = Node(1, "window", "w", Node(2, "button", "OK")), Focused = focused });
        Assert.Contains("Focus: [77] edit \"Address and search bar\"", built.Text);
        Assert.Same(focused, built.IdMap[77]);
    }

    [Fact]
    public void FocusIsMatchedByRuntimeId()
    {
        var inTree = new UiNode { Id = 7, Role = "edit", Name = "Search", RuntimeId = "42.1.7" };
        var fromFastQuery = new UiNode { Id = 0, Role = "edit", Name = "Search", RuntimeId = "42.1.7" };
        var built = ContextBuilder.Build(new ScreenContext { Root = Node(1, "window", "w", inTree), Focused = fromFastQuery });
        Assert.Contains("Focus: [7] edit \"Search\"", built.Text);
        Assert.Same(inTree, built.IdMap[7]);
    }

    [Fact]
    public void WalkerTruncationAndAdminWindowAreAnnounced()
    {
        var built = ContextBuilder.Build(new ScreenContext { AppName = "Registry", IsElevated = true, Truncated = true });
        Assert.Contains(ContextBuilder.TruncatedNote, built.Text);
        Assert.Contains(ContextBuilder.ElevatedNote, built.Text);
        Assert.True(built.Truncated);
    }

    [Fact]
    public void DuplicateTextChildIsDropped()
    {
        var built = ContextBuilder.Build(new ScreenContext { Root = Node(1, "window", "w", Node(2, "link", "Home", Node(3, "text", "Home"))) });
        Assert.DoesNotContain("[3]", built.Text);
    }

    [Fact]
    public void UnnamedButtonUsesHelpText()
    {
        var button = new UiNode { Id = 4, Role = "button", HelpText = "Attach file" };
        var built = ContextBuilder.Build(new ScreenContext { Root = Node(1, "window", "w", button, Node(5, "image")) });
        Assert.Contains("[4] button \"Attach file\"", built.Text);
        Assert.DoesNotContain("[5]", built.Text);
    }

    [Fact]
    public void EmptyContextStillHasAHeader()
    {
        var built = ContextBuilder.Build(new ScreenContext());
        Assert.Equal("App: unknown", built.Text);
        Assert.Equal(0, built.ElementCount);
        Assert.Empty(built.IdMap);
    }

    [Fact]
    public void WindowTitleAndSelectionAreRedacted()
    {
        var ctx = new ScreenContext
        {
            AppName = "Chrome",
            WindowTitle = "Inbox (3) - omar@example.com - Gmail",
            Url = "https://mail.google.com/mail/u/0/#inbox",
            Selection = "IBAN GB82 WEST 1234 5698 7654 32",
        };
        var built = ContextBuilder.Build(ctx);
        Assert.Contains("Window: \"Inbox (3) - [email] - Gmail\"", built.Text);
        Assert.Contains("Site: mail.google.com", built.Text);
        Assert.Contains("Selection: \"IBAN [iban]\"", built.Text);
        Assert.DoesNotContain("omar@", built.Text);
    }

    [Fact]
    public void FocusedPasswordFieldInFocusLineHasNoValue()
    {
        var pwd = new UiNode { Id = 3, Role = "edit", Name = "Password", Value = "s3cret!", IsPassword = true, IsFocused = true };
        var built = ContextBuilder.Build(new ScreenContext { Focused = pwd });
        Assert.DoesNotContain("s3cret", built.Text);
        Assert.Contains("Focus: [3] edit \"Password\"", built.Text);
    }

    [Fact]
    public void ToStringOfNodesHasNoContent()
    {
        var pwd = new UiNode { Id = 3, Role = "edit", Name = "Password", Value = "s3cret!", IsPassword = true };
        var screen = new ScreenContext { WindowTitle = "My bank - omar@example.com", Url = "https://bank.example/secret", Focused = pwd };
        Assert.DoesNotContain("s3cret", pwd.ToString());
        Assert.DoesNotContain("omar", screen.ToString());
        Assert.DoesNotContain("secret", screen.ToString());
    }
}
