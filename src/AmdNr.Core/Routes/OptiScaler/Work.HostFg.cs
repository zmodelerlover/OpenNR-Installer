// danielblnc 0.8.0's DLSS frame generation in a D3D12 game that has none of its own. The hosted runtime (NeuralHub)
// arms DLSS-FG when nvngx_dlssg.dll sits in OptiScaler's library folder (OptiScaler\), and OptiScaler's DLSSG output
// hands it the frames through Streamline, which it loads from OptiScaler\streamline\. Both are NVIDIA's own: the
// production DLLs of the Streamline SDK release and the 310.9.1 nvngx_dlssg.dll of the DLSS repository. OptiScaler.ini
// takes frame generation from the upscaler (FGInput=upscaler) out through DLSSG (FGOutput=dlssg), and nothing else in
// it changes; left off again, the two keys go back to auto while they still say that.
//
// Only when ticked, and never where it cannot work. A game with its own nvngx_dlssg.dll (Cyberpunk 2077, Crimson
// Desert, RE9) runs danielblnc's FG through its own DLSS-FG with nothing added. A game with no DLSS, FSR or XeSS has
// no upscaler call to take the frames from; OptiScaler 0.5.8's final-image NR can feed DLSSG its optical flow instead,
// but that was never tried in a game, so it stays out. Off D3D12 the host has no FG at all (Vulkan), or it was never
// tried (D3D11). Armed, the host computes on D3D12 instead of HIP and danielblnc's
// own NR is off there, which the report says when the ini runs danielblnc.

namespace AmdNr.Core;

/// <summary>Why danielblnc's DLSS-FG is not offered for a game (Work.HostFg.cs).</summary>
public enum HostFgBlock { None, Runtime, Width, Api, OwnFg, NoUpscaler }

public sealed partial class PayloadPins
{
    /// <summary>Streamline's DLLs by their path in the payload folder (streamline\), and NVIDIA's nvngx_dlssg.dll by
    /// its path: empty when the OptiScaler version chosen carries neither.</summary>
    public IReadOnlyDictionary<string, string> StreamlineFiles { get; set; } = new Dictionary<string, string>();
    public string Dlssg { get; set; } = string.Empty;
    public string DlssgSha { get; set; } = string.Empty;

    /// <summary>danielblnc's DLSS-FG was ticked for this game: set by the app, like <see cref="RuntimeChosen"/>.</summary>
    public bool HostFg { get; set; }
}

public static partial class Work
{
    private const string HostFgDlssg = "OptiScaler/nvngx_dlssg.dll";
    private const string StreamlinePayload = "streamline/";

    /// <summary>Whether a name in the game folder is one of the files this puts in.</summary>
    private static bool IsHostFgFile(string name) =>
        name == HostFgDlssg || name.StartsWith("OptiScaler/" + StreamlinePayload, StringComparison.Ordinal);

    /// <summary>What keeps danielblnc's DLSS-FG out of this game with this runtime, or None.</summary>
    public static HostFgBlock HostFgBlocked(string gameDir, string runtimeVersion, GraphicsApi? api = null) =>
        HostFgBlocked(ResolveSource(gameDir), VersionOf(runtimeVersion), api);

    private static HostFgBlock HostFgBlocked(string dir, Version? runtime, GraphicsApi? api)
    {
        if (runtime is null || runtime < HostedRuntime) return HostFgBlock.Runtime;
        if (dir.Length == 0 || !Directory.Exists(dir)) return HostFgBlock.Width;
        var detected = GraphicsDetector.Detect(dir);
        if (detected.Width == Route.X86) return HostFgBlock.Width;
        if ((api ?? detected.OptiApi(true)) != GraphicsApi.D3D12) return HostFgBlock.Api;
        string[] fg = ["nvngx_dlssg.dll", "sl.dlss_g.dll"];
        var own = GamesOwn(dir, [.. fg, .. UpscalerFiles]).Select(Path.GetFileName).ToList();
        if (own.Any(n => fg.Contains(n, StringComparer.OrdinalIgnoreCase))) return HostFgBlock.OwnFg;
        return own.Count > 0 || detected.Executable is { } exe && GraphicsDetector.BuiltIn(exe).Length > 0
            ? HostFgBlock.None
            : HostFgBlock.NoUpscaler;
    }

    private static string HostFgWhy(HostFgBlock block) => block switch
    {
        HostFgBlock.Runtime => "it needs danielblnc 0.8.0 or newer as the NR runtime.",
        HostFgBlock.Width => "it runs in 64-bit games only.",
        HostFgBlock.Api => "it runs in D3D12 games only.",
        HostFgBlock.OwnFg => "the game has its own DLSS frame generation, which runs danielblnc's FG with nothing added.",
        _ => "no DLSS, FSR or XeSS was found in this game, and OptiScaler's DLSS-FG output takes its frames from the "
             + "game's upscaler.",
    };

    /// <summary>The DLLs of these names in the game's tree, eight levels down: an Unreal game keeps Streamline in
    /// Engine\Plugins\Runtime\Nvidia\Streamline\Binaries\ThirdParty\Win64 (Hogwarts Legacy). What this app or the host
    /// put there is not the game's (OptiScaler\, OptiScaler64\, dlssnr_amd_host\), and neither is a folder named as a
    /// backup, ours or one made by hand: nothing loads from there.</summary>
    private static IEnumerable<string> GamesOwn(string dir, string[] names)
    {
        var wanted = new HashSet<string>(names, StringComparer.OrdinalIgnoreCase);
        string[] ours = ["OptiScaler", OptiHostFolder, "dlssnr_amd_host"];
        List<string> dlls;
        try
        {
            dlls = Directory.EnumerateFiles(dir, "*.dll", new EnumerationOptions
            {
                RecurseSubdirectories = true, MaxRecursionDepth = 8, IgnoreInaccessible = true,
            }).Take(20_000).ToList();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return []; }
        return dlls.Where(f => wanted.Contains(Path.GetFileName(f))).Select(f => Path.GetRelativePath(dir, f))
            .Where(r => !ours.Any(o => r.StartsWith(o + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                        && !Path.GetDirectoryName(r)!.Contains("backup", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Whether this app put danielblnc's DLSS-FG in this game folder and it is still there.</summary>
    public static bool HasHostFg(string gameDir)
    {
        var dir = ResolveSource(gameDir);
        return dir.Length > 0 && File.Exists(Path.Combine(dir, HostFgDlssg))
               && InstalledManifest(dir)?.Entries.Any(e => e.Owned && e.Name == HostFgDlssg) == true;
    }

    /// <summary>What the pre-flight says once it has been ticked.</summary>
    private static void CheckHostFg(string dir, string src, PayloadPins pins, GraphicsApi? api, Report report)
    {
        if (!pins.HostFg) return;
        if (HostFgBlocked(dir, VersionOf(pins.OptiRuntimeVersion), api) is var block and not HostFgBlock.None)
            report.Info($"danielblnc's DLSS-FG is ticked and stays out: {HostFgWhy(block)}");
        else if (pins.StreamlineFiles.Count == 0 || pins.Dlssg.Length == 0)
            report.Err(NoHostFg);
        else if (src.Length > 0 && Directory.Exists(src))
            report.Ok("danielblnc's DLSS-FG goes in: NVIDIA's Streamline in OptiScaler\\streamline\\ and nvngx_dlssg.dll in OptiScaler\\.");
    }

    private const string NoHostFg = "The OptiScaler version chosen has no Streamline or nvngx_dlssg.dll for danielblnc's DLSS-FG. "
                                    + "Pick 0.5.8-opennr or newer, or leave it off.";

    /// <summary>Streamline and nvngx_dlssg.dll, verified, when ticked and the game can use them; the ini about to be
    /// written takes the two keys too.</summary>
    private static void AddHostFg(SortedDictionary<string, byte[]> files, string dir, string src, PayloadPins pins,
        Version? runtime, GraphicsApi? api, Report report)
    {
        if (!pins.HostFg) return;
        if (HostFgBlocked(dir, runtime, api) is var block and not HostFgBlock.None)
        {
            report.Info($"danielblnc's DLSS-FG is ticked and stays out: {HostFgWhy(block)}");
            return;
        }
        if (pins.StreamlineFiles.Count == 0 || pins.Dlssg.Length == 0)
        {
            report.Err(NoHostFg);
            return;
        }
        foreach (var (path, sha) in pins.StreamlineFiles)
            if (path.StartsWith(StreamlinePayload, StringComparison.Ordinal) && VerifiedPayload(src, path, sha, report) is { } bytes)
                files["OptiScaler/" + path] = bytes;
        if (VerifiedPayload(src, pins.Dlssg, pins.DlssgSha, report) is { } dlssg) files[HostFgDlssg] = dlssg;
        if (files.TryGetValue(OptiScalerIni, out var ini))
            files[OptiScalerIni] = System.Text.Encoding.UTF8.GetBytes(HostFgIni(System.Text.Encoding.UTF8.GetString(ini), true));
    }

    /// <summary>The two keys set (<paramref name="on"/>), or back to auto while they still hold what this set.</summary>
    private static string HostFgIni(string text, bool on)
    {
        string Key(string key) => Engine.Trim(Engine.GetIni(text, "FrameGen", key));
        if (on) return Engine.SetIni(Engine.SetIni(text, "FrameGen", "FGInput", "upscaler"), "FrameGen", "FGOutput", "dlssg");
        return string.Equals(Key("FGInput"), "upscaler", StringComparison.OrdinalIgnoreCase)
               && string.Equals(Key("FGOutput"), "dlssg", StringComparison.OrdinalIgnoreCase)
            ? Engine.SetIni(Engine.SetIni(text, "FrameGen", "FGInput", "auto"), "FrameGen", "FGOutput", "auto")
            : text;
    }

    /// <summary>After an install with danielblnc's DLSS-FG, or one or an uninstall that took it out: an OptiScaler.ini
    /// kept here gets the two keys or gives them back, and the report says what to do in the game.</summary>
    private static void PointIniAtHostFg(string dir, bool on, Report report)
    {
        var ini = Path.Combine(dir, OptiScalerIni);
        try
        {
            if (!File.Exists(ini)) return;
            var before = File.ReadAllText(ini);
            var after = HostFgIni(before, on);
            if (after != before) File.WriteAllText(ini, after);
            if (!on)
            {
                if (after != before) report.Info($"{OptiScalerIni} no longer runs danielblnc's DLSS-FG: FGInput and FGOutput are back to auto.");
                return;
            }
            report.Info($"danielblnc's DLSS-FG is in: {OptiScalerIni} takes frame generation from the game's upscaler "
                        + "(FGInput=upscaler) through NVIDIA's Streamline (FGOutput=dlssg). In the game turn its DLSS, FSR or "
                        + "XeSS on, then frame generation on in OptiScaler's menu (Insert).");
            if (Engine.Trim(Engine.GetIni(after, "DlssNr", "NrBackend")) is "" or "auto" or "daniel")
                report.Warn("With its DLSS-FG armed danielblnc's runtime runs on D3D12 instead of HIP and its own NR is off. "
                            + "For NR with it, pick mochizuki or lmxxf under NR runtime in OptiScaler's Neural tab.");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            report.Warn($"{OptiScalerIni} could not be updated for danielblnc's DLSS-FG: {e.Message}");
        }
    }
}
