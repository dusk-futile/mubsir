using System.Globalization;
using System.Runtime.InteropServices;
using Basira.App.Screen;
using Interop.UIAutomationClient;

namespace Basira.App.Actions;

/// <summary>
/// Element operations. Every method here runs on a UIA worker thread (via UiaScreenReader.RunUiaAsync) and turns
/// COM failures into result codes instead of exceptions.
/// </summary>
internal static class UiaActions
{
    private const double NoScroll = -1;

    /// <summary>The element still exists and is the same one the model saw (same runtime id).</summary>
    public static bool IsAlive(IUIAutomationElement element, string? runtimeId)
    {
        try
        {
            if (runtimeId is not null)
            {
                var current = UiaClassifier.RuntimeIdKey(element.GetRuntimeId() as int[]);
                if (current is not null && current != runtimeId)
                {
                    return false;
                }
            }

            _ = element.CurrentProcessId;
            return true;
        }
        catch (COMException)
        {
            return false;
        }
        catch (InvalidCastException)
        {
            return false;
        }
    }

    public static bool IsPassword(IUIAutomationElement element)
    {
        try
        {
            return element.CurrentIsPassword != 0;
        }
        catch (COMException)
        {
            return true; // unknown counts as protected
        }
    }

    /// <summary>Activates the element with the most specific pattern it supports; clicks its center as a last resort.</summary>
    /// <returns>The method used ("invoke", "toggle", "select", "expand", "collapse", "default_action", "click"), or null.</returns>
    public static string? Activate(IUIAutomationElement element)
    {
        if (TryPattern<IUIAutomationInvokePattern>(element, UiaIds.InvokePattern, p => p.Invoke()))
        {
            return "invoke";
        }

        if (TryPattern<IUIAutomationTogglePattern>(element, UiaIds.TogglePattern, p => p.Toggle()))
        {
            return "toggle";
        }

        if (TryPattern<IUIAutomationSelectionItemPattern>(element, UiaIds.SelectionItemPattern, p => p.Select()))
        {
            return "select";
        }

        try
        {
            if (element.GetCurrentPattern(UiaIds.ExpandCollapsePattern) is IUIAutomationExpandCollapsePattern ec)
            {
                if (ec.CurrentExpandCollapseState == ExpandCollapseState.ExpandCollapseState_Expanded)
                {
                    ec.Collapse();
                    return "collapse";
                }

                if (ec.CurrentExpandCollapseState != ExpandCollapseState.ExpandCollapseState_LeafNode)
                {
                    ec.Expand();
                    return "expand";
                }
            }
        }
        catch (COMException)
        {
        }

        if (TryPattern<IUIAutomationLegacyIAccessiblePattern>(element, UiaIds.LegacyIAccessiblePattern, p => p.DoDefaultAction()))
        {
            return "default_action";
        }

        if (Center(element) is { } c && InputSender.Click(c.X, c.Y))
        {
            return "click";
        }

        return null;
    }

    /// <summary>Center of the element on screen, when it is on screen.</summary>
    public static (int X, int Y)? Center(IUIAutomationElement element)
    {
        try
        {
            if (element.CurrentIsOffscreen != 0)
            {
                return null;
            }

            var r = element.CurrentBoundingRectangle;
            if (r.right <= r.left || r.bottom <= r.top)
            {
                return null;
            }

            return ((r.left + r.right) / 2, (r.top + r.bottom) / 2);
        }
        catch (COMException)
        {
            return null;
        }
    }

    /// <summary>ValuePattern.SetValue and read back. Returns the value now in the field, or null when the pattern is unusable.</summary>
    public static string? SetValue(IUIAutomationElement element, string text)
    {
        try
        {
            if (element.GetCurrentPattern(UiaIds.ValuePattern) is not IUIAutomationValuePattern value || value.CurrentIsReadOnly != 0)
            {
                return null;
            }

            value.SetValue(text);
            return value.CurrentValue ?? "";
        }
        catch (COMException)
        {
            return null;
        }
    }

    public static string? ReadValue(IUIAutomationElement element)
    {
        try
        {
            if (element.CurrentIsPassword != 0)
            {
                return null;
            }

            return (element.GetCurrentPattern(UiaIds.ValuePattern) as IUIAutomationValuePattern)?.CurrentValue;
        }
        catch (COMException)
        {
            return null;
        }
    }

    public static bool SetFocus(IUIAutomationElement element)
    {
        try
        {
            element.SetFocus();
            return true;
        }
        catch (COMException)
        {
            return false;
        }
    }

    /// <summary>Runtime id of the focused element, if any.</summary>
    public static string? FocusedRuntimeId(IUIAutomation uia)
    {
        try
        {
            return UiaClassifier.RuntimeIdKey(uia.GetFocusedElement()?.GetRuntimeId() as int[]);
        }
        catch (COMException)
        {
            return null;
        }
    }

    /// <summary>
    /// Scrolls the element or its nearest scrollable ancestor. Returns the new vertical position (0–100, or -1 when
    /// unknown) or null when nothing scrollable was found.
    /// </summary>
    public static double? Scroll(IUIAutomation uia, IUIAutomationElement start, string direction, string amount)
    {
        var target = ScrollableAncestor(uia, start, direction is "left" or "right");
        if (target is null)
        {
            return null;
        }

        try
        {
            bool horizontal = direction is "left" or "right";
            bool backward = direction is "up" or "left";
            if (amount is "top" or "bottom")
            {
                double pct = amount == "top" ? 0 : 100;
                target.SetScrollPercent(horizontal ? pct : NoScroll, horizontal ? NoScroll : pct);
            }
            else
            {
                var step = amount == "line"
                    ? (backward ? ScrollAmount.ScrollAmount_SmallDecrement : ScrollAmount.ScrollAmount_SmallIncrement)
                    : (backward ? ScrollAmount.ScrollAmount_LargeDecrement : ScrollAmount.ScrollAmount_LargeIncrement);
                if (horizontal)
                {
                    target.Scroll(step, ScrollAmount.ScrollAmount_NoAmount);
                }
                else
                {
                    target.Scroll(ScrollAmount.ScrollAmount_NoAmount, step);
                }
            }

            return horizontal ? target.CurrentHorizontalScrollPercent : target.CurrentVerticalScrollPercent;
        }
        catch (COMException)
        {
            return null;
        }
    }

    public static string Percent(double value) =>
        value < 0 ? "unknown" : value.ToString("0", CultureInfo.InvariantCulture) + "%";

    private static IUIAutomationScrollPattern? ScrollableAncestor(IUIAutomation uia, IUIAutomationElement start, bool horizontal)
    {
        var walker = uia.ControlViewWalker;
        var e = start;
        for (int i = 0; i < 25 && e is not null; i++)
        {
            try
            {
                if (e.GetCurrentPattern(UiaIds.ScrollPattern) is IUIAutomationScrollPattern s
                    && (horizontal ? s.CurrentHorizontallyScrollable : s.CurrentVerticallyScrollable) != 0)
                {
                    return s;
                }

                e = walker.GetParentElement(e);
            }
            catch (COMException)
            {
                return null;
            }
        }

        return null;
    }

    /// <summary>Runs a call on the pattern when the element supports it; false when absent or when the call failed.</summary>
    private static bool TryPattern<T>(IUIAutomationElement element, int patternId, Action<T> call)
        where T : class
    {
        try
        {
            if (element.GetCurrentPattern(patternId) is not T pattern)
            {
                return false;
            }

            call(pattern);
            return true;
        }
        catch (COMException)
        {
            return false;
        }
    }
}
