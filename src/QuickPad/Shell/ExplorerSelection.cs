using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using QuickPad.Interop;

namespace QuickPad.Shell;

/// <summary>A file selected in File Explorer. <see cref="ItemBounds"/> is in physical screen pixels, null if unknown.</summary>
public sealed record ExplorerHit(string Path, Rect? ItemBounds);

public static class ExplorerSelection
{
    /// <summary>The first selected file in the active tab of the given Explorer window; null if nothing is
    /// selected, the selection is a folder, or anything fails. Never throws. Synchronous; safe on an STA thread.</summary>
    public static ExplorerHit? Get(IntPtr explorerHwnd)
    {
        try
        {
            nint tab = Native.FindWindowEx(explorerHwnd, 0, "ShellTabWindowClass", null); // first in Z order = active tab
            if (tab == 0) return null;
            string? path = SelectedFilePath(explorerHwnd, tab);
            return path is null ? null : new ExplorerHit(path, SelectedItemBounds(tab));
        }
        catch (Exception ex)
        {
            LogError(ex, "ExplorerSelection.Get");
            return null;
        }
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
                    LogError(ex, "ExplorerSelection window"); // a stale entry in Shell.Windows() must not hide the others
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
            // ListItem keeps out the navigation pane, whose current folder is a selected TreeItem inside the same tab.
            var selected = AutomationElement.FromHandle(tab).FindFirst(TreeScope.Descendants, new AndCondition(
                new PropertyCondition(SelectionItemPattern.IsSelectedProperty, true),
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ListItem)));
            if (selected is null) return null;
            Rect r = selected.Current.BoundingRectangle;
            return r.IsEmpty || !double.IsFinite(r.X + r.Y + r.Width + r.Height) ? null : r;
        }
        catch (Exception ex)
        {
            LogError(ex, "ExplorerSelection bounds");
            return null;
        }
    }

    static void Release(object? o)
    {
        if (o is not null && Marshal.IsComObject(o)) Marshal.ReleaseComObject(o);
    }

    // shortcut: Debug only until Log exists (Task 9)
    static void LogError(Exception ex, string context) => Debug.WriteLine($"{context}: {ex}");
}
