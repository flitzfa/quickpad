using System.Windows;
using Microsoft.Win32;

namespace QuickPad.UI;

public static class Theme
{
    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    /// <summary>True when Windows apps use the light theme (also when the value is missing, as on a fresh install).</summary>
    public static bool IsLight()
    {
        using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
        return key?.GetValue("AppsUseLightTheme") is not int value || value != 0;
    }

    public static ResourceDictionary Dictionary(bool light) =>
        new() { Source = new Uri($"/QuickPad;component/UI/Themes/{(light ? "Light" : "Dark")}.xaml", UriKind.Relative) };
}
