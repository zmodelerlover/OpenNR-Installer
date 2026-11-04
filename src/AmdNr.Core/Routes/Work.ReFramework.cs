// REFramework, praydog's mod framework for RE Engine games (MIT, github.com/praydog/REFramework), beside either 64-bit
// route: its dinput8.dll and its licence next to the game's executable, under a record of its own
// (amd-nr.reframework.json), so it goes in and comes out on its own whatever the route does. Only when ticked;
// unticked, what this app put in comes out, before the route writes anything.
//
// A dinput8.dll here that is not REFramework (an ASI loader, a ReShade under that name) is never written over: the
// pre-flight says so and REFramework stays out. Another REFramework build the person put in is backed up and
// replaced, and uninstall puts it back. REFramework makes reframework\ beside the game the first time it runs, and
// that is where the person's scripts and plugins go: uninstall takes the folder only when this install found it
// missing and nothing but empty folders is in it. reframework\plugins\ is left to REFramework and the person; a
// plugin of this project's would go there as a recorded file. The settings and lists REFramework writes beside the
// game (re*_fw_config.txt, reframework_*.txt) stay.

namespace AmdNr.Core;

public sealed partial class PayloadPins
{
    /// <summary>The REFramework build picked for this game (its component, "reframework-re9"), its version, and its files
    /// with REFramework's licence by their path in the payload folder. Empty when the payload has none for the game.</summary>
    public string ReFrameworkBuild { get; set; } = string.Empty;
    public string ReFrameworkVersion { get; set; } = string.Empty;
    public IReadOnlyDictionary<string, string> ReFrameworkFiles { get; set; } = new Dictionary<string, string>();

    /// <summary>REFramework ticked (true) or unticked (false) for this game, set by the app; null where it is not
    /// offered, which leaves an install of it alone.</summary>
    public bool? ReFramework { get; set; }
}

public static partial class Work
{
    public const string ReFrameworkDll = "dinput8.dll";
    public const string ReFrameworkLicense = "REFramework-LICENSE.txt";
    private const string ReFrameworkFolder = "reframework";
    private const string ReFrameworkPayload = "reframework/";

    /// <summary>What every REFramework build says when it cannot find the system's dinput8.dll.</summary>
    private static readonly byte[] ReFrameworkMark = "REFramework: Unable to load the original dinput8"u8.ToArray();

    private const string NoReFramework = "The payload list has no REFramework build for this game, so REFramework stays out.";

    /// <summary>Where a REFramework payload file goes in the game: its dinput8.dll and its licence beside the executable.</summary>
    internal static string? ReFrameworkDestination(string payloadPath) => payloadPath switch
    {
        ReFrameworkPayload + ReFrameworkDll => ReFrameworkDll,
        ReFrameworkPayload + "LICENSE.txt" => ReFrameworkLicense,
        _ => null,
    };

    /// <summary>The routes it goes beside: the 64-bit ones but FiveM. RE Engine games are 64-bit D3D11 and D3D12.</summary>
    public static bool ReFrameworkRoute(Preset preset) => preset.Route() == Route.X64 && preset != Preset.FiveM;

    /// <summary>Whether this file is a REFramework build, by the string every build carries. Read in pieces.</summary>
    public static bool IsReFramework(string path)
    {
        try { return File.Exists(path) && Engine.ScanFile(path, b => b.IndexOf(ReFrameworkMark.AsSpan()) >= 0, ReFrameworkMark.Length); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InstallException) { return false; }
    }

    private static Manifest? ReFrameworkRecord(string dir)
    {
        var path = Path.Combine(dir, Engine.ReFrameworkManifestName);
        if (!File.Exists(path)) return null;
        try { return Manifest.Decode(System.Text.Encoding.UTF8.GetString(Engine.Read(path))); }
        catch (InstallException) { return null; }
    }

    /// <summary>Whether this app installed REFramework in this game folder and its dinput8.dll is still the one it wrote.</summary>
    public static bool HasReFramework(string gameDir)
    {
        var dir = ResolveSource(gameDir);
        if (dir.Length == 0 || !Directory.Exists(dir)) return false;
        var dll = Path.Combine(dir, ReFrameworkDll);
        return ReFrameworkRecord(dir)?.Entries.FirstOrDefault(e => e.Name == ReFrameworkDll) is { Owned: true } entry
               && PayloadCache.HashOf(dll) == entry.Hash;
    }

    /// <summary>Why REFramework cannot go in here, or null: ReShade going in under its name, or a dinput8.dll that is not
    /// REFramework. The dinput8.dll is only read.</summary>
    public static string? ReFrameworkBlocked(string gameDir, Preset preset, string? proxy)
    {
        if (!preset.IsOptiScaler() && string.Equals(proxy, ReFrameworkDll, StringComparison.OrdinalIgnoreCase))
            return "ReShade goes in as dinput8.dll, the name REFramework loads under. Pick dxgi.dll as ReShade's name to "
                   + "have both, or leave REFramework off.";
        var dir = ResolveSource(gameDir);
        var dll = Path.Combine(dir, ReFrameworkDll);
        if (dir.Length == 0 || !File.Exists(dll) || IsReFramework(dll)) return null;
        if (InstalledManifest(dir)?.Entries.Any(e => e.Name == ReFrameworkDll && e.Owned && e.Hash != Engine.Sha([])) == true)
            return "dinput8.dll here is the ReShade this app installed, and REFramework loads under that name too. Pick "
                   + "dxgi.dll as ReShade's name and install again to have both.";
        var (isReShade, product, version) = Identify(dll);
        var named = isReShade ? "ReShade" : product is null ? "a file with no version information" : $"{product}{(version is null ? "" : $" {version}")}";
        return $"dinput8.dll here is {named}, not REFramework. REFramework loads under that name, and this app does not "
               + "write over another mod's file: move that dinput8.dll out of the game folder to have REFramework, or "
               + "leave REFramework off.";
    }

    /// <summary>What the pre-flight says about REFramework: whether its files are here, what is in its way, and what
    /// an untick takes out.</summary>
    private static Report CheckReFramework(string gameDir, string payloadDir, Preset preset, PayloadPins pins, string? proxy,
        Report report)
    {
        if (pins.ReFramework is not { } on || !ReFrameworkRoute(preset)) return report;
        var dir = ResolveSource(gameDir);
        if (dir.Length == 0 || !Directory.Exists(dir)) return report;
        if (!on)
        {
            if (ReFrameworkRecord(dir) is not null)
                report.Info("REFramework is installed here and is left off this time: installing takes out what this app put "
                            + "in of it. Scripts and plugins in reframework\\ stay.");
            return report;
        }
        if (pins.ReFrameworkFiles.Count == 0)
        {
            report.Warn(NoReFramework);
            return report;
        }
        if (ReFrameworkBlocked(dir, preset, proxy) is { } why)
        {
            if (!preset.IsOptiScaler() && string.Equals(proxy, ReFrameworkDll, StringComparison.OrdinalIgnoreCase)) report.Err(why);
            else report.Warn(why);
            return report;
        }
        var src = ResolveSource(payloadDir);
        if (src.Length > 0 && Directory.Exists(src)
            && pins.ReFrameworkFiles.Keys.Where(n => !File.Exists(Path.Combine(src, n))).ToList() is { Count: > 0 } missing)
            report.Err($"{Joined(missing)} {(missing.Count == 1 ? "is" : "are")} not in the payload folder.");
        var dll = Path.Combine(dir, ReFrameworkDll);
        report.Info(File.Exists(dll) && !HasReFramework(dir)
            ? $"dinput8.dll here is another REFramework build: it is backed up and REFramework {pins.ReFrameworkVersion} goes in "
              + "in its place, and uninstall puts it back."
            : $"REFramework {pins.ReFrameworkVersion} goes in as dinput8.dll beside the game, with its licence ({ReFrameworkLicense}).");
        return report;
    }

    /// <summary>Before the route: an untick takes REFramework out, so a route that goes in as dinput8.dll finds the name
    /// free; ReShade under REFramework's name is refused. True when the install stops here.</summary>
    private static bool ReFrameworkFirst(string gameDir, Preset preset, PayloadPins pins, string? proxy, Report report)
    {
        if (pins.ReFramework is not { } on || !ReFrameworkRoute(preset)) return false;
        if (on && !preset.IsOptiScaler() && string.Equals(proxy, ReFrameworkDll, StringComparison.OrdinalIgnoreCase))
        {
            report.Err(ReFrameworkBlocked(gameDir, preset, proxy)!);
            report.Info("Nothing was written: fix the problem above and run it again.");
            return true;
        }
        if (!on && ResolveSource(gameDir) is { Length: > 0 } dir && ReFrameworkRecord(dir) is not null)
            UninstallReFramework(dir, report);
        return false;
    }

    /// <summary>After the route, when ticked and the route went in: REFramework's files, verified, through a transaction of
    /// their own. What keeps it out is a warning, not a failure: the route is in either way.</summary>
    private static Report ReFrameworkAfter(string gameDir, string payloadDir, Preset preset, PayloadPins pins, string? proxy,
        Report report)
    {
        if (pins.ReFramework != true || report.Failed || !ReFrameworkRoute(preset)) return report;
        var dir = ResolveSource(gameDir);
        var src = ResolveSource(payloadDir);
        if (pins.ReFrameworkFiles.Count == 0)
        {
            report.Warn(NoReFramework);
            return report;
        }
        if (ReFrameworkBlocked(dir, preset, proxy) is { } why)
        {
            report.Warn(why);
            return report;
        }
        var check = new Report();
        var files = new SortedDictionary<string, Blob>(StringComparer.Ordinal);
        foreach (var (path, sha) in pins.ReFrameworkFiles)
            if (ReFrameworkDestination(path) is { } to && VerifiedPayload(src, path, sha, check) is { } bytes)
                files[to] = bytes;
        if (check.Failed || !files.ContainsKey(ReFrameworkDll))
        {
            foreach (var (_, line) in check.Lines) report.Warn(line);
            report.Warn("REFramework did not go in; the rest did.");
            return report;
        }
        var log = new List<string>();
        try
        {
            Transaction.Apply(dir, Engine.ReFrameworkPreset, Route.X64, files, log,
                manifestName: Engine.ReFrameworkManifestName, created: [ReFrameworkFolder]);
            foreach (var line in log) Narrate(line, report);
        }
        catch (InstallException e)
        {
            foreach (var line in log) Narrate(line, report);
            report.Warn($"REFramework did not go in: {RolledBack(e)}");
            return report;
        }
        report.Info($"REFramework {pins.ReFrameworkVersion} is in (praydog's, MIT licence: {ReFrameworkLicense}). Its scripts and "
                    + "plugins go in reframework\\, which it makes the first time the game runs.");
        if (preset.IsOptiScaler())
            report.Info("REFramework's menu and OptiScaler's both open with Insert: change one of them, REFramework's under "
                        + "Menu Key in its own menu, or OptiScaler's ShortcutKey in OptiScaler.ini.");
        return report;
    }

    /// <summary>Takes back what this app put in of REFramework, through its own record, and reframework\ when this
    /// install found it missing and it is still empty of files. Returns how many records it undid.</summary>
    private static int UninstallReFramework(string dir, Report report)
    {
        if (!File.Exists(Path.Combine(dir, Engine.ReFrameworkManifestName))) return 0;
        var log = new List<string>();
        try
        {
            Transaction.Uninstall(dir, Route.X64, removeConfigs: false, log, manifestName: Engine.ReFrameworkManifestName);
        }
        catch (InstallException e)
        {
            report.Warn($"could not take REFramework out: {e.Message}");
            return 0;
        }
        finally
        {
            foreach (var line in log.Where(l => !l.StartsWith("Uninstall complete", StringComparison.Ordinal))) Narrate(line, report);
        }
        var left = Directory.EnumerateFiles(dir, "*_fw_config.txt").Concat(Directory.EnumerateFiles(dir, "reframework_*.txt"))
            .Select(Path.GetFileName).OfType<string>().ToList();
        if (left.Count > 0) report.Info($"REFramework's own settings and lists stay: {Joined(left)}.");
        return 1;
    }
}
