using System.IO;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Microsoft.Win32;
using QuickPad.Files;
using QuickPad.Interop;

namespace QuickPad.UI;

/// <summary>The single reusable acrylic window. Created once, then shown and hidden.</summary>
public partial class GlassWindow : Window
{
    private const double Gap = 8;
    private const double PreviewWidth = 520;
    private nint _hwnd;
    private bool? _light;
    private bool _hiding;

    public event Action? Hidden;

    public GlassWindow()
    {
        InitializeComponent();
        ApplyTheme();
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        Closed += (_, _) => SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
    }

    /// <summary>Shows the read-only preview of <paramref name="path"/> next to <paramref name="anchor"/>
    /// (physical screen pixels), or next to the cursor when null. With unsaved changes it first asks
    /// Guardar / Descartar / Seguir editando, and does nothing on Seguir editando.</summary>
    public async Task ShowPreview(string path, Rect? anchor)
    {
        if (!await ResolveUnsavedChangesAsync(bringForward: true)) return;
        LoadPreview(path);
        Width = PreviewWidth;
        Pop(PlaceNear(anchor ?? CursorRect()));
    }

    /// <summary>Closes the window; with unsaved changes it asks first and stays open on Seguir editando.</summary>
    public void HideAnimated() => _ = CloseAsync(bringForward: true);

    /// <summary>Hides with the shrink animation, then frees the editor. Only once there is nothing left to save.</summary>
    private void HideNow()
    {
        if (!IsVisible || _hiding) return;
        _hiding = true;
        Editor.IsReadOnly = true; // keys typed during the animation would be lost with the reset
        Animate(0.9, 0, 180, new CubicEase { EasingMode = EasingMode.EaseIn }, completed: (_, _) =>
        {
            if (!_hiding) return; // reopened while closing
            Hide();
            _hiding = false;
            ResetEditor();
            Hidden?.Invoke();
        });
    }

    /// <summary>Top-left (DIP) for a window of <paramref name="size"/> to the right of <paramref name="anchor"/>,
    /// or to its left when it does not fit, kept inside <paramref name="workArea"/>; plus the
    /// RenderTransformOrigin of the window corner nearest the anchor.</summary>
    public static (Point TopLeft, Point Origin) Place(Rect anchor, Size size, Rect workArea)
    {
        bool right = anchor.Right + Gap + size.Width <= workArea.Right;
        double x = right ? anchor.Right + Gap : anchor.Left - Gap - size.Width;
        x = Math.Clamp(x, workArea.Left, Math.Max(workArea.Left, workArea.Right - size.Width));
        double y = Math.Clamp(anchor.Top, workArea.Top, Math.Max(workArea.Top, workArea.Bottom - size.Height));
        bool anchorAbove = anchor.Top + anchor.Height / 2 < y + size.Height / 2;
        return (new Point(x, y), new Point(right ? 0 : 1, anchorAbove ? 0 : 1));
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _hwnd = new WindowInteropHelper(this).Handle;
        HwndSource.FromHwnd(_hwnd).CompositionTarget.BackgroundColor = Colors.Transparent; // otherwise black, not glass
        // The caption style is what lets DWM draw the backdrop; without the system menu DWM draws no caption buttons.
        Native.SetWindowLongPtr(_hwnd, Native.GWL_STYLE, Native.GetWindowLongPtr(_hwnd, Native.GWL_STYLE) & ~Native.WS_SYSMENU);
        Native.DwmSetWindowAttribute(_hwnd, Native.DWMWA_SYSTEMBACKDROP_TYPE, Native.DWMSBT_TRANSIENTWINDOW);
        Native.DwmSetWindowAttribute(_hwnd, Native.DWMWA_WINDOW_CORNER_PREFERENCE, Native.DWMWCP_ROUND);
        Native.DwmSetWindowAttribute(_hwnd, Native.DWMWA_USE_IMMERSIVE_DARK_MODE, _light == true ? 0 : 1);
    }

    protected override void OnDeactivated(EventArgs e)
    {
        base.OnDeactivated(e);
        // A dialog waits for its answer wherever the focus went; Hide() itself deactivates while closing.
        if (!Dialog.IsOpen && !_hiding) _ = CloseAsync();
    }

    /// <summary>Fills the header and the preview (or a notice) for <paramref name="path"/> and leaves edit mode.</summary>
    private void LoadPreview(string path)
    {
        ResetEditor();
        CurrentPath = path;
        FileName.Text = Path.GetFileName(path);
        FolderName.Text = Path.GetDirectoryName(path);
        try
        {
            ShowSegments(PreviewFormatter.Format(TextFile.Open(path, TextFile.PreviewBytes, lenient: true).Text));
        }
        catch (NotTextException) { ShowNotice("Este archivo no es texto"); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { ShowNotice("No se pudo abrir el archivo"); }
    }

    /// <summary>Shows and activates the window with the open spring, scaling from <paramref name="origin"/>.</summary>
    private void Pop(Point origin)
    {
        bool wasVisible = IsVisible;
        _hiding = false;
        Root.RenderTransformOrigin = origin;
        Show();
        BringForward();
        // WPF restores focus to the last focused element; after an edit that is the now hidden editor, which leaves
        // no element focused and keys (Esc) unheard until a click.
        if (!IsKeyboardFocusWithin) Focus();
        Animate(1, 1, 250, new BackEase { Amplitude = 0.3, EasingMode = EasingMode.EaseOut }, fromStart: !wasVisible);
    }

    /// <summary>Activates the window, so Esc and click-outside work. Windows refuses the foreground to a background
    /// (tray) process that didn't get the last input; an empty injected mouse event makes it the last, and sharing
    /// the foreground thread's input state is the fallback.</summary>
    private void BringForward()
    {
        if (Native.GetForegroundWindow() == _hwnd) return;
        Native.SendEmptyMouseInput();
        if (Native.SetForegroundWindow(_hwnd) && Activate() && Native.GetForegroundWindow() == _hwnd) return;
        uint foreground = Native.GetWindowThreadProcessId(Native.GetForegroundWindow(), out _);
        uint self = Native.GetCurrentThreadId();
        bool attached = foreground != 0 && foreground != self && Native.AttachThreadInput(self, foreground, true);
        try { Native.SetForegroundWindow(_hwnd); }
        finally { if (attached) Native.AttachThreadInput(self, foreground, false); }
        if (Native.GetForegroundWindow() != _hwnd) Log.Info("BringForward: Windows refused the foreground");
    }

    private void ShowSegments(IReadOnlyList<Segment> segments)
    {
        Footer.Text = "Clic para editar · Esc cierra";
        Notice.Visibility = Visibility.Collapsed;
        PreviewText.Visibility = Visibility.Visible;
        PreviewText.Inlines.Clear();
        foreach (var s in segments)
        {
            if (s.Text == "\n") { PreviewText.Inlines.Add(new LineBreak()); continue; }
            var run = new Run(s.Text);
            if (s.IsKey) run.SetResourceReference(TextElement.ForegroundProperty, "Accent");
            PreviewText.Inlines.Add(run);
        }
    }

    private void ShowNotice(string message)
    {
        PreviewText.Inlines.Clear();
        PreviewText.Visibility = Visibility.Collapsed;
        Notice.Text = message;
        Footer.Text = "Esc cierra";
        Notice.Visibility = Visibility.Visible;
    }

    private static Rect CursorRect()
    {
        var p = System.Windows.Forms.Cursor.Position;
        return new Rect(p.X, p.Y, 0, 0);
    }

    /// <summary>Sizes the window to its content and moves it next to <paramref name="anchorPx"/>; returns the scale origin.</summary>
    private Point PlaceNear(Rect anchorPx)
    {
        var workArea = WorkArea(System.Windows.Forms.Screen.FromRectangle(new System.Drawing.Rectangle(
            (int)anchorPx.X, (int)anchorPx.Y, (int)anchorPx.Width, (int)anchorPx.Height)));
        var anchor = Rect.Transform(anchorPx, FromDevice());

        Root.Measure(new Size(Width, double.PositiveInfinity));
        Height = Math.Min(Root.DesiredSize.Height, workArea.Height - 2 * Gap);

        var (topLeft, origin) = Place(anchor, new Size(Width, Height), workArea);
        Left = topLeft.X;
        Top = topLeft.Y;
        return origin;
    }

    /// <summary>Work area of <paramref name="screen"/> in DIP.</summary>
    private Rect WorkArea(System.Windows.Forms.Screen screen)
    {
        var px = screen.WorkingArea;
        return Rect.Transform(new Rect(px.X, px.Y, px.Width, px.Height), FromDevice());
    }

    // shortcut: converts with the DPI of the monitor the window is on now; a different-DPI target monitor may land a bit off.
    private Matrix FromDevice()
    {
        _hwnd = new WindowInteropHelper(this).EnsureHandle();
        return HwndSource.FromHwnd(_hwnd).CompositionTarget.TransformFromDevice;
    }

    /// <summary>Animates scale and opacity from their current values, or from 0.9 and 0 when <paramref name="fromStart"/>.</summary>
    private void Animate(double toScale, double toOpacity, int ms, IEasingFunction ease, bool fromStart = false, EventHandler? completed = null)
    {
        var duration = TimeSpan.FromMilliseconds(ms);
        var scale = new DoubleAnimation(toScale, duration) { From = fromStart ? 0.9 : null, EasingFunction = ease };
        var fade = new DoubleAnimation(toOpacity, duration) { From = fromStart ? 0 : null, EasingFunction = ease };
        if (completed is not null) fade.Completed += completed;
        RootScale.BeginAnimation(ScaleTransform.ScaleXProperty, scale);
        RootScale.BeginAnimation(ScaleTransform.ScaleYProperty, scale);
        Root.BeginAnimation(OpacityProperty, fade);
    }

    private void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category == UserPreferenceCategory.General) Dispatcher.BeginInvoke(ApplyTheme);
    }

    private void ApplyTheme()
    {
        bool light = Theme.IsLight();
        if (_light == light) return;
        _light = light;
        Resources.MergedDictionaries.Clear();
        Resources.MergedDictionaries.Add(Theme.Dictionary(light));
        if (_hwnd != 0) Native.DwmSetWindowAttribute(_hwnd, Native.DWMWA_USE_IMMERSIVE_DARK_MODE, light ? 0 : 1);
    }
}
