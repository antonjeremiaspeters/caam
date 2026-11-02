using Microsoft.Win32;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;

namespace TVController;

public sealed class MainForm : Form
{
    static Color Bg => ThemeManager.Current.Background;
    static Color Card => ThemeManager.Current.Surface;
    static Color Accent => ThemeManager.Current.Accent;
    static Color Muted => ThemeManager.Current.Muted;
    AppSettings settings = AppSettings.Load();
    ControllerEngine? engine;
    readonly Label status = new();
    readonly FlowLayoutPanel mappings = new();
    Label? stickHelpLabel;
    readonly NotifyIcon tray;
    bool realClose;
    bool updatingStartupUi;
    bool lastControllerConnected, lastControllerPaused;
    string lastControllerDevice = "";
    CheckBox? normalStartupToggle, priorityStartupToggle;
    WebView2? modernView;
    bool modernReady;
    bool nativeUiBuilt;

    static readonly string[] Actions = { "None", "Left click", "Right click", "Middle click", "Enter", "Escape", "Space", "Up", "Down", "Left", "Right", "Page up", "Page down", "Alt+Tab", "Win+D", "Keyboard", "Volume up", "Volume down", "Mute", "Play/Pause", "Browser back", "Browser forward" };
    sealed record Choice(string Id, string Label) { public override string ToString() => Label; }

    public MainForm(bool startHidden = false)
    {
        Text = "CaaM — Controller as a Mouse"; Width = 900; Height = 690; MinimumSize = new Size(760, 580); BackColor = Bg; ForeColor = Color.White;
        Font = new Font("Segoe UI", 10); StartPosition = FormStartPosition.CenterScreen; DoubleBuffered = true;
        Icon = AppBrand.LoadIcon();
        if (settings.Theme == "Minimal") BuildUi();
        tray = new NotifyIcon { Text = "CaaM — Controller as a Mouse", Icon = AppBrand.LoadIcon(), Visible = true };
        tray.DoubleClick += (_, _) => Restore();
        RefreshTrayMenu();
        FormClosing += OnClosing; Resize += (_, _) => { if (WindowState == FormWindowState.Minimized) HideToTray(); };
        Shown += async (_, _) =>
        {
            if (startHidden) Hide();
            engine = new ControllerEngine(settings);
            engine.StatusChanged += (connected, paused, device) =>
            {
                if (!IsDisposed && IsHandleCreated) BeginInvoke(() => SetStatus(connected, paused, device));
            };
            engine.Start();
            if (settings.Theme != "Minimal" && !startHidden) await InitializeModernUi();
        };
    }

    void BuildUi()
    {
        if (nativeUiBuilt) return;
        nativeUiBuilt = true;
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(30), RowCount = 3, ColumnCount = 1 };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 100)); root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 78)); Controls.Add(root);
        var header = new Panel { Dock = DockStyle.Fill };
        header.Controls.Add(new PictureBox { Image = AppBrand.LoadImage(), SizeMode = PictureBoxSizeMode.Zoom, Size = new Size(54, 54), Location = new Point(0, 1) });
        header.Controls.Add(new Label { Text = "CaaM", Font = new Font("Segoe UI Semibold", 25), AutoSize = true, Location = new Point(68, 2) });
        header.Controls.Add(new Label { Text = Loc.T("tagline"), ForeColor = Muted, AutoSize = true, Location = new Point(71, 52) });
        status.Text = Loc.T("searching"); status.ForeColor = Muted; status.AutoSize = false; status.AutoEllipsis = true; status.TextAlign = ContentAlignment.MiddleRight; status.Height = 30; status.Anchor = AnchorStyles.Top | AnchorStyles.Right; status.Location = new Point(300, 12); header.Controls.Add(status);
        header.Resize += (_, _) => PositionStatus();
        root.Controls.Add(header, 0, 0);

        var tabs = new TabControl { Dock = DockStyle.Fill, Appearance = TabAppearance.Normal, DrawMode = TabDrawMode.OwnerDrawFixed, ItemSize = new Size(150, 38), SizeMode = TabSizeMode.Fixed };
        tabs.DrawItem += DrawTab;
        tabs.TabPages.Add(BuildMappings()); tabs.TabPages.Add(BuildSettings()); tabs.TabPages.Add(BuildAbout()); root.Controls.Add(tabs, 0, 1);
        var footer = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 18, 0, 0) };
        var save = Button(Loc.T("save"), Accent, ThemeManager.Current.AccentText); save.Tag = "primary"; save.Width = 150; save.Dock = DockStyle.Right; save.Click += (_, _) => Save(); footer.Controls.Add(save);
        stickHelpLabel = new Label { Text = Loc.T(settings.SwapSticks ? "stickHelpSwapped" : "stickHelp"), ForeColor = Muted, AutoSize = true, Location = new Point(0, 27) }; footer.Controls.Add(stickHelpLabel); root.Controls.Add(footer, 0, 2);
    }

    void DrawTab(object? sender, DrawItemEventArgs e)
    {
        if (sender is not TabControl tabs) return;
        var selected = e.Index == tabs.SelectedIndex; var rect = e.Bounds;
        using var background = new SolidBrush(selected ? ThemeManager.Current.Surface : ThemeManager.Current.Background);
        e.Graphics.FillRectangle(background, rect);
        using var text = new SolidBrush(selected ? ThemeManager.Current.Foreground : ThemeManager.Current.Muted);
        using var font = new Font("Segoe UI Semibold", 10);
        var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        e.Graphics.DrawString(tabs.TabPages[e.Index].Text, font, text, rect, format);
        if (selected) using (var accent = new SolidBrush(Accent)) e.Graphics.FillRectangle(accent, rect.Left + 18, rect.Bottom - 3, rect.Width - 36, 3);
    }

    void ApplyTheme(Control root)
    {
        root.BackColor = Bg;
        if (root is Label label && label != status) label.ForeColor = label.ForeColor == Color.Transparent ? ThemeManager.Current.Foreground : (label.Font.Size < 10 || label.Margin.Top <= 2 ? Muted : ThemeManager.Current.Foreground);
        if (root is CheckBox check) { check.BackColor = Bg; check.ForeColor = ThemeManager.Current.Foreground; }
        if (root is ComboBox combo) { combo.BackColor = ThemeManager.Current.Input; combo.ForeColor = ThemeManager.Current.Foreground; combo.FlatStyle = FlatStyle.Flat; }
        if (root is TrackBar track) track.BackColor = Bg;
        if (root is Panel panel && Equals(panel.Tag, "card")) { panel.BackColor = Card; ThemeManager.Round(panel, 12); }
        if (root is Button button)
        {
            var primary = Equals(button.Tag, "primary"); button.BackColor = primary ? Accent : Card; button.ForeColor = primary ? ThemeManager.Current.AccentText : ThemeManager.Current.Foreground;
            if (button is ModernButton) ThemeManager.Round(button, 10);
        }
        foreach (Control child in root.Controls) ApplyTheme(child);
        if (root is TabControl tabs) tabs.Invalidate();
        if (root == this) RenderControllerStatus();
    }

    TabPage BuildMappings()
    {
        var page = Page(Loc.T("mappings")); mappings.Dock = DockStyle.Fill; mappings.AutoScroll = true; mappings.WrapContents = true; mappings.Padding = new Padding(15);
        var swap = Toggle(Loc.T("swapSticks"), settings.SwapSticks, value =>
        {
            settings.SwapSticks = value;
            settings.Save();
            if (stickHelpLabel is not null) stickHelpLabel.Text = Loc.T(value ? "stickHelpSwapped" : "stickHelp");
        });
        swap.Width = 520; swap.Height = 58; swap.Margin = new Padding(8); swap.Tag = "card"; mappings.Controls.Add(swap);
        foreach (var key in AppSettings.Defaults().Keys)
        {
            var card = new Panel { Width = 245, Height = 76, BackColor = Card, Margin = new Padding(8), Tag = "card" };
            card.Resize += (_, _) => ThemeManager.Round(card, 12); ThemeManager.Round(card, 12);
            card.Controls.Add(new Label { Name = $"label:{key}", Text = LayoutLabel(key, settings.ControllerLayout == "Auto" ? "Xbox" : settings.ControllerLayout), AutoSize = true, ForeColor = Color.White, Location = new Point(14, 10), Font = new Font(Font, FontStyle.Bold) });
            var combo = new ComboBox { Name = key, DropDownStyle = ComboBoxStyle.DropDownList, Width = 215, Location = new Point(14, 36), BackColor = Color.FromArgb(35, 41, 52), ForeColor = Color.White };
            var choices = Actions.Select(a => new Choice(a, Loc.T(a))).ToArray(); combo.Items.AddRange(choices); combo.SelectedItem = choices.First(c => c.Id == settings.Bindings.GetValueOrDefault(key, "None")); card.Controls.Add(combo); mappings.Controls.Add(card);
        }
        page.Controls.Add(mappings); return page;
    }

    TabPage BuildSettings()
    {
        var page = Page(Loc.T("settings")); var flow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, Padding = new Padding(24), AutoScroll = true, WrapContents = false };
        flow.Controls.Add(Toggle(Loc.T("enabled"), settings.Enabled, v => settings.Enabled = v));
        normalStartupToggle = Toggle(Loc.T("autostart"), settings.StartWithWindows, NormalStartupChanged); flow.Controls.Add(normalStartupToggle);
        priorityStartupToggle = Toggle(Loc.T("priority"), settings.PriorityStartup, PriorityStartupChanged); flow.Controls.Add(priorityStartupToggle);
        flow.Controls.Add(Toggle(Loc.T("gamePause"), settings.PauseInFullscreen, v => settings.PauseInFullscreen = v));
        flow.Controls.Add(WrappingNote(Loc.T("gamePauseHelp"), new Padding(13, 0, 4, 8)));
        flow.Controls.Add(Toggle(Loc.T("keepAlive"), settings.KeepControllerAwake, v => settings.KeepControllerAwake = v));
        flow.Controls.Add(Toggle(Loc.T("psTouchpad"), settings.PlayStationTouchpad, v => settings.PlayStationTouchpad = v));
        flow.Controls.Add(Toggle(Loc.T("autoKeyboard"), settings.AutoKeyboard, v => settings.AutoKeyboard = v));
        flow.Controls.Add(WrappingNote(Loc.T("autoKeyboardHelp"), new Padding(13, 0, 4, 8)));
        flow.Controls.Add(new Label { Text = Loc.T("controllerLayout"), AutoSize = true, Margin = new Padding(4, 12, 4, 2) });
        var layout = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220, Margin = new Padding(4, 2, 4, 8) };
        var layoutChoices = new[] { new Choice("Auto", Loc.T("layoutAuto")), new Choice("Xbox", "Xbox"), new Choice("PlayStation", "PlayStation"), new Choice("Switch", "Nintendo Switch"), new Choice("Generic", Loc.T("layoutGeneric")) };
        layout.Items.AddRange(layoutChoices); layout.SelectedItem = layoutChoices.First(x => x.Id == settings.ControllerLayout);
        layout.SelectedIndexChanged += (_, _) =>
        {
            if (layout.SelectedItem is Choice choice)
            {
                settings.ControllerLayout = choice.Id;
                var effective = choice.Id == "Auto" ? DetectedLayout(lastControllerDevice) : choice.Id;
                ApplyControllerLayout(effective);
                RenderControllerStatus();
            }
        }; flow.Controls.Add(layout);
        flow.Controls.Add(new Label { Text = Loc.T("theme"), AutoSize = true, Margin = new Padding(4, 12, 4, 2) });
        var theme = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220, Margin = new Padding(4, 2, 4, 8), FlatStyle = FlatStyle.Flat };
        var themeChoices = new[] { new Choice("System", Loc.T("themeSystem")), new Choice("Light", Loc.T("themeLight")), new Choice("Dark", Loc.T("themeDark")), new Choice("Minimal", Loc.T("themeMinimal")) };
        theme.Items.AddRange(themeChoices); theme.SelectedItem = themeChoices.First(x => x.Id == settings.Theme);
        theme.SelectedIndexChanged += (_, _) => { if (theme.SelectedItem is Choice choice) { settings.Theme = choice.Id; ThemeManager.Configure(choice.Id); ApplyTheme(this); RefreshTrayMenu(); Invalidate(true); if (choice.Id != "Minimal" && modernView is null) _ = InitializeModernUi(); } }; flow.Controls.Add(theme);
        flow.Controls.Add(new Label { Text = Loc.T("customCss"), AutoSize = true, Margin = new Padding(4, 12, 4, 2) });
        var customCss = new TextBox { Multiline = true, Width = 650, Height = 100, ScrollBars = ScrollBars.Vertical, Text = settings.CustomCss, Font = new Font("Consolas", 9), Margin = new Padding(4, 2, 4, 3) };
        customCss.TextChanged += (_, _) => settings.CustomCss = customCss.Text; flow.Controls.Add(customCss);
        flow.Controls.Add(WrappingNote(Loc.T("customCssHelp"), new Padding(4, 0, 4, 8)));
        var languageLabel = new Label { Text = Loc.T("language"), AutoSize = true, Margin = new Padding(4, 12, 4, 2) }; flow.Controls.Add(languageLabel);
        var language = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220, Margin = new Padding(4, 2, 4, 4) }; language.Items.AddRange(new object[] { "System", "Deutsch", "English" }); language.SelectedItem = settings.Language; language.SelectedIndexChanged += (_, _) => settings.Language = language.SelectedItem?.ToString() ?? "System"; flow.Controls.Add(language);
        flow.Controls.Add(WrappingNote(Loc.T("languageRestart"), new Padding(4, 2, 4, 8)));
        var speed = Slider(Loc.T("mouseSpeed"), 1, 100, settings.MouseSpeed, v => settings.MouseSpeed = v); flow.Controls.Add(speed);
        var scrollSpeed = Slider(Loc.T("scrollSpeed"), 1, 100, settings.ScrollSpeed, v => settings.ScrollSpeed = v); flow.Controls.Add(scrollSpeed);
        flow.Controls.Add(new Label { Text = Loc.T("dataTools"), AutoSize = true, Font = new Font("Segoe UI Semibold", 11), Margin = new Padding(4, 18, 4, 5) });
        flow.Controls.Add(BuildDataTools());
        flow.Controls.Add(new Panel { Width = 1, Height = 34, Margin = new Padding(0) });
        flow.Resize += (_, _) => FitBehaviorControls(flow);
        FitBehaviorControls(flow);
        page.Controls.Add(flow); return page;
    }

    TabPage BuildAbout()
    {
        var page = Page(Loc.T("about"));
        var panel = new Panel { Width = 620, Height = 340, BackColor = Card, Tag = "card" };
        panel.Controls.Add(new PictureBox { Image = AppBrand.LoadImage(), SizeMode = PictureBoxSizeMode.Zoom, Size = new Size(72, 72), Location = new Point(274, 18) });
        panel.Controls.Add(new Label { Text = "CaaM", Font = new Font("Segoe UI Semibold", 22), AutoSize = true, Location = new Point(267, 92) });
        panel.Controls.Add(new Label { Text = $"{Loc.T("version")} {AppVersion}", ForeColor = Muted, AutoSize = true, Location = new Point(260, 132) });
        panel.Controls.Add(new Label { Text = "Anton Jeremias Peters", Font = new Font("Segoe UI Semibold", 12), AutoSize = true, Location = new Point(220, 163) });
        var profile = Button("◉  GitHub", Card, Color.White); profile.SetBounds(58, 205, 155, 42); profile.Click += (_, _) => OpenExternal("https://github.com/antonjeremiaspeters"); panel.Controls.Add(profile);
        var repository = Button("</>  Repository", Card, Color.White); repository.SetBounds(232, 205, 155, 42); repository.Click += (_, _) => OpenExternal("https://github.com/antonjeremiaspeters/caam"); panel.Controls.Add(repository);
        var support = Button("☕  Buy me a coffee", Card, Color.White); support.SetBounds(406, 205, 155, 42); support.Click += (_, _) => OpenExternal("https://www.paypal.com/paypalme/antonjeremiaspeters"); panel.Controls.Add(support);
        panel.Controls.Add(new Label { Text = "© 2026 Anton Jeremias Peters. All rights reserved.", ForeColor = Muted, AutoSize = true, Location = new Point(157, 294) });
        panel.Resize += (_, _) => ThemeManager.Round(panel, 14); ThemeManager.Round(panel, 14);
        page.Controls.Add(panel);
        page.Resize += (_, _) => { panel.Left = Math.Max(10, (page.ClientSize.Width - panel.Width) / 2); panel.Top = Math.Max(10, (page.ClientSize.Height - panel.Height) / 2); };
        return page;
    }

    static TabPage Page(string title) => new(title) { BackColor = Bg, ForeColor = Color.White, Padding = new Padding(8) };
    static Button Button(string text, Color bg, Color fg) => new ModernButton { Text = text, BackColor = bg, ForeColor = fg, FlatStyle = FlatStyle.Flat, Height = 40, Font = new Font("Segoe UI Semibold", 10), Cursor = Cursors.Hand, UseVisualStyleBackColor = false };
    static CheckBox Toggle(string text, bool value, Action<bool> changed)
    {
        var c = new CheckBox { Text = text, Checked = value, AutoSize = false, Width = 650, Height = 42, ForeColor = Color.White, Font = new Font("Segoe UI", 11), Margin = new Padding(4, 5, 4, 5), Padding = new Padding(5), TextAlign = ContentAlignment.MiddleLeft };
        c.CheckedChanged += (_, _) => changed(c.Checked); return c;
    }
    static Label WrappingNote(string text, Padding margin) => new() { Text = text, ForeColor = Muted, AutoSize = true, MaximumSize = new Size(650, 0), Margin = margin };
    static void FitBehaviorControls(FlowLayoutPanel flow)
    {
        var width = Math.Max(260, flow.ClientSize.Width - flow.Padding.Horizontal - SystemInformation.VerticalScrollBarWidth - 12);
        foreach (Control control in flow.Controls)
        {
            if (control is CheckBox) control.Width = width;
            else if (control is Label label && label.MaximumSize.Width > 0) label.MaximumSize = new Size(width, 0);
            else if (control is Panel panel)
            {
                panel.Width = width;
                foreach (Control child in panel.Controls) if (child is TrackBar track) track.Width = Math.Max(180, width - 20);
            }
        }
    }

    void NormalStartupChanged(bool enabled)
    {
        if (updatingStartupUi) return;
        settings.StartWithWindows = enabled;
        if (!enabled && settings.PriorityStartup)
        {
            if (!StartupManager.SetPriorityStartup(false))
            {
                updatingStartupUi = true; normalStartupToggle!.Checked = true; updatingStartupUi = false; settings.StartWithWindows = true; return;
            }
            settings.PriorityStartup = false;
            updatingStartupUi = true; priorityStartupToggle!.Checked = false; updatingStartupUi = false;
        }
        SetAutostart(enabled && !settings.PriorityStartup);
    }

    void PriorityStartupChanged(bool enabled)
    {
        if (updatingStartupUi) return;
        if (!StartupManager.SetPriorityStartup(enabled))
        {
            updatingStartupUi = true; priorityStartupToggle!.Checked = !enabled; updatingStartupUi = false; return;
        }
        settings.PriorityStartup = enabled;
        if (enabled)
        {
            settings.StartWithWindows = true;
            SetAutostart(false);
            updatingStartupUi = true; normalStartupToggle!.Checked = true; updatingStartupUi = false;
        }
        else SetAutostart(settings.StartWithWindows);
        settings.Save();
    }
    static Control Slider(string text, int min, int max, int value, Action<int> changed)
    {
        var p = new Panel { Width = 650, Height = 78, Margin = new Padding(4, 14, 4, 4) }; var l = new Label { Text = $"{text}: {value}", AutoSize = true, Location = new Point(4, 0) };
        var t = new TrackBar { Minimum = min, Maximum = max, Value = value, TickStyle = TickStyle.None, Width = 520, Location = new Point(0, 27) }; t.ValueChanged += (_, _) => { l.Text = $"{text}: {t.Value}"; changed(t.Value); }; p.Controls.Add(l); p.Controls.Add(t); return p;
    }
    Control BuildDataTools()
    {
        var grid = new TableLayoutPanel { Width = 650, Height = 104, ColumnCount = 2, RowCount = 2, Margin = new Padding(4, 0, 4, 14) };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 50)); grid.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        var resetSettings = Button(Loc.T("resetSettings"), Card, Color.White); resetSettings.Dock = DockStyle.Fill; resetSettings.Margin = new Padding(3); resetSettings.Click += (_, _) => ResetSettings();
        var resetBindings = Button(Loc.T("resetBindings"), Card, Color.White); resetBindings.Dock = DockStyle.Fill; resetBindings.Margin = new Padding(3); resetBindings.Click += (_, _) => ResetButtonMappings();
        var backup = Button(Loc.T("createBackup"), Card, Color.White); backup.Dock = DockStyle.Fill; backup.Margin = new Padding(3); backup.Click += (_, _) => CreateBackup();
        var restore = Button(Loc.T("restoreBackup"), Card, Color.White); restore.Dock = DockStyle.Fill; restore.Margin = new Padding(3); restore.Click += (_, _) => RestoreBackup();
        grid.Controls.Add(resetSettings, 0, 0); grid.Controls.Add(resetBindings, 1, 0); grid.Controls.Add(backup, 0, 1); grid.Controls.Add(restore, 1, 1);
        return grid;
    }

    void ResetSettings()
    {
        if (MessageBox.Show(this, Loc.T("confirmSettings"), Loc.T("confirmTitle"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        var bindings = new Dictionary<string, string>(settings.Bindings);
        if (settings.PriorityStartup) StartupManager.SetPriorityStartup(false);
        SetAutostart(false); settings = new AppSettings { Bindings = bindings }; settings.Save(); RestartApp();
    }

    void ResetButtonMappings()
    {
        if (MessageBox.Show(this, Loc.T("confirmBindings"), Loc.T("confirmTitle"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        settings.Bindings = AppSettings.Defaults(); settings.Save(); RestartApp();
    }

    void CreateBackup(bool syncNativeUi = true)
    {
        if (syncNativeUi) Save(); else settings.Save();
        using var dialog = new SaveFileDialog { Filter = "CaaM Backup (*.caam.json)|*.caam.json|JSON (*.json)|*.json", FileName = $"caam-backup-{DateTime.Now:yyyy-MM-dd}.caam.json", AddExtension = true, OverwritePrompt = true };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        settings.Export(dialog.FileName); MessageBox.Show(this, Loc.T("backupSaved"), "CaaM", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    void RestoreBackup()
    {
        using var dialog = new OpenFileDialog { Filter = "CaaM Backup (*.caam.json;*.tvcontroller.json;*.json)|*.caam.json;*.tvcontroller.json;*.json|All files (*.*)|*.*", CheckFileExists = true };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        if (!AppSettings.TryImport(dialog.FileName, out var imported)) { MessageBox.Show(this, Loc.T("backupInvalid"), "CaaM", MessageBoxButtons.OK, MessageBoxIcon.Error); return; }
        if (settings.PriorityStartup && !imported.PriorityStartup) StartupManager.SetPriorityStartup(false);
        settings = imported;
        if (settings.PriorityStartup && !StartupManager.SetPriorityStartup(true)) settings.PriorityStartup = false;
        SetAutostart(settings.StartWithWindows && !settings.PriorityStartup); settings.Save(); RestartApp();
    }

    void RestartApp()
    {
        MessageBox.Show(this, Loc.T("restartInfo"), "CaaM", MessageBoxButtons.OK, MessageBoxIcon.Information);
        Process.Start(new ProcessStartInfo(Application.ExecutablePath, $"--restart-from={Environment.ProcessId}") { UseShellExecute = true });
        realClose = true; Close();
    }
    void PositionStatus() { if (status.Parent is not null) { status.Width = Math.Max(260, Math.Min(540, status.Parent.ClientSize.Width - 220)); status.Left = Math.Max(8, status.Parent.ClientSize.Width - status.Width - 12); } }
    void SetStatus(bool connected, bool paused, string device)
    {
        var controllerChanged = connected != lastControllerConnected || !string.Equals(device, lastControllerDevice, StringComparison.Ordinal);
        lastControllerConnected = connected; lastControllerPaused = paused; lastControllerDevice = device;
        if (settings.ControllerLayout == "Auto" && controllerChanged) ApplyControllerLayout(DetectedLayout(device));
        RenderControllerStatus();
        if (modernReady && settings.ControllerLayout == "Auto" && controllerChanged) SendModernState(); else SendModernStatus();
    }
    void RenderControllerStatus()
    {
        var device = lastControllerDevice;
        if (lastControllerConnected && settings.ControllerLayout != "Auto")
        {
            var separator = device.LastIndexOf(" · ", StringComparison.Ordinal);
            var hardwareName = separator >= 0 ? device[..separator] : device;
            device = $"{hardwareName} · {DisplayLayout(settings.ControllerLayout)}";
        }
        status.Text = lastControllerPaused ? $"{Loc.T("paused")} · {device}" : lastControllerConnected ? $"{Loc.T("connected")} · {device}" : Loc.T("disconnected");
        status.ForeColor = lastControllerPaused ? Color.FromArgb(251,191,36) : lastControllerConnected ? Accent : Muted; status.Tag = status.Text; PositionStatus();
    }
    static string DetectedLayout(string device)
    {
        if (ContainsAny(device, "PlayStation", "DualSense", "DualShock", "PS4 Controller", "PS5 Controller")) return "PlayStation";
        if (ContainsAny(device, "Nintendo", "Switch", "Joy-Con", "JoyCon")) return "Switch";
        if (ContainsAny(device, "Xbox", "XInput", "X-Box", "XInput Controller")) return "Xbox";
        return "Generic";
    }
    static bool ContainsAny(string value, params string[] terms) => terms.Any(term => value.Contains(term, StringComparison.OrdinalIgnoreCase));
    static string DisplayLayout(string layout) => layout == "Switch" ? "Nintendo Switch" : layout == "Generic" ? Loc.T("layoutGeneric") : layout;
    void ApplyControllerLayout(string layout)
    {
        foreach (Panel card in mappings.Controls)
            foreach (Control control in card.Controls)
                if (control is Label label && label.Name.StartsWith("label:", StringComparison.Ordinal)) label.Text = LayoutLabel(label.Name[6..], layout);
    }
    static string LayoutLabel(string key, string layout) => (layout, key) switch
    {
        ("PlayStation", "A") => "✕  Cross", ("PlayStation", "B") => "○  Circle", ("PlayStation", "X") => "□  Square", ("PlayStation", "Y") => "△  Triangle",
        ("PlayStation", "LB") => "L1", ("PlayStation", "RB") => "R1", ("PlayStation", "LT") => "L2", ("PlayStation", "RT") => "R2",
        ("PlayStation", "View") => "Create / Share", ("PlayStation", "Menu") => "Options", ("PlayStation", "Left stick") => "L3", ("PlayStation", "Right stick") => "R3",
        ("Switch", "A") => "B", ("Switch", "B") => "A", ("Switch", "X") => "Y", ("Switch", "Y") => "X",
        ("Switch", "LB") => "L", ("Switch", "RB") => "R", ("Switch", "LT") => "ZL", ("Switch", "RT") => "ZR",
        ("Switch", "View") => "−  Minus", ("Switch", "Menu") => "+  Plus", ("Switch", "Left stick") => "Left Stick", ("Switch", "Right stick") => "Right Stick",
        ("Generic", "A") => "Button 1", ("Generic", "B") => "Button 2", ("Generic", "X") => "Button 3", ("Generic", "Y") => "Button 4",
        ("Generic", "LB") => "Left Shoulder", ("Generic", "RB") => "Right Shoulder", ("Generic", "LT") => "Left Trigger", ("Generic", "RT") => "Right Trigger",
        (_, "LT") => "LT", (_, "RT") => "RT", _ => Loc.T(key)
    };
    void Save()
    {
        foreach (Panel card in mappings.Controls) foreach (Control c in card.Controls) if (c is ComboBox combo && combo.SelectedItem is Choice action) settings.Bindings[combo.Name] = action.Id;
        settings.Save(); SetAutostart(settings.StartWithWindows && !settings.PriorityStartup); Text = Loc.T("saved"); Task.Delay(1200).ContinueWith(_ => BeginInvoke(() => Text = "CaaM — Controller as a Mouse"));
    }

    async Task InitializeModernUi()
    {
        if (modernView is not null || settings.Theme == "Minimal") return;
        try
        {
            modernView = new WebView2 { Dock = DockStyle.Fill, DefaultBackgroundColor = Bg };
            Controls.Add(modernView); modernView.BringToFront();
            var webData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CaaM", "WebView2");
            var webEnvironment = await CoreWebView2Environment.CreateAsync(userDataFolder: webData);
            await modernView.EnsureCoreWebView2Async(webEnvironment);
            modernView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            modernView.CoreWebView2.Settings.AreDevToolsEnabled = false;
            modernView.CoreWebView2.Settings.IsStatusBarEnabled = false;
            modernView.CoreWebView2.WebMessageReceived += ModernMessageReceived;
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("CaaM.ModernShell") ?? throw new InvalidOperationException("Modern UI resource missing.");
            using var reader = new StreamReader(stream);
            var html = (await reader.ReadToEndAsync()).Replace("__CAAM_LOGO__", AppBrand.DataUrl, StringComparison.Ordinal);
            modernView.NavigateToString(html);
        }
        catch
        {
            modernView?.Dispose(); modernView = null; modernReady = false;
            BuildUi(); ApplyTheme(this);
        }
    }

    void ModernMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            using var message = JsonDocument.Parse(e.WebMessageAsJson);
            var root = message.RootElement;
            switch (root.GetProperty("type").GetString())
            {
                case "ready": modernReady = true; SendModernState(); break;
                case "save": SaveModern(root); break;
                case "resetSettings": ResetSettings(); break;
                case "resetBindings": ResetButtonMappings(); break;
                case "backup": CreateBackup(false); break;
                case "restore": RestoreBackup(); break;
                case "swapSticks":
                    settings.SwapSticks = root.GetProperty("value").GetBoolean();
                    settings.Save();
                    SendModernState();
                    break;
                case "openUrl": OpenExternal(root.GetProperty("url").GetString() ?? ""); break;
            }
        }
        catch { }
    }

    void SaveModern(JsonElement root)
    {
        var values = root.GetProperty("values");
        var previousPriority = settings.PriorityStartup;
        settings.Enabled = values.GetProperty("Enabled").GetBoolean();
        settings.StartWithWindows = values.GetProperty("StartWithWindows").GetBoolean();
        settings.PriorityStartup = values.GetProperty("PriorityStartup").GetBoolean();
        settings.PauseInFullscreen = values.GetProperty("PauseInFullscreen").GetBoolean();
        settings.KeepControllerAwake = values.GetProperty("KeepControllerAwake").GetBoolean();
        settings.PlayStationTouchpad = values.GetProperty("PlayStationTouchpad").GetBoolean();
        settings.AutoKeyboard = values.GetProperty("AutoKeyboard").GetBoolean();
        settings.SwapSticks = values.GetProperty("SwapSticks").GetBoolean();
        settings.ControllerLayout = values.GetProperty("ControllerLayout").GetString() ?? "Auto";
        settings.Theme = values.GetProperty("Theme").GetString() ?? "System";
        settings.CustomCss = values.GetProperty("CustomCss").GetString() ?? "";
        settings.Language = values.GetProperty("Language").GetString() ?? "System";
        settings.MouseSpeed = values.GetProperty("MouseSpeed").GetInt32();
        settings.ScrollSpeed = values.GetProperty("ScrollSpeed").GetInt32();
        foreach (var binding in root.GetProperty("bindings").EnumerateObject()) settings.Bindings[binding.Name] = binding.Value.GetString() ?? "None";

        if (settings.PriorityStartup != previousPriority && !StartupManager.SetPriorityStartup(settings.PriorityStartup)) settings.PriorityStartup = previousPriority;
        if (settings.PriorityStartup) settings.StartWithWindows = true;
        SetAutostart(settings.StartWithWindows && !settings.PriorityStartup);
        settings.Save();
        if (settings.Theme == "Minimal") { RestartApp(); return; }
        ThemeManager.Configure(settings.Theme); RefreshTrayMenu();
        var effective = settings.ControllerLayout == "Auto" ? DetectedLayout(lastControllerDevice) : settings.ControllerLayout;
        ApplyControllerLayout(effective); RenderControllerStatus(); SendModernState();
        PostModern(new { type = "saved", text = Loc.T("saved") });
    }

    void SendModernState()
    {
        if (!modernReady) return;
        var effectiveLayout = settings.ControllerLayout == "Auto" ? DetectedLayout(lastControllerDevice) : settings.ControllerLayout;
        var state = new
        {
            theme = settings.Theme,
            customCss = settings.CustomCss, customCssLabel = Loc.T("customCss"), customCssHelp = Loc.T("customCssHelp"),
            tagline = Loc.T("tagline"), mappingTitle = Loc.T("mappings"), settingsTitle = Loc.T("settings"), aboutTitle = Loc.T("about"), versionLabel = Loc.T("version"), version = AppVersion,
            stickHelp = Loc.T(settings.SwapSticks ? "stickHelpSwapped" : "stickHelp"), save = Loc.T("save"), behaviorGroup = Loc.T("behavior"),
            swapSticks = new { key="SwapSticks", label=Loc.T("swapSticks"), help=Loc.T("swapSticksHelp"), value=settings.SwapSticks },
            interfaceGroup = Loc.T("theme"), dataTools = Loc.T("dataTools"), status = ModernStatus(),
            actions = Actions.Select(x => new { id = x, label = Loc.T(x) }),
            mappings = AppSettings.Defaults().Keys.Select(key => new { key, label = LayoutLabel(key, effectiveLayout), action = settings.Bindings.GetValueOrDefault(key, "None") }),
            toggles = new object[]
            {
                new { key="Enabled", label=Loc.T("enabled"), value=settings.Enabled, help="" },
                new { key="StartWithWindows", label=Loc.T("autostart"), value=settings.StartWithWindows, help="" },
                new { key="PriorityStartup", label=Loc.T("priority"), value=settings.PriorityStartup, help="" },
                new { key="PauseInFullscreen", label=Loc.T("gamePause"), value=settings.PauseInFullscreen, help=Loc.T("gamePauseHelp") },
                new { key="KeepControllerAwake", label=Loc.T("keepAlive"), value=settings.KeepControllerAwake, help="" },
                new { key="PlayStationTouchpad", label=Loc.T("psTouchpad"), value=settings.PlayStationTouchpad, help="" },
                new { key="AutoKeyboard", label=Loc.T("autoKeyboard"), value=settings.AutoKeyboard, help=Loc.T("autoKeyboardHelp") }
            },
            selects = new object[]
            {
                new { key="ControllerLayout", label=Loc.T("controllerLayout"), value=settings.ControllerLayout, help="", choices=new[]{new{id="Auto",label=Loc.T("layoutAuto")},new{id="Xbox",label="Xbox"},new{id="PlayStation",label="PlayStation"},new{id="Switch",label="Nintendo Switch"},new{id="Generic",label=Loc.T("layoutGeneric")} } },
                new { key="Theme", label=Loc.T("theme"), value=settings.Theme, help="", choices=new[]{new{id="System",label=Loc.T("themeSystem")},new{id="Light",label=Loc.T("themeLight")},new{id="Dark",label=Loc.T("themeDark")},new{id="Minimal",label=Loc.T("themeMinimal")} } },
                new { key="Language", label=Loc.T("language"), value=settings.Language, help=Loc.T("languageRestart"), choices=new[]{new{id="System",label="System"},new{id="Deutsch",label="Deutsch"},new{id="English",label="English"} } }
            },
            sliders = new[] { new { key="MouseSpeed", label=Loc.T("mouseSpeed"), value=settings.MouseSpeed }, new { key="ScrollSpeed", label=Loc.T("scrollSpeed"), value=settings.ScrollSpeed } },
            tools = new[] { new { command="resetSettings",label=Loc.T("resetSettings") },new { command="resetBindings",label=Loc.T("resetBindings") },new { command="backup",label=Loc.T("createBackup") },new { command="restore",label=Loc.T("restoreBackup") } }
        };
        PostModern(new { type = "state", state });
    }

    object ModernStatus() => new { text = CleanModernStatus(string.IsNullOrWhiteSpace(status.Text) ? Loc.T("searching") : status.Text), kind = lastControllerPaused ? "native" : lastControllerConnected ? "connected" : "" };
    void SendModernStatus() { if (modernReady) PostModern(new { type = "status", text = CleanModernStatus(status.Text), kind = lastControllerPaused ? "native" : lastControllerConnected ? "connected" : "" }); }
    static string CleanModernStatus(string value) => value.TrimStart().TrimStart('●').TrimStart();
    void PostModern(object value)
    {
        try { modernView?.CoreWebView2?.PostWebMessageAsJson(JsonSerializer.Serialize(value)); } catch { }
    }

    static string AppVersion => typeof(MainForm).Assembly.GetName().Version?.ToString(3) ?? "0.28.0";
    static void OpenExternal(string url)
    {
        if (url is not ("https://github.com/antonjeremiaspeters" or "https://github.com/antonjeremiaspeters/caam" or "https://www.paypal.com/paypalme/antonjeremiaspeters")) return;
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch { }
    }

    void RefreshTrayMenu()
    {
        var old = tray.ContextMenuStrip;
        var menu = new ContextMenuStrip
        {
            BackColor = ThemeManager.Current.Surface,
            ForeColor = ThemeManager.Current.Foreground,
            Font = new Font("Segoe UI Variable Text", 10),
            Padding = new Padding(6),
            ShowImageMargin = false,
            ShowCheckMargin = false,
            Renderer = new ModernTrayRenderer(ThemeManager.Current)
        };
        var open = menu.Items.Add(Loc.T("open")); open.Padding = new Padding(12, 7, 52, 7); open.Font = new Font(menu.Font, FontStyle.Bold); open.Click += (_, _) => Restore();
        menu.Items.Add(new ToolStripSeparator());
        var exit = menu.Items.Add(Loc.T("exit")); exit.Padding = new Padding(12, 7, 52, 7); exit.Click += (_, _) => { realClose = true; Close(); };
        tray.ContextMenuStrip = menu;
        old?.Dispose();
    }

    static void SetAutostart(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
            key?.DeleteValue("TVController", false);
            if (enabled) key?.SetValue("CaaM", $"\"{Application.ExecutablePath}\" --background"); else key?.DeleteValue("CaaM", false);
        }
        catch { }
    }
    void SuspendModernUi()
    {
        if (modernView is null) return;
        try { modernView.CoreWebView2.WebMessageReceived -= ModernMessageReceived; } catch { }
        Controls.Remove(modernView); modernView.Dispose(); modernView = null; modernReady = false;
    }
    void HideToTray() { Hide(); SuspendModernUi(); }
    async void Restore()
    {
        Show(); WindowState = FormWindowState.Normal; Activate();
        if (settings.Theme != "Minimal" && modernView is null) await InitializeModernUi();
    }
    void OnClosing(object? sender, FormClosingEventArgs e) { if (!realClose) { e.Cancel = true; HideToTray(); settings.Save(); } else { SuspendModernUi(); engine?.Dispose(); tray.Dispose(); } }
}
