// HexNeural, Emion's NR runtime for RX 6000 cards (RDNA2 graphics cards, HIP 6.4, GPL; source at
// github.com/philippraschke75-spec), beside OptiScaler from the build that knows NrBackend=rdna2: Rdna2NrRuntime.dll on
// the lmxxf interface, nr_runtime.dll (its network), Rdna2NrRuntime.ini (its settings, which OptiScaler's menu edits),
// the kernels and plans in nr_data_h15\, and a notice with its licence and where its source is. No weights and nothing
// of NVIDIA's: the runtime reads the weights out of the game's own nvngx_dlssnr.dll.
//
// Offered on an RX 6000 graphics card only, off unless ticked. Ticked, its files go in and OptiScaler.ini gets
// NrBackend=rdna2; unticked, an earlier install's files come out and an ini still naming it goes back to auto (mochizuki
// where that is here). Its ini is the player's settings: kept when changed, like OptiScaler.ini.

namespace AmdNr.Core;

public sealed partial class PayloadPins
{
    /// <summary>HexNeural's files by their path in the payload folder, and its version: empty when the OptiScaler version
    /// chosen does not carry it.</summary>
    public IReadOnlyDictionary<string, string> HexNeuralFiles { get; set; } = new Dictionary<string, string>();
    public string HexNeuralVersion { get; set; } = string.Empty;

    /// <summary>HexNeural ticked (true) or unticked (false) for this game, set by the app; null where it is not offered,
    /// which leaves an earlier install's files and the ini alone.</summary>
    public bool? HexNeural { get; set; }

    /// <summary>Set by the install: HexNeural was not in this folder before it, so a kept OptiScaler.ini is pointed at it.
    /// An ini that already had it keeps the runtime the player picked in the menu since.</summary>
    internal bool HexNeuralArrives { get; set; }
}

public static partial class Work
{
    public const string HexNeuralRuntimeName = "Rdna2NrRuntime.dll";
    internal const string HexNeuralNetworkName = "nr_runtime.dll";
    internal const string HexNeuralIniName = "Rdna2NrRuntime.ini";
    internal const string HexNeuralNoticeName = "HexNeural-NOTICE.txt";
    internal const string HexNeuralLog = "Rdna2NrRuntime.log";

    /// <summary>What OptiScaler.ini names it by: [DlssNr] NrBackend=rdna2 (hexneural is the same).</summary>
    internal const string HexNeuralBackend = "rdna2";

    private const string NoHexNeural = "The OptiScaler version chosen has no HexNeural. Pick one that carries it, or leave it off.";

    private static readonly string[] HexNeuralTop = [HexNeuralRuntimeName, HexNeuralNetworkName, HexNeuralIniName, HexNeuralNoticeName];

    /// <summary>An RX 6000 graphics card (Navi 21, 22, 23 and 24, gfx103x), by device id or by its name: the cards
    /// HexNeural runs on. Not the RDNA2 APUs <see cref="IsRx6000"/> also counts, which the runtime refuses.</summary>
    public static bool IsRx6000Card(uint vendor, uint device, string name) =>
        vendor == 0x1002
        && (device is >= 0x73A0 and <= 0x73FF or >= 0x7420 and <= 0x743F
            || System.Text.RegularExpressions.Regex.IsMatch(name, @"\bRX\s*6\d{3}\b|\bPRO\s*W6\d{3}\b",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase));

    /// <summary>Where a HexNeural payload file goes in the game. A payload path is two levels deep at most and the
    /// kernels sit four below nr_data_h15\, so the folder part spells the folders with dots, as mochizuki's does:
    /// nr_data_h15.&lt;model&gt;.1280x720.kernels/x.co is nr_data_h15\&lt;model&gt;\1280x720\kernels\x.co.</summary>
    internal static string? HexNeuralDestination(string payloadPath)
    {
        if (HexNeuralTop.Contains(payloadPath, StringComparer.Ordinal)) return payloadPath;
        var slash = payloadPath.IndexOf('/');
        if (slash <= 0) return null;
        var to = payloadPath[..slash].Replace('.', '/') + payloadPath[slash..];
        return Engine.IsHexNeuralData(to) ? to : null;
    }

    private static bool IsHexNeuralName(string name) =>
        HexNeuralTop.Contains(name, StringComparer.Ordinal) || Engine.IsHexNeuralData(name);

    /// <summary>What an install recorded of HexNeural here, its ini aside: what comes out when it is left off.</summary>
    private static IEnumerable<string> HexNeuralRecorded(Manifest? manifest) =>
        manifest?.Entries.Where(e => e.Owned && !e.Configuration && IsHexNeuralName(e.Name)).Select(e => e.Name) ?? [];

    /// <summary>What of HexNeural an earlier install put in and this one does not write again, once the app has said
    /// whether it is wanted here: all of it when it is left off.</summary>
    private static IEnumerable<string> HexNeuralLeaving(PayloadPins pins, Manifest? manifest, IDictionary<string, Blob> files) =>
        pins.HexNeural is null ? [] : HexNeuralRecorded(manifest).Where(n => !files.ContainsKey(n));

    /// <summary>Whether this app installed HexNeural in this game folder and it is still there.</summary>
    public static bool HasHexNeural(string gameDir)
    {
        var dir = ResolveSource(gameDir);
        if (dir.Length == 0 || !Directory.Exists(dir)) return false;
        return HexNeuralRecorded(InstalledManifest(dir)).Contains(HexNeuralRuntimeName)
               && File.Exists(Path.Combine(dir, HexNeuralRuntimeName));
    }

    /// <summary>What the pre-flight says about HexNeural.</summary>
    private static void CheckHexNeural(string dir, string src, PayloadPins pins, bool x86, Report report)
    {
        if (pins.HexNeural != true)
        {
            if (pins.HexNeural == false && dir.Length > 0 && HexNeuralRecorded(InstalledManifest(dir)).Any())
                report.Info("HexNeural is installed here and is left off this time: installing takes it out.");
            return;
        }
        if (pins.HexNeuralFiles.Count == 0)
        {
            report.Err(NoHexNeural);
            return;
        }
        if (x86)
        {
            report.Err("HexNeural runs in 64-bit games only. Leave it off for this one.");
            return;
        }
        if (src.Length == 0 || !Directory.Exists(src)) return;
        var missing = pins.HexNeuralFiles.Keys.Where(n => !File.Exists(Path.Combine(src, n))).ToList();
        if (missing.Count > 0) report.Err($"{Joined(missing)} {(missing.Count == 1 ? "is" : "are")} not in the payload folder.");
        else report.Ok($"HexNeural {pins.HexNeuralVersion} is there: {HexNeuralRuntimeName}, {HexNeuralNetworkName} and "
                       + "nr_data_h15\\ go in beside OptiScaler.");
    }

    /// <summary>HexNeural's files, verified, when ticked, and NrBackend=rdna2 in the ini about to be written.</summary>
    private static void AddHexNeural(SortedDictionary<string, Blob> files, string dir, string src, PayloadPins pins, bool x86,
        string iniName, Report report)
    {
        pins.HexNeuralArrives = false;
        if (pins.HexNeural != true || x86) return;
        pins.HexNeuralArrives = !HasHexNeural(dir);
        if (pins.HexNeuralFiles.Count == 0)
        {
            report.Err(NoHexNeural);
            return;
        }
        foreach (var (path, sha) in pins.HexNeuralFiles)
            if (HexNeuralDestination(path) is { } to && VerifiedPayload(src, path, sha, report) is { } bytes)
                files[to] = bytes;
        if (files.TryGetValue(iniName, out var ini))
            files[iniName] = System.Text.Encoding.UTF8.GetBytes(
                Engine.SetIni(System.Text.Encoding.UTF8.GetString(ini.ToArray()), "DlssNr", "NrBackend", HexNeuralBackend));
    }

    private static bool NamesHexNeural(string backend) =>
        backend.Equals(HexNeuralBackend, StringComparison.OrdinalIgnoreCase) || backend.Equals("hexneural", StringComparison.OrdinalIgnoreCase);

    /// <summary>After an install: a kept OptiScaler.ini names HexNeural when it went in, and stops naming it when it came
    /// out; what the runtime wrote beside the game and its emptied folders go with its files.</summary>
    private static void PointIniAtHexNeural(string dir, PayloadPins pins, bool x86, IReadOnlyCollection<string> recorded, Report report)
    {
        if (x86 || pins.HexNeural is not { } on) return;
        var here = on && File.Exists(Path.Combine(dir, HexNeuralRuntimeName));
        if (!here && recorded.Any(IsHexNeuralName))
        {
            if (!File.Exists(Path.Combine(dir, HexNeuralRuntimeName))) RemoveFile(dir, HexNeuralLog, report);
            PruneHexNeuralData(dir, report);
        }
        var ini = Path.Combine(dir, OptiScalerIni);
        try
        {
            if (!File.Exists(ini)) return;
            var before = File.ReadAllText(ini);
            var named = Engine.Trim(Engine.GetIni(before, "DlssNr", "NrBackend"));
            var after = here ? pins.HexNeuralArrives ? Engine.SetIni(before, "DlssNr", "NrBackend", HexNeuralBackend) : before
                : NamesHexNeural(named) ? Engine.SetIni(before, "DlssNr", "NrBackend",
                    File.Exists(Path.Combine(dir, MochizukiRuntimeName)) ? MochizukiBackend : "auto")
                : before;
            if (after != before) File.WriteAllText(ini, after);
            if (here && !NamesHexNeural(Engine.Trim(Engine.GetIni(after, "DlssNr", "NrBackend"))))
                report.Info($"HexNeural is in, and {OptiScalerIni} keeps the NR runtime it names: to use it, pick RDNA2 (HexNeural) "
                            + "under NR runtime in OptiScaler's Neural tab and restart the game.");
            else if (here)
                report.Info("HexNeural is in (experimental, by Emion, GPL; RX 6000 graphics cards only) and OptiScaler.ini runs it "
                            + "(NrBackend=rdna2). It builds its network from the game's own nvngx_dlssnr.dll. Its settings are "
                            + $"in {HexNeuralIniName} and under RDNA2 network in OptiScaler's Neural tab; its log is {HexNeuralLog}.");
            else if (after != before)
                report.Info($"HexNeural came out, so {OptiScalerIni} no longer names it: NrBackend is "
                            + Engine.Trim(Engine.GetIni(after, "DlssNr", "NrBackend")) + " now.");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            report.Warn($"{OptiScalerIni} could not be updated for HexNeural: {e.Message}");
        }
    }

    /// <summary>nr_data_h15\ and the folders in it, deepest first, each once nothing is left in it.</summary>
    private static void PruneHexNeuralData(string dir, Report report)
    {
        var root = Path.Combine(dir, Engine.HexNeuralData);
        if (!Directory.Exists(root)) return;
        try
        {
            var folders = Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories)
                .OrderByDescending(f => f.Count(c => c == Path.DirectorySeparatorChar)).Append(root).ToList();
            foreach (var folder in folders)
                if (!Directory.EnumerateFileSystemEntries(folder).Any()) Directory.Delete(folder);
            if (!Directory.Exists(root)) report.Ok($"removed {Engine.HexNeuralData}\\");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            report.Warn($"could not remove {Engine.HexNeuralData}: {e.Message}");
        }
    }
}
