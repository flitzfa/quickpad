using System.IO;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace QuickPad.Files;

public static class TextFile
{
    public const int PreviewBytes = 64 * 1024;
    public const long EditSizeLimit = 5L * 1024 * 1024;
    private const int BinarySniffBytes = 8 * 1024;

    static TextFile() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    public static bool IsBinary(ReadOnlySpan<byte> head)
    {
        if (head.Length >= 2 && ((head[0] == 0xFF && head[1] == 0xFE) || (head[0] == 0xFE && head[1] == 0xFF)))
            return false;
        return head[..Math.Min(head.Length, BinarySniffBytes)].Contains((byte)0);
    }

    public static TextDocument Open(string path, int maxBytes = int.MaxValue)
    {
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        long length = fs.Length;
        var buf = new byte[Math.Min(length, maxBytes)];
        fs.ReadExactly(buf);
        bool truncated = length > buf.Length;

        if (IsBinary(buf)) throw new NotTextException(path);

        Encoding enc;
        int skip;
        if (buf is [0xEF, 0xBB, 0xBF, ..]) { enc = new UTF8Encoding(true); skip = 3; }
        else if (buf is [0xFF, 0xFE, ..]) { enc = new UnicodeEncoding(false, true); skip = 2; }
        else if (buf is [0xFE, 0xFF, ..]) { enc = new UnicodeEncoding(true, true); skip = 2; }
        else { enc = new UTF8Encoding(false); skip = 0; }

        var body = buf.AsSpan(skip);
        // Decoder (flush only when complete) holds back a multibyte char cut by truncation.
        var strict = skip == 0 ? new UTF8Encoding(false, true) : enc;
        var chars = new char[strict.GetMaxCharCount(body.Length)];
        int n;
        try
        {
            n = strict.GetDecoder().GetChars(body, chars, flush: !truncated);
        }
        catch (DecoderFallbackException) when (skip == 0)
        {
            enc = Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.ANSICodePage);
            chars = new char[enc.GetMaxCharCount(body.Length)];
            n = enc.GetDecoder().GetChars(body, chars, flush: true);
        }
        var text = new string(chars, 0, n);

        return new TextDocument(path, text, enc, DetectLineEnding(text), File.GetLastWriteTimeUtc(path), length, truncated);
    }

    public static TextDocument Save(TextDocument doc, string newText, bool force = false)
    {
        if (doc.Truncated) throw new InvalidOperationException("Cannot save a truncated document");
        var path = doc.Path;
        var info = new FileInfo(path);
        if (!force && (!info.Exists || info.LastWriteTimeUtc != doc.LastWriteUtc || info.Length != doc.Length))
            throw new FileChangedExternallyException(path);

        var text = Regex.Replace(newText, "\r\n|\r|\n", doc.LineEnding == LineEnding.CrLf ? "\r\n" : "\n");
        var tmp = path + ".quickpad.tmp";
        try
        {
            using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                fs.Write(doc.Encoding.GetPreamble());
                fs.Write(doc.Encoding.GetBytes(text));
            }
            if (info.Exists) File.Replace(tmp, path, null); else File.Move(tmp, path);
        }
        finally { File.Delete(tmp); }

        info.Refresh();
        return doc with { Text = text, LastWriteUtc = info.LastWriteTimeUtc, Length = info.Length };
    }

    private static LineEnding DetectLineEnding(string text)
    {
        int crlf = 0, lf = 0;
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] != '\n') continue;
            if (i > 0 && text[i - 1] == '\r') crlf++; else lf++;
        }
        return crlf >= lf ? LineEnding.CrLf : LineEnding.Lf;
    }
}
