using System.Text;

namespace QuickPad.Files;

public enum LineEnding { CrLf, Lf }

public sealed record TextDocument(string Path, string Text, Encoding Encoding, LineEnding LineEnding, DateTime LastWriteUtc, long Length, bool Truncated);

public sealed class NotTextException(string path) : Exception($"Not a text file: {path}")
{
    public string Path { get; } = path;
}

public sealed class FileChangedExternallyException(string path) : Exception($"File changed on disk: {path}")
{
    public string Path { get; } = path;
}
