using Microsoft.Win32;

namespace QuickPad;

/// <summary>The notification-area icon with "Iniciar con Windows" and "Salir".</summary>
public sealed class Tray : IDisposable
{
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run", RunValue = "QuickPad";

    readonly System.Windows.Forms.NotifyIcon _icon;

    public Tray(Action exit)
    {
        var startup = new System.Windows.Forms.ToolStripMenuItem("Iniciar con Windows");
        startup.Click += (_, _) => ToggleStartup();
        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add(startup);
        menu.Items.Add("Salir", null, (_, _) => exit());
        menu.Opening += (_, _) => startup.Checked = StartsWithWindows();

        using var stream = typeof(Tray).Assembly.GetManifestResourceStream("QuickPad.ico")!;
        _icon = new System.Windows.Forms.NotifyIcon
        {
            Icon = new System.Drawing.Icon(stream, System.Windows.Forms.SystemInformation.SmallIconSize),
            Text = "QuickPad",
            ContextMenuStrip = menu,
            Visible = true,
        };
    }

    static bool StartsWithWindows()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(RunValue) is not null;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Tray startup read");
            return false;
        }
    }

    static void ToggleStartup()
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey);
            if (key.GetValue(RunValue) is not null) key.DeleteValue(RunValue);
            else key.SetValue(RunValue, $"\"{Environment.ProcessPath}\"");
        }
        catch (Exception ex) { Log.Error(ex, "Tray startup toggle"); }
    }

    public void Dispose()
    {
        _icon.Visible = false; // otherwise the icon lingers until the mouse passes over it
        _icon.ContextMenuStrip?.Dispose();
        _icon.Icon?.Dispose();
        _icon.Dispose();
    }
}
