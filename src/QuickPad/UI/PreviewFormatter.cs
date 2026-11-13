using System.Text.RegularExpressions;

namespace QuickPad.UI;

public sealed record Segment(string Text, bool IsKey);

public static partial class PreviewFormatter
{
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
        var match = JsonLine().Match(line);
        if (!match.Success) match = EnvLine().Match(line);

        var parts = new List<Segment>();
        if (match.Success)
        {
            var key = match.Groups["key"];
            if (key.Index > 0) parts.Add(new Segment(line[..key.Index], false));
            parts.Add(new Segment(key.Value, true));
            parts.Add(new Segment(line[(key.Index + key.Length)..], false));
        }
        else parts.Add(new Segment(line, false));

        if (line.Length > maxChars)
        {
            int left = maxChars - 1;
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
}
