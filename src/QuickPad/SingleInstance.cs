using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Security.Principal;
using System.Text;

namespace QuickPad;

/// <summary>One QuickPad per user session: later instances forward their file path to the first one and exit.</summary>
public static class SingleInstance
{
    const string MutexName = "QuickPad.Singleton"; // session-local, like the pipe name below
    const int MaxMessageBytes = 64 * 1024; // a path is at most 32K UTF-16 chars
    const int ReadTimeoutMs = 5000;

    // Pipe names are machine-wide: without user and session, another signed-in user's QuickPad would own it.
    static readonly string PipeName = $"QuickPad.Pipe.{WindowsIdentity.GetCurrent().User}.{Process.GetCurrentProcess().SessionId}";

    /// <summary>The mutex while this process is the first instance (keep it alive until exit); null otherwise.</summary>
    public static Mutex? TryClaim()
    {
        var mutex = new Mutex(initiallyOwned: true, MutexName, out bool createdNew);
        if (createdNew) return mutex;
        mutex.Dispose();
        return null;
    }

    /// <summary>Sends <paramref name="path"/> (UTF-8) to the first instance. Never throws.</summary>
    public static void Send(string path)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out, PipeOptions.CurrentUserOnly);
            // Longer than the server's read timeout: the first instance may be starting, or busy dropping a hung client.
            client.Connect(ReadTimeoutMs * 2);
            client.Write(Encoding.UTF8.GetBytes(path));
        }
        catch (Exception ex) { Log.Error(ex, "SingleInstance.Send"); }
    }

    /// <summary>Receives paths from later instances forever, calling <paramref name="onPath"/> on the caller's
    /// synchronization context (the UI thread when started from it) with each fully qualified path.</summary>
    public static async Task ListenAsync(Action<string> onPath)
    {
        int failures = 0;
        while (true)
        {
            string? path = null;
            try
            {
                using var server = new NamedPipeServerStream(PipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await server.WaitForConnectionAsync();
                failures = 0;
                path = await ReadMessageAsync(server);
            }
            catch (OperationCanceledException) { } // a client that never finished writing: drop it, keep listening
            catch (Exception ex)
            {
                if (failures++ == 0) Log.Error(ex, "SingleInstance.Listen"); // a persistent fault logs once, not per retry
                await Task.Delay(TimeSpan.FromSeconds(Math.Min(60, 1 << Math.Min(failures, 6))));
            }
            if (path is not null && Path.IsPathFullyQualified(path)) onPath(path);
        }
    }

    /// <summary>The whole message as UTF-8, or null when it is too big. Gives up after <see cref="ReadTimeoutMs"/> so a hung
    /// client can't hold the single pipe instance.</summary>
    static async Task<string?> ReadMessageAsync(Stream stream)
    {
        using var timeout = new CancellationTokenSource(ReadTimeoutMs);
        var buffer = new MemoryStream();
        var chunk = new byte[4096];
        int n;
        while ((n = await stream.ReadAsync(chunk, timeout.Token)) > 0)
        {
            buffer.Write(chunk, 0, n);
            if (buffer.Length > MaxMessageBytes) return null;
        }
        return Encoding.UTF8.GetString(buffer.ToArray());
    }
}
