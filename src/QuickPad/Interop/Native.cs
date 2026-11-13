using System.Runtime.InteropServices;

namespace QuickPad.Interop;

internal static partial class Native
{
    public delegate nint HookProc(int nCode, nint wParam, nint lParam);

    public const int WH_KEYBOARD_LL = 13;

    [StructLayout(LayoutKind.Sequential)]
    public struct KBDLLHOOKSTRUCT { public uint vkCode, scanCode, flags, time; public nint dwExtraInfo; }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int left, top, right, bottom; }

    [StructLayout(LayoutKind.Sequential)]
    public struct GUITHREADINFO
    {
        public uint cbSize, flags;
        public nint hwndActive, hwndFocus, hwndCapture, hwndMenuOwner, hwndMoveSize, hwndCaret;
        public RECT rcCaret;
    }

    [LibraryImport("user32.dll", EntryPoint = "SetWindowsHookExW", SetLastError = true)]
    public static partial nint SetWindowsHookEx(int idHook, nint lpfn, nint hMod, uint dwThreadId);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool UnhookWindowsHookEx(nint hhk);

    [LibraryImport("user32.dll")]
    public static partial nint CallNextHookEx(nint hhk, int nCode, nint wParam, nint lParam);

    [LibraryImport("user32.dll")]
    public static partial nint GetForegroundWindow();

    [LibraryImport("user32.dll", EntryPoint = "GetClassNameW", SetLastError = true)]
    public static unsafe partial int GetClassName(nint hWnd, char* lpClassName, int nMaxCount);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetGUIThreadInfo(uint idThread, ref GUITHREADINFO lpgui);

    [LibraryImport("user32.dll")]
    public static partial uint GetWindowThreadProcessId(nint hWnd, out uint lpdwProcessId);

    [LibraryImport("user32.dll", EntryPoint = "FindWindowExW", StringMarshalling = StringMarshalling.Utf16)]
    public static partial nint FindWindowEx(nint hWndParent, nint hWndChildAfter, string? lpszClass, string? lpszWindow);

    [LibraryImport("kernel32.dll", EntryPoint = "GetModuleHandleW", StringMarshalling = StringMarshalling.Utf16)]
    public static partial nint GetModuleHandle(string? lpModuleName);

    public const int GWL_STYLE = -16;
    public const nint WS_SYSMENU = 0x00080000;

    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    public static partial nint GetWindowLongPtr(nint hWnd, int nIndex);

    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    public static partial nint SetWindowLongPtr(nint hWnd, int nIndex, nint dwNewLong);

    public const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    public const int DWMWA_WINDOW_CORNER_PREFERENCE = 33, DWMWA_CAPTION_COLOR = 35;
    public const int DWMWCP_ROUND = 2;

    [LibraryImport("dwmapi.dll")]
    public static partial int DwmSetWindowAttribute(nint hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

    public static int DwmSetWindowAttribute(nint hwnd, int attribute, int value) =>
        DwmSetWindowAttribute(hwnd, attribute, ref value, sizeof(int));

    [LibraryImport("kernel32.dll")]
    public static partial nint GetCurrentProcess();

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetProcessWorkingSetSize(nint hProcess, nint dwMinimumWorkingSetSize, nint dwMaximumWorkingSetSize);

    [LibraryImport("kernel32.dll")]
    public static partial uint GetCurrentThreadId();

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool AttachThreadInput(uint idAttach, uint idAttachTo, [MarshalAs(UnmanagedType.Bool)] bool fAttach);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetForegroundWindow(nint hWnd);

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT { public int x, y; }

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetCursorPos(out POINT lpPoint);

    [LibraryImport("user32.dll")]
    public static partial nint WindowFromPoint(POINT point);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool IsChild(nint hWndParent, nint hWnd);

    public const uint SWP_NOZORDER = 0x0004, SWP_NOACTIVATE = 0x0010;

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetWindowPos(nint hWnd, nint hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

    public const uint GA_ROOT = 2;

    [LibraryImport("user32.dll")]
    public static partial nint GetAncestor(nint hwnd, uint gaFlags);

    /// <summary>INPUT with a MOUSEINPUT union: type at 0, the pointer-aligned union at 8 (40 bytes on 64-bit).</summary>
    [StructLayout(LayoutKind.Explicit, Size = 40)]
    public struct MOUSE_INPUT { [FieldOffset(0)] public uint type; }

    [LibraryImport("user32.dll")]
    public static partial uint SendInput(uint cInputs, ref MOUSE_INPUT pInputs, int cbSize);

    /// <summary>Injects a mouse event that moves nothing and clicks nothing. Windows only lets the process that got the
    /// last input take the foreground, and the double Ctrl went to Explorer (same workaround as PowerToys).</summary>
    public static void SendEmptyMouseInput()
    {
        var input = new MOUSE_INPUT { type = 0 }; // INPUT_MOUSE, dx = dy = flags = 0
        SendInput(1, ref input, Marshal.SizeOf<MOUSE_INPUT>());
    }

    public const uint WM_QUIT = 0x0012;

    [StructLayout(LayoutKind.Sequential)]
    public struct MSG { public nint hwnd; public uint message; public nint wParam, lParam; public uint time; public int ptX, ptY; public uint lPrivate; }

    [LibraryImport("user32.dll", EntryPoint = "GetMessageW")]
    public static partial int GetMessage(out MSG lpMsg, nint hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [LibraryImport("user32.dll", EntryPoint = "PostThreadMessageW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool PostThreadMessage(uint idThread, uint msg, nint wParam, nint lParam);

    [LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool ShutdownBlockReasonCreate(nint hWnd, string pwszReason);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool ShutdownBlockReasonDestroy(nint hWnd);

    public static unsafe string ClassNameOf(nint hWnd)
    {
        char* buf = stackalloc char[256];
        return new string(buf, 0, GetClassName(hWnd, buf, 256));
    }
}
