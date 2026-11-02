using System.Reflection;
using System.Runtime.InteropServices;

namespace TVController;

/// <summary>SDL's HID gamepad layer covers DualSense, DualShock, Switch, Xbox and mapped third-party pads.</summary>
internal sealed class SdlControllerReader : IDisposable
{
    const uint SDL_INIT_GAMEPAD = 0x00002000;
    IntPtr gamepad;
    uint gamepadId;
    uint packet;
    bool available;
    bool touchActive, touchClickDown;
    float lastTouchX, lastTouchY;
    string touchClickButton = "Left click";

    internal SdlControllerReader()
    {
        try
        {
            SDL_SetHint("SDL_JOYSTICK_ALLOW_BACKGROUND_EVENTS", "1");
            available = SDL_Init(SDL_INIT_GAMEPAD) != 0;
            if (available) LoadMappings();
        }
        catch { available = false; }
    }

    internal bool TryRead(out Native.XInputState state, out string description)
    {
        state = default; description = "";
        if (!available) return false;
        try
        {
            SDL_UpdateGamepads();
            if (gamepad != IntPtr.Zero && SDL_GamepadConnected(gamepad) == 0) CloseCurrent();
            if (gamepad == IntPtr.Zero && !OpenFirst()) return false;
            state.PacketNumber = ++packet;
            state.Gamepad = new Native.Gamepad
            {
                Buttons = Buttons(),
                ThumbLX = SDL_GetGamepadAxis(gamepad, 0), ThumbLY = Invert(SDL_GetGamepadAxis(gamepad, 1)),
                ThumbRX = SDL_GetGamepadAxis(gamepad, 2), ThumbRY = Invert(SDL_GetGamepadAxis(gamepad, 3)),
                LeftTrigger = Trigger(SDL_GetGamepadAxis(gamepad, 4)), RightTrigger = Trigger(SDL_GetGamepadAxis(gamepad, 5))
            };
            var name = Utf8(SDL_GetGamepadName(gamepad));
            var layout = Layout(SDL_GetGamepadType(gamepad));
            description = $"{(string.IsNullOrWhiteSpace(name) ? $"Controller {SDL_GetGamepadVendor(gamepad):X4}:{SDL_GetGamepadProduct(gamepad):X4}" : name)} · {layout}";
            return true;
        }
        catch { CloseCurrent(); return false; }
    }

    bool OpenFirst()
    {
        var ids = SDL_GetGamepads(out var count);
        if (ids == IntPtr.Zero || count <= 0) return false;
        try
        {
            for (var i = 0; i < count; i++)
            {
                var id = unchecked((uint)Marshal.ReadInt32(ids, i * sizeof(uint)));
                var opened = SDL_OpenGamepad(id);
                if (opened != IntPtr.Zero) { gamepad = opened; gamepadId = id; return true; }
            }
        }
        finally { SDL_free(ids); }
        return false;
    }

    ushort Buttons()
    {
        ushort value = 0;
        if (Pressed(0)) value |= 0x1000; if (Pressed(1)) value |= 0x2000; if (Pressed(2)) value |= 0x4000; if (Pressed(3)) value |= 0x8000;
        if (Pressed(11)) value |= 0x0001; if (Pressed(12)) value |= 0x0002; if (Pressed(13)) value |= 0x0004; if (Pressed(14)) value |= 0x0008;
        if (Pressed(9)) value |= 0x0100; if (Pressed(10)) value |= 0x0200; if (Pressed(4)) value |= 0x0020; if (Pressed(6)) value |= 0x0010;
        if (Pressed(7)) value |= 0x0040; if (Pressed(8)) value |= 0x0080;
        return value;
    }
    bool Pressed(int button) => SDL_GetGamepadButton(gamepad, button) != 0;

    internal void ProcessTouchpad(int mouseSpeed)
    {
        if (gamepad == IntPtr.Zero || SDL_GetGamepadType(gamepad) is not (5 or 6) || SDL_GetNumGamepadTouchpads(gamepad) < 1) { ReleaseTouchpad(); return; }
        byte fingerDown; float x, y, pressure;
        var hasFinger = SDL_GetGamepadTouchpadFinger(gamepad, 0, 0, out fingerDown, out x, out y, out pressure) != 0 && fingerDown != 0;
        var clicked = Pressed(20);
        if (hasFinger)
        {
            if (touchActive && !clicked && !touchClickDown)
            {
                var factor = 1800f * Math.Clamp(mouseSpeed, 1, 100) / 25f;
                var dx = (int)Math.Round((x - lastTouchX) * factor);
                var dy = (int)Math.Round((y - lastTouchY) * factor);
                if (Math.Abs(dx) < 500 && Math.Abs(dy) < 500 && (dx != 0 || dy != 0)) Native.MouseMove(dx, dy);
            }
            lastTouchX = x; lastTouchY = y; touchActive = true;
        }
        else touchActive = false;

        if (clicked && !touchClickDown)
        {
            touchClickButton = hasFinger && x >= .5f ? "Right click" : "Left click";
            Native.Click(touchClickButton, true); touchClickDown = true;
        }
        else if (!clicked && touchClickDown)
        {
            Native.Click(touchClickButton, false); touchClickDown = false;
        }
    }

    internal void ReleaseTouchpad()
    {
        touchActive = false;
        if (touchClickDown) { Native.Click(touchClickButton, false); touchClickDown = false; }
    }

    void LoadMappings()
    {
        try
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("CaaM.ControllerMappings");
            if (stream is null) return;
            using var reader = new StreamReader(stream);
            while (reader.ReadLine() is { } line) if (line.Length > 32 && !line.StartsWith('#')) SDL_AddGamepadMapping(line);
        }
        catch { }
    }
    static string Layout(int type) => type switch { 2 or 3 => "Xbox", 4 or 5 or 6 => "PlayStation", 7 or 8 or 9 or 10 => "Switch", 11 => "Steam", _ => "Generic" };
    static string Utf8(IntPtr value) => value == IntPtr.Zero ? "" : Marshal.PtrToStringUTF8(value) ?? "";
    static byte Trigger(short value) => (byte)Math.Clamp((int)Math.Round(Math.Max(0, (int)value) / 32767d * 255), 0, 255);
    static short Invert(short value) => value == short.MinValue ? short.MaxValue : (short)-value;
    void CloseCurrent() { ReleaseTouchpad(); if (gamepad != IntPtr.Zero) SDL_CloseGamepad(gamepad); gamepad = IntPtr.Zero; gamepadId = 0; }
    public void Dispose() { CloseCurrent(); if (available) SDL_Quit(); available = false; }

    [DllImport("SDL3", CallingConvention = CallingConvention.Cdecl)] static extern byte SDL_Init(uint flags);
    [DllImport("SDL3", CallingConvention = CallingConvention.Cdecl)] static extern void SDL_Quit();
    [DllImport("SDL3", CallingConvention = CallingConvention.Cdecl)] static extern byte SDL_SetHint([MarshalAs(UnmanagedType.LPUTF8Str)] string name, [MarshalAs(UnmanagedType.LPUTF8Str)] string value);
    [DllImport("SDL3", CallingConvention = CallingConvention.Cdecl)] static extern IntPtr SDL_GetGamepads(out int count);
    [DllImport("SDL3", CallingConvention = CallingConvention.Cdecl)] static extern IntPtr SDL_OpenGamepad(uint instanceId);
    [DllImport("SDL3", CallingConvention = CallingConvention.Cdecl)] static extern void SDL_CloseGamepad(IntPtr gamepad);
    [DllImport("SDL3", CallingConvention = CallingConvention.Cdecl)] static extern byte SDL_GamepadConnected(IntPtr gamepad);
    [DllImport("SDL3", CallingConvention = CallingConvention.Cdecl)] static extern void SDL_UpdateGamepads();
    [DllImport("SDL3", CallingConvention = CallingConvention.Cdecl)] static extern short SDL_GetGamepadAxis(IntPtr gamepad, int axis);
    [DllImport("SDL3", CallingConvention = CallingConvention.Cdecl)] static extern byte SDL_GetGamepadButton(IntPtr gamepad, int button);
    [DllImport("SDL3", CallingConvention = CallingConvention.Cdecl)] static extern IntPtr SDL_GetGamepadName(IntPtr gamepad);
    [DllImport("SDL3", CallingConvention = CallingConvention.Cdecl)] static extern ushort SDL_GetGamepadVendor(IntPtr gamepad);
    [DllImport("SDL3", CallingConvention = CallingConvention.Cdecl)] static extern ushort SDL_GetGamepadProduct(IntPtr gamepad);
    [DllImport("SDL3", CallingConvention = CallingConvention.Cdecl)] static extern int SDL_GetGamepadType(IntPtr gamepad);
    [DllImport("SDL3", CallingConvention = CallingConvention.Cdecl)] static extern int SDL_GetNumGamepadTouchpads(IntPtr gamepad);
    [DllImport("SDL3", CallingConvention = CallingConvention.Cdecl)] static extern byte SDL_GetGamepadTouchpadFinger(IntPtr gamepad, int touchpad, int finger, out byte down, out float x, out float y, out float pressure);
    [DllImport("SDL3", CallingConvention = CallingConvention.Cdecl)] static extern int SDL_AddGamepadMapping([MarshalAs(UnmanagedType.LPUTF8Str)] string mapping);
    [DllImport("SDL3", CallingConvention = CallingConvention.Cdecl)] static extern void SDL_free(IntPtr memory);
}
