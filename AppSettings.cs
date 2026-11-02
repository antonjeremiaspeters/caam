using System.Text.Json;

namespace TVController;

public sealed class AppSettings
{
    public bool Enabled { get; set; } = true;
    public bool StartWithWindows { get; set; }
    public bool PriorityStartup { get; set; }
    public bool PauseInFullscreen { get; set; } = true;
    public int SettingsVersion { get; set; } = 5;
    public bool AutoKeyboard { get; set; }
    public bool KeepControllerAwake { get; set; } = true;
    public bool PlayStationTouchpad { get; set; } = true;
    public bool SwapSticks { get; set; }
    public string Language { get; set; } = "System";
    public string ControllerLayout { get; set; } = "Auto";
    public string Theme { get; set; } = "System";
    public string CustomCss { get; set; } = "";
    public int MouseSpeed { get; set; } = 25;
    public int ScrollSpeed { get; set; } = 25;
    public int Deadzone { get; set; } = 6500;
    public Dictionary<string, string> Bindings { get; set; } = Defaults();

    public static Dictionary<string, string> Defaults() => new()
    {
        ["A"] = "Left click", ["B"] = "Escape", ["X"] = "Right click", ["Y"] = "Keyboard",
        ["D-pad Up"] = "Up", ["D-pad Down"] = "Down", ["D-pad Left"] = "Left", ["D-pad Right"] = "Right",
        ["LB"] = "Browser back", ["RB"] = "Browser forward", ["View"] = "Alt+Tab", ["Menu"] = "Enter",
        ["LT"] = "None", ["RT"] = "None", ["Left stick"] = "Middle click", ["Right stick"] = "None"
    };

    static string Folder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CaaM");
    static string LegacyFilePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TVController", "settings.json");
    static string FilePath => Path.Combine(Folder, "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(FilePath) && File.Exists(LegacyFilePath))
            {
                Directory.CreateDirectory(Folder);
                File.Copy(LegacyFilePath, FilePath, false);
            }
            if (File.Exists(FilePath))
            {
                var loaded = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new();
                // Version 1 used an over-broad WindowsApps heuristic which could pause
                // controller input a few seconds after launch. Migrate it to the safe default.
                if (loaded.SettingsVersion < 2) { loaded.PauseInFullscreen = false; loaded.SettingsVersion = 2; }
                if (loaded.SettingsVersion < 3) { loaded.PauseInFullscreen = true; loaded.SettingsVersion = 3; }
                if (loaded.SettingsVersion < 4)
                {
                    if (loaded.MouseSpeed == 16) loaded.MouseSpeed = 25;
                    if (loaded.ScrollSpeed == 3) loaded.ScrollSpeed = 25;
                    loaded.SettingsVersion = 4;
                }
                loaded.Normalize();
                return loaded;
            }
        }
        catch { }
        return new();
    }

    public void Save()
    {
        Directory.CreateDirectory(Folder);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }

    public void Export(string path) => File.WriteAllText(path, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));

    public static bool TryImport(string path, out AppSettings imported)
    {
        imported = new();
        try { imported = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path)) ?? new(); imported.Normalize(); return true; }
        catch { return false; }
    }

    void Normalize()
    {
        MouseSpeed = Math.Clamp(MouseSpeed, 1, 100); ScrollSpeed = Math.Clamp(ScrollSpeed, 1, 100); Deadzone = Math.Clamp(Deadzone, 0, 30000);
        Language = Language is "System" or "Deutsch" or "English" ? Language : "System";
        ControllerLayout = ControllerLayout is "Auto" or "Xbox" or "PlayStation" or "Switch" or "Generic" ? ControllerLayout : "Auto";
        Theme = Theme is "System" or "Light" or "Dark" or "Minimal" ? Theme : "System";
        CustomCss ??= "";
        if (CustomCss.Length > 100_000) CustomCss = CustomCss[..100_000];
        var defaults = Defaults();
        string[] valid = { "None", "Left click", "Right click", "Middle click", "Enter", "Escape", "Space", "Up", "Down", "Left", "Right", "Page up", "Page down", "Alt+Tab", "Win+D", "Keyboard", "Volume up", "Volume down", "Mute", "Play/Pause", "Browser back", "Browser forward" };
        Bindings ??= Defaults();
        foreach (var key in defaults.Keys) if (!Bindings.TryGetValue(key, out var action) || !valid.Contains(action)) Bindings[key] = defaults[key];
        SettingsVersion = 5;
    }
}
