using System.IO;

namespace QuickPad;

/// <summary>Appends dated lines to %LOCALAPPDATA%\QuickPad\log.txt. Never throws.</summary>
public static class Log
{
    static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "QuickPad", "log.txt");
    static readonly Lock Gate = new();

    static Log() // first log line of each run: keeps the log under ~2 MB across runs
    {
        try
        {
            if (new FileInfo(FilePath).Length > 1024 * 1024)
                File.Move(FilePath, Path.ChangeExtension(FilePath, ".old.txt"), overwrite: true);
        }
        catch { /* missing, or in use by another instance: try again next start */ }
    }

    public static void Error(Exception ex, string context) => Write($"ERROR {context}: {ex}");

    /// <summary>For measurements only.</summary>
    public static void Info(string message) => Write(message);

    static void Write(string line)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                File.AppendAllText(FilePath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {line}{Environment.NewLine}");
            }
        }
        catch { /* logging must never take the app down */ }
    }
}
