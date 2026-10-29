// The mochizuki runtime, the third one OptiScaler can drive from 0.4.0 on, and the second one the
// ReShade add-on can drive from v0.6.8 on: DLSS 5's NR network as
// Vulkan compute shaders (mochizuki0323/DLSSNR-AMD), run on a Vulkan device of its own beside the
// game's D3D12. It needs RDNA4's FP8 matrix instructions, and it goes in on every RX 9000 card (the
// app decides, by the card it reads), and on an RX 7000 when ticked there (an OptiScaler release
// with mochizuki-rdna3: mauri870's port of the network to FP16 matrices, experimental), and on no
// other. An install without it takes out what an earlier one put in, so the card is what the folder
// holds. On the ReShade routes the add-on runs it when amd-nr.ini names no runtime; on OptiScaler a
// fresh OptiScaler.ini runs lmxxf, with danielblnc and mochizuki beside it. Either is switched under NR runtime in the add-on's panel or OptiScaler's
// Neural tab.
//
// Everything else is the OptiScaler route's own: the same transaction and manifest, every file
// pinned, and uninstall taking back what went in.

namespace AmdNr.Core;

public static partial class Work
{
    /// <summary>The runtime itself, beside OptiScaler, which loads it by this name.</summary>
    internal const string MochizukiRuntimeName = "MochizukiNrRuntime.dll";

    /// <summary>What OptiScaler.ini names this runtime by: [DlssNr] NrBackend=mochizuki.</summary>
    internal const string MochizukiBackend = "mochizuki";

    /// <summary>The runtime's log, beside the DLL. Swept on uninstall with the others: see Droppings.</summary>
    internal const string MochizukiLog = "mochizuki_nr.log";

    /// <summary>The Vulkan pipeline cache the runtime writes into dlssnr-amd\ while a game runs.</summary>
    private const string MochizukiPipelineCache = "pipeline.cache";

    /// <summary>Where the runtime keeps ACO's network once imported (OptiScaler's MochizukiAco): one .nrb a pipeline.</summary>
    private const string MochizukiAcoCache = "aco-cache";

    /// <summary>Where one mochizuki payload file goes in the game folder. A payload path may be two
    /// levels deep at most -- every app already out there refuses a whole manifest that has a deeper
    /// one -- and the runtime reads files three levels down, so the folder part of a payload path
    /// spells the folders with dots: dlssnr-amd.shaders.runtime/x.spv is dlssnr-amd\shaders\runtime\x.spv.
    /// The runtime and the model need no spelling: MochizukiNrRuntime.dll, dlssnr-amd/dlssnr.bin.</summary>
    internal static string MochizukiDestination(string payloadPath)
    {
        var slash = payloadPath.IndexOf('/');
        return slash < 0 ? payloadPath : payloadPath[..slash].Replace('.', '/') + payloadPath[slash..];
    }

    /// <summary>The components the mochizuki runtime comes in: the runtime, its model, its second model, and its RX 7000
    /// network.</summary>
    internal static readonly string[] MochizukiComponents =
        [PayloadManifest.MochizukiComponent, PayloadManifest.MochizukiModelComponent, PayloadManifest.MochizukiSmallModelComponent,
         PayloadManifest.MochizukiRdna3Component];

    /// <summary>Where the RX 7000 network's shaders go: dlssnr-amd\shaders-rdna3\, its runtime\ and temporal\ inside.</summary>
    internal const string MochizukiRdna3Folder = Engine.MochizukiFolder + "/shaders-rdna3/";

    /// <summary>An RX 7000 card (RDNA3, Navi 31, 32 and 33: device ids 0x74xx) or an RDNA3 / RDNA 3.5 APU (Radeon
    /// 740M-780M, 820M-890M, 8040S-8060S), by device id or by its name: the GPUs mochizuki's RX 7000 network is offered
    /// on, experimental and off unless ticked. The Radeon 8060S runs it as an RX 7000 does. RDNA2 APUs (610M, 660M,
    /// 680M) are not among them.</summary>
    public static bool IsRx7000(uint vendor, uint device, string name) =>
        vendor == 0x1002
        && ((device & 0xFF00) == 0x7400 || Rdna3Apus.Contains(device)
            || System.Text.RegularExpressions.Regex.IsMatch(name,
                @"\bRX\s*7\d{3}\b|\bPRO\s*W7\d{3}\b|\bRadeon(\s*\(TM\))?\s*(7[4-8]0M|8[2-9]0M|80[4-6]0S)\b",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase));

    /// <summary>Phoenix, Phoenix 2, Hawk Point, Strix Point, Krackan Point and Strix Halo.</summary>
    private static readonly uint[] Rdna3Apus = [0x15BF, 0x15C8, 0x1900, 0x150E, 0x1114, 0x1586];

    /// <summary>Whether this app installed mochizuki's RX 7000 network in this game folder: what the sheet goes by
    /// when nobody has said, on this machine, whether they want it.</summary>
    public static bool HasMochizukiRdna3(string gameDir)
    {
        var dir = ResolveSource(gameDir);
        if (dir.Length == 0 || !Directory.Exists(dir)) return false;
        return new[] { Route.X64, Route.X86 }.Any(route =>
            InstalledManifest(dir, route)?.Entries.Any(e => e.Name.StartsWith(MochizukiRdna3Folder, StringComparison.Ordinal)) == true);
    }

    /// <summary>A name in the game folder that is the mochizuki runtime's: the runtime, or anything
    /// in its dlssnr-amd tree.</summary>
    private static bool IsMochizukiName(string name) =>
        name == MochizukiRuntimeName || name.StartsWith(Engine.MochizukiFolder + "/", StringComparison.Ordinal);

    /// <summary>What an install recorded of mochizuki in this folder. An install hands these to the
    /// transaction to take out when it does not write them again: all of them when mochizuki is left
    /// off, and those an older build had and this one has not when it is on.</summary>
    private static List<string> MochizukiRecorded(Manifest? manifest) =>
        manifest?.Entries.Where(e => !e.Configuration && IsMochizukiName(e.Name)).Select(e => e.Name).ToList() ?? [];

    /// <summary>Whether this app installed the mochizuki runtime in this game folder and it is still
    /// there: what the sheet goes by when nobody has said, on this machine, whether they want it.
    /// Without it, a game added again, or the app on another PC, would leave the box off and the next
    /// update would take mochizuki out.</summary>
    public static bool HasMochizuki(string gameDir)
    {
        var dir = ResolveSource(gameDir);
        if (dir.Length == 0 || !Directory.Exists(dir)) return false;
        return new[] { Route.X64, Route.X86 }.Any(route =>
                   InstalledManifest(dir, route)?.Entries.Any(e => e.Name == MochizukiRuntimeName) == true)
               && File.Exists(Path.Combine(dir, MochizukiRuntimeName));
    }

    private const string MochizukiComesOut =
        "mochizuki is installed here and is left off this time, so installing takes out what this app put in "
        + "of it: MochizukiNrRuntime.dll and its dlssnr-amd folder.";

    /// <summary>After an install that took mochizuki out: what its runtime wrote beside it, its empty
    /// folders, and a word when the OptiScaler.ini left in place still names it.</summary>
    private static void AfterMochizukiRetired(string dir, Report report)
    {
        report.Info(File.Exists(Path.Combine(dir, MochizukiRuntimeName))
            ? $"mochizuki was left off. The {MochizukiRuntimeName} here stays: this app did not put it there, "
              + "or it was changed since."
            : "mochizuki was left off, so what this app had put in of it came out.");
        AfterMochizukiUninstall(dir, report);
        PruneEmpty(dir, [Engine.BackupDir], report);
        var ini = Path.Combine(dir, OptiScalerIni);
        if (File.Exists(ini) && !File.Exists(Path.Combine(dir, MochizukiRuntimeName))
            && string.Equals(Engine.Trim(Engine.GetIni(File.ReadAllText(ini), "DlssNr", "NrBackend")), MochizukiBackend,
                StringComparison.OrdinalIgnoreCase))
            report.Warn(
                $"{OptiScalerIni} holds your settings and still names mochizuki as the NR runtime, which is not "
                + "here any more. Pick daniel or lmxxf under NR runtime in OptiScaler's Neural tab, or tick mochizuki "
                + "and install again.");
    }

    /// <summary>The mochizuki files that are not yet in the game folder at their size, and how big.</summary>
    private static ulong MochizukiNeed(string src, string dir, PayloadPins pins) =>
        pins.MochizukiFiles.Keys
            .Select(p => (Size: Engine.SizeOf(Path.Combine(src, p)) ?? 0, Destination: MochizukiDestination(p)))
            .Where(f => Engine.SizeOf(Path.Combine(dir, f.Destination)) != f.Size)
            .Aggregate(0UL, (sum, f) => sum + f.Size);

    /// <summary>The names the running game would hold open, which fail the copy.</summary>
    private static IEnumerable<string> MochizukiHeld(string dir) =>
        new[] { MochizukiRuntimeName, Engine.MochizukiFolder + "/dlssnr.bin" }
            .Where(n => Engine.IsLocked(Path.Combine(dir, n)));

    /// <summary>What the pre-flight says about mochizuki once it has been asked for.</summary>
    private static void CheckMochizukiPayload(string src, PayloadPins pins, Report report)
    {
        if (pins.MochizukiFiles.Count == 0)
        {
            report.Err(NoMochizuki);
            return;
        }
        if (src.Length == 0 || !Directory.Exists(src)) return;
        var missing = pins.MochizukiFiles.Keys.Where(n => !File.Exists(Path.Combine(src, n))).ToList();
        if (missing.Count > 0)
            report.Err($"{Joined(missing)} {(missing.Count == 1 ? "is" : "are")} not in the payload folder.");
        else
            report.Ok("The mochizuki runtime, its shaders and its model are there. Installing verifies the SHA-256 of each one.");
    }

    private const string NoMochizuki =
        "The version chosen has no mochizuki runtime. Pick OptiScaler 0.4.0-amd-nr or the add-on v0.6.8 or newer, "
        + "or leave mochizuki off.";

    private const string MochizukiPickInAddon =
        "It runs the network unless amd-nr.ini names another runtime: NR runtime in the add-on's panel (Home) "
        + "switches to danielblnc, from the next start of the game.";

    /// <summary>Every mochizuki file, verified against its pin, under the name it takes in the game.</summary>
    private static void AddMochizuki(SortedDictionary<string, byte[]> files, string src, PayloadPins pins, Report report)
    {
        foreach (var (path, sha) in pins.MochizukiFiles)
            if (VerifiedPayload(src, path, sha, report) is { } bytes)
                files[MochizukiDestination(path)] = bytes;
    }

    private const string MochizukiInstalled =
        "The mochizuki runtime went in too: MochizukiNrRuntime.dll, and dlssnr-amd\\ with its shaders, its prewarm "
        + "list and its model. It is experimental and runs on RDNA4 cards (RX 9000 series, AMD driver 25.10 or "
        + "newer). The first start in each game compiles its network for a few seconds before NR shows; its log is "
        + MochizukiLog + ".";

    private const string MochizukiRdna3Installed =
        "The RX 7000 network went in too (dlssnr-amd\\shaders-rdna3, experimental): the same network on FP16 matrices, "
        + "slower than on an RX 9000 and about 2 GB more VRAM at 1080p. Before its first build the runtime checks the "
        + "card's matrix layout and compiles every pipeline once apart from the game; if either fails NR stays off and "
        + "OptiScaler's mochizuki page says why.";

    /// <summary>What the mochizuki runtime writes beside its files while a game runs -- the pipeline
    /// cache, ACO's imported network, int4 mixed's own cache and binaries, the marker of a network build that never
    /// finished (build.pending, or build.failed as the add-on renames it), the mark of a game whose colour switches float
    /// formats (float-colour-shared), the RX 7000 network's pipeline cache, prewarm list and check (rdna3-check.txt, and
    /// the child's list and log in rdna3-check\), and the temporary file a write cut short
    /// leaves -- once the runtime itself is gone, and then its folders, deepest first, each only when nothing is left in it. With the runtime still
    /// here (the game has it open, or it was not this app's) nothing of it is touched.</summary>
    private static void AfterMochizukiUninstall(string dir, Report report)
    {
        var root = Path.Combine(dir, Engine.MochizukiFolder);
        if (!Directory.Exists(root) || File.Exists(Path.Combine(dir, MochizukiRuntimeName))) return;
        try
        {
            // int4 mixed keeps a pipeline cache of its own and its rewritten pipeline binaries under int4\.
            var acoCache = Path.Combine(root, MochizukiAcoCache);
            var int4Binaries = Path.Combine(root, "int4", "pipeline-binaries");
            var rdna3Check = Path.Combine(root, "rdna3-check");
            foreach (var file in Directory.EnumerateFiles(root).Concat(Leaves(Path.Combine(root, "prewarm")))
                         .Concat(Leaves(Path.Combine(root, "int4"))).Concat(Leaves(acoCache))
                         .Concat(Leaves(int4Binaries)).Concat(Leaves(rdna3Check)).ToList())
            {
                var name = Path.GetFileName(file);
                var folder = Path.GetDirectoryName(file);
                var written = name is MochizukiPipelineCache or "pipeline-rdna3.cache" or "manifest-rdna3.txt"
                              || folder == rdna3Check
                              || folder == root && name is "build.pending" or "build.failed" or "float-colour-shared"
                                  or "rdna3-check.txt"
                              || folder == int4Binaries
                              || folder == acoCache
                              && (name.EndsWith(".nrb", StringComparison.OrdinalIgnoreCase)
                                  || name.EndsWith(".nrb.tmp", StringComparison.OrdinalIgnoreCase))
                              || name.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase)
                              && (name.StartsWith(MochizukiPipelineCache + ".", StringComparison.Ordinal)
                                  || name.StartsWith("manifest.txt.", StringComparison.Ordinal));
                if (!written) continue;
                Engine.Writable(file);
                File.Delete(file);
                report.Ok($"removed {Engine.MochizukiFolder}\\{Path.GetRelativePath(root, file)}");
            }

            // Deepest first, four levels below dlssnr-amd at most (aco\int4\shaders\temporal), which is as deep as
            // anything of ours goes.
            foreach (var folder in Below(root, 4).Append(root))
            {
                if (!Directory.Exists(folder) || Directory.EnumerateFileSystemEntries(folder).Any()) continue;
                Directory.Delete(folder);
                if (folder == root) report.Ok($"removed {Engine.MochizukiFolder}\\");
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            report.Warn($"could not remove everything in {Engine.MochizukiFolder}: {e.Message}");
        }

        static IEnumerable<string> Leaves(string folder) =>
            Directory.Exists(folder) ? Directory.EnumerateFiles(folder) : [];

        static IEnumerable<string> Below(string folder, int levels) =>
            levels == 0 ? [] : Directory.GetDirectories(folder).SelectMany(d => Below(d, levels - 1).Append(d));
    }
}
