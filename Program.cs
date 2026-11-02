namespace TVController;

internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        var restartArg = args.FirstOrDefault(a => a.StartsWith("--restart-from=", StringComparison.OrdinalIgnoreCase));
        if (restartArg is not null && int.TryParse(restartArg.AsSpan(restartArg.IndexOf('=') + 1), out var oldPid))
        {
            try { System.Diagnostics.Process.GetProcessById(oldPid).WaitForExit(5000); } catch { }
        }
        using var singleInstance = new Mutex(true, "CaaM.SingleInstance", out var first);
        if (!first) return;
        ApplicationConfiguration.Initialize();
        var startupSettings = AppSettings.Load();
        Loc.Configure(startupSettings.Language);
        ThemeManager.Configure(startupSettings.Theme);
        Application.Run(new MainForm(args.Contains("--background", StringComparer.OrdinalIgnoreCase)));
    }
}
