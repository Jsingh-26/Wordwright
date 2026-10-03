using System.Diagnostics;
using System.Windows.Automation;

namespace E2E;

/// <summary>UI Automation helpers: find, wait, read, invoke.</summary>
internal static class Ui
{
    public static AutomationElement Root => AutomationElement.RootElement;

    public static T Wait<T>(Func<T?> probe, int timeoutMs = 5000, int stepMs = 100) where T : class
    {
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            try
            {
                var v = probe();
                if (v is not null) return v;
            }
            catch (ElementNotAvailableException) { }
            catch (InvalidOperationException) { }
            Thread.Sleep(stepMs);
        }
        throw new TimeoutException("timed out after " + timeoutMs + " ms");
    }

    public static bool WaitTrue(Func<bool> probe, int timeoutMs = 5000, int stepMs = 50)
    {
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            try { if (probe()) return true; } catch (ElementNotAvailableException) { } catch (InvalidOperationException) { }
            Thread.Sleep(stepMs);
        }
        return false;
    }

    public static AutomationElement[] TopWindows(int pid) =>
        Root.FindAll(TreeScope.Children, new PropertyCondition(AutomationElement.ProcessIdProperty, pid)).Cast<AutomationElement>().ToArray();

    public static AutomationElement? TopWindow(int pid, string name) =>
        TopWindows(pid).FirstOrDefault(w => w.Current.Name == name);

    public static AutomationElement? Find(AutomationElement scope, string? name = null, ControlType? type = null, string? automationId = null)
    {
        var conds = new List<Condition>();
        if (name is not null) conds.Add(new PropertyCondition(AutomationElement.NameProperty, name));
        if (type is not null) conds.Add(new PropertyCondition(AutomationElement.ControlTypeProperty, type));
        if (automationId is not null) conds.Add(new PropertyCondition(AutomationElement.AutomationIdProperty, automationId));
        Condition c = conds.Count == 1 ? conds[0] : new AndCondition(conds.ToArray());
        return scope.FindFirst(TreeScope.Descendants, c);
    }

    public static AutomationElement[] FindAll(AutomationElement scope, ControlType type) =>
        scope.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, type)).Cast<AutomationElement>().ToArray();

    public static string Text(AutomationElement e)
    {
        if (e.TryGetCurrentPattern(ValuePattern.Pattern, out var vp)) return ((ValuePattern)vp).Current.Value ?? "";
        if (e.TryGetCurrentPattern(TextPattern.Pattern, out var tp)) return ((TextPattern)tp).DocumentRange.GetText(-1) ?? "";
        return e.Current.Name ?? "";
    }

    public static void SetValue(AutomationElement e, string value) =>
        ((ValuePattern)e.GetCurrentPattern(ValuePattern.Pattern)).SetValue(value);

    public static void Invoke(AutomationElement e)
    {
        if (e.TryGetCurrentPattern(InvokePattern.Pattern, out var ip)) { ((InvokePattern)ip).Invoke(); return; }
        if (e.TryGetCurrentPattern(SelectionItemPattern.Pattern, out var sp)) { ((SelectionItemPattern)sp).Select(); return; }
        ClickCenter(e);
    }

    public static void ClickCenter(AutomationElement e, bool right = false)
    {
        var r = e.Current.BoundingRectangle;
        Native.Click((int)(r.Left + r.Width / 2), (int)(r.Top + r.Height / 2), right);
    }

    public static Native.Rect RectOf(AutomationElement e)
    {
        var r = e.Current.BoundingRectangle;
        return new Native.Rect { L = (int)r.Left, T = (int)r.Top, R = (int)r.Right, B = (int)r.Bottom };
    }

    public static IntPtr Hwnd(AutomationElement e) => (IntPtr)e.Current.NativeWindowHandle;

    public static string FocusedName()
    {
        var f = AutomationElement.FocusedElement;
        return f is null ? "" : $"{f.Current.ControlType.ProgrammaticName.Replace("ControlType.", "")} \"{f.Current.Name}\"";
    }
}
