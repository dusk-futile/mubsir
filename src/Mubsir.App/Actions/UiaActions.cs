using System.Globalization;
using System.Runtime.InteropServices;
using Interop.UIAutomationClient;

namespace Mubsir.App.Actions;

internal static class UiaActions
{
    private const double NoScroll = -1;

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
            return true;
        }
    }

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
