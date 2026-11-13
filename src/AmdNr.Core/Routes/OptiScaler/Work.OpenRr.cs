// OpenRR, this project's DLSS Ray Reconstruction runtime (OpenNR-Denoiser) for RX 9000, beside OptiScaler from the
// build that carries it: OpenNrRrRuntime.dll next to OptiScaler and its kernels in opennr-rr\shaders\. OptiScaler loads
// it for a D3D12 or Vulkan game's Ray Reconstruction, FSR-RR or DLSSD otherwise. Its network is read out of
// nvngx_dlssd.dll, and only 310.9.1 works. OptiScaler takes opennr-rr\nvngx_dlssd.dll first, then the one beside
// itself, so a D3D12 game with another version, or none, gets NVIDIA's 310.9.1 beside OptiScaler (openrr-dlssd), the
// game's own to the backup and back at uninstall. A Vulkan game gets it in opennr-rr\ and keeps its own where it is
// (Indiana Jones loads its 310 from streamline\).
//
// OptiScaler.ini's [OpenNR-RR] Enabled is auto (on) in the package, so a ticked install leaves it alone. Unticked,
// OpenRR stays out, an earlier install's comes out, and the ini says Enabled=false.

namespace AmdNr.Core;

/// <summary>Why OpenRR is not offered for a game (Work.OpenRr.cs).</summary>
public enum OpenRrBlock { None, Width, Api }

/// <summary>An nvngx_dlssd.dll in a game folder, by its path there, and its FileVersion (null when it gives none).</summary>
public sealed record DlssdFile(string Path, Version? Version)
{
    /// <summary>Whether OpenRR can build its network from it.</summary>
    public bool Works => Version is { Major: 310, Minor: 9, Build: 1 };
}

public sealed partial class PayloadPins
{
    /// <summary>OpenRR's files by their path in the payload folder, and its version: empty when the OptiScaler version
    /// chosen does not carry it. NVIDIA's nvngx_dlssd.dll 310.9.1 by its path, when it was downloaded.</summary>
    public IReadOnlyDictionary<string, string> OpenRrFiles { get; set; } = new Dictionary<string, string>();
    public string OpenRrVersion { get; set; } = string.Empty;
    public string OpenRrDlssd { get; set; } = string.Empty;
    public string OpenRrDlssdSha { get; set; } = string.Empty;

    /// <summary>OpenRR ticked (true) or unticked (false) for this game, set by the app like <see cref="HostFg"/>; null
    /// where it is not offered, which leaves OptiScaler.ini alone.</summary>
    public bool? OpenRr { get; set; }
}

public static partial class Work
{
    public const string OpenRrRuntimeName = "OpenNrRrRuntime.dll";
    private const string OpenRrPayload = "opennr-rr.shaders/";
    private const string OpenRrGfx11Payload = "opennr-rr.shaders-gfx11/";
    private const string OpenRrLog = "opennr_rr.log";
    private const string DlssdName = "nvngx_dlssd.dll";
    private const string DlssdOpenRr = "opennr-rr/nvngx_dlssd.dll";

    /// <summary>Whether an install brings NVIDIA's 310.9.1 nvngx_dlssd.dll where the game's is another or missing.</summary>
    public static bool SupplyOpenRrDlssd { get; set; } = true;

    private const string NoOpenRr = "The OptiScaler version chosen has no OpenRR. Pick one that carries it, or leave OpenRR off.";

    /// <summary>Where an OpenRR payload file goes in the game: the runtime beside OptiScaler, a kernel in opennr-rr\shaders\
    /// (an RX 7000 one in opennr-rr\shaders-gfx11\).</summary>
    internal static string? OpenRrDestination(string payloadPath) =>
        payloadPath == OpenRrRuntimeName ? payloadPath
        : payloadPath.StartsWith(OpenRrPayload, StringComparison.Ordinal) && payloadPath.Length > OpenRrPayload.Length
            ? Engine.OpenRrShaders + payloadPath[OpenRrPayload.Length..]
            : payloadPath.StartsWith(OpenRrGfx11Payload, StringComparison.Ordinal) && payloadPath.Length > OpenRrGfx11Payload.Length
                ? Engine.OpenRrShadersGfx11 + payloadPath[OpenRrGfx11Payload.Length..]
                : null;

    private static bool IsOpenRrFile(string name) =>
        name is OpenRrRuntimeName or DlssdOpenRr || name.StartsWith(Engine.OpenRrShaders, StringComparison.Ordinal)
        || name.StartsWith(Engine.OpenRrShadersGfx11, StringComparison.Ordinal);

    /// <summary>Whether OptiScaler runs on Vulkan in this game, where 310.9.1 goes in opennr-rr\.</summary>
    public static bool OpenRrVulkan(string dir, GraphicsApi? api) =>
        (api ?? GraphicsDetector.Detect(dir).OptiApi(true)) == GraphicsApi.Vulkan;

    /// <summary>Where NVIDIA's 310.9.1 goes for OpenRR: beside OptiScaler in a D3D12 game, opennr-rr\ in a Vulkan one.</summary>
    private static string OpenRrDlssdTarget(string dir, GraphicsApi? api) => OpenRrVulkan(dir, api) ? DlssdOpenRr : DlssdName;

    /// <summary>What an install recorded of OpenRR here, NVIDIA's nvngx_dlssd.dll with it: what comes out when it is left off.</summary>
    private static IEnumerable<string> OpenRrRecorded(Manifest? manifest) =>
        manifest?.Entries.Where(e => e.Owned && !e.Configuration && (IsOpenRrFile(e.Name) || e.Name == DlssdName && e.Hash != Engine.Sha([])))
            .Select(e => e.Name) ?? [];

    /// <summary>The pins an install of OpenRR is compared against, by its path in the game.</summary>
    private static IEnumerable<(string Name, string Sha)> OpenRrPins(PayloadManifest payload) =>
        payload.Has(PayloadManifest.OpenRrComponent)
            ? payload.Component(PayloadManifest.OpenRrComponent).Installed
                .Select(f => (OpenRrDestination(f.RelativePath), Engine.Lower(f.Sha256)))
                .Where(p => p.Item1 is not null).Select(p => (p.Item1!, p.Item2))
            : [];

    /// <summary>What keeps OpenRR from this game: 64-bit D3D12 games, and the Vulkan ones on the RR list (Api here,
    /// let through by OpenRrCardFor).</summary>
    public static OpenRrBlock OpenRrBlocked(string gameDir, GraphicsApi? api = null)
    {
        var dir = ResolveSource(gameDir);
        if (dir.Length == 0 || !Directory.Exists(dir)) return OpenRrBlock.Api;
        var detected = GraphicsDetector.Detect(dir);
        if (detected.Width == Route.X86) return OpenRrBlock.Width;
        return (api ?? detected.OptiApi(true)) == GraphicsApi.D3D12 ? OpenRrBlock.None : OpenRrBlock.Api;
    }

    /// <summary>The nvngx_dlssd.dll files here OpenRR could read: opennr-rr\'s first (what OptiScaler takes when it is
    /// there), then the game's own, the one beside OptiScaler and then deeper ones, shallowest first; then OptiScaler\'s
    /// and the copy danielblnc 0.8.0's host keeps in dlssnr_amd_host\, which OptiScaler takes instead while that host
    /// runs. Empty when the game has none: it may not use Ray Reconstruction.</summary>
    public static IReadOnlyList<DlssdFile> OpenRrDlssd(string gameDir)
    {
        var dir = ResolveSource(gameDir);
        if (dir.Length == 0 || !Directory.Exists(dir)) return [];
        var first = Path.Combine("opennr-rr", DlssdName);
        var found = GamesOwn(dir, [DlssdName]).Where(r => !r.Equals(first, StringComparison.OrdinalIgnoreCase))
            .OrderBy(r => r.Count(c => c == Path.DirectorySeparatorChar)).ToList();
        if (File.Exists(Path.Combine(dir, first))) found.Insert(0, first);
        foreach (var ours in new[] { "OptiScaler", "dlssnr_amd_host" })
            if (File.Exists(Path.Combine(dir, ours, DlssdName))) found.Add(Path.Combine(ours, DlssdName));
        return found.Select(r => new DlssdFile(r, DlssVersion(Path.Combine(dir, r)))).ToList();
    }

    /// <summary>Whether an OpenRR install in this game needs NVIDIA's 310.9.1 nvngx_dlssd.dll: the game's is missing or
    /// another version, or this app put 310.9.1 here already and keeps it.</summary>
    public static bool OpenRrNeedsDlssd(string gameDir, GraphicsApi? api = null)
    {
        if (!SupplyOpenRrDlssd) return false;
        var dir = ResolveSource(gameDir);
        if (dir.Length == 0 || !Directory.Exists(dir)) return false;
        var recorded = OpenRrRecorded(InstalledManifest(dir)).ToList();
        return recorded.Contains(DlssdName) || recorded.Contains(DlssdOpenRr) || OpenRrDlssd(dir) is not [{ Works: true }, ..];
    }

    /// <summary>What the game's nvngx_dlssd.dll means for OpenRR, said once it is ticked.</summary>
    private static void NoteDlssd(string dir, bool supplied, bool vulkan, Report report)
    {
        var found = OpenRrDlssd(dir);
        if (supplied && vulkan)
        {
            // opennr-rr's copy is what OptiScaler reads first, the game's own stays for the game
            report.Info("NVIDIA's nvngx_dlssd.dll 310.9.1 goes in opennr-rr\\ for OpenRR to build its network from; "
                        + "the game's own nvngx_dlssd.dll stays where it is.");
            return;
        }
        if (supplied)
        {
            var had = found.FirstOrDefault(f => f.Path == DlssdName);
            report.Info(had is { Works: false }
                ? $"The game's nvngx_dlssd.dll is {had.Version?.ToString() ?? "of no version"}: OpenRR builds its network from 310.9.1 only, "
                  + "so NVIDIA's 310.9.1 goes in beside OptiScaler, the game's own to the backup (uninstall puts it back)."
                : "NVIDIA's nvngx_dlssd.dll 310.9.1 goes in beside OptiScaler for OpenRR to build its network from.");
            found = found.Where(f => f.Path != DlssdName).ToList();
        }
        else if (found.Count == 0)
        {
            report.Info("No nvngx_dlssd.dll in this game: OpenRR runs only where the game uses DLSS Ray Reconstruction.");
            return;
        }
        var off = found.Where(f => !f.Works && (!supplied || f.Path.StartsWith("dlssnr_amd_host", StringComparison.OrdinalIgnoreCase))).ToList();
        if (off.Count > 0)
            report.Warn($"{Joined(off.Select(f => $"{f.Path} is {f.Version?.ToString() ?? "of no version"}").ToList())}: "
                        + "OpenRR builds its network from nvngx_dlssd.dll 310.9.1 only, so with that one Ray Reconstruction "
                        + "stays on FSR-RR.");
        else if (!supplied) report.Ok("nvngx_dlssd.dll 310.9.1 is here: OpenRR builds its network from it.");
    }

    /// <summary>What the pre-flight says about OpenRR.</summary>
    private static void CheckOpenRr(string dir, string src, PayloadPins pins, bool x86, GraphicsApi? api, Report report)
    {
        if (pins.OpenRr != true)
        {
            if (pins.OpenRr == false && dir.Length > 0 && OpenRrRecorded(InstalledManifest(dir)).Any())
                report.Info("OpenRR is installed here and is left off this time: installing takes it out, and OptiScaler.ini "
                            + "gets Enabled=false under [OpenNR-RR].");
            return;
        }
        if (pins.OpenRrFiles.Count == 0)
        {
            report.Err(NoOpenRr);
            return;
        }
        if (x86 || dir.Length == 0 || !Directory.Exists(dir)) return;
        if (src.Length > 0 && Directory.Exists(src))
        {
            var missing = pins.OpenRrFiles.Keys.Where(n => !File.Exists(Path.Combine(src, n))).ToList();
            if (missing.Count > 0) report.Err($"{Joined(missing)} {(missing.Count == 1 ? "is" : "are")} not in the payload folder.");
            else report.Ok($"OpenRR {pins.OpenRrVersion} is there: OpenNrRrRuntime.dll and opennr-rr\\shaders\\ go in beside OptiScaler.");
        }
        NoteDlssd(dir, OpenRrNeedsDlssd(dir, api) && pins.OpenRrDlssd.Length > 0, OpenRrVulkan(dir, api), report);
    }

    /// <summary>OpenRR's files, verified, when ticked, and NVIDIA's nvngx_dlssd.dll when the game needs it. Unticked,
    /// the ini about to be written gets Enabled=false. Never in a 32-bit game.</summary>
    private static void AddOpenRr(SortedDictionary<string, Blob> files, string dir, string src, PayloadPins pins, bool x86,
        string iniName, GraphicsApi? api, Report report)
    {
        if (pins.OpenRr is not { } on || x86) return;
        if (files.TryGetValue(iniName, out var ini))
            files[iniName] = System.Text.Encoding.UTF8.GetBytes(OpenRrIni(System.Text.Encoding.UTF8.GetString(ini.ToArray()), on));
        if (!on) return;
        if (pins.OpenRrFiles.Count == 0)
        {
            report.Err(NoOpenRr);
            return;
        }
        foreach (var (path, sha) in pins.OpenRrFiles)
            if (OpenRrDestination(path) is { } to && VerifiedPayload(src, path, sha, report) is { } bytes)
                files[to] = bytes;
        var supply = OpenRrNeedsDlssd(dir, api);
        if (supply && (pins.OpenRrDlssd.Length == 0 || !File.Exists(Path.Combine(src, pins.OpenRrDlssd))))
        {
            report.Warn("NVIDIA's nvngx_dlssd.dll 310.9.1 was not downloaded: OpenRR runs only if the game's own is 310.9.1.");
            supply = false;
        }
        if (supply && VerifiedPayload(src, pins.OpenRrDlssd, pins.OpenRrDlssdSha, report) is { } dlssd)
            files[OpenRrDlssdTarget(dir, api)] = dlssd;
        NoteDlssd(dir, supply, OpenRrVulkan(dir, api), report);
    }

    /// <summary>[OpenNR-RR] Enabled=false when OpenRR is left off; ticked again, a false there goes back to auto.</summary>
    private static string OpenRrIni(string text, bool on)
    {
        if (!on) return Engine.SetIni(text, "OpenNR-RR", "Enabled", "false");
        return string.Equals(Engine.Trim(Engine.GetIni(text, "OpenNR-RR", "Enabled")), "false", StringComparison.OrdinalIgnoreCase)
            ? Engine.SetIni(text, "OpenNR-RR", "Enabled", "auto")
            : text;
    }

    /// <summary>After an install: an OptiScaler.ini kept here gets OpenRR's key as <see cref="OpenRrIni"/> says, and
    /// what OpenRR wrote beside the game goes with its files.</summary>
    private static void PointIniAtOpenRr(string dir, PayloadPins pins, bool x86, IReadOnlyCollection<string> recorded, Report report)
    {
        if (x86) return;
        var here = File.Exists(Path.Combine(dir, OpenRrRuntimeName)) && pins.OpenRr == true;
        if (!here && recorded.Any(IsOpenRrFile))
        {
            RemoveFile(dir, OpenRrLog, report);
            PruneEmpty(dir, [Engine.OpenRrShaders.TrimEnd('/'), Engine.OpenRrShadersGfx11.TrimEnd('/'), "opennr-rr"], report);
        }
        if (pins.OpenRr is not { } on) return;
        var ini = Path.Combine(dir, OptiScalerIni);
        try
        {
            if (!File.Exists(ini)) return;
            var before = File.ReadAllText(ini);
            var after = OpenRrIni(before, on);
            if (after != before) File.WriteAllText(ini, after);
            report.Info(here
                ? "OpenRR is in. In the game turn DLSS Ray Reconstruction on: OptiScaler's menu (Insert) shows OpenRR, and "
                  + "its log is opennr_rr.log beside the game."
                : $"{OptiScalerIni} has OpenRR off (Enabled=false under [OpenNR-RR]): Ray Reconstruction runs on FSR-RR.");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            report.Warn($"{OptiScalerIni} could not be updated for OpenRR: {e.Message}");
        }
    }
}
