using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace TVController;

internal static class Native
{
    [StructLayout(LayoutKind.Sequential)] internal struct XInputState { public uint PacketNumber; public Gamepad Gamepad; }
    [StructLayout(LayoutKind.Sequential)] internal struct Gamepad
    {
        public ushort Buttons; public byte LeftTrigger; public byte RightTrigger;
        public short ThumbLX; public short ThumbLY; public short ThumbRX; public short ThumbRY;
    }
    [DllImport("xinput1_4.dll")] internal static extern uint XInputGetState(uint index, out XInputState state);
    [StructLayout(LayoutKind.Sequential)] internal struct XInputVibration { public ushort LeftMotorSpeed, RightMotorSpeed; }
    [DllImport("xinput1_4.dll")] internal static extern uint XInputSetState(uint index, ref XInputVibration vibration);

    internal static bool KeepAlive(uint index)
    {
        var silent = new XInputVibration();
        return XInputSetState(index, ref silent) == 0;
    }

    [StructLayout(LayoutKind.Sequential)] internal struct INPUT { public uint type; public InputUnion U; }
    [StructLayout(LayoutKind.Explicit)] internal struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT mi; [FieldOffset(0)] public KEYBDINPUT ki;
    }
    [StructLayout(LayoutKind.Sequential)] internal struct MOUSEINPUT
    { public int dx, dy; public uint mouseData, dwFlags, time; public UIntPtr dwExtraInfo; }
    [StructLayout(LayoutKind.Sequential)] internal struct KEYBDINPUT
    { public ushort wVk, wScan; public uint dwFlags, time; public UIntPtr dwExtraInfo; }
    [DllImport("user32.dll", SetLastError = true)] static extern uint SendInput(uint count, INPUT[] inputs, int size);

    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
    [DllImport("user32.dll")] static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);
    [DllImport("user32.dll")] static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFO info);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowTextLength(IntPtr hwnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr hwnd, StringBuilder text, int maxCount);
    [DllImport("user32.dll")] static extern bool GetGUIThreadInfo(uint idThread, ref GUITHREADINFO info);
    [DllImport("user32.dll")] static extern bool PostMessage(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);
    delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll")] static extern bool GetCursorPos(out POINT point);
    [DllImport("user32.dll")] static extern bool SetCursorPos(int x, int y);
    [StructLayout(LayoutKind.Sequential)] struct POINT { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] struct RECT { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] struct MONITORINFO { public uint cbSize; public RECT rcMonitor, rcWork; public uint dwFlags; }
    [StructLayout(LayoutKind.Sequential)] struct GUITHREADINFO
    {
        public uint cbSize, flags; public IntPtr hwndActive, hwndFocus, hwndCapture, hwndMenuOwner, hwndMoveSize, hwndCaret; public RECT rcCaret;
    }

    const uint INPUT_MOUSE = 0, INPUT_KEYBOARD = 1, KEYUP = 2;
    const uint MOVE = 0x0001, LEFTDOWN = 0x0002, LEFTUP = 0x0004, RIGHTDOWN = 0x0008, RIGHTUP = 0x0010;
    const uint MIDDLEDOWN = 0x0020, MIDDLEUP = 0x0040, WHEEL = 0x0800;
    static POINT clickAnchor;
    static int heldMouseButtons;

    internal static void MouseMove(int x, int y) { if (Volatile.Read(ref heldMouseButtons) == 0) Mouse(x, y, MOVE); }
    internal static void Click(string button, bool down)
    {
        uint f = button switch { "Left click" => down ? LEFTDOWN : LEFTUP, "Right click" => down ? RIGHTDOWN : RIGHTUP, _ => down ? MIDDLEDOWN : MIDDLEUP };
        if (down)
        {
            if (Interlocked.Increment(ref heldMouseButtons) == 1) GetCursorPos(out clickAnchor);
        }
        else
        {
            SetCursorPos(clickAnchor.X, clickAnchor.Y);
        }
        Mouse(0, 0, f);
        if (!down && Interlocked.Decrement(ref heldMouseButtons) < 0) Interlocked.Exchange(ref heldMouseButtons, 0);
    }
    internal static void Scroll(int delta) => Mouse(0, 0, WHEEL, unchecked((uint)delta));
    static void Mouse(int x, int y, uint flags, uint data = 0)
    {
        var i = new INPUT { type = INPUT_MOUSE, U = new InputUnion { mi = new MOUSEINPUT { dx = x, dy = y, mouseData = data, dwFlags = flags } } };
        SendInput(1, new[] { i }, Marshal.SizeOf<INPUT>());
    }
    internal static void Key(ushort vk, bool down)
    {
        var i = new INPUT { type = INPUT_KEYBOARD, U = new InputUnion { ki = new KEYBDINPUT { wVk = vk, dwFlags = down ? 0u : KEYUP } } };
        SendInput(1, new[] { i }, Marshal.SizeOf<INPUT>());
    }
    internal static void Chord(params ushort[] keys)
    {
        foreach (var k in keys) Key(k, true);
        for (var n = keys.Length - 1; n >= 0; n--) Key(keys[n], false);
    }
    internal static (int ProcessId, string Name, string Path, string Title, bool Fullscreen) ForegroundApp()
    {
        var w = GetForegroundWindow(); if (w == IntPtr.Zero || !GetWindowRect(w, out var r)) return (0, "", "", "", false);
        GetWindowThreadProcessId(w, out var pid);
        var titleBuffer = new StringBuilder(Math.Max(1, GetWindowTextLength(w) + 1));
        GetWindowText(w, titleBuffer, titleBuffer.Capacity);
        var title = titleBuffer.ToString();
        string name = "", path = "";
        try
        {
            var p = Process.GetProcessById((int)pid); name = p.ProcessName;
            try { path = p.MainModule?.FileName ?? ""; } catch { }
            // The controller app itself loads XInput, and Explorer may load gaming
            // components through shell extensions. Neither must ever trigger handoff.
            if (pid == Environment.ProcessId || name.Equals("explorer", StringComparison.OrdinalIgnoreCase)) return (0, name, path, title, false);
        }
        catch { return (0, "", "", title, false); }
        var m = new MONITORINFO { cbSize = (uint)Marshal.SizeOf<MONITORINFO>() };
        if (!GetMonitorInfo(MonitorFromWindow(w, 2), ref m)) return ((int)pid, name, path, title, false);
        var fullscreen = r.Left <= m.rcMonitor.Left && r.Top <= m.rcMonitor.Top && r.Right >= m.rcMonitor.Right && r.Bottom >= m.rcMonitor.Bottom;
        return ((int)pid, name, path, title, fullscreen);
    }
    internal static bool HasTextCaret()
    {
        var g = new GUITHREADINFO { cbSize = (uint)Marshal.SizeOf<GUITHREADINFO>() };
        return GetGUIThreadInfo(0, ref g) && g.hwndCaret != IntPtr.Zero;
    }
    internal static bool IsKeyboardVisible()
    {
        var visible = false;
        EnumWindows((hwnd, _) =>
        {
            if (!IsWindowVisible(hwnd) || !GetWindowRect(hwnd, out var rect) || rect.Right - rect.Left < 200 || rect.Bottom - rect.Top < 100) return true;
            GetWindowThreadProcessId(hwnd, out var pid);
            try
            {
                var name = Process.GetProcessById((int)pid).ProcessName;
                if (name.Equals("TextInputHost", StringComparison.OrdinalIgnoreCase) || name.Equals("TabTip", StringComparison.OrdinalIgnoreCase) || name.Equals("osk", StringComparison.OrdinalIgnoreCase))
                { visible = true; return false; }
            }
            catch { }
            return true;
        }, IntPtr.Zero);
        return visible;
    }
    internal static void CloseKeyboard()
    {
        foreach (var name in new[] { "TabTip", "osk" })
            foreach (var p in Process.GetProcessesByName(name)) PostMessage(p.MainWindowHandle, 0x0010, IntPtr.Zero, IntPtr.Zero);
    }
    internal static void ShowKeyboard()
    {
        var tabTip = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFiles), "Microsoft Shared", "ink", "TabTip.exe");
        try { Process.Start(new ProcessStartInfo(File.Exists(tabTip) ? tabTip : "osk.exe") { UseShellExecute = true }); } catch { }
    }
}
