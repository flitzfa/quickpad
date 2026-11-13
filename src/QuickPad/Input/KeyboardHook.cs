using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using QuickPad.Interop;

namespace QuickPad.Input;

/// <summary>Global low-level keyboard hook: raises <see cref="DoubleCtrl"/> on the dispatcher on double Ctrl in File Explorer.
/// The hook lives on its own thread: Windows silently removes a low-level hook whose thread stops answering for about
/// a second, which the UI thread does while it loads a big file or waits on Explorer.</summary>
public sealed class KeyboardHook : IDisposable
{
    const int HC_ACTION = 0, LLKHF_INJECTED = 0x10;
    const int WM_KEYDOWN = 0x100, WM_KEYUP = 0x101, WM_SYSKEYDOWN = 0x104, WM_SYSKEYUP = 0x105;

    readonly Dispatcher _dispatcher;
    readonly DoubleTapDetector _detector = new(); // hook thread only
    readonly Native.HookProc _proc; // field keeps the delegate alive for the GC
    readonly uint _threadId;
    nint _hook;
    long _lastLog = -10_000;

    public event Action<IntPtr>? DoubleCtrl;

    public KeyboardHook(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher;
        _proc = HookCallback;
        int error = 0;
        uint threadId = 0;
        using var installed = new ManualResetEventSlim();
        new Thread(() =>
        {
            threadId = Native.GetCurrentThreadId();
            _hook = Native.SetWindowsHookEx(Native.WH_KEYBOARD_LL, Marshal.GetFunctionPointerForDelegate(_proc),
                Native.GetModuleHandle(null), 0);
            if (_hook == 0) error = Marshal.GetLastWin32Error();
            installed.Set();
            if (_hook == 0) return;
            while (Native.GetMessage(out _, 0, 0, 0) > 0) { } // the hook is called from inside GetMessage
            Native.UnhookWindowsHookEx(_hook);
        }) { IsBackground = true, Name = "KeyboardHook" }.Start();
        installed.Wait();
        if (_hook == 0) throw new Win32Exception(error);
        _threadId = threadId;
    }

    nint HookCallback(int nCode, nint wParam, nint lParam)
    {
        try
        {
            if (nCode == HC_ACTION)
            {
                var k = Marshal.PtrToStructure<Native.KBDLLHOOKSTRUCT>(lParam);
                int msg = (int)wParam;
                if ((k.flags & LLKHF_INJECTED) == 0 && msg is WM_KEYDOWN or WM_KEYUP or WM_SYSKEYDOWN or WM_SYSKEYUP)
                {
                    var fg = Native.GetForegroundWindow();
                    nint explorer = ExplorerFor(fg);
                    if (explorer == 0)
                        _detector.Reset();
                    else if (_detector.OnKey((int)k.vkCode, msg is WM_KEYDOWN or WM_SYSKEYDOWN, k.time) && !IsRenaming(fg))
                        _dispatcher.BeginInvoke(() => DoubleCtrl?.Invoke(explorer));
                }
            }
        }
        catch (Exception ex) // never let an exception escape into the hook chain
        {
            if (Environment.TickCount64 - _lastLog >= 10_000) // a fault here repeats on every key: one line per 10 s
            {
                _lastLog = Environment.TickCount64;
                Log.Error(ex, "KeyboardHook");
            }
        }
        return Native.CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    /// <summary>The Explorer window a double Ctrl is meant for: the foreground one, or, while QuickPad itself is in
    /// front (its preview is open), the one under the pointer, so hovering another file and double Ctrl switches to it.</summary>
    static nint ExplorerFor(nint fg)
    {
        if (fg == 0) return 0;
        if (Native.ClassNameOf(fg) == "CabinetWClass") return fg;
        Native.GetWindowThreadProcessId(fg, out uint pid);
        if (pid != Environment.ProcessId || !Native.GetCursorPos(out var p)) return 0;
        nint root = Native.GetAncestor(Native.WindowFromPoint(p), Native.GA_ROOT);
        return root != 0 && Native.ClassNameOf(root) == "CabinetWClass" ? root : 0;
    }

    static bool IsRenaming(nint fg)
    {
        var info = new Native.GUITHREADINFO { cbSize = (uint)Marshal.SizeOf<Native.GUITHREADINFO>() };
        return Native.GetGUIThreadInfo(Native.GetWindowThreadProcessId(fg, out _), ref info)
            && info.hwndFocus != 0 && Native.ClassNameOf(info.hwndFocus) == "Edit";
    }

    /// <summary>Ends the hook thread's loop; the thread unhooks on its way out.</summary>
    public void Dispose() => Native.PostThreadMessage(_threadId, Native.WM_QUIT, 0, 0);
}
