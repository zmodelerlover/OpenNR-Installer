namespace AmdNr.Core.Tests;

/// <summary>danielblnc 0.8.0, NeuralHub's host module: OptiScaler 0.5.7 on runs it, the add-on never does. It is
/// offered beside the default on every card and is the default on none, and it goes in as pass 1 alone.</summary>
public class Runtime080Tests
{
    private const string Raw080 = "be9172b68d0e77f98f04f941f4e2abe2c73eb01b8aafe94b26ee31eefefb5eb8";

    private static PayloadManifest Shipped() =>
        PayloadManifest.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "payload.json")));

    private static ComponentRelease V080() => Shipped().RuntimeVersions().Single(r => r.Version == "0.8.0");

    [Fact]
    public void ItRunsOnOptiScalerFrom057AndNeverOnTheReShadeRoutes()
    {
        var v080 = V080();
        Assert.False(v080.Components.ContainsKey(PayloadManifest.RuntimeComponent));
        Assert.DoesNotContain(Shipped().Releases![PayloadManifest.RuntimeComponent], r => r.Version == "0.8.0");
        Assert.False(Work.RuntimeRunsOn(v080, optiScaler: true, "0.5.6-opennr"));
        Assert.True(Work.RuntimeRunsOn(v080, optiScaler: true, "0.5.7-opennr"));
        Assert.False(Work.RuntimeRunsOn(v080, optiScaler: false, "0.7.14"));
        Assert.False(Work.RuntimeRunsOn(v080, optiScaler: false, "9.9.9"));
        Assert.Equal(new Version(0, 8, 0), Work.AcceptedRuntime(Raw080, "0.5.7-opennr"));
        Assert.Null(Work.AcceptedRuntime(Raw080, "0.5.6-opennr"));
        Assert.True(Work.RuntimeSized(101_768_704));
    }

    private static List<ComponentRelease> Runs(bool opti, string version) =>
        Shipped().RuntimeVersions().Where(r => Work.RuntimeRunsOn(r, opti, version)).ToList();

    [Fact]
    public void TheDefaultStaysOnEveryCard()
    {
        // RX 9000: every version, 0.4.3 the default, 0.8.0 unstable like the rest.
        var rdna4 = Work.RuntimeOffer(Runs(true, "0.5.7-opennr"), rdna4: true);
        Assert.Equal(["0.8.0", "0.6.0", "0.5.1", "0.5.0", "0.4.3"], rdna4.Select(r => r.Version));
        Assert.Equal("0.4.3", Work.RuntimePick(rdna4, null, rdna4: true));
        Assert.Equal("0.8.0", Work.RuntimePick(rdna4, "0.8.0", rdna4: true));
        Assert.Equal(Work.RuntimeBadge.Unstable, Work.BadgeFor("0.8.0", rdna4: true));
        Assert.NotNull(Work.UnstableOnRdna4("0.8.0", rdna4: true));

        // Any other card: 0.6.0 the default, 0.8.0 beside it, both untagged.
        var other = Work.RuntimeOffer(Runs(true, "0.5.7-opennr"), rdna4: false);
        Assert.Equal(["0.8.0", "0.6.0"], other.Select(r => r.Version));
        Assert.Equal("0.6.0", Work.RuntimeDefault(other, rdna4: false));
        Assert.Equal("0.6.0", Work.RuntimePick(other, null, rdna4: false));
        Assert.Equal("0.8.0", Work.RuntimePick(other, "0.8.0", rdna4: false));
        Assert.Equal(Work.RuntimeBadge.None, Work.BadgeFor("0.8.0", rdna4: false));

        // Before 0.5.7, and on the add-on, nothing changes: the newest only, and the release's own pin.
        var older = Work.RuntimeOffer(Runs(true, "0.5.6-opennr"), rdna4: false);
        Assert.Equal(["0.6.0"], older.Select(r => r.Version));
        Assert.Null(Work.RuntimePick(older, "0.8.0", rdna4: false));
        foreach (var rdna in new[] { true, false })
            Assert.DoesNotContain("0.8.0", Work.RuntimeOffer(Runs(false, "0.7.14"), rdna).Select(r => r.Version));
        Assert.Null(Work.RuntimePick(Work.RuntimeOffer(Runs(false, "0.7.14"), false), "0.8.0", rdna4: false));
    }

    [Fact]
    public void PickingItChangesOnlyOptiScalersRuntime()
    {
        var shipped = Shipped().Newest(PayloadManifest.OptiScalerComponent);
        var plain = shipped.Pins();
        var pins = shipped.WithRuntime("0.8.0").Pins();
        Assert.Equal(Raw080, pins.OptiRuntimeSha);
        Assert.Equal("0.8.0", pins.OptiRuntimeVersion);
        Assert.Equal(101_768_704UL, pins.OptiRuntimeSize);
        Assert.Equal(plain.RuntimeSha, pins.RuntimeSha);
        Assert.Equal(plain.WeightsSha, pins.WeightsSha);
        Assert.Equal(Engine.RuntimeSha, Engine.Lower(Shipped().Pins().RuntimeSha));
    }

    [Fact]
    public void ItGoesInAsPass1AloneWithTheWeightsAndTheHostFolderGoesOnUninstall()
    {
        var game = Fixture.Temp("runtime-080");
        File.WriteAllBytes(Path.Combine(game, "Game.exe"), Fixture.PeWithImports(true, ["d3d12.dll"]));
        File.WriteAllText(Path.Combine(game, "nvngx_dlss.dll"), "dlss");

        var (src060, pins060) = OptiScalerRouteTests.Payloads("runtime-080-060", runtimeVersion: "0.6.0");
        var report = Work.Install(game, src060, Preset.OptiScaler, pins060);
        Assert.False(report.Failed, report.ToLog("install 0.6.0"));
        Assert.All(Work.OptiPasses, p => Assert.True(File.Exists(Path.Combine(game, p)), p));

        var (src, pins) = OptiScalerRouteTests.Payloads("runtime-080", runtimeVersion: "0.8.0");
        Assert.True(Work.Preflight(game, src, Preset.OptiScaler, pins).Lines.All(l => l.Level != Level.Err));
        report = Work.Install(game, src, Preset.OptiScaler, pins);
        Assert.False(report.Failed, report.ToLog("install 0.8.0"));
        Assert.Equal(Engine.Sha(File.ReadAllBytes(Path.Combine(src, pins.OptiRuntimeName))),
            Engine.HashFile(Path.Combine(game, "dlssnr_amd_pass1.dll")));
        Assert.False(File.Exists(Path.Combine(game, "dlssnr_amd_pass2.dll")));
        Assert.False(File.Exists(Path.Combine(game, "dlssnr_amd_pass3.dll")));
        Assert.True(File.Exists(Path.Combine(game, Work.WeightsName)));

        // What OptiScaler writes for it while the game runs.
        var host = Directory.CreateDirectory(Path.Combine(game, "dlssnr_amd_host")).FullName;
        File.WriteAllText(Path.Combine(host, "neuralhub_host.dll"), "hosted copy");
        File.WriteAllText(Path.Combine(host, "neuralhub.ini"), "[Upscaling]");
        report = Work.Uninstall(game, Preset.OptiScaler);
        Assert.False(report.Failed, report.ToLog("uninstall"));
        Assert.False(Directory.Exists(host));
        Assert.DoesNotContain(Work.OptiPasses, p => File.Exists(Path.Combine(game, p)));
    }
}
