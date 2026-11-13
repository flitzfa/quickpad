using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace QuickPad.UI;

/// <summary>A glass strip inside the window with a message and a few buttons; never an OS dialog.</summary>
public partial class InlineDialog : UserControl
{
    private TaskCompletionSource<int>? _pending;

    public InlineDialog() => InitializeComponent();

    public bool IsOpen => _pending is not null;

    /// <summary>Shows <paramref name="message"/> and completes with the index of the clicked button
    /// (the first one is the highlighted default), or -1 when dismissed.</summary>
    public Task<int> Ask(string message, params string[] buttons)
    {
        Dismiss();
        var pending = _pending = new TaskCompletionSource<int>();
        Message.Text = message;
        Buttons.Children.Clear();
        for (int i = 0; i < buttons.Length; i++)
        {
            int choice = i;
            var button = new Button { Content = buttons[i], Style = (Style)Resources[i == 0 ? "PrimaryButton" : "DialogButton"] };
            button.Click += (_, _) => Close(choice);
            Buttons.Children.Add(button);
        }
        Visibility = Visibility.Visible;
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        Strip.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(160)) { EasingFunction = ease });
        StripShift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(8, 0, TimeSpan.FromMilliseconds(160)) { EasingFunction = ease });
        Dispatcher.BeginInvoke(DispatcherPriority.Input, () => { if (_pending == pending) Buttons.Children[0].Focus(); });
        return pending.Task;
    }

    public void Dismiss() => Close(-1);

    private void Close(int choice)
    {
        var pending = _pending;
        if (pending is null) return;
        _pending = null;
        Visibility = Visibility.Collapsed;
        pending.SetResult(choice);
    }
}
