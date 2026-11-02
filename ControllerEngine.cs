namespace TVController;

internal sealed class ControllerEngine : IDisposable
{
    readonly AppSettings settings;
    readonly CancellationTokenSource stop = new();
    readonly SdlControllerReader sdl = new();
    readonly UniversalControllerReader universal = new();
    readonly Dictionary<ushort, string> buttons = new()
    {
        [0x1000]="A", [0x2000]="B", [0x4000]="X", [0x8000]="Y", [0x0001]="D-pad Up", [0x0002]="D-pad Down",
        [0x0004]="D-pad Left", [0x0008]="D-pad Right", [0x0100]="LB", [0x0200]="RB", [0x0020]="View", [0x0010]="Menu",
        [0x0040]="Left stick", [0x0080]="Right stick"
    };
    ushort previous;
    bool previousLeftTrigger, previousRightTrigger;
    uint activeSlot = uint.MaxValue;
    bool keyboardShownByUs;
    int caretStableChecks;
    double mouseRemainderX, mouseRemainderY, scrollRemainder;
    static readonly Dictionary<int, (DateTime Checked, bool Native)> NativeInputCache = new();
    public event Action<bool, bool, string>? StatusChanged;

    public ControllerEngine(AppSettings settings) { this.settings = settings; }
    public void Start() => _ = Task.Run(Loop);

    async Task Loop()
    {
        var lastConnected = false; var lastPaused = false; var lastDevice = ""; var caretTicks = 0; var foregroundTicks = 0; var keepAliveTicks = 0; var gameInForeground = false;
        while (!stop.IsCancellationRequested)
        {
            try
            {
                var connected = TryReadController(out var state, out var device);
                // Some third-party receivers incorrectly idle while only being read. A silent
                // output report keeps those links active without vibration or synthetic input.
                if (connected && activeSlot < 4 && settings.KeepControllerAwake && ++keepAliveTicks >= 5000)
                {
                    keepAliveTicks = 0;
                    if (!Native.KeepAlive(activeSlot)) { Log($"Keep-alive auf Slot {activeSlot + 1} fehlgeschlagen"); activeSlot = uint.MaxValue; }
                }
                if (++foregroundTicks >= (connected ? 25 : 1))
                {
                    foregroundTicks = 0;
                    gameInForeground = settings.PauseInFullscreen && LooksLikeGame(Native.ForegroundApp());
                }
                var paused = gameInForeground;
                if (connected != lastConnected || paused != lastPaused || device != lastDevice)
                {
                    Log(connected ? $"Controller verbunden: {device}" : "Controller getrennt");
                    if (paused != lastPaused) Log(paused ? "Für erkanntes Spiel pausiert" : "Steuerung fortgesetzt");
                    StatusChanged?.Invoke(connected, paused, device); lastConnected = connected; lastPaused = paused; lastDevice = device;
                }
                if (connected && settings.Enabled && !paused)
                {
                    Process(state.Gamepad);
                    if (settings.PlayStationTouchpad) sdl.ProcessTouchpad(settings.MouseSpeed); else sdl.ReleaseTouchpad();
                }
                else { if (previous != 0 || previousLeftTrigger || previousRightTrigger) ReleaseAll(); sdl.ReleaseTouchpad(); }

                if (settings.AutoKeyboard && ++caretTicks >= 120)
                {
                    caretTicks = 0; var caret = Native.HasTextCaret();
                    if (!caret) { caretStableChecks = 0; keyboardShownByUs = false; }
                    else if (Native.IsKeyboardVisible()) { caretStableChecks = 0; keyboardShownByUs = true; }
                    else if (!keyboardShownByUs && ++caretStableChecks >= 3)
                    {
                        Native.ShowKeyboard(); keyboardShownByUs = true; caretStableChecks = 0;
                    }
                }
                await Task.Delay(connected ? 4 : 250, stop.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stop.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                Log($"Eingabeschleife abgefangen: {ex.GetType().Name}: {ex.Message}");
                try { await Task.Delay(250, stop.Token).ConfigureAwait(false); } catch { break; }
            }
        }
    }

    bool TryReadController(out Native.XInputState state, out string device)
    {
        if (sdl.TryRead(out state, out device)) { activeSlot = uint.MaxValue; return true; }
        if (universal.TryRead(out state, out device)) { activeSlot = uint.MaxValue; return true; }
        device = "Xbox Controller · XInput";
        if (activeSlot < 4 && Native.XInputGetState(activeSlot, out state) == 0) return true;
        activeSlot = uint.MaxValue;
        for (uint slot = 0; slot < 4; slot++)
            if (Native.XInputGetState(slot, out state) == 0) { activeSlot = slot; return true; }
        state = default; device = ""; return false;
    }

    static bool LooksLikeGame((int ProcessId, string Name, string Path, string Title, bool Fullscreen) app)
    {
        if (app.Name.Equals("steam", StringComparison.OrdinalIgnoreCase) || app.Name.Equals("steamwebhelper", StringComparison.OrdinalIgnoreCase))
            return app.Fullscreen || app.Title.Contains("Big Picture", StringComparison.OrdinalIgnoreCase) || app.Title.Contains("Gamepad", StringComparison.OrdinalIgnoreCase);
        // WebView2 renders CaaM's CSS UI in a child process. It may load GameInput
        // for browser APIs, but it is part of our desktop UI and never a game.
        string[] desktopApps = { "CaaM", "TV Controller", "explorer", "steam", "steamwebhelper", "msedge", "msedgewebview2", "chrome", "firefox", "brave", "opera", "ApplicationFrameHost", "SearchHost", "StartMenuExperienceHost", "TextInputHost", "ShellExperienceHost" };
        if (app.ProcessId <= 0 || desktopApps.Contains(app.Name, StringComparer.OrdinalIgnoreCase)) return false;
        var path = app.Path.Replace('/', '\\');
        if (path.Contains("\\steamapps\\common\\", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("\\XboxGames\\", StringComparison.OrdinalIgnoreCase)) return true;
        string[] known = { "eldenring", "cyberpunk2077", "witcher3", "gta5", "rdr2", "starfield", "forza", "minecraft", "fortnite", "valorant", "overwatch", "diablo", "dota2", "cs2", "hl2", "game" };
        if (known.Any(x => app.Name.Contains(x, StringComparison.OrdinalIgnoreCase))) return true;
        lock (NativeInputCache)
        {
            if (NativeInputCache.TryGetValue(app.ProcessId, out var cached) && DateTime.UtcNow - cached.Checked < (cached.Native ? TimeSpan.FromMinutes(2) : TimeSpan.FromSeconds(5))) return cached.Native;
        }
        var native = false;
        try
        {
            var process = System.Diagnostics.Process.GetProcessById(app.ProcessId); process.Refresh();
            string[] controllerModules = { "xinput1_4.dll", "xinput1_3.dll", "xinput9_1_0.dll", "gameinput.dll", "dinput8.dll", "sdl2.dll", "sdl3.dll", "steam_api.dll", "steam_api64.dll" };
            foreach (System.Diagnostics.ProcessModule module in process.Modules)
                if (controllerModules.Contains(module.ModuleName, StringComparer.OrdinalIgnoreCase)) { native = true; break; }
        }
        catch { native = app.Fullscreen && (path.Contains("\\Games\\", StringComparison.OrdinalIgnoreCase) || path.Contains("\\GOG Galaxy\\Games\\", StringComparison.OrdinalIgnoreCase)); }
        lock (NativeInputCache) NativeInputCache[app.ProcessId] = (DateTime.UtcNow, native);
        return native;
    }

    static void Log(string message)
    {
        try
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CaaM");
            Directory.CreateDirectory(folder);
            File.AppendAllText(Path.Combine(folder, "events.log"), $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}  {message}{Environment.NewLine}");
        }
        catch { }
    }

    void ReleaseAll()
    {
        foreach (var pair in buttons)
            if ((previous & pair.Key) != 0 && settings.Bindings.TryGetValue(pair.Value, out var action)) Run(action, false);
        if (previousLeftTrigger && settings.Bindings.TryGetValue("LT", out var lt)) Run(lt, false);
        if (previousRightTrigger && settings.Bindings.TryGetValue("RT", out var rt)) Run(rt, false);
        previousLeftTrigger = previousRightTrigger = false;
        previous = 0;
    }

    void Process(Native.Gamepad g)
    {
        foreach (var pair in buttons)
        {
            var down = (g.Buttons & pair.Key) != 0; var was = (previous & pair.Key) != 0;
            if (down != was && settings.Bindings.TryGetValue(pair.Value, out var action)) Run(action, down);
        }
        previous = g.Buttons;
        HandleTrigger("LT", g.LeftTrigger > 30, ref previousLeftTrigger);
        HandleTrigger("RT", g.RightTrigger > 30, ref previousRightTrigger);
        var dz = settings.Deadzone;
        // Convert to Int32 before Abs: a stick can report -32768, whose positive
        // counterpart cannot be represented by Int16 and previously caused overflow.
        var rawX = settings.SwapSticks ? (int)g.ThumbRX : (int)g.ThumbLX;
        var rawY = settings.SwapSticks ? (int)g.ThumbRY : (int)g.ThumbLY;
        var velocityX = Math.Abs(rawX) > dz ? Scale(rawX, settings.MouseSpeed) : 0d;
        var velocityY = Math.Abs(rawY) > dz ? -Scale(rawY, settings.MouseSpeed) : 0d;
        mouseRemainderX += velocityX;
        mouseRemainderY += velocityY;
        var x = (int)Math.Truncate(mouseRemainderX);
        var y = (int)Math.Truncate(mouseRemainderY);
        mouseRemainderX -= x;
        mouseRemainderY -= y;
        if (x != 0 || y != 0) Native.MouseMove(x, y);

        var rawScroll = settings.SwapSticks ? (int)g.ThumbLY : (int)g.ThumbRY;
        if (Math.Abs(rawScroll) > dz)
        {
            // Remove the dead-zone from the useful range, then apply a quadratic
            // curve. This makes a slight tilt precise while retaining useful speed
            // at full deflection. Fractional wheel units are preserved between polls.
            var fullRange = rawScroll < 0 ? 32768d : 32767d;
            var normalized = (Math.Abs(rawScroll) - dz) / (fullRange - dz);
            scrollRemainder += Math.Pow(normalized, 2.2) * settings.ScrollSpeed * Math.Sign(rawScroll);
            var wheelDelta = (int)Math.Truncate(scrollRemainder);
            if (wheelDelta != 0)
            {
                Native.Scroll(wheelDelta);
                scrollRemainder -= wheelDelta;
            }
        }
        else scrollRemainder = 0;
    }
    void HandleTrigger(string name, bool down, ref bool previousState)
    {
        if (down != previousState && settings.Bindings.TryGetValue(name, out var action)) Run(action, down);
        previousState = down;
    }
    static double Scale(int value, int speed)
    {
        var magnitude = Math.Abs(value) / (value < 0 ? 32768d : 32767d);
        return Math.Pow(magnitude, 1.7) * (speed / 2d) * Math.Sign(value);
    }
    static ushort Vk(string action) => action switch
    {
        "Enter"=>0x0D, "Escape"=>0x1B, "Space"=>0x20, "Up"=>0x26, "Down"=>0x28, "Left"=>0x25, "Right"=>0x27,
        "Page up"=>0x21, "Page down"=>0x22, "Volume up"=>0xAF, "Volume down"=>0xAE, "Mute"=>0xAD,
        "Play/Pause"=>0xB3, "Browser back"=>0xA6, "Browser forward"=>0xA7, _=>0
    };
    static void Run(string action, bool down)
    {
        if (action == "None") return;
        if (action is "Left click" or "Right click" or "Middle click") { Native.Click(action, down); return; }
        if (!down) { var v = Vk(action); if (v != 0) Native.Key(v, false); return; }
        if (action == "Escape") Native.CloseKeyboard();
        if (action == "Keyboard") { Native.ShowKeyboard(); return; }
        if (action == "Alt+Tab") { Native.Chord(0x12, 0x09); return; }
        if (action == "Win+D") { Native.Chord(0x5B, 0x44); return; }
        var vk = Vk(action); if (vk != 0) Native.Key(vk, true);
    }
    public void Dispose() { stop.Cancel(); sdl.Dispose(); stop.Dispose(); }
}
