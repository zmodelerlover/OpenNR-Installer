using AmdNr.Core;

namespace AmdNr.Core.Tests;

/// <summary>An install left in another folder of the same game, when the executable the install goes beside changed
/// (Phantom Blade Zero, 2026-11-10: v0.9.5 installed beside 8za_v2.exe in ProjectPBZ\Binaries\Win64, and the game
/// is platform\steam\PBZ.exe).</summary>
public class ElsewhereTests
{
    private static (string Root, string Old, string Game) Pbz(string tag)
    {
        var root = Fixture.Temp(tag);
        var old = Directory.CreateDirectory(Path.Combine(root, "ProjectPBZ", "Binaries", "Win64")).FullName;
        var steam = Directory.CreateDirectory(Path.Combine(root, "platform", "steam")).FullName;
        File.WriteAllBytes(Path.Combine(old, "8za_v2.exe"), Fixture.Pe(true));
        File.WriteAllBytes(Path.Combine(steam, "PBZ.exe"), Fixture.PeWithImports(true, ["d3d12.dll", "d3d11.dll"]));
        return (root, Path.Combine(old, "8za_v2.exe"), Path.Combine(steam, "PBZ.exe"));
    }

    private static Dictionary<string, string> Snapshot(string dir) =>
        Fixture.Walk(dir).ToDictionary(f => Path.GetRelativePath(dir, f), Engine.HashFile);

    [Fact]
    public void AnEarlierInstallInAnotherFolderComesOutAndTheNewOneGoesIn()
    {
        var (root, old, game) = Pbz("elsewhere");
        var oldDir = Path.GetDirectoryName(old)!;
        var newDir = Path.GetDirectoryName(game)!;
        File.WriteAllText(Path.Combine(oldDir, "dxgi.dll"), "somebody else's dxgi");
        var (src, pins) = OptiScalerRouteTests.Payloads("elsewhere");
        Assert.False(Work.Install(old, src, Preset.OptiScaler, pins).Failed);
        // And a 32-bit route's record in the root, from further back.
        Transaction.Apply(root, "D3D11", Route.X86, new SortedDictionary<string, Blob>(StringComparer.Ordinal)
        {
            [Work.Addon32Name] = Fixture.Pe(false),
        }, []);

        var tree = new GameTree(root, []);
        Assert.Equal([root, oldDir], Work.InstalledElsewhere(game, Preset.OptiScaler, tree));
        // Nothing is looked for without the game's folder, which is every caller before this.
        Assert.Empty(Work.InstalledElsewhere(game, Preset.OptiScaler, null));

        var report = Work.Install(game, src, Preset.OptiScaler, pins, tree: tree);
        Assert.False(report.Failed, report.ToLog("install"));
        Assert.True(Fixture.HasAny(report, $"An earlier install is in {oldDir}"), report.ToLog("install"));

        Assert.Equal("somebody else's dxgi", File.ReadAllText(Path.Combine(oldDir, "dxgi.dll")));
        Assert.False(File.Exists(Path.Combine(oldDir, Work.WeightsName)));
        Assert.False(Directory.Exists(Path.Combine(oldDir, "OptiScaler")));
        Assert.True(File.Exists(Path.Combine(oldDir, Work.OptiScalerIni)), "its settings stay");
        Assert.True(File.Exists(old), "the folder's own files are not ours");
        Assert.False(File.Exists(Path.Combine(root, Work.Addon32Name)));
        Assert.Null(GameScanner.InstalledAs(oldDir));

        Assert.Equal("stand-in OptiScaler.dll", File.ReadAllText(Path.Combine(newDir, "dxgi.dll")));
        Assert.Equal(RouteFamily.OptiScaler, GameScanner.InstalledAs(newDir));
        Assert.Empty(Work.InstalledElsewhere(game, Preset.OptiScaler, tree));
    }

    [Fact]
    public void UninstallTakesTheEarlierInstallOutToo()
    {
        var (root, old, game) = Pbz("elsewhere-uninstall");
        var (src, pins) = OptiScalerRouteTests.Payloads("elsewhere-uninstall");
        Assert.False(Work.Install(old, src, Preset.OptiScaler, pins).Failed);

        // Nothing is where the game installs now, and the earlier install is what Uninstall is for.
        var tree = new GameTree(root, []);
        var report = Work.Uninstall(game, Preset.OptiScaler, tree: tree);
        Assert.False(report.Failed, report.ToLog("uninstall"));
        Assert.False(Fixture.HasAny(report, "Nothing of ours"), report.ToLog("uninstall"));
        Assert.Null(GameScanner.InstalledAs(Path.GetDirectoryName(old)!));

        Assert.True(Fixture.HasAny(Work.Uninstall(game, Preset.OptiScaler, tree: tree), "Nothing of ours"));
    }

    [Fact]
    public void NothingIsTakenOutWithoutARecord()
    {
        var (root, old, game) = Pbz("elsewhere-no-record");
        var oldDir = Path.GetDirectoryName(old)!;
        // Copied in by hand: every name is one this app writes, and no record says so.
        foreach (var name in new[] { "dxgi.dll", Work.OptiScalerIni, Work.AddonName, Work.RuntimeName, Work.WeightsName, "dlssnr_amd_pass2.dll" })
            File.WriteAllText(Path.Combine(oldDir, name), $"by hand: {name}");
        // A record this build cannot read is not one to act on either.
        var broken = Directory.CreateDirectory(Path.Combine(root, "Engine")).FullName;
        File.WriteAllText(Path.Combine(broken, Engine.ManifestNameX64), "{ not a record");
        File.WriteAllText(Path.Combine(broken, "dxgi.dll"), "whose is this");
        // Nor one an uninstall kept for the settings alone.
        var kept = Directory.CreateDirectory(Path.Combine(root, "platform", "other")).FullName;
        var (src, pins) = OptiScalerRouteTests.Payloads("elsewhere-no-record");
        Assert.False(Work.Install(kept, src, Preset.OptiScaler, pins).Failed);
        Assert.False(Work.Uninstall(kept, Preset.OptiScaler).Failed);
        Assert.True(File.Exists(Path.Combine(kept, Engine.ManifestNameX64)));
        var before = Snapshot(root);

        var tree = new GameTree(root, []);
        Assert.Empty(Work.InstalledElsewhere(game, Preset.OptiScaler, tree));
        var report = Work.Install(game, src, Preset.OptiScaler, pins, tree: tree);
        Assert.False(report.Failed, report.ToLog("install"));
        Assert.False(Fixture.HasAny(report, "An earlier install"), report.ToLog("install"));

        var after = Snapshot(root);
        foreach (var (file, sha) in before) Assert.Equal(sha, after.GetValueOrDefault(file));
        Assert.Equal(RouteFamily.OptiScaler, GameScanner.InstalledAs(Path.GetDirectoryName(game)!));
    }

    /// <summary>The Witcher 3 has witcher3.exe in bin\x64 (D3D11) and in bin\x64_dx12, and either can hold an install.
    /// A folder that is another game in the list (a folder added by hand inside this one) is that game's.</summary>
    [Fact]
    public void AnotherBuildOfTheGameAndAnotherGamesFolderAreLeftAlone()
    {
        var root = Fixture.Temp("elsewhere-twin");
        var dx11 = Directory.CreateDirectory(Path.Combine(root, "bin", "x64")).FullName;
        var dx12 = Directory.CreateDirectory(Path.Combine(root, "bin", "x64_dx12")).FullName;
        File.WriteAllBytes(Path.Combine(dx11, "witcher3.exe"), Fixture.PeWithImports(true, ["d3d11.dll"]));
        File.WriteAllBytes(Path.Combine(dx12, "witcher3.exe"), Fixture.PeWithImports(true, ["d3d12.dll"]));
        var other = Directory.CreateDirectory(Path.Combine(root, "mods", "standalone")).FullName;
        File.WriteAllBytes(Path.Combine(other, "standalone.exe"), Fixture.PeWithImports(true, ["d3d11.dll"]));
        var below = Directory.CreateDirectory(Path.Combine(other, "bin")).FullName;

        var (reshade, reshadePins) = Fixture.Payloads("elsewhere-twin");
        var (src, pins) = OptiScalerRouteTests.Payloads("elsewhere-twin");
        Assert.False(Work.Install(dx11, reshade, Preset.Dx11, reshadePins).Failed);
        Assert.False(Work.Install(other, reshade, Preset.Dx11, reshadePins).Failed);
        Assert.False(Work.Install(below, reshade, Preset.Dx11, reshadePins).Failed);
        var before = new[] { dx11, other }.Select(Snapshot).ToList();

        var game = Path.Combine(dx12, "witcher3.exe");
        var tree = new GameTree(root, [other, Path.Combine(root, "not", "there"), Path.GetDirectoryName(root)!]);
        Assert.Empty(Work.InstalledElsewhere(game, Preset.OptiScaler, tree));
        var report = Work.Install(game, src, Preset.OptiScaler, pins, tree: tree);
        Assert.False(report.Failed, report.ToLog("install"));
        Assert.True(Fixture.HasAny(report, "another build of the game"), report.ToLog("install"));

        Assert.Equal(before[0], Snapshot(dx11));
        Assert.Equal(before[1], Snapshot(other));
        Assert.Equal(RouteFamily.ReShade, GameScanner.InstalledAs(dx11));
        Assert.Equal(RouteFamily.OptiScaler, GameScanner.InstalledAs(dx12));

        // The other game's folder is what kept it: out of the list, its installs are this game's earlier ones.
        Assert.Equal([other, below], Work.InstalledElsewhere(game, Preset.OptiScaler, new GameTree(root, [])));
    }

    [Fact]
    public void TheSearchStaysInTheGameFolderThreeLevelsDownAndFollowsNoLink()
    {
        var (root, _, game) = Pbz("elsewhere-bounds");
        var (src, pins) = Fixture.Payloads("elsewhere-bounds");
        var deep = Directory.CreateDirectory(Path.Combine(root, "a", "b", "c", "d")).FullName;
        var outside = Fixture.Temp("elsewhere-outside");
        Assert.False(Work.Install(deep, src, Preset.Dx11, pins).Failed);
        Assert.False(Work.Install(outside, src, Preset.Dx11, pins).Failed);
        using (var mklink = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
                   "cmd.exe", $"/c mklink /J \"{Path.Combine(root, "linked")}\" \"{outside}\"") { CreateNoWindow = true, UseShellExecute = false }))
            mklink!.WaitForExit();
        Assert.True(File.Exists(Path.Combine(root, "linked", Work.AddonName)));

        Assert.Empty(Work.InstalledElsewhere(game, Preset.OptiScaler, new GameTree(root, [])));
        Assert.Equal([deep], Work.InstalledElsewhere(game, Preset.OptiScaler, new GameTree(Path.Combine(root, "a"), [])));
    }

    [Fact]
    public void AFileHeldOpenInTheEarlierInstallLeavesBothFoldersAsTheyWere()
    {
        var (root, old, game) = Pbz("elsewhere-held");
        var oldDir = Path.GetDirectoryName(old)!;
        var newDir = Path.GetDirectoryName(game)!;
        var (src, pins) = OptiScalerRouteTests.Payloads("elsewhere-held");
        Assert.False(Work.Install(old, src, Preset.OptiScaler, pins).Failed);
        var before = Snapshot(root);
        var tree = new GameTree(root, []);

        // The game still has the proxy loaded: readable, not replaceable.
        using (File.Open(Path.Combine(oldDir, "dxgi.dll"), FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            var refused = Work.Install(game, src, Preset.OptiScaler, pins, tree: tree);
            Assert.True(refused.Failed, refused.ToLog("held"));
            Assert.True(Fixture.HasErr(refused, "dxgi.dll is open"), refused.ToLog("held"));
            Assert.True(Fixture.HasAny(refused, "Nothing was taken out of the earlier install"), refused.ToLog("held"));
        }
        Assert.Equal(before, Snapshot(root));
        Assert.Equal(RouteFamily.OptiScaler, GameScanner.InstalledAs(oldDir));
        Assert.Equal(["PBZ.exe"], Directory.GetFileSystemEntries(newDir).Select(Path.GetFileName));

        // Closed, the same install goes through.
        var report = Work.Install(game, src, Preset.OptiScaler, pins, tree: tree);
        Assert.False(report.Failed, report.ToLog("after"));
        Assert.Null(GameScanner.InstalledAs(oldDir));
        Assert.Equal(RouteFamily.OptiScaler, GameScanner.InstalledAs(newDir));
    }
}
