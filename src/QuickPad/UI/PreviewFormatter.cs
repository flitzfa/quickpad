using System.Text.RegularExpressions;

namespace QuickPad.UI;

public sealed record Segment(string Text, bool IsKey);

public static partial class PreviewFormatter
{
    private static readonly string[] SecretNames = { "KEY", "TOKEN", "SECRET", "PASSWORD", "PASS" };
    private static readonly string[] KnownPrefixes = { "sk-", "ghp_", "gho_", "xoxb-", "xoxp-", "AKIA", "AIza" };

    [GeneratedRegex(@"^(\s*""(?<key>[^""]+)""\s*:\s*)(?<val>.*)$")]
    private static partial Regex JsonLine();

    [GeneratedRegex(@"^(\s*(export\s+)?(?<key>[A-Za-z_][\w.-]*)\s*[=:]\s*)(?<val>.*)$")]
    private static partial Regex EnvLine();

    public static IReadOnlyList<Segment> Format(string text, int maxLines = 40, int maxLineChars = 200)
    {
        var result = new List<Segment>();
        var lines = text.Split('\n');
        for (int i = 0; i < lines.Length && i < maxLines; i++)
        {
            if (i > 0) result.Add(new Segment("\n", false));
            AddLine(result, lines[i].TrimEnd('\r'), maxLineChars);
        }
        return result;
    }

    private static void AddLine(List<Segment> result, string line, int maxChars)
    {
        var m = JsonLine();
        var match = m.Match(line);
        if (!match.Success) match = EnvLine().Match(line);

        var parts = new List<Segment>();
        if (match.Success)
        {
            var key = match.Groups["key"];
            var val = match.Groups["val"];
            if (key.Index > 0) parts.Add(new Segment(line[..key.Index], false));
            parts.Add(new Segment(key.Value, true));
            var value = IsSecret(key.Value, val.Value) ? MaskValue(val.Value) : val.Value;
            parts.Add(new Segment(line[(key.Index + key.Length)..val.Index] + value, false));
        }
        else parts.Add(new Segment(line, false));

        int left = maxChars;
        if (parts.Sum(p => p.Text.Length) > maxChars)
        {
            left = maxChars - 1;
            foreach (var p in parts)
            {
                if (left <= 0) break;
                var t = p.Text.Length > left ? p.Text[..left] : p.Text;
                if (t.Length > 0) result.Add(p with { Text = t });
                left -= t.Length;
            }
            result.Add(new Segment("…", false));
            return;
        }
        result.AddRange(parts.Where(p => p.Text.Length > 0));
    }

    private static string Unwrap(string value, out string open, out string close)
    {
        var v = value.TrimEnd();
        close = "";
        open = "";
        if (v.EndsWith(',')) { close = ","; v = v[..^1]; }
        if (v.Length >= 2 && (v[0] == '"' || v[0] == '\'') && v[^1] == v[0])
        {
            open = v[0].ToString();
            close = open + close;
            v = v[1..^1];
        }
        return v;
    }

    private static bool IsSecret(string key, string value)
    {
        if (SecretNames.Any(n => key.Contains(n, StringComparison.OrdinalIgnoreCase))) return true;
        var v = Unwrap(value, out _, out _);
        return KnownPrefixes.Any(p => v.StartsWith(p, StringComparison.Ordinal));
    }

    private static string MaskValue(string value)
    {
        var v = Unwrap(value, out var open, out var close);
        return v.Length == 0 ? value : open + Mask(v) + close;
    }

    public static string Mask(string value)
    {
        if (value.Length <= 8) return "••••••••";
        var prefix = KnownPrefixes.FirstOrDefault(p => value.StartsWith(p, StringComparison.Ordinal)) ?? value[..3];
        return prefix + "••••" + value[^4..];
    }
}
