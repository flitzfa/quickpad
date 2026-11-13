using System.IO;
using System.Text;
using QuickPad.Files;

namespace QuickPad.Tests;

public class TextFileSaveTests
{
    private static string Write(byte[] bytes)
    {
        var p = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".txt");
        File.WriteAllBytes(p, bytes);
        return p;
    }

    private static byte[] Bytes(string s) => Encoding.UTF8.GetBytes(s);

    [Fact]
    public void Save_RoundTrip_PreservesBytes()
    {
        var original = new byte[] { 0xEF, 0xBB, 0xBF }.Concat(Bytes("a\r\nb\r\n")).ToArray();
        var p = Write(original);
        try
        {
            TextFile.Save(TextFile.Open(p), TextFile.Open(p).Text);
            Assert.Equal(original, File.ReadAllBytes(p));
        }
        finally { File.Delete(p); }
    }

    [Fact]
    public void Save_TextboxCrLf_IntoLfFile_StaysLf()
    {
        var p = Write(Bytes("a\nb"));
        try
        {
            TextFile.Save(TextFile.Open(p), "a\r\nb\r\nc");
            Assert.Equal(Bytes("a\nb\nc"), File.ReadAllBytes(p));
        }
        finally { File.Delete(p); }
    }

    [Fact]
    public void Save_NoTrailingNewline_StaysAbsent()
    {
        var p = Write(Bytes("a\nb"));
        try
        {
            var doc = TextFile.Open(p);
            TextFile.Save(doc, doc.Text);
            Assert.Equal(Bytes("a\nb"), File.ReadAllBytes(p));
        }
        finally { File.Delete(p); }
    }

    [Fact]
    public void Save_LeavesNoTempFile()
    {
        var p = Write(Bytes("a"));
        try
        {
            TextFile.Save(TextFile.Open(p), "b");
            Assert.False(File.Exists(p + ".quickpad.tmp"));
        }
        finally { File.Delete(p); }
    }

    [Fact]
    public void Save_Twice_Succeeds()
    {
        var p = Write(Bytes("a\nz"));
        try
        {
            var doc = TextFile.Save(TextFile.Open(p), "b\r\nc");
            Assert.Equal("b\nc", doc.Text);
            TextFile.Save(doc, "d");
            Assert.Equal(Bytes("d"), File.ReadAllBytes(p));
        }
        finally { File.Delete(p); }
    }

    [Fact]
    public void Save_ModifiedExternally_Throws()
    {
        var p = Write(Bytes("a"));
        try
        {
            var doc = TextFile.Open(p);
            File.WriteAllText(p, "other content");
            Assert.Throws<FileChangedExternallyException>(() => TextFile.Save(doc, "mine"));
            TextFile.Save(doc, "mine", force: true);
            Assert.Equal(Bytes("mine"), File.ReadAllBytes(p));
        }
        finally { File.Delete(p); }
    }

    [Fact]
    public void Save_DeletedFile_ThrowsChangedExternally()
    {
        var p = Write(Bytes("a"));
        try
        {
            var doc = TextFile.Open(p);
            File.Delete(p);
            Assert.Throws<FileChangedExternallyException>(() => TextFile.Save(doc, "mine"));
            TextFile.Save(doc, "mine", force: true);
            Assert.Equal(Bytes("mine"), File.ReadAllBytes(p));
        }
        finally { File.Delete(p); }
    }

    [Fact]
    public void Save_ReadOnly_ThrowsUnauthorized()
    {
        var p = Write(Bytes("a"));
        try
        {
            var doc = TextFile.Open(p);
            File.SetAttributes(p, FileAttributes.ReadOnly);
            Assert.Throws<UnauthorizedAccessException>(() => TextFile.Save(doc, "b"));
        }
        finally { File.SetAttributes(p, FileAttributes.Normal); File.Delete(p); File.Delete(p + ".quickpad.tmp"); }
    }

    [Fact]
    public void Save_TruncatedDoc_Throws()
    {
        var p = Write(Bytes("0123456789"));
        try
        {
            var doc = TextFile.Open(p, 4);
            Assert.True(doc.Truncated);
            Assert.Throws<InvalidOperationException>(() => TextFile.Save(doc, "x", force: true));
            Assert.Equal(Bytes("0123456789"), File.ReadAllBytes(p));
        }
        finally { File.Delete(p); }
    }

    [Fact]
    public void Save_AnsiUnrepresentableChar_Throws()
    {
        var original = new byte[] { (byte)'a', 0xF1 };
        var p = Write(original);
        try
        {
            var doc = TextFile.Open(p);
            Assert.Throws<EncoderFallbackException>(() => TextFile.Save(doc, doc.Text + "€✓"));
            Assert.Equal(original, File.ReadAllBytes(p));
            Assert.False(File.Exists(p + ".quickpad.tmp"));
        }
        finally { File.Delete(p); }
    }
}
