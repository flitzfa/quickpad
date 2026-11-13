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

    const int UiaTimeoutMs = 250;
    // shortcut: with extensions hidden, a hovered name is matched by scanning the folder; past this many items the
    // hover is ignored and the selection is used. Raise it if huge folders matter.
    const int MaxScan = 1000;

    sealed record Hover(string Name, Rect Bounds);

    /// <summary>The file under the mouse in the active tab of the given Explorer window, or else its first selected
    /// file; null if that is a folder, nothing applies, or anything fails. Never throws. Call it on the UI (STA)
    /// thread: the Shell path lookups run there while UI Automation runs on the thread pool, given up after
    /// 250 ms (the window then opens next to the cursor).</summary>
    public static async Task<ExplorerHit?> GetAsync(IntPtr explorerHwnd)
    {
        try
        {
            nint tab = Native.FindWindowEx(explorerHwnd, 0, "ShellTabWindowClass", null); // first in Z order = active tab
            if (tab == 0) return null;
            nint view = FindDescendant(tab, "SHELLDLL_DefView"); // the items view: a much smaller tree than the tab
            var hover = Task.Run(() => HoveredItem(view));
            var bounds = Task.Run(() => SelectedItemBounds(view != 0 ? view : tab));

            if ((await Task.WhenAny(hover, Task.Delay(UiaTimeoutMs)) == hover ? hover.Result : null) is { } h)
            {
                (string? hovered, bool isFolder) = FromWindow<(string?, bool)>(explorerHwnd, tab, w => HoveredFilePathOf(w, h.Name));
                if (isFolder) return null;
                if (hovered is not null) return new ExplorerHit(hovered, h.Bounds);
            }

            string? path = FromWindow(explorerHwnd, tab, SelectedFilePathOf);
            if (path is null) return null;
            return new ExplorerHit(path, await Task.WhenAny(bounds, Task.Delay(UiaTimeoutMs)) == bounds ? bounds.Result : null);
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

    static void WarmShell() => Try(() => FromWindow(0, 0, SelectedFilePathOf), "Warm"); // walks Shell.Windows(); matches no window

    static void Try(Action action, string context)
    {
        try { action(); }
        catch (Exception ex) { Log.Error(ex, "ExplorerSelection." + context); }
    }

    /// <summary>Runs <paramref name="pick"/> on the Shell window of the given Explorer tab; default if there is none.</summary>
    static T? FromWindow<T>(nint explorerHwnd, nint tab, Func<dynamic, T?> pick)
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
                    return pick(w);
                }
                catch (COMException ex)
                {
                    Log.Error(ex, "ExplorerSelection window"); // a stale entry in Shell.Windows() must not hide the others
                }
                finally { Release(w); }
            }
            return default;
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

    /// <summary>The hovered item's path, or that it is a folder; (null, false) when the name can't be pinned to one item.</summary>
    static (string? Path, bool IsFolder) HoveredFilePathOf(dynamic window, string name)
    {
        dynamic? doc = null, folder = null, item = null, items = null;
        try
        {
            doc = window.Document;
            folder = doc?.Folder;
            if (folder is null) return default;
            if (!ExtensionsHidden())
            {
                item = folder.ParseName(name); // the shown name is the file name
                if (item is not null) return item.IsFolder ? (null, true) : ((string)item.Path, false);
            }
            items = folder.Items();
            int count = items.Count;
            if (count > MaxScan) return default;
            var all = new List<(string Name, string Path, bool IsFolder)>(count);
            for (int i = 0; i < count; i++)
            {
                dynamic? it = items.Item(i);
                try { if (it is not null) all.Add(((string)it.Name, (string)it.Path, (bool)it.IsFolder)); }
                finally { Release(it); }
            }
            string? path = MatchByName(name, all, out bool isFolder);
            return (path, isFolder);
        }
        finally
        {
            Release(item);
            Release(items);
            Release(folder);
            Release(doc);
        }
    }

    /// <summary>The path of the only item shown as <paramref name="name"/> if it is a file, with
    /// <paramref name="isFolder"/> set when it is a folder; null when no item or more than one shows that name
    /// (e.g. config.env and config.txt with extensions hidden): then nothing is guessed.</summary>
    public static string? MatchByName(string name, IEnumerable<(string Name, string Path, bool IsFolder)> items, out bool isFolder)
    {
        var matches = items.Where(i => i.Name == name).Take(2).ToList();
        isFolder = matches.Count == 1 && matches[0].IsFolder;
        return matches.Count == 1 && !isFolder ? matches[0].Path : null;
    }

    static bool ExtensionsHidden()
    {
        using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced");
        return key?.GetValue("HideFileExt") is not int v || v != 0; // Windows' default is hidden
    }

    /// <summary>The list item under the mouse, if the mouse is over the given items view; null otherwise.</summary>
    static Hover? HoveredItem(nint view)
    {
        try
        {
            if (view == 0 || !Native.GetCursorPos(out var p)) return null;
            nint under = Native.WindowFromPoint(p);
            if (under != view && !Native.IsChild(view, under)) return null; // over the tree, address bar, another window…
            var e = AutomationElement.FromPoint(new Point(p.x, p.y));
            for (int depth = 0; e is not null && depth < 4; depth++, e = TreeWalker.ControlViewWalker.GetParent(e))
                if (e.Current.ControlType == ControlType.ListItem)
                    return Valid(e.Current.BoundingRectangle) is { } b ? new Hover(e.Current.Name, b) : null;
            return null; // empty space in the view
        }
        catch (Exception ex)
        {
            Log.Error(ex, "ExplorerSelection hover");
            return null;
        }
    }

    static Rect? SelectedItemBounds(nint root)
    {
        try
        {
            return Valid(FindSelected(AutomationElement.FromHandle(root), TreeScope.Descendants));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "ExplorerSelection bounds");
            return null;
        }
    }

    static Rect? Valid(Rect? r) => r is { } b && !b.IsEmpty && double.IsFinite(b.X + b.Y + b.Width + b.Height) ? b : null;

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
