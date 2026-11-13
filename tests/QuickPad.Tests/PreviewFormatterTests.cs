using QuickPad.UI;

namespace QuickPad.Tests;

public class PreviewFormatterTests
{
    private static string Join(IReadOnlyList<Segment> s) => string.Concat(s.Select(x => x.Text));

    [Fact]
    public void Format_EnvKey_IsKeySegment()
    {
        var s = PreviewFormatter.Format("TIMEOUT=30");
        Assert.Equal(new[] { new Segment("TIMEOUT", true), new Segment("=30", false) }, s);
    }

    [Fact]
    public void Format_SecretByName_IsMasked() =>
        Assert.Equal("API_KEY=••••••••", Join(PreviewFormatter.Format("API_KEY=abcdef123456")));

    [Fact]
    public void Format_LongSecretWithoutPrefix_KeepsEdges() =>
        Assert.Equal("PASSWORD=abc••••6789", Join(PreviewFormatter.Format("PASSWORD=abcdefghij0123456789")));

    [Fact]
    public void Format_MinifiedJsonSecret_IsMasked()
    {
        var j = Join(PreviewFormatter.Format("{\"api_key\":\"sk-proj9999abcd3f9a\",\"x\":1}"));
        Assert.Contains("sk-••••3f9a", j);
        Assert.DoesNotContain("proj9999", j);
    }

    [Fact]
    public void Format_YamlListSecret_IsMasked() =>
        Assert.Equal("- ghp_••••1234", Join(PreviewFormatter.Format("- ghp_aaaaaaaa1234")));

    [Fact]
    public void Format_PrefixInsideWord_Untouched() =>
        Assert.Equal("run task-manager now", Join(PreviewFormatter.Format("run task-manager now")));

    [Fact]
    public void Format_SecretBeyondCut_MaskedBeforeCut()
    {
        var j = Join(PreviewFormatter.Format(new string('a', 180) + " ghp_aaaaaaaa1234 " + new string('b', 50)));
        Assert.EndsWith("…", j);
        Assert.Contains("ghp_••••1234", j);
    }

    [Fact]
    public void Format_SecretByPrefix_IsMasked() =>
        Assert.Contains("sk-••••3f9a", Join(PreviewFormatter.Format("OPENAI=sk-proj9999abcd3f9a")));

    [Fact]
    public void Format_JsonQuotedSecret_KeepsQuotes() =>
        Assert.Contains("\"ghp_••••1234\",", Join(PreviewFormatter.Format("\"token\": \"ghp_aaaaaaaa1234\",")));

    [Fact]
    public void Format_ShortSecret_FullyMasked() =>
        Assert.Equal("PASS=••••••••", Join(PreviewFormatter.Format("PASS=abc")));

    [Fact]
    public void Format_NonSecret_Untouched()
    {
        var s = PreviewFormatter.Format("DEBUG=false");
        Assert.Equal("=false", s[1].Text);
    }

    [Fact]
    public void Format_Over40Lines_Cut()
    {
        var text = string.Join("\n", Enumerable.Range(0, 100).Select(i => "line" + i));
        var s = PreviewFormatter.Format(text);
        Assert.Equal(39, s.Count(x => x.Text == "\n" && !x.IsKey));
        Assert.Equal(40, Join(s).Split('\n').Length);
    }

    [Fact]
    public void Format_LongLine_IsTruncated()
    {
        var j = Join(PreviewFormatter.Format(new string('a', 5000)));
        Assert.Equal(200, j.Length);
        Assert.EndsWith("…", j);
    }

    [Fact]
    public void Format_CrLf_SplitsLines() =>
        Assert.Equal("A=1\nB=2", Join(PreviewFormatter.Format("A=1\r\nB=2")));
}
