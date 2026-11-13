using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using QuickPad.Input;
using QuickPad.Interop;
using QuickPad.Shell;
using QuickPad.UI;

namespace QuickPad;

/// <summary>Tray-resident app: one instance, one reusable window, opened by double Ctrl in Explorer or "Abrir con…".</summary>
public partial class App : Application
{
    const double OffScreen = -20000;

    Mutex? _instance;
    GlassWindow _window = null!;
    Tray? _tray;
    KeyboardHook? _hook;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        string? path = null;
        if (e.Args.Length > 0 && e.Args[0].Length > 0)
        {
            try { path = Path.GetFullPath(e.Args[0]); }
            catch (Exception ex) { Log.Error(ex, "Argumento"); } // a malformed path opens nothing, it doesn't crash
        }

        _instance = SingleInstance.TryClaim();
        if (_instance is null)
        {
            if (path is not null) SingleInstance.Send(path);
            Shutdown();
            return;
        }

        DispatcherUnhandledException += (_, args) => { Log.Error(args.Exception, "Dispatcher"); args.Handled = true; };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            Log.Error(args.ExceptionObject as Exception ?? new Exception(args.ExceptionObject?.ToString()), "AppDomain");
            _tray?.Dispose(); // the process is going down: don't leave a ghost icon in the tray
        };
        TaskScheduler.UnobservedTaskException += (_, args) => { Log.Error(args.Exception, "Task"); args.SetObserved(); };

        _window = new GlassWindow();
        _window.Hidden += TrimWorkingSet;
        _tray = new Tray(ExitFromTray);
        _ = SingleInstance.ListenAsync(OpenEditor);
        if (path is not null) OpenEditor(path);
        else Prewarm();
        // After the first frame, so the first double Ctrl doesn't pay for cold COM and UI Automation.
        Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, () =>
        {
            ExplorerSelection.Warm();
            if (!_window.IsVisible) TrimWorkingSet();
        });

        try
        {
            _hook = new KeyboardHook(Dispatcher);
            _hook.DoubleCtrl += OnDoubleCtrl;
        }
        catch (Exception ex) { Log.Error(ex, "KeyboardHook install"); } // "Abrir con…" and the tray still work
    }

    /// <summary>Renders the window once off screen and hides it, so the first real open is already warm.</summary>
    void Prewarm()
    {
        _window.ShowActivated = false;
        _window.Left = _window.Top = OffScreen;
        _window.ContentRendered += Rendered;
        _window.Show();

        void Rendered(object? sender, EventArgs e)
        {
            _window.ContentRendered -= Rendered;
            _window.ShowActivated = true;
            if (_window.Left != OffScreen) return; // a real open came first
            _window.Hide();
        }
    }

    async void OpenEditor(string path) => await _window.ShowEditor(path);

    async void OnDoubleCtrl(IntPtr explorer)
    {
        var clock = Stopwatch.StartNew(); // from the hand-off by the hook thread, which is ~immediate on an idle UI thread
        var hit = await ExplorerSelection.GetAsync(explorer);
        long getMs = clock.ElapsedMilliseconds;
        if (hit is null) return;

        if (_window.IsVisible && string.Equals(_window.CurrentPath, hit.Path, StringComparison.OrdinalIgnoreCase))
        {
            _window.HideAnimated();
            return;
        }

        EventHandler? firstFrame = null;
        firstFrame = (_, _) =>
        {
            CompositionTarget.Rendering -= firstFrame;
            Log.Info($"double Ctrl -> first frame {clock.ElapsedMilliseconds} ms (ExplorerSelection.GetAsync {getMs} ms)");
        };
        CompositionTarget.Rendering += firstFrame;
        await _window.ShowPreview(hit.Path, hit.ItemBounds);
    }

    /// <summary>Tray "Salir": never loses unsaved edits; stays on Seguir editando.</summary>
    async void ExitFromTray()
    {
        if (await _window.TryCloseAsync()) Shutdown();
    }

    static void TrimWorkingSet() => Native.SetProcessWorkingSetSize(Native.GetCurrentProcess(), -1, -1);

    protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
    {
        base.OnSessionEnding(e);
        if (_window?.IsDirty != true) return;
        Log.Error(new InvalidOperationException($"Windows se cierra con cambios sin guardar en {_window.CurrentPath}"), "SessionEnding");
        e.Cancel = true;
        _ = AskBeforeSessionEnds();
    }

    /// <summary>While the question is open, Windows' "apps preventing shutdown" screen names the reason.</summary>
    async Task AskBeforeSessionEnds()
    {
        nint hwnd = new WindowInteropHelper(_window).Handle;
        Native.ShutdownBlockReasonCreate(hwnd, "Tenés cambios sin guardar");
        try { await _window.TryCloseAsync(); }
        finally { Native.ShutdownBlockReasonDestroy(hwnd); }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _hook?.Dispose();
        _tray?.Dispose();
        _instance?.Dispose();
        base.OnExit(e);
    }
}
