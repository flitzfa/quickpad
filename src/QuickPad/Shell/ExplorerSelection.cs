using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using QuickPad.Interop;

namespace QuickPad.Shell;

/// <summary>A file selected in File Explorer. <see cref="ItemBounds"/> is in physical screen pixels, null if unknown.</summary>
public sealed record ExplorerHit(string Path, Rect? ItemBounds);

public static class ExplorerSelection
{
    // ListItem keeps out the navigation pane, whose current folder is a selected TreeItem inside the same tab.
    static readonly AndCondition SelectedListItem = new AndCondition(
        new PropertyCondition(SelectionItemPattern.IsSelectedProperty, true),
        new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ListItem));

    /// <summary>The first selected file in the active tab of the given Explorer window; null if nothing is
    /// selected, the selection is a folder, or anything fails. Never throws. Call it on the UI (STA) thread:
    /// the Shell path lookup runs there while the item bounds are looked up on the thread pool, given up after
    /// 250 ms (the window then opens next to the cursor).</summary>
    public static async Task<ExplorerHit?> GetAsync(IntPtr explorerHwnd)
    {
        try
        {
            nint tab = Native.FindWindowEx(explorerHwnd, 0, "ShellTabWindowClass", null); // first in Z order = active tab
            if (tab == 0) return null;
            var bounds = Task.Run(() => SelectedItemBounds(tab));
            string? path = SelectedFilePath(explorerHwnd, tab);
            if (path is null) return null;
            return new ExplorerHit(path, await Task.WhenAny(bounds, Task.Delay(250)) == bounds ? bounds.Result : null);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "ExplorerSelection.GetAsync");
            return null;
        }
    }

    /// <summary>Pays the one-time cost of the first <see cref="GetAsync"/> ahead of time. Call it on the UI thread.
    /// Shows nothing and never throws.</summary>
    public static async Task Warm()
    {
        // The late-bound call sites and UI Automation warm on the thread pool; only the Shell COM proxies are per
        // apartment, so the UI thread is left with just those.
        var uia = Task.Run(() => Try(() => FindSelected(AutomationElement.RootElement, TreeScope.Children), "Warm UIA"));
        await Task.Run(WarmShell);
        WarmShell();
        await uia;
    }

    static void WarmShell() => Try(() => SelectedFilePath(0, 0), "Warm"); // walks Shell.Windows(); matches no window

    static void Try(Action action, string context)
    {
        try { action(); }
        catch (Exception ex) { Log.Error(ex, "ExplorerSelection." + context); }
    }

    static string? SelectedFilePath(nint explorerHwnd, nint tab)
    {
        var shellType = Type.GetTypeFromProgID("Shell.Application", throwOnError: true)!;
        dynamic shell = Activator.CreateInstance(shellType)!;
        dynamic? windows = null;
        try
        {
            windows = shell.Windows();
            int count = windows.Count;
            for (int i = 0; i < count; i++)
            {
                dynamic? w = null;
                try
                {
                    w = windows.Item(i);
                    if (w is null || (nint)Convert.ToInt64(w.HWND) != explorerHwnd || TabOf(w) != tab) continue;
                    return SelectedFilePathOf(w);
                }
                catch (COMException ex)
                {
                    Log.Error(ex, "ExplorerSelection window"); // a stale entry in Shell.Windows() must not hide the others
                }
                finally { Release(w); }
            }
            return null;
        }
        finally
        {
            Release(windows);
            Release(shell);
        }
    }

    static nint TabOf(object window)
    {
        if (window is not IServiceProvider sp) return 0;
        Guid sid = ShellGuids.SID_STopLevelBrowser, iid = ShellGuids.IID_IShellBrowser;
        if (sp.QueryService(ref sid, ref iid, out object? browser) < 0 || browser is null) return 0;
        try { return ((IShellBrowser)browser).GetWindow(out nint hwnd) < 0 ? 0 : hwnd; }
        finally { Release(browser); }
    }

    static string? SelectedFilePathOf(dynamic window)
    {
        dynamic? doc = null, items = null, item = null;
        try
        {
            doc = window.Document;
            if (doc is null) return null;
            items = doc.SelectedItems();
            if (items is null || items.Count == 0) return null;
            item = items!.Item(0);
            if (item is null || item.IsFolder) return null;
            return (string)item!.Path;
        }
        finally
        {
            Release(item);
            Release(items);
            Release(doc);
        }
    }

    static Rect? SelectedItemBounds(nint tab)
    {
        try
        {
            nint view = FindDescendant(tab, "SHELLDLL_DefView"); // the items view: a much smaller tree than the tab
            Rect? r = FindSelected(AutomationElement.FromHandle(view != 0 ? view : tab), TreeScope.Descendants);
            return r is { } b && !b.IsEmpty && double.IsFinite(b.X + b.Y + b.Width + b.Height) ? b : null;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "ExplorerSelection bounds");
            return null;
        }
    }

    /// <summary>Bounds of the first selected list item under <paramref name="root"/>, fetched in the same
    /// cross-process call as the search.</summary>
    static Rect? FindSelected(AutomationElement root, TreeScope scope)
    {
        var cache = new CacheRequest { AutomationElementMode = AutomationElementMode.None };
        cache.Add(AutomationElement.BoundingRectangleProperty);
        using (cache.Activate())
            return root.FindFirst(scope, SelectedListItem)?.Cached.BoundingRectangle;
    }

    static nint FindDescendant(nint parent, string className)
    {
        for (nint child = Native.FindWindowEx(parent, 0, null, null); child != 0; child = Native.FindWindowEx(parent, child, null, null))
        {
            if (Native.ClassNameOf(child) == className) return child;
            nint found = FindDescendant(child, className);
            if (found != 0) return found;
        }
        return 0;
    }

    static void Release(object? o)
    {
        if (o is not null && Marshal.IsComObject(o)) Marshal.ReleaseComObject(o);
    }
}
