using System.IO;
using System.IO.Pipes;
using System.Text;

namespace QuickPad;

/// <summary>One QuickPad per user session: later instances forward their file path to the first one and exit.</summary>
public static class SingleInstance
{
    const string MutexName = "QuickPad.Singleton", PipeName = "QuickPad.Pipe";
    const int MaxMessageBytes = 64 * 1024; // a path is at most 32K UTF-16 chars

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
            client.Connect(3000); // the first instance may still be starting
            client.Write(Encoding.UTF8.GetBytes(path));
        }
        catch (Exception ex) { Log.Error(ex, "SingleInstance.Send"); }
    }

    /// <summary>Receives paths from later instances forever, calling <paramref name="onPath"/> on the caller's
    /// synchronization context (the UI thread when started from it) with each fully qualified path.</summary>
    public static async Task ListenAsync(Action<string> onPath)
    {
        while (true)
        {
            string? path = null;
            try
            {
                using var server = new NamedPipeServerStream(PipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await server.WaitForConnectionAsync();
                var buffer = new MemoryStream();
                var chunk = new byte[4096];
                int n;
                while ((n = await server.ReadAsync(chunk)) > 0 && buffer.Length <= MaxMessageBytes) buffer.Write(chunk, 0, n);
                if (buffer.Length <= MaxMessageBytes) path = Encoding.UTF8.GetString(buffer.ToArray());
            }
            catch (Exception ex)
            {
                Log.Error(ex, "SingleInstance.Listen");
                await Task.Delay(1000); // don't spin if the pipe keeps failing
            }
            if (path is not null && Path.IsPathFullyQualified(path)) onPath(path);
        }
    }
}
