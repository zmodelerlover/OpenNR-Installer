// danielblnc 0.8.0 on the OptiScaler route is hosted (NeuralHub) and serves DLSS-SR with NVIDIA's own networks, read
// from the nvngx_dlss.dll OptiScaler copies beside it: the one in OptiScaler's library folder (OptiScaler\) first, the
// game's otherwise. A DLSS older than 310 (Red Dead Redemption 2 ships 2.2.10) has no weights it can read, so it falls
// back to its FSR provider and the game crashes once DLSS is picked. Such a game, or one with no nvngx_dlss.dll beside
// it, gets NVIDIA's 310.9.1 in OptiScaler\: the payload's wronggpu-dlss, the file WrongGPU's setup converts from.
// It is recorded like every other file, so uninstall takes it, and so does an install that no longer needs it.
// nvngx_dlssd.dll (ray reconstruction) is left to the game: the payload has no 310 one.

using System.Diagnostics;

namespace AmdNr.Core;

public static partial class Work
{
    private const string HostDlss = "OptiScaler/" + DlssName;

    private static readonly Version HostDlssFloor = new(310, 0);

    /// <summary>The FileVersion of an nvngx_dlss.dll ("2.2.10"), or null when there is none or it says nothing.</summary>
    private static Version? DlssVersion(string path)
    {
        if (!File.Exists(path)) return null;
        try
        {
            var v = FileVersionInfo.GetVersionInfo(path);
            return v.FileMajorPart == 0 && v.FileMinorPart == 0 ? null : new Version(v.FileMajorPart, v.FileMinorPart, v.FileBuildPart);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return null; }
    }

    /// <summary>Why this runtime needs NVIDIA's 310 in OptiScaler\ of this 64-bit game folder, or null: a hosted build,
    /// and the game's own nvngx_dlss.dll missing or older than 310.</summary>
    private static string? HostDlssReason(string dir, Version? runtime)
    {
        if (runtime is null || runtime < HostedRuntime || dir.Length == 0 || !Directory.Exists(dir)
            || GraphicsDetector.Detect(dir).Width == Route.X86) return null;
        var game = Path.Combine(dir, DlssName);
        var version = DlssVersion(game);
        if (version >= HostDlssFloor) return null;
        var whose = $"danielblnc {runtime.ToString(3)}'s DLSS-SR";
        return File.Exists(game)
            ? $"The game's DLSS {version?.ToString() ?? "(no version)"} is too old for {whose}"
            : $"The game has no nvngx_dlss.dll beside it for {whose}";
    }

    /// <summary>Whether the file in OptiScaler\ is one this app did not put there: it stays, whatever it is.</summary>
    private static bool UsersHostDlss(string dir, Manifest? manifest) =>
        File.Exists(Path.Combine(dir, HostDlss)) && manifest?.Entries.Any(e => e.Owned && e.Name == HostDlss) != true;

    /// <summary>Whether an install of this runtime in this game needs NVIDIA's DLSS downloaded for OptiScaler\.</summary>
    public static bool HostNeedsDlss(string gameDir, string runtimeVersion)
    {
        var dir = ResolveSource(gameDir);
        return HostDlssReason(dir, VersionOf(runtimeVersion)) is not null && !UsersHostDlss(dir, InstalledManifest(dir));
    }

    /// <summary>NVIDIA's 310 for OptiScaler\ when this runtime and game need it, with the reason in the report.</summary>
    private static void AddHostDlss(SortedDictionary<string, byte[]> files, string dir, string src, PayloadPins pins,
        Manifest? manifest, Version? runtime, Report report)
    {
        if (HostDlssReason(dir, runtime) is not { } why) return;
        if (UsersHostDlss(dir, manifest))
        {
            report.Info($"{why}; OptiScaler hands it the "
                        + "OptiScaler\\nvngx_dlss.dll already here, which stays as it is.");
            return;
        }
        var staged = pins.WrongGpuDlss.Length > 0 ? Path.Combine(src, pins.WrongGpuDlss) : "";
        if (!File.Exists(staged) || VerifiedPayload(src, pins.WrongGpuDlss, pins.WrongGpuDlssSha, report) is not { } bytes)
        {
            if (!report.Failed)
                report.Warn($"{why}, and NVIDIA's 310 was not downloaded: picking DLSS "
                            + "in the game can crash it. Put a DLSS 310 nvngx_dlss.dll in the OptiScaler folder.");
            return;
        }
        files[HostDlss] = bytes;
        report.Info($"{why}; OptiScaler hands it "
                    + $"{DlssVersion(staged)?.ToString() ?? "310"} (OptiScaler\\nvngx_dlss.dll).");
    }
}
