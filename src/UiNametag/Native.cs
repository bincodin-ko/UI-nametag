using System.Diagnostics;
using System.Runtime.InteropServices;

namespace UiNametag;

static class Native
{
    public const int WM_HOTKEY = 0x0312, WH_MOUSE_LL = 14, WM_LBUTTONDOWN = 0x201, WM_LBUTTONUP = 0x202, WM_RBUTTONDOWN = 0x204, WM_RBUTTONUP = 0x205, WM_MOUSEMOVE = 0x200;
    public const uint MOD_CONTROL = 2, MOD_SHIFT = 4, MOD_NOREPEAT = 0x4000;
    public const int GWL_EXSTYLE = -20, WS_EX_TRANSPARENT = 0x20, WS_EX_LAYERED = 0x80000, WS_EX_TOOLWINDOW = 0x80, WS_EX_NOACTIVATE = 0x8000000;

    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] public struct MSLLHOOKSTRUCT { public POINT pt; public uint mouseData, flags, time; public IntPtr extra; }
    public delegate IntPtr HookProc(int code, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint mods, uint vk);
    [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    [DllImport("user32.dll")] public static extern IntPtr SetWindowsHookEx(int idHook, HookProc fn, IntPtr hMod, uint threadId);
    [DllImport("user32.dll")] public static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")] public static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll")] public static extern IntPtr GetModuleHandle(string? name);
    [DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr h, int i);
    [DllImport("user32.dll")] public static extern int SetWindowLong(IntPtr h, int i, int v);
    [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT p);
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int v, int size);
    public static void DarkTitle(IntPtr h) { int on = 1; DwmSetWindowAttribute(h, 20, ref on, 4); } // 어두운 제목 줄
    [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h, int msg, IntPtr w, IntPtr l);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] public static extern IntPtr WindowFromPoint(POINT p);
    [DllImport("user32.dll")] public static extern IntPtr GetAncestor(IntPtr h, uint flags);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
    [DllImport("user32.dll")] public static extern void mouse_event(uint flags, int dx, int dy, uint data, IntPtr extra);
    [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, IntPtr extra);

    public static void ClickThrough(IntPtr h, bool on)
    {
        var s = GetWindowLong(h, GWL_EXSTYLE) | WS_EX_LAYERED | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
        SetWindowLong(h, GWL_EXSTYLE, on ? s | WS_EX_TRANSPARENT : s & ~WS_EX_TRANSPARENT);
    }

    public static System.Windows.Point Cursor() { GetCursorPos(out var p); return new(p.X, p.Y); }

    // 시험용: 실제 마우스로 클릭
    // 시험용: 실제 마우스로 끌기 (picker 훅이 막으므로 아래 앱엔 안 감)
    public static void Drag(int x1, int y1, int x2, int y2)
    {
        SetCursorPos(x1, y1); Thread.Sleep(80); mouse_event(2, 0, 0, 0, 0);
        for (int i = 1; i <= 10; i++) { SetCursorPos(x1 + (x2 - x1) * i / 10, y1 + (y2 - y1) * i / 10); Thread.Sleep(30); }
        mouse_event(4, 0, 0, 0, 0);
    }

    public static void Click(int x, int y) { SetCursorPos(x, y); Thread.Sleep(80); mouse_event(2, 0, 0, 0, 0); mouse_event(4, 0, 0, 0, 0); }

    public static void Keys(params byte[] vks) { foreach (var k in vks) keybd_event(k, 0, 0, 0); foreach (var k in vks.Reverse()) keybd_event(k, 0, 2, 0); }

    public static IntPtr MouseHook(HookProc fn) => SetWindowsHookEx(WH_MOUSE_LL, fn, GetModuleHandle(Process.GetCurrentProcess().MainModule!.ModuleName), 0);
}
