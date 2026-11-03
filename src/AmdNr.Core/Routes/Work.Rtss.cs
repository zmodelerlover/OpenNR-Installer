// RivaTuner Statistics Server, said before an install: it hooks every game it sees, and with NR on that showed as
// stutter in Cities: Skylines II (2026-11) and held frame generation's presents back elsewhere. OptiScaler's menu says
// the same in game once it finds RTSSHooks64.dll loaded.

using System.Diagnostics;
using Microsoft.Win32;

namespace AmdNr.Core;

public static partial class Work
{
    internal const string RtssRunning =
        "RivaTuner Statistics Server (RTSS, also started by MSI Afterburner) is running. It hooks into every game, and with "
        + "NR on that shows as stutter (Cities: Skylines II) and a lower frame rate with frame generation. Close it before "
        + "playing, or add this game to RTSS with Application detection level set to None.";

    internal const string RtssInstalled =
        "RivaTuner Statistics Server (RTSS, comes with MSI Afterburner) is installed. When it runs it hooks into every game, "
        + "which with NR on shows as stutter: close it before playing, or set this game's Application detection level to "
        + "None in RTSS.";

    /// <summary>Warns while RTSS runs and notes it when it is only installed; nothing when neither. The two
    /// overrides are for tests, and AMDNR_NO_RTSS keeps the test run's own machine out of every other report.</summary>
    internal static void CheckRtss(Report report, bool? running = null, bool? installed = null)
    {
        if (running is null && installed is null && Environment.GetEnvironmentVariable("AMDNR_NO_RTSS") is { Length: > 0 }) return;
        if (running ?? RtssProcessRunning()) report.Warn(RtssRunning);
        else if (installed ?? RtssInstalledHere()) report.Info(RtssInstalled);
    }

    private static bool RtssProcessRunning()
    {
        try
        {
            var found = Process.GetProcessesByName("RTSS");
            foreach (var p in found) p.Dispose();
            return found.Length > 0;
        }
        catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception) { return false; }
    }

    private static bool RtssInstalledHere()
    {
        try
        {
            using var machine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32);
            using var key = machine.OpenSubKey(@"SOFTWARE\Unwinder\RTSS");
            if (key?.GetValue("InstallDir") is string dir && File.Exists(Path.Combine(dir, "RTSS.exe"))) return true;
        }
        catch (Exception e) when (e is System.Security.SecurityException or UnauthorizedAccessException or IOException) { }
        var x86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        return x86.Length > 0 && File.Exists(Path.Combine(x86, "RivaTuner Statistics Server", "RTSS.exe"));
    }
}
