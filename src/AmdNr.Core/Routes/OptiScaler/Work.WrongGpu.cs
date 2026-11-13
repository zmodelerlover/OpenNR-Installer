// WrongGPU, phantoslayer21's DLSS 4.5 super resolution (Model M) and Ray Reconstruction for RX 9000, beside OptiScaler
// with his written permission (integration kit, section 10): the kit's files, unchanged, with their notices. Only when
// ticked, and only in a 64-bit game. Our fork has no custom upscaler folders, so it goes in by the kit's route C: the
// front end beside the game as wronggpu_ffx_dx12.dll, named by FfxDx12SRPath, Model M at index 0 of its FSR version
// list. Its Ray Reconstruction is the kit's plugin in plugins\, ticked per game instead of OpenRR, never both.
//
// The model and the RR data are made from NVIDIA's networks, so neither they nor any NVIDIA file are ever shipped or
// fetched for them: WrongGPU's own launcher converts them on the player's PC into %LOCALAPPDATA%\WrongGPU\model\, always
// from a file named on its command line, a game's own nvngx_dlss.dll 310.5+ or nvngx_dlssd.dll 310.7+, never one this
// app put in a game. Left to look by itself, the launcher takes the first it finds, ours included, and a --model= file
// it cannot use sends it looking too, so what it reports as its source is checked after. The launcher comes from
// WrongGPU's release page once the player said yes (Work.WrongGpu.Launcher.cs).

using System.Diagnostics;

namespace AmdNr.Core;

/// <summary>What WrongGPU converts on the player's PC: Model M, or its Ray Reconstruction data.</summary>
public enum WrongGpuData { Model, Rr }

public sealed partial class PayloadPins
{
    /// <summary>WrongGPU's Ray Reconstruction ticked for this game, set by the app; never together with OpenRR.</summary>
    public bool WrongGpuRr { get; set; }

    /// <summary>WrongGPU's own launcher from its official release, downloaded after the player said yes, when the
    /// model or the Ray Reconstruction data has to be converted; null otherwise.</summary>
    public string? WrongGpuLauncher { get; set; }
}

public static partial class Work
{
    private const string WrongGpuPayload = "wronggpu/";
    internal const string WrongGpuFfx = "wronggpu_ffx_dx12.dll";
    internal const string WrongGpuIni = "wronggpu.ini";
    private const string WrongGpuRuntime = "wronggpu_runtime.dll";
    private const string WrongGpuNotices = "wronggpu-notices.txt";
    internal const string WrongGpuAsi = "plugins/WrongGPU-RR.asi";
    internal const string WrongGpuRrIni = "WrongGPU/wronggpu.ini";
    private const string WrongGpuFrontEnd = "amd_fidelityfx_upscaler_dx12.dll";
    private const string DlssName = "nvngx_dlss.dll";

    public const string NoWrongGpuDlss =
        "WrongGPU needs DLSS 310.5 or newer from a game on this PC. Install or update a game with DLSS, then try again.";
    public const string NoWrongGpuDlssd =
        "WrongGPU Ray Reconstruction needs nvngx_dlssd.dll 310.7 or newer from a game on this PC.";

    /// <summary>Everything of WrongGPU's in a game folder this app may have put there, 0.5.5-ea's names included, so
    /// an update from it takes them out.</summary>
    internal static readonly string[] WrongGpuNames =
    [
        WrongGpuFfx, WrongGpuRuntime, WrongGpuNotices, WrongGpuAsi, WrongGpuRrIni,
        "wronggpu-amd-compare.dll", "plugins/WrongGPU.asi", WrongGpuIni, "wronggpu-license.txt",
    ];

    /// <summary>What WrongGPU writes beside the game while it runs (0.5.5-ea's too).</summary>
    private static readonly string[] WrongGpuLogs = ["wronggpu-fsr.log", "wronggpu-rr.log", "wronggpu-rr.log.old", "wronggpu-asi.log"];

    /// <summary>A fresh WrongGPU\wronggpu.ini: the RR mode the kit recommends. WrongGPU fills in the rest itself.</summary>
    internal const string WrongGpuRrIniText = "[wronggpu]\r\nrr_mode=fast\r\n";

    /// <summary>Where WrongGPU keeps what it converted. A test points it elsewhere.</summary>
    internal static string WrongGpuModelDir { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WrongGPU", "model");

    /// <summary>The log of WrongGPU's launcher's last run, beside its model folder.</summary>
    public static string WrongGpuLauncherLog =>
        Path.Combine(Path.GetDirectoryName(WrongGpuModelDir)!, "logs", "last-operation.log");

    /// <summary>Other games' folders to look for a DLSS file in, after this game's: the app's library.</summary>
    public static Func<IEnumerable<string>> WrongGpuOtherGames { get; set; } = () => [];

    private static (string Name, Version Floor, string Command, string Option, string Folder) Spec(WrongGpuData data) =>
        data == WrongGpuData.Model
            ? (DlssName, new Version(310, 5), "model", "--model=", "")
            : ("nvngx_dlssd.dll", new Version(310, 7), "rr-data", "--dll=", "F");

    /// <summary>Whether a payload component is WrongGPU's integration kit (0.6.0 on), not 0.5.5-ea's setup and DLLs.</summary>
    public static bool IsWrongGpuKit(PayloadComponent component) =>
        component.Files.Any(f => f.RelativePath == WrongGpuPayload + WrongGpuFrontEnd);

    /// <summary>Where a kit file goes in the game: the front end under its route C name, the RR plugin in plugins\.</summary>
    internal static string? WrongGpuDestination(string payloadPath) => payloadPath switch
    {
        WrongGpuPayload + WrongGpuFrontEnd => WrongGpuFfx,
        WrongGpuPayload + "WrongGPU-RR.asi" => WrongGpuAsi,
        WrongGpuPayload + WrongGpuRuntime => WrongGpuRuntime,
        WrongGpuPayload + WrongGpuNotices => WrongGpuNotices,
        _ => null,
    };

    /// <summary>The pins an install of WrongGPU is compared against, by its path in the game.</summary>
    private static IEnumerable<(string Name, string Sha)> WrongGpuPins(PayloadManifest payload) =>
        payload.Has(PayloadManifest.WrongGpuComponent) && IsWrongGpuKit(payload.Component(PayloadManifest.WrongGpuComponent))
            ? payload.Component(PayloadManifest.WrongGpuComponent).Installed
                .Select(f => (WrongGpuDestination(f.RelativePath), Engine.Lower(f.Sha256)))
                .Where(p => p.Item1 is not null).Select(p => (p.Item1!, p.Item2))
            : [];

    /// <summary>Whether WrongGPU's model, or its RR data, is on this PC already.</summary>
    public static bool WrongGpuReady(WrongGpuData data) =>
        File.Exists(Path.Combine(WrongGpuModelDir, Spec(data).Folder, "model.wgcm"));

    /// <summary>The file WrongGPU may convert from: a game's own nvngx_dlss.dll 310.5+ (Model M) or nvngx_dlssd.dll
    /// 310.7+ (RR), the newest in this game, else in the first other game that has one. Never one this app recorded as
    /// its own, nor one in a folder this app or the host made (OptiScaler\, dlssnr_amd_host\, opennr-rr\, backups).</summary>
    public static string? WrongGpuSource(string gameDir, WrongGpuData data)
    {
        var (name, floor, _, _, _) = Spec(data);
        foreach (var dir in WrongGpuOtherGames().Prepend(gameDir).Select(ResolveSource)
                     .Where(d => d.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!Directory.Exists(dir)) continue;
            var ours = InstalledManifest(dir)?.Entries.Where(e => e.Owned).Select(e => e.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase) ?? [];
            var best = GamesOwn(dir, [name])
                .Where(r => !r.StartsWith("opennr-rr" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                            && !ours.Contains(r.Replace('\\', '/')))
                .Select(r => (Path: Path.Combine(dir, r), Version: DlssVersion(Path.Combine(dir, r))))
                .Where(f => f.Version >= floor).OrderByDescending(f => f.Version).FirstOrDefault();
            if (best.Path is not null) return best.Path;
        }
        return null;
    }

    /// <summary>What an install of WrongGPU here has to convert first and has a file for: what WrongGPU's launcher is
    /// needed for. The app asks before it downloads it.</summary>
    public static IReadOnlyList<WrongGpuData> WrongGpuToConvert(string gameDir, bool rr) =>
        new[] { WrongGpuData.Model, WrongGpuData.Rr }
            .Where(d => (d == WrongGpuData.Model || rr) && !WrongGpuReady(d) && WrongGpuSource(gameDir, d) is not null).ToList();

    /// <summary>Whether WrongGPU's Ray Reconstruction can reach this game: through the game's own Streamline
    /// (sl.interposer.dll), which its plugin hooks. Not in an Unreal game, which creates Ray Reconstruction itself and
    /// needs the WrongGPU edition's plugin interface this fork does not have, nor in Spider-Man 2, where it is unstable.</summary>
    public static bool WrongGpuRrFits(string gameDir)
    {
        var dir = ResolveSource(gameDir);
        if (dir.Length == 0 || !Directory.Exists(dir) || File.Exists(Path.Combine(dir, "Spider-Man2.exe"))) return false;
        if (dir.TrimEnd('\\', '/').EndsWith(Path.Combine("Binaries", "Win64"), StringComparison.OrdinalIgnoreCase)) return false;
        return GamesOwn(dir, ["sl.interposer.dll"]).Any();
    }

    /// <summary>Runs WrongGPU's launcher with these arguments and gives its exit code and what it printed. A test
    /// replaces it.</summary>
    internal static Func<string, string[], (int Code, string Output)> RunWrongGpuLauncher { get; set; } = (exe, arguments) =>
    {
        var start = new ProcessStartInfo(exe)
        {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
            WorkingDirectory = Path.GetDirectoryName(exe)!,
        };
        foreach (var a in arguments) start.ArgumentList.Add(a);
        using var process = Process.Start(start) ?? throw new InstallException($"{Path.GetFileName(exe)} did not start.");
        var output = process.StandardOutput.ReadToEndAsync();
        var errors = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(TimeSpan.FromMinutes(5)))
        {
            try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
            throw new InstallException($"{Path.GetFileName(exe)} did not finish in five minutes.");
        }
        return (process.ExitCode, (output.Result + errors.Result).Trim());
    };

    /// <summary>The file a conversion says it was made from (model-source.txt), or null.</summary>
    private static string? WrongGpuMadeFrom(string folder)
    {
        try
        {
            return File.ReadLines(Path.Combine(folder, "model-source.txt"))
                .FirstOrDefault(l => l.StartsWith("source\t", StringComparison.Ordinal))?["source\t".Length..].Trim();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return null; }
    }

    /// <summary>Converts the model or the RR data from this file with WrongGPU's launcher. What it made from any other
    /// file is taken out again.</summary>
    private static bool ConvertWrongGpu(string launcher, string from, WrongGpuData data, Report report)
    {
        var (_, _, command, option, folder) = Spec(data);
        var what = data == WrongGpuData.Model ? "model" : "Ray Reconstruction data";
        try
        {
            var (code, output) = RunWrongGpuLauncher(launcher, [command, "convert", option + from]);
            var made = Path.Combine(WrongGpuModelDir, folder);
            var source = WrongGpuMadeFrom(made);
            if (code == 0 && WrongGpuReady(data) && source is not null
                && string.Equals(Path.GetFullPath(source), Path.GetFullPath(from), StringComparison.OrdinalIgnoreCase))
            {
                report.Ok($"WrongGPU's {what} is converted on this PC from {from}.");
                return true;
            }
            if (code == 0 && WrongGpuReady(data))
                foreach (var name in new[] { "model.wgcm", "model-source.txt" })
                    File.Delete(Path.Combine(made, name));
            var said = output.Split('\n').Select(l => l.Trim()).LastOrDefault(l => l.Length > 0) ?? "";
            report.Err($"WrongGPU could not convert its {what} from {from} (exit code {code}){(said.Length > 0 ? ": " + said : ".")} "
                       + "Its log is %LOCALAPPDATA%\\WrongGPU\\logs\\last-operation.log.");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            report.Err($"WrongGPU's launcher could not run: {e.Message}");
        }
        return false;
    }

    /// <summary>What the pre-flight says about WrongGPU once it has been ticked.</summary>
    private static void CheckWrongGpu(string dir, string src, PayloadPins pins, bool x86, Report report)
    {
        if (pins.WrongGpuFiles.Count == 0)
        {
            report.Err("The OptiScaler version chosen has no WrongGPU 0.6.0 or newer. Pick one that carries it, or leave WrongGPU off.");
            return;
        }
        if (x86)
        {
            report.Err("WrongGPU runs in 64-bit games only. Leave it off for this one.");
            return;
        }
        if (pins.WrongGpuRr && pins.OpenRr == true)
            report.Err("Pick one Ray Reconstruction for this game, OpenRR or WrongGPU's, not both.");
        if (dir.Length > 0 && File.Exists(Path.Combine(dir, WrongGpuRuntime))
            && InstalledManifest(dir)?.Entries.Any(e => e.Owned && e.Name == WrongGpuRuntime) != true)
            report.Err("WrongGPU's own launcher installed it in this game. Uninstall it there first (More, Uninstall WrongGPU): "
                       + "one installer per game.");
        if (src.Length == 0 || !Directory.Exists(src)) return;
        var missing = pins.WrongGpuFiles.Keys.Where(n => !File.Exists(Path.Combine(src, n))).ToList();
        if (missing.Count > 0)
        {
            report.Err($"{Joined(missing)} {(missing.Count == 1 ? "is" : "are")} not in the payload folder.");
            return;
        }
        report.Ok($"WrongGPU {pins.WrongGpuVersion} by phantoslayer21 is there (https://github.com/phantoslayer21/wronggpu).");
        if (!WrongGpuReady(WrongGpuData.Model) || pins.WrongGpuRr && !WrongGpuReady(WrongGpuData.Rr))
            report.Info("WrongGPU converts what it needs on this PC first, with its own launcher from its release page, "
                        + "from a DLSS file that came with one of your games. You are asked before anything is downloaded.");
    }

    /// <summary>WrongGPU's files, verified, under the names they take in the game, once its model is on this PC; its RR
    /// plugin too when ticked and its data is there. Nothing is added when the model cannot be made.</summary>
    private static void AddWrongGpu(SortedDictionary<string, Blob> files, string dir, string src, PayloadPins pins, Report report)
    {
        CheckWrongGpu(dir, src, pins, false, report);
        if (report.Failed) return;
        var wanted = new SortedDictionary<string, Blob>(StringComparer.Ordinal);
        foreach (var (path, sha) in pins.WrongGpuFiles)
            if (WrongGpuDestination(path) is { } to && VerifiedPayload(src, path, sha, report) is { } bytes)
                wanted[to] = bytes;
        if (report.Failed) return;

        if (!WrongGpuReady(WrongGpuData.Model))
        {
            if (WrongGpuSource(dir, WrongGpuData.Model) is not { } dlss)
            {
                report.Err(NoWrongGpuDlss);
                return;
            }
            if (pins.WrongGpuLauncher is not { } launcher)
            {
                report.Err("WrongGPU's model is not on this PC yet, and WrongGPU was not downloaded to convert it. "
                           + "Leave WrongGPU off to install OptiScaler without it.");
                return;
            }
            if (!ConvertWrongGpu(launcher, dlss, WrongGpuData.Model, report)) return;
        }

        var rr = pins.WrongGpuRr && WrongGpuRrData(dir, pins, report);
        if (!rr) wanted.Remove(WrongGpuAsi);
        else if (!File.Exists(Path.Combine(dir, WrongGpuRrIni.Replace('/', Path.DirectorySeparatorChar))))
            wanted[WrongGpuRrIni] = System.Text.Encoding.UTF8.GetBytes(WrongGpuRrIniText);
        foreach (var (name, bytes) in wanted) files[name] = bytes;
        report.Info("One installer per game: don't also install WrongGPU into this game with WrongGPU's own launcher.");
    }

    /// <summary>Whether WrongGPU's RR data is on this PC, converted now when it can be. When it cannot, its RR stays
    /// out and the report says why; Model M is not affected.</summary>
    private static bool WrongGpuRrData(string dir, PayloadPins pins, Report report)
    {
        if (WrongGpuReady(WrongGpuData.Rr)) return true;
        if (WrongGpuSource(dir, WrongGpuData.Rr) is not { } dlssd)
        {
            report.Warn(NoWrongGpuDlssd);
            report.Info("WrongGPU's Ray Reconstruction stays off in this game; Model M is not affected.");
            return false;
        }
        if (pins.WrongGpuLauncher is not { } launcher)
        {
            report.Warn("WrongGPU's Ray Reconstruction data is not on this PC yet, and WrongGPU was not downloaded to convert "
                        + "it: its Ray Reconstruction stays off in this game.");
            return false;
        }
        var converted = new Report();
        if (ConvertWrongGpu(launcher, dlssd, WrongGpuData.Rr, converted)) return true;
        foreach (var line in converted.Lines) report.Warn(line.Text);
        report.Info("WrongGPU's Ray Reconstruction stays off in this game; Model M is not affected.");
        return false;
    }

    /// <summary>Whether this app installed WrongGPU in this game folder and it is still there.</summary>
    public static bool HasWrongGpu(string gameDir) => HasWrongGpuFile(gameDir, WrongGpuFfx);

    /// <summary>Whether this app installed WrongGPU's Ray Reconstruction plugin in this game folder.</summary>
    public static bool HasWrongGpuRr(string gameDir) => HasWrongGpuFile(gameDir, WrongGpuAsi);

    private static bool HasWrongGpuFile(string gameDir, string name)
    {
        var dir = ResolveSource(gameDir);
        return dir.Length > 0 && Directory.Exists(dir) && File.Exists(Path.Combine(dir, name))
               && WrongGpuRecorded(InstalledManifest(dir)).Contains(name);
    }

    /// <summary>What an install recorded of WrongGPU in this folder: what comes out when it is left off.</summary>
    private static IEnumerable<string> WrongGpuRecorded(Manifest? manifest) =>
        manifest?.Entries.Where(e => e.Owned && !e.Configuration && WrongGpuNames.Contains(e.Name)).Select(e => e.Name) ?? [];

    /// <summary>After an install with WrongGPU (<paramref name="on"/>), or one or an uninstall that took it out:
    /// OptiScaler.ini names WrongGPU's front end as its FFX upscaler, Model M first in its list, and loads the RR plugin
    /// while it is here; once WrongGPU is gone, those keys go back to OptiScaler's own choice. Every other setting stays
    /// as it was. The logs WrongGPU wrote go with it.</summary>
    private static void PointIniAtWrongGpu(string dir, bool on, Report report)
    {
        var ini = Path.Combine(dir, OptiScalerIni);
        var ffx = Path.Combine(dir, WrongGpuFfx);
        var here = on && File.Exists(ffx);
        var rr = here && File.Exists(Path.Combine(dir, WrongGpuAsi));
        if (!on && File.Exists(ffx)) return;
        try
        {
            foreach (var log in WrongGpuLogs.Where(l => !here || !rr && l.StartsWith("wronggpu-rr", StringComparison.Ordinal))
                         .Select(l => Path.Combine(dir, l))
                         .Concat(here ? [] : Directory.EnumerateFiles(dir, "wronggpu-diagnostics-*.jsonl")))
                if (File.Exists(log)) File.Delete(log);
            var plugins = Path.Combine(dir, "plugins");
            if (!rr && Directory.Exists(plugins) && !Directory.EnumerateFileSystemEntries(plugins).Any()) Directory.Delete(plugins);
            if (!File.Exists(ini)) return;
            var before = File.ReadAllText(ini);
            var named = Engine.Trim(Engine.GetIni(before, "Libraries", "FfxDx12SRPath"));
            string after;
            if (here)
            {
                after = Engine.SetIni(before, "Libraries", "FfxDx12SRPath", ffx);
                after = Engine.SetIni(after, "Upscalers", "Dx12Upscaler", "ffx");
                after = Engine.SetIni(after, "FSR", "Fsr4Update", "false");
                after = Engine.SetIni(after, "FSR", "UpscalerIndex", "0");
                after = Engine.SetIni(after, "Plugins", "LoadAsiPlugins", rr ? "true" : "auto");
                if (rr) after = Engine.SetIni(after, "Plugins", "Path", "auto");
            }
            else if (string.Equals(named, ffx, StringComparison.OrdinalIgnoreCase))
            {
                after = Engine.SetIni(before, "Libraries", "FfxDx12SRPath", "auto");
                after = Engine.SetIni(after, "Upscalers", "Dx12Upscaler", "auto");
                after = Engine.SetIni(after, "FSR", "UpscalerIndex", "auto");
                after = Engine.SetIni(after, "Plugins", "LoadAsiPlugins", "auto");
            }
            else after = before;
            if (after != before) File.WriteAllText(ini, after);
            if (after == before && !here) return;
            report.Info(here
                ? $"{OptiScalerIni} has WrongGPU as its FFX upscaler (FfxDx12SRPath, Dx12Upscaler=ffx, Model M first in its "
                  + "FSR version list)" + (rr ? " and loads its Ray Reconstruction plugin (LoadAsiPlugins)" : "")
                  + "; the rest of it is as it was. In the game pick DLSS (or FSR) as the upscaler"
                  + (rr ? ", and turn DLSS Ray Reconstruction on for WrongGPU's" : "") + ". WrongGPU by phantoslayer21, "
                  + "https://github.com/phantoslayer21/wronggpu"
                : $"{OptiScalerIni} no longer names WrongGPU: its upscaler keys are back to auto.");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            report.Warn($"{OptiScalerIni} could not be updated for WrongGPU: {e.Message}");
        }
    }
}
