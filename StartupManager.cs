using System.Diagnostics;
using System.Security;
using System.Security.Principal;

namespace TVController;

internal static class StartupManager
{
    const string TaskName = "CaaM Priority Startup";

    internal static bool SetPriorityStartup(bool enabled)
    {
        try
        {
            var arguments = enabled ? CreateTaskArguments() : $"/Delete /TN \"{TaskName}\" /F";
            var process = Process.Start(new ProcessStartInfo("schtasks.exe", arguments)
            {
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Hidden
            });
            if (process is null) return false;
            process.WaitForExit();
            return process.ExitCode == 0;
        }
        catch { return false; }
    }

    static string CreateTaskArguments()
    {
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CaaM");
        Directory.CreateDirectory(folder);
        var xmlPath = Path.Combine(folder, "priority-startup-task.xml");
        var sid = WindowsIdentity.GetCurrent().User?.Value ?? throw new InvalidOperationException("Benutzer-ID nicht verfügbar.");
        var exe = SecurityElement.Escape(Application.ExecutablePath);
        var user = SecurityElement.Escape(sid);
        var xml = $"""
<?xml version="1.0" encoding="UTF-16"?>
<Task version="1.4" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">
  <RegistrationInfo><Description>Startet CaaM früh und startet die App nach einem Absturz erneut.</Description></RegistrationInfo>
  <Triggers><LogonTrigger><Enabled>true</Enabled><UserId>{user}</UserId></LogonTrigger></Triggers>
  <Principals><Principal id="Author"><UserId>{user}</UserId><LogonType>InteractiveToken</LogonType><RunLevel>HighestAvailable</RunLevel></Principal></Principals>
  <Settings>
    <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
    <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
    <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
    <AllowHardTerminate>true</AllowHardTerminate>
    <StartWhenAvailable>true</StartWhenAvailable>
    <RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable>
    <IdleSettings><StopOnIdleEnd>false</StopOnIdleEnd><RestartOnIdle>false</RestartOnIdle></IdleSettings>
    <AllowStartOnDemand>true</AllowStartOnDemand>
    <Enabled>true</Enabled>
    <Hidden>false</Hidden>
    <RunOnlyIfIdle>false</RunOnlyIfIdle>
    <WakeToRun>false</WakeToRun>
    <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
    <Priority>4</Priority>
    <RestartOnFailure><Interval>PT10S</Interval><Count>5</Count></RestartOnFailure>
  </Settings>
  <Actions Context="Author"><Exec><Command>{exe}</Command><Arguments>--background</Arguments></Exec></Actions>
</Task>
""";
        File.WriteAllText(xmlPath, xml, System.Text.Encoding.Unicode);
        return $"/Create /TN \"{TaskName}\" /XML \"{xmlPath}\" /F";
    }
}
