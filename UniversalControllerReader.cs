using Windows.Gaming.Input;

namespace TVController;

/// <summary>Uses Windows' unified gaming-input projection first and leaves XInput as fallback.</summary>
internal sealed class UniversalControllerReader
{
    internal bool TryRead(out Native.XInputState state, out string deviceDescription)
    {
        state = default; deviceDescription = "";
        try
        {
            if (Gamepad.Gamepads.Count > 0)
            {
                var gamepad = Gamepad.Gamepads[0];
                var reading = gamepad.GetCurrentReading();
                state.PacketNumber = unchecked((uint)reading.Timestamp);
                state.Gamepad = new Native.Gamepad
                {
                    Buttons = MapButtons(reading.Buttons),
                    LeftTrigger = ToTrigger(reading.LeftTrigger), RightTrigger = ToTrigger(reading.RightTrigger),
                    ThumbLX = ToAxis(reading.LeftThumbstickX), ThumbLY = ToAxis(reading.LeftThumbstickY),
                    ThumbRX = ToAxis(reading.RightThumbstickX), ThumbRY = ToAxis(reading.RightThumbstickY)
                };
                var raw = RawGameController.FromGameController(gamepad);
                deviceDescription = Describe(raw);
                return true;
            }

            if (RawGameController.RawGameControllers.Count > 0)
            {
                var raw = RawGameController.RawGameControllers[0];
                var buttons = new bool[raw.ButtonCount];
                var switches = new GameControllerSwitchPosition[raw.SwitchCount];
                var axes = new double[raw.AxisCount];
                state.PacketNumber = unchecked((uint)raw.GetCurrentReading(buttons, switches, axes));
                state.Gamepad = MapRaw(buttons, switches, axes);
                deviceDescription = Describe(raw);
                return true;
            }
        }
        catch { }
        return false;
    }

    static string Describe(RawGameController? raw)
    {
        if (raw is null) return "Gamepad · Auto";
        var name = string.IsNullOrWhiteSpace(raw.DisplayName) ? $"Controller {raw.HardwareVendorId:X4}:{raw.HardwareProductId:X4}" : raw.DisplayName.Trim();
        var lower = name.ToLowerInvariant();
        var layout = lower.Contains("dualshock") || lower.Contains("dualsense") || lower.Contains("playstation") ? "PlayStation" :
            lower.Contains("switch") || lower.Contains("nintendo") || lower.Contains("joy-con") ? "Switch" :
            lower.Contains("xbox") || raw.HardwareVendorId == 0x045E ? "Xbox" : "Generic";
        return $"{name} · {layout}";
    }

    static Native.Gamepad MapRaw(bool[] b, GameControllerSwitchPosition[] switches, double[] a)
    {
        ushort buttons = 0;
        ushort[] map = { 0x1000, 0x2000, 0x4000, 0x8000, 0x0100, 0x0200, 0x0020, 0x0010, 0x0040, 0x0080 };
        for (var i = 0; i < Math.Min(b.Length, map.Length); i++) if (b[i]) buttons |= map[i];
        if (switches.Length > 0)
        {
            var value = switches[0];
            if (value is GameControllerSwitchPosition.Up or GameControllerSwitchPosition.UpRight or GameControllerSwitchPosition.UpLeft) buttons |= 0x0001;
            if (value is GameControllerSwitchPosition.Down or GameControllerSwitchPosition.DownRight or GameControllerSwitchPosition.DownLeft) buttons |= 0x0002;
            if (value is GameControllerSwitchPosition.Left or GameControllerSwitchPosition.UpLeft or GameControllerSwitchPosition.DownLeft) buttons |= 0x0004;
            if (value is GameControllerSwitchPosition.Right or GameControllerSwitchPosition.UpRight or GameControllerSwitchPosition.DownRight) buttons |= 0x0008;
        }
        return new Native.Gamepad
        {
            Buttons = buttons,
            ThumbLX = RawAxis(a, 0), ThumbLY = Invert(RawAxis(a, 1)), ThumbRX = RawAxis(a, 2), ThumbRY = Invert(RawAxis(a, 3)),
            LeftTrigger = RawTrigger(a, 4), RightTrigger = RawTrigger(a, 5)
        };
    }

    static ushort MapButtons(GamepadButtons b)
    {
        ushort value = 0;
        if (b.HasFlag(GamepadButtons.A)) value |= 0x1000; if (b.HasFlag(GamepadButtons.B)) value |= 0x2000;
        if (b.HasFlag(GamepadButtons.X)) value |= 0x4000; if (b.HasFlag(GamepadButtons.Y)) value |= 0x8000;
        if (b.HasFlag(GamepadButtons.DPadUp)) value |= 0x0001; if (b.HasFlag(GamepadButtons.DPadDown)) value |= 0x0002;
        if (b.HasFlag(GamepadButtons.DPadLeft)) value |= 0x0004; if (b.HasFlag(GamepadButtons.DPadRight)) value |= 0x0008;
        if (b.HasFlag(GamepadButtons.LeftShoulder)) value |= 0x0100; if (b.HasFlag(GamepadButtons.RightShoulder)) value |= 0x0200;
        if (b.HasFlag(GamepadButtons.View)) value |= 0x0020; if (b.HasFlag(GamepadButtons.Menu)) value |= 0x0010;
        if (b.HasFlag(GamepadButtons.LeftThumbstick)) value |= 0x0040; if (b.HasFlag(GamepadButtons.RightThumbstick)) value |= 0x0080;
        return value;
    }
    static byte ToTrigger(double value) => (byte)Math.Clamp((int)Math.Round(value * 255), 0, 255);
    static short ToAxis(double value) => (short)Math.Clamp((int)Math.Round(value * 32767), -32768, 32767);
    static short RawAxis(double[] axes, int index) => index < axes.Length ? ToAxis((axes[index] - .5) * 2) : (short)0;
    static byte RawTrigger(double[] axes, int index) => index < axes.Length ? ToTrigger(axes[index]) : (byte)0;
    static short Invert(short value) => value == short.MinValue ? short.MaxValue : (short)-value;
}
