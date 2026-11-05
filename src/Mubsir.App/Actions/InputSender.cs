using System.Runtime.InteropServices;
using Mubsir.App.Actions.Interop;
using Mubsir.App.Screen.Interop;

namespace Mubsir.App.Actions;

internal static unsafe class InputSender
{
    public const int TextChunk = 64;

    private static readonly TimeSpan ChunkPause = TimeSpan.FromMilliseconds(15);

    public static bool SendStroke(KeyStroke stroke)
    {
        var inputs = new List<ActionsNative.Input>();
        foreach (var m in stroke.Modifiers)
        {
            inputs.Add(Key(m, up: false));
        }

        inputs.Add(Key(stroke.Key, up: false));
        inputs.Add(Key(stroke.Key, up: true));
        for (int i = stroke.Modifiers.Count - 1; i >= 0; i--)
        {
            inputs.Add(Key(stroke.Modifiers[i], up: true));
        }

        return Send(inputs);
    }

    public static bool SendStrokes(IEnumerable<KeyStroke> strokes)
    {
        foreach (var s in strokes)
        {
            if (!SendStroke(s))
            {
                return false;
            }

            Thread.Sleep(30);
        }

        return true;
    }

    /// <summary>A real mouse click where the screen shows the thing.</summary>
    public static bool Click(int x, int y, string button)
    {
        if (!Native.User32.SetCursorPos(x, y))
        {
            return false;
        }

        Thread.Sleep(40);
        bool right = button == "right";
        uint down = right ? ActionsNative.MouseRightDown : ActionsNative.MouseLeftDown;
        uint up = right ? ActionsNative.MouseRightUp : ActionsNative.MouseLeftUp;
        for (int i = 0; i < (button == "double" ? 2 : 1); i++)
        {
            ActionsNative.mouse_event(down, 0, 0, 0, 0);
            ActionsNative.mouse_event(up, 0, 0, 0, 0);
            Thread.Sleep(60);
        }

        return true;
    }

    public static string? ClipboardText()
    {
        for (int attempt = 0; attempt < 5; attempt++)
        {
            if (ActionsNative.OpenClipboard(0))
            {
                try
                {
                    nint data = ActionsNative.GetClipboardData(ActionsNative.CfUnicodeText);
                    if (data == 0)
                    {
                        return null;
                    }

                    nint locked = ActionsNative.GlobalLock(data);
                    if (locked == 0)
                    {
                        return null;
                    }

                    try
                    {
                        return Marshal.PtrToStringUni(locked);
                    }
                    finally
                    {
                        ActionsNative.GlobalUnlock(data);
                    }
                }
                finally
                {
                    ActionsNative.CloseClipboard();
                }
            }

            Thread.Sleep(50);
        }

        return null;
    }

    public static bool TypeText(string text, CancellationToken ct)
    {
        var batch = new List<ActionsNative.Input>(TextChunk * 2);
        foreach (var piece in TextPieces(text))
        {
            ct.ThrowIfCancellationRequested();
            if (piece == "\n")
            {
                if (!Flush(batch) || !SendStroke(new KeyStroke([KeyChordParser.VkShift], KeyChordParser.VkReturn, "shift+enter")))
                {
                    return false;
                }

                continue;
            }

            foreach (char ch in piece)
            {
                batch.Add(Unicode(ch, up: false));
                batch.Add(Unicode(ch, up: true));
                if (batch.Count >= TextChunk * 2 && !Flush(batch))
                {
                    return false;
                }
            }
        }

        return Flush(batch);
    }

    public static bool Click(int x, int y)
    {
        int vx = ScreenNative.GetSystemMetrics(ScreenNative.SmXVirtualScreen);
        int vy = ScreenNative.GetSystemMetrics(ScreenNative.SmYVirtualScreen);
        int vw = Math.Max(1, ScreenNative.GetSystemMetrics(ScreenNative.SmCxVirtualScreen));
        int vh = Math.Max(1, ScreenNative.GetSystemMetrics(ScreenNative.SmCyVirtualScreen));
        int ax = (int)Math.Round((x - vx) * 65535.0 / Math.Max(1, vw - 1));
        int ay = (int)Math.Round((y - vy) * 65535.0 / Math.Max(1, vh - 1));
        const uint abs = ActionsNative.MouseeventfAbsolute | ActionsNative.MouseeventfVirtualDesk;
        var inputs = new List<ActionsNative.Input>
        {
            Mouse(ax, ay, ActionsNative.MouseeventfMove | abs),
            Mouse(ax, ay, ActionsNative.MouseeventfLeftDown | abs),
            Mouse(ax, ay, ActionsNative.MouseeventfLeftUp | abs),
        };
        return Send(inputs);
    }

    internal static IEnumerable<string> TextPieces(string text)
    {
        var normalized = text.Replace("\r\n", "\n").Replace('\r', '\n');
        int start = 0;
        for (int i = 0; i < normalized.Length; i++)
        {
            if (normalized[i] == '\n')
            {
                if (i > start)
                {
                    yield return normalized[start..i];
                }

                yield return "\n";
                start = i + 1;
            }
        }

        if (start < normalized.Length)
        {
            yield return normalized[start..];
        }
    }

    private static bool Flush(List<ActionsNative.Input> batch)
    {
        if (batch.Count == 0)
        {
            return true;
        }

        bool ok = Send(batch);
        batch.Clear();
        Thread.Sleep(ChunkPause);
        return ok;
    }

    private static bool Send(List<ActionsNative.Input> inputs)
    {
        if (inputs.Count == 0)
        {
            return true;
        }

        var span = CollectionsMarshal.AsSpan(inputs);
        fixed (ActionsNative.Input* p = span)
        {
            uint sent = ActionsNative.SendInput((uint)span.Length, p, sizeof(ActionsNative.Input));
            return sent == (uint)span.Length;
        }
    }

    private static ActionsNative.Input Key(ushort vk, bool up)
    {
        uint flags = up ? ActionsNative.KeyeventfKeyUp : 0;
        if (KeyChordParser.IsExtended(vk))
        {
            flags |= ActionsNative.KeyeventfExtendedKey;
        }

        return new ActionsNative.Input
        {
            Type = ActionsNative.InputKeyboard,
            U = new ActionsNative.InputUnion
            {
                Keyboard = new ActionsNative.KeybdInput
                {
                    Vk = vk,
                    Scan = (ushort)ActionsNative.MapVirtualKeyW(vk, 0),
                    Flags = flags,
                },
            },
        };
    }

    private static ActionsNative.Input Unicode(char ch, bool up) => new()
    {
        Type = ActionsNative.InputKeyboard,
        U = new ActionsNative.InputUnion
        {
            Keyboard = new ActionsNative.KeybdInput
            {
                Vk = 0,
                Scan = ch,
                Flags = ActionsNative.KeyeventfUnicode | (up ? ActionsNative.KeyeventfKeyUp : 0),
            },
        },
    };

    private static ActionsNative.Input Mouse(int x, int y, uint flags) => new()
    {
        Type = ActionsNative.InputMouse,
        U = new ActionsNative.InputUnion { Mouse = new ActionsNative.MouseInput { Dx = x, Dy = y, Flags = flags } },
    };
}
