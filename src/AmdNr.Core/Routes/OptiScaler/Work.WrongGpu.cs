// WrongGPU, phantoslayer21's DLSS 4.5 super resolution (Model M) for RX 9000, as an FFX upscaler provider of
// OptiScaler's: closed source, redistributed here with the author's written permission. Only when ticked, and only in
// a 64-bit game. It goes in with OptiScaler, through the same transaction: its DLLs, its ASI plugin in plugins\, its
// ini, and AMD's FSR 4 from OptiScaler's own folder as the DLL it compares against.
//
// Its model is NVIDIA's, so it is never shipped: WrongGPU's own setup converts it into
// %LOCALAPPDATA%\WrongGPU\model\model.wgcm from a DLSS 310.5 or newer it finds on the PC. The setup is run silently in
// a folder of its own with a stand-in game (and, when the PC has no such DLSS, NVIDIA's 310.9.1 from NVIDIA's GitHub
// beside it), then uninstalled from there; nothing of it touches the game. The setup reuses a model it made before.
// OptiScaler.ini then names WrongGPU as the FFX upscaler (FfxDx12SRPath) and loads the plugin (LoadAsiPlugins), with
// every other setting left as it was.

using System.Diagnostics;

namespace AmdNr.Core;

public static partial class Work
{
    private const string WrongGpuPayload = "wronggpu/";
    internal const string WrongGpuFfx = "wronggpu_ffx_dx12.dll";
    internal const string WrongGpuIni = "wronggpu.ini";
    private const string WrongGpuAsi = "plugins/WrongGPU.asi";
    private const string WrongGpuCompare = "wronggpu-amd-compare.dll";
    private const string WrongGpuSetupPrefix = "WrongGPU-Setup-";
    private const string DlssName = "nvngx_dlss.dll";

    /// <summary>The FSR 4 OptiScaler ships, which WrongGPU's Home key compares against: the very file its setup copies.</summary>
    private const string OptiFsr4 = "OptiScaler/amd_fidelityfx_upscaler_dx12.dll";

    /// <summary>Everything of WrongGPU's in a game folder.</summary>
    internal static readonly string[] WrongGpuNames =
        [WrongGpuFfx, "wronggpu_runtime.dll", WrongGpuCompare, WrongGpuAsi, WrongGpuIni, "wronggpu-license.txt", "wronggpu-notices.txt"];

    /// <summary>What WrongGPU writes beside the game while it runs.</summary>
    private static readonly string[] WrongGpuLogs = ["wronggpu-asi.log", "wronggpu-fsr.log"];

    /// <summary>Model M lives in DLSS from 310.5 on.</summary>
    private static readonly Version WrongGpuDlssFloor = new(310, 5);

    /// <summary>What a fresh wronggpu.ini says: the network upscaler, Model M, on Direct3D 12 (0.5.5's fast path).</summary>
    internal const string WrongGpuIniText =
        "[fsr]\r\nupscaler=network\r\nruntime_dll=wronggpu_runtime.dll\r\nruntime_options=model=M;executor=dx12\r\n";

    /// <summary>Where WrongGPU keeps the model it converted. A test points it elsewhere.</summary>
    internal static string WrongGpuModel { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WrongGPU", "model", "model.wgcm");

    /// <summary>Runs WrongGPU's setup with these arguments and gives its exit code. A test replaces it.</summary>
    internal static Func<string, string[], int> RunWrongGpuSetup { get; set; } = (exe, arguments) =>
    {
        var start = new ProcessStartInfo(exe) { UseShellExecute = false, CreateNoWindow = true };
        foreach (var a in arguments) start.ArgumentList.Add(a);
        using var process = Process.Start(start) ?? throw new InstallException($"{Path.GetFileName(exe)} did not start.");
        if (!process.WaitForExit(TimeSpan.FromMinutes(5)))
        {
            try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
            throw new InstallException($"{Path.GetFileName(exe)} did not finish in five minutes.");
        }
        return process.ExitCode;
    };

    /// <summary>Where a WrongGPU payload file goes in the game: the plugin in plugins\, the rest beside the game, the
    /// setup and NVIDIA's DLSS nowhere.</summary>
    internal static string? WrongGpuDestination(string payloadPath)
    {
        if (!payloadPath.StartsWith(WrongGpuPayload, StringComparison.Ordinal)) return null;
        var name = payloadPath[WrongGpuPayload.Length..];
        if (name.StartsWith(WrongGpuSetupPrefix, StringComparison.OrdinalIgnoreCase) || name == DlssName) return null;
        return name == "WrongGPU.asi" ? WrongGpuAsi : name;
    }

    private static string? WrongGpuSetup(PayloadPins pins) =>
        pins.WrongGpuFiles.Keys.FirstOrDefault(k => k.StartsWith(WrongGpuPayload + WrongGpuSetupPrefix, StringComparison.OrdinalIgnoreCase));

    /// <summary>A DLSS file WrongGPU can convert Model M from: nvngx_dlss.dll 310.5 or newer in this folder.</summary>
    private static string? ConvertibleDlss(string dir)
    {
        var path = Path.Combine(dir, DlssName);
        if (!File.Exists(path)) return null;
        try
        {
            var v = FileVersionInfo.GetVersionInfo(path);
            return new Version(v.FileMajorPart, v.FileMinorPart) >= WrongGpuDlssFloor ? path : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return null; }
    }

    /// <summary>Whether a WrongGPU install in this game needs NVIDIA's DLSS downloaded for its setup: there is no model
    /// on this PC yet and the game has no DLSS 310.5 or newer of its own.</summary>
    public static bool WrongGpuNeedsDlss(string gameDir)
    {
        var dir = ResolveSource(gameDir);
        return !File.Exists(WrongGpuModel) && (dir.Length == 0 || ConvertibleDlss(dir) is null);
    }

    /// <summary>What the pre-flight says about WrongGPU once it has been ticked.</summary>
    private static void CheckWrongGpu(string src, PayloadPins pins, bool x86, Report report)
    {
        if (pins.WrongGpuFiles.Count == 0 || WrongGpuSetup(pins) is null)
        {
            report.Err("The OptiScaler version chosen has no WrongGPU. Pick OptiScaler 0.5.4-amd-nr or newer, or leave WrongGPU off.");
            return;
        }
        if (x86)
        {
            report.Err("WrongGPU runs in 64-bit games only. Leave it off for this one.");
            return;
        }
        if (src.Length == 0 || !Directory.Exists(src)) return;
        var missing = pins.WrongGpuFiles.Keys.Where(n => !File.Exists(Path.Combine(src, n))).ToList();
        if (missing.Count > 0) report.Err($"{Joined(missing)} {(missing.Count == 1 ? "is" : "are")} not in the payload folder.");
        else report.Ok($"WrongGPU {pins.WrongGpuVersion} is there. Installing converts its model from a DLSS file on this PC first.");
    }

    /// <summary>WrongGPU's files, verified, under the names they take in the game; the model converted first. Nothing is
    /// added when the conversion fails, and the report says why.</summary>
    private static void AddWrongGpu(SortedDictionary<string, byte[]> files, string dir, string src, PayloadPins pins, Report report)
    {
        CheckWrongGpu(src, pins, false, report);
        if (report.Failed) return;
        var wanted = new SortedDictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var (path, sha) in pins.WrongGpuFiles)
            if (WrongGpuDestination(path) is { } to && VerifiedPayload(src, path, sha, report) is { } bytes)
                wanted[to] = bytes;
        if (files.TryGetValue(OptiFsr4, out var fsr4)) wanted[WrongGpuCompare] = fsr4;
        var setup = WrongGpuSetup(pins)!;
        if (report.Failed || VerifiedPayload(src, setup, pins.WrongGpuFiles[setup], report) is null) return;
        if (!ConvertWrongGpuModel(dir, src, Path.Combine(src, setup), pins, report)) return;

        // The person's own wronggpu.ini stays as it is, as OptiScaler.ini does.
        if (!File.Exists(Path.Combine(dir, WrongGpuIni)) || InstalledManifest(dir)?.Entries.Any(e => e.Name == WrongGpuIni) == true)
            wanted[WrongGpuIni] = System.Text.Encoding.UTF8.GetBytes(WrongGpuIniText);
        foreach (var (name, bytes) in wanted) files[name] = bytes;
    }

    /// <summary>Runs WrongGPU's setup in a folder of its own, which leaves the model in %LOCALAPPDATA%\WrongGPU, and
    /// uninstalls it from there. OptiScaler's DLL and ini make the package it asks for; the game is a stand-in.</summary>
    private static bool ConvertWrongGpuModel(string dir, string src, string setup, PayloadPins pins, Report report)
    {
        var work = Path.Combine(Path.GetTempPath(), "amd-nr-wronggpu-" + Guid.NewGuid().ToString("N"));
        var game = Path.Combine(work, "game");
        var package = Path.Combine(work, "optiscaler");
        try
        {
            Directory.CreateDirectory(game);
            Directory.CreateDirectory(package);
            File.WriteAllBytes(Path.Combine(game, "Game.exe"), "MZ"u8.ToArray());
            File.Copy(Path.Combine(src, OptiScalerDllPayload), Path.Combine(package, OptiScalerDllPayload));
            File.Copy(Path.Combine(src, OptiScalerIni), Path.Combine(package, OptiScalerIni));
            // The setup looks in "this game" first: the game's own DLSS, else NVIDIA's download.
            string? dlss = null;
            // NVIDIA's DLSS is staged only when WrongGpuNeedsDlss said so; otherwise it is simply not there.
            if (pins.WrongGpuDlss.Length > 0 && File.Exists(Path.Combine(src, pins.WrongGpuDlss)) &&
                VerifiedPayload(src, pins.WrongGpuDlss, pins.WrongGpuDlssSha, report) is not null)
                dlss = Path.Combine(src, pins.WrongGpuDlss);
            dlss ??= ConvertibleDlss(dir);
            if (dlss is not null) File.Copy(dlss, Path.Combine(game, DlssName));

            // Run from the work folder: the staged payload's path can be longer than CreateProcess takes.
            var exe = Path.Combine(work, Path.GetFileName(setup));
            File.Copy(setup, exe);
            var logBefore = WrongGpuSetupLogText();
            var code = RunWrongGpuSetup(exe, ["/silent", $"/dir={game}", $"/optiscaler={package}", "/experimental"]);
            var converted = code == 0 && File.Exists(WrongGpuModel);
            if (code == 0) RunWrongGpuSetup(exe, ["/silent", "/uninstall", $"/dir={game}"]);
            if (!converted)
            {
                // Its own log says why: every refusal had been put down to a missing DLSS, an anti-cheat installed
                // for another game (WG-SETUP-4) too.
                var why = WrongGpuSetupError(logBefore) is { } error
                    ? error + (error.Contains("anti-cheat", StringComparison.OrdinalIgnoreCase)
                        ? " WrongGPU's setup refuses any PC with it installed, for every game, not only this one."
                        : "")
                    : dlss is null ? "it needs nvngx_dlss.dll 310.5 or newer on this PC." : "its log does not say why.";
                report.Err($"WrongGPU's setup stopped (exit code {code}): {why} Its log is %LOCALAPPDATA%\\WrongGPU\\setup.log. "
                           + "Nothing was installed; leave WrongGPU off to install OptiScaler without it.");
                return false;
            }
            report.Ok($"WrongGPU's model is ready: {WrongGpuModel}");
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            report.Err($"WrongGPU's setup could not run: {e.Message}");
            return false;
        }
        finally
        {
            try { if (Directory.Exists(work)) Directory.Delete(work, recursive: true); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        }
    }

    /// <summary>WrongGPU's setup log, beside its model folder.</summary>
    public static string WrongGpuSetupLog =>
        Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(WrongGpuModel)!)!, "setup.log");

    /// <summary>The last error WrongGPU's setup logged ("ERROR: Anti-cheat found (BattlEye) - ... [WG-SETUP-4]"),
    /// without its prefix, or null when there is none to read. The log keeps every run: with the text it had before
    /// this one, only what this run added counts, since an older setup's refusal is not this one's.</summary>
    internal static string? WrongGpuSetupError(string? before = null)
    {
        var text = WrongGpuSetupLogText();
        if (text is null) return null;
        if (before is not null && text.StartsWith(before, StringComparison.Ordinal)) text = text[before.Length..];
        string? last = null;
        foreach (var line in text.Split('\n'))
            if (line.IndexOf("ERROR:", StringComparison.Ordinal) is var at and >= 0)
                last = line[(at + "ERROR:".Length)..].Trim();
        return string.IsNullOrEmpty(last) ? null : last;
    }

    private static string? WrongGpuSetupLogText()
    {
        try
        {
            using var stream = new FileStream(WrongGpuSetupLog, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return new StreamReader(stream).ReadToEnd();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return null; }
    }

    /// <summary>Whether this app installed WrongGPU in this game folder and it is still there.</summary>
    public static bool HasWrongGpu(string gameDir)
    {
        var dir = ResolveSource(gameDir);
        return dir.Length > 0 && Directory.Exists(dir) && File.Exists(Path.Combine(dir, WrongGpuFfx))
               && WrongGpuRecorded(InstalledManifest(dir)).Contains(WrongGpuFfx);
    }

    /// <summary>What an install recorded of WrongGPU in this folder: what comes out when it is left off.</summary>
    private static IEnumerable<string> WrongGpuRecorded(Manifest? manifest) =>
        manifest?.Entries.Where(e => e.Owned && !e.Configuration && WrongGpuNames.Contains(e.Name)).Select(e => e.Name) ?? [];

    /// <summary>After an install with WrongGPU (<paramref name="on"/>), or one or an uninstall that took it out:
    /// OptiScaler.ini names WrongGPU as its FFX upscaler and loads the plugin while WrongGPU is here, and goes back to
    /// OptiScaler's own choice once it is not. Every other setting in
    /// it stays as it was. The logs WrongGPU wrote go with it.</summary>
    private static void PointIniAtWrongGpu(string dir, bool on, Report report)
    {
        var ini = Path.Combine(dir, OptiScalerIni);
        var ffx = Path.Combine(dir, WrongGpuFfx);
        var here = on && File.Exists(ffx);
        if (!on && File.Exists(ffx)) return;
        try
        {
            if (!here)
                foreach (var log in WrongGpuLogs.Select(l => Path.Combine(dir, l))
                             .Concat(Directory.EnumerateFiles(dir, "wronggpu-diagnostics-*.jsonl")))
                    if (File.Exists(log)) File.Delete(log);
            var plugins = Path.Combine(dir, "plugins");
            if (!here && Directory.Exists(plugins) && !Directory.EnumerateFileSystemEntries(plugins).Any()) Directory.Delete(plugins);
            if (!File.Exists(ini)) return;
            var before = File.ReadAllText(ini);
            var named = Engine.Trim(Engine.GetIni(before, "Libraries", "FfxDx12SRPath"));
            var after = here
                ? Engine.SetIni(Engine.SetIni(before, "Libraries", "FfxDx12SRPath", ffx), "Plugins", "LoadAsiPlugins", "true")
                : string.Equals(named, ffx, StringComparison.OrdinalIgnoreCase)
                    ? Engine.SetIni(Engine.SetIni(before, "Libraries", "FfxDx12SRPath", "auto"), "Plugins", "LoadAsiPlugins", "auto")
                    : before;
            if (after == before) return;
            File.WriteAllText(ini, after);
            report.Info(here
                ? $"{OptiScalerIni} names WrongGPU as its FFX upscaler (FfxDx12SRPath) and loads its plugin (LoadAsiPlugins); "
                  + "the rest of it is as it was. In the game pick FSR as the upscaler, then WrongGPU in OptiScaler's FSR "
                  + "version list (Insert)."
                : $"{OptiScalerIni} no longer names WrongGPU: FfxDx12SRPath and LoadAsiPlugins are back to auto.");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            report.Warn($"{OptiScalerIni} could not be updated for WrongGPU: {e.Message}");
        }
    }
}
