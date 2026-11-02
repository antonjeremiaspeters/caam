using Microsoft.Win32;
using System.Drawing.Drawing2D;

namespace TVController;

internal sealed record ThemePalette(Color Background, Color Surface, Color Input, Color Foreground, Color Muted, Color Accent, Color AccentText, bool Minimal);

internal static class ThemeManager
{
    internal static ThemePalette Current { get; private set; } = Dark();

    internal static void Configure(string selection)
    {
        var effective = selection;
        if (selection == "System")
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                effective = Convert.ToInt32(key?.GetValue("AppsUseLightTheme", 0)) != 0 ? "Light" : "Dark";
            }
            catch { effective = "Dark"; }
        }
        Current = effective switch
        {
            "Light" => new(Color.FromArgb(246, 248, 252), Color.White, Color.FromArgb(237, 241, 247), Color.FromArgb(20, 28, 43), Color.FromArgb(94, 107, 128), Color.FromArgb(37, 99, 235), Color.White, false),
            "Minimal" => new(Color.FromArgb(14, 17, 23), Color.FromArgb(24, 29, 38), Color.FromArgb(35, 41, 52), Color.White, Color.FromArgb(148, 163, 184), Color.FromArgb(94, 234, 212), Color.FromArgb(8, 20, 22), true),
            _ => Dark()
        };
    }

    static ThemePalette Dark() => new(Color.FromArgb(10, 14, 22), Color.FromArgb(20, 27, 39), Color.FromArgb(31, 41, 55), Color.FromArgb(241, 245, 249), Color.FromArgb(148, 163, 184), Color.FromArgb(94, 234, 212), Color.FromArgb(8, 20, 22), false);

    internal static void Round(Control control, int radius = 12)
    {
        var path = new GraphicsPath();
        var r = Math.Min(radius, Math.Min(control.Width, control.Height) / 2); var d = r * 2;
        path.AddArc(0, 0, d, d, 180, 90); path.AddArc(control.Width - d, 0, d, d, 270, 90);
        path.AddArc(control.Width - d, control.Height - d, d, d, 0, 90); path.AddArc(0, control.Height - d, d, d, 90, 90); path.CloseFigure();
        control.Region?.Dispose(); control.Region = new Region(path); path.Dispose();
    }
}

internal sealed class ModernButton : Button
{
    protected override void OnResize(EventArgs e) { base.OnResize(e); ThemeManager.Round(this, 10); }
}

internal sealed class ModernTrayRenderer : ToolStripProfessionalRenderer
{
    readonly ThemePalette palette;
    internal ModernTrayRenderer(ThemePalette palette) : base(new ModernTrayColors(palette))
    {
        this.palette = palette;
        RoundedEdges = true;
    }

    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
    {
        using var pen = new Pen(Color.FromArgb(90, palette.Muted));
        e.Graphics.DrawRectangle(pen, 0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
    }
}

internal sealed class ModernTrayColors : ProfessionalColorTable
{
    readonly ThemePalette p;
    internal ModernTrayColors(ThemePalette palette) { p = palette; UseSystemColors = false; }
    public override Color ToolStripDropDownBackground => p.Surface;
    public override Color MenuItemSelected => p.Input;
    public override Color MenuItemBorder => p.Accent;
    public override Color MenuBorder => p.Muted;
    public override Color SeparatorDark => Color.FromArgb(80, p.Muted);
    public override Color SeparatorLight => p.Surface;
    public override Color ImageMarginGradientBegin => p.Surface;
    public override Color ImageMarginGradientMiddle => p.Surface;
    public override Color ImageMarginGradientEnd => p.Surface;
}
