using System.Globalization;

namespace TVController;

internal static class Loc
{
    static string language = "en";
    static readonly Dictionary<string, string> De = new()
    {
        ["tagline"]="Steuere deinen Computer komplett vom Sofa aus", ["searching"]="●  Suche Controller …",
        ["open"]="Öffnen", ["exit"]="Beenden", ["mappings"]="Tastenbelegung", ["behavior"]="Verhalten", ["settings"]="Einstellungen",
        ["about"]="Über CaaM", ["version"]="Version",
        ["save"]="Speichern", ["stickHelp"]="Linker Stick: Maus  ·  Rechter Stick: Scrollen", ["stickHelpSwapped"]="Rechter Stick: Maus  ·  Linker Stick: Scrollen",
        ["swapSticks"]="Maus- und Scroll-Stick tauschen", ["swapSticksHelp"]="Für Links- oder Rechtshänder: rechter Stick steuert die Maus, linker Stick scrollt.",
        ["enabled"]="Controller-Steuerung aktiv", ["autostart"]="Mit Windows starten",
        ["priority"]="Priorisierter Start (früh und mit Neustartschutz)", ["gamePause"]="Automatische Controller-Übergabe an Spiele",
        ["gamePauseHelp"]="Empfohlen: Erkennt XInput, GameInput, DirectInput, Steam Input und SDL im Vordergrundprogramm. Im Spiel wird nur die Maus-/Tastaturumsetzung pausiert; der echte Controller funktioniert nativ weiter. Nach dem Spiel übernimmt die App automatisch wieder.",
        ["keepAlive"]="Controller-Verbindung aktiv halten", ["autoKeyboard"]="Bildschirmtastatur erzwingen (Fallback)",
        ["psTouchpad"]="PlayStation-Touchpad als Maus verwenden",
        ["autoKeyboardHelp"]="Normalerweise öffnet Windows die Tastatur selbst. Diese Option wartet zunächst und greift nur ein, wenn in einem Textfeld keine Bildschirmtastatur erscheint.",
        ["mouseSpeed"]="Mausgeschwindigkeit", ["scrollSpeed"]="Scrollgeschwindigkeit", ["language"]="Sprache",
        ["controllerLayout"]="Controller-Layout", ["layoutAuto"]="Automatisch", ["layoutGeneric"]="Generisch",
        ["theme"]="Design", ["themeSystem"]="System", ["themeLight"]="Hell", ["themeDark"]="Dunkel", ["themeMinimal"]="Minimal",
        ["customCss"]="Eigenes CSS", ["customCssHelp"]="Ergänzt das gewählte Theme. Die Regeln werden nur geladen, solange das Hauptfenster geöffnet ist.",
        ["languageRestart"]="Eine neue Sprache wird beim nächsten Start der App vollständig übernommen.",
        ["dataTools"]="Sicherung und Zurücksetzen", ["resetSettings"]="Einstellungen zurücksetzen", ["resetBindings"]="Tastenbelegung zurücksetzen",
        ["createBackup"]="Backup erstellen", ["restoreBackup"]="Backup wiederherstellen", ["confirmSettings"]="Alle Einstellungen auf Standard zurücksetzen? Deine Tastenbelegungen bleiben erhalten.",
        ["confirmBindings"]="Alle Tastenbelegungen auf Standard zurücksetzen? Die übrigen Einstellungen bleiben erhalten.", ["confirmTitle"]="Zurücksetzen bestätigen",
        ["backupSaved"]="Das vollständige Backup wurde gespeichert.", ["backupInvalid"]="Die ausgewählte Datei ist kein gültiges CaaM-Backup.", ["restartInfo"]="Die Änderung wurde übernommen. CaaM wird neu gestartet.",
        ["paused"]="●  Nativer Controller-Modus", ["connected"]="●  Controller verbunden", ["disconnected"]="●  Kein Controller", ["saved"]="CaaM — gespeichert",
        ["None"]="Keine Aktion", ["Left click"]="Linksklick", ["Right click"]="Rechtsklick", ["Middle click"]="Mittelklick",
        ["Keyboard"]="Bildschirmtastatur", ["Volume up"]="Lauter", ["Volume down"]="Leiser", ["Mute"]="Stumm",
        ["Play/Pause"]="Wiedergabe/Pause", ["Browser back"]="Browser zurück", ["Browser forward"]="Browser vor",
        ["Page up"]="Seite hoch", ["Page down"]="Seite runter",
        ["Enter"]="Eingabetaste", ["Escape"]="Escape / Zurück", ["Space"]="Leertaste", ["Up"]="Oben", ["Down"]="Unten", ["Left"]="Links", ["Right"]="Rechts",
        ["D-pad Up"]="Steuerkreuz oben", ["D-pad Down"]="Steuerkreuz unten", ["D-pad Left"]="Steuerkreuz links", ["D-pad Right"]="Steuerkreuz rechts",
        ["Left stick"]="Linker Stick", ["Right stick"]="Rechter Stick", ["View"]="Ansicht", ["Menu"]="Menü"
    };

    internal static void Configure(string selected)
    {
        language = selected switch
        {
            "Deutsch" => "de", "English" => "en",
            _ => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("de", StringComparison.OrdinalIgnoreCase) ? "de" : "en"
        };
    }

    internal static string T(string key) => language == "de" && De.TryGetValue(key, out var value) ? value : English(key);
    static string English(string key) => key switch
    {
        "tagline"=>"Control your computer completely from your couch", "searching"=>"●  Looking for a controller …",
        "open"=>"Open", "exit"=>"Exit", "mappings"=>"Button mapping", "behavior"=>"Behavior", "settings"=>"Settings", "about"=>"About", "version"=>"Version", "save"=>"Save",
        "stickHelp"=>"Left stick: Mouse  ·  Right stick: Scroll", "stickHelpSwapped"=>"Right stick: Mouse  ·  Left stick: Scroll", "swapSticks"=>"Swap mouse and scroll sticks", "swapSticksHelp"=>"For left- or right-handed use: the right stick moves the mouse and the left stick scrolls.", "enabled"=>"Controller control enabled",
        "autostart"=>"Start with Windows", "priority"=>"Priority startup (early with restart protection)",
        "gamePause"=>"Automatic controller handoff to games", "gamePauseHelp"=>"Recommended: Detects XInput, GameInput, DirectInput, Steam Input, and SDL in the foreground app. In a game, only mouse and keyboard emulation pauses while the real controller continues natively. The app takes over again after leaving the game.", "keepAlive"=>"Keep controller connection alive",
        "autoKeyboard"=>"Force on-screen keyboard (fallback)", "autoKeyboardHelp"=>"Windows normally opens the keyboard itself. This option waits first and intervenes only when no on-screen keyboard appears in a text field.", "psTouchpad"=>"Use PlayStation touchpad as mouse", "mouseSpeed"=>"Mouse speed",
        "scrollSpeed"=>"Scroll speed", "language"=>"Language", "controllerLayout"=>"Controller layout", "layoutAuto"=>"Automatic", "layoutGeneric"=>"Generic", "theme"=>"Theme", "themeSystem"=>"System", "themeLight"=>"Light", "themeDark"=>"Dark", "themeMinimal"=>"Minimal", "customCss"=>"Custom CSS", "customCssHelp"=>"Extends the selected theme. These rules are loaded only while the main window is open.", "languageRestart"=>"A new language is applied completely the next time the app starts.",
        "dataTools"=>"Backup and reset", "resetSettings"=>"Reset settings", "resetBindings"=>"Reset button mapping", "createBackup"=>"Create backup", "restoreBackup"=>"Restore backup",
        "confirmSettings"=>"Reset all settings to their defaults? Your button mapping will be preserved.", "confirmBindings"=>"Reset all button mappings to their defaults? All other settings will be preserved.",
        "confirmTitle"=>"Confirm reset", "backupSaved"=>"The complete backup was saved.", "backupInvalid"=>"The selected file is not a valid CaaM backup.", "restartInfo"=>"The change was applied. CaaM will restart.",
        "paused"=>"●  Native controller mode", "connected"=>"●  Controller connected", "disconnected"=>"●  No controller", "saved"=>"CaaM — saved",
        _=>key
    };
}
