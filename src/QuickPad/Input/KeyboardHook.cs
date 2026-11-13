using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using QuickPad.Interop;

namespace QuickPad.Input;

/// <summary>Global low-level keyboard hook: raises <see cref="DoubleCtrl"/> on double Ctrl in File Explorer.
/// Must be created on a thread with a message loop (the UI thread).</summary>
public sealed class KeyboardHook : IDisposable
{
    const int HC_ACTION = 0, LLKHF_INJECTED = 0x10;
    const int WM_KEYDOWN = 0x100, WM_KEYUP = 0x101, WM_SYSKEYDOWN = 0x104, WM_SYSKEYUP = 0x105;

    readonly Dispatcher _dispatcher;
    readonly DoubleTapDetector _detector = new();
    readonly Native.HookProc _proc; // field keeps the delegate alive for the GC
    nint _hook;

    public event Action<IntPtr>? DoubleCtrl;

    public KeyboardHook(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher;
        _proc = HookCallback;
        _hook = Native.SetWindowsHookEx(Native.WH_KEYBOARD_LL, Marshal.GetFunctionPointerForDelegate(_proc),
            Native.GetModuleHandle(null), 0);
        if (_hook == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
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
                    if (fg == 0 || Native.ClassNameOf(fg) != "CabinetWClass")
                        _detector.Reset();
                    else if (_detector.OnKey((int)k.vkCode, msg is WM_KEYDOWN or WM_SYSKEYDOWN, k.time) && !IsRenaming(fg))
                        _dispatcher.BeginInvoke(() => DoubleCtrl?.Invoke(fg));
                }
            }
        }
        catch { /* never let an exception escape into the hook chain */ }
        return Native.CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    static bool IsRenaming(nint fg)
    {
        var info = new Native.GUITHREADINFO { cbSize = (uint)Marshal.SizeOf<Native.GUITHREADINFO>() };
        return Native.GetGUIThreadInfo(Native.GetWindowThreadProcessId(fg, out _), ref info)
            && info.hwndFocus != 0 && Native.ClassNameOf(info.hwndFocus) == "Edit";
    }

    public void Dispose()
    {
        if (_hook == 0) return;
        Native.UnhookWindowsHookEx(_hook);
        _hook = 0;
    }
}
