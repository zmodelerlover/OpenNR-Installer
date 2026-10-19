using System.Text;
using AmdNr.Core;

namespace AmdNr.Core.Tests;

/// <summary>lmxxf 0.44's Small model: its own component, in native-game-tiled-assets\small\ beside the weights, under a
/// payload path the installers already out there can read.</summary>
public class LmxxfSmallTests
{
    private static readonly string[] SmallPaths =
        ["native-game-tiled-assets.small/block0-ffn.f16", "native-game-tiled-assets.small/model-manifest.json"];

    private static string Sha(string s) => Engine.Sha(Encoding.UTF8.GetBytes(s));

    private static string Extract(IEnumerable<string> paths) => string.Join(",", paths.Select(p =>
        $"{{\"name\":\"{Path.GetFileName(p)}\",\"path\":\"{p}\",\"size\":1,\"sha256\":\"{Sha($"stand-in {p}")}\"}}"));

    private static string Releases(IEnumerable<string> small) => $$$"""
        ,"releases":{"optiscaler":[{"version":"0.5.6-opennr","components":{
          "optiscaler":{"version":"0.5.6-opennr","files":[{"name":"o56.zip","size":1,"sha256":"{{{new string('4', 64)}}}","url":"https://x/o56"}],
            "extract":[{{{Extract(["OptiScaler.dll", "OptiScaler.ini", "LmxxfNrRuntime.dll", "lmxxf-modules/SHA256SUMS"])}}}]},
          "lmxxf-weights":{"version":"1","files":[{"name":"native-game-tiled-assets.zip","size":1,"sha256":"{{{new string('5', 64)}}}","url":"https://x/w"}],
            "extract":[{{{Extract(["native-game-tiled-assets/block0-ffn.f16"])}}}]},
          "lmxxf-weights-small":{"version":"1","files":[{"name":"lmxxf-weights-small-1.zip","size":1,"sha256":"{{{new string('6', 64)}}}","url":"https://x/s"}],
            "extract":[{{{Extract(small)}}}]}
        }}]}
        """;

    [Fact]
    public void APayloadCarryingItParsesAndADeeperPathWouldNot()
    {
        var manifest = OptiScalerVersionTests.Payload(Releases(SmallPaths));
        Assert.DoesNotContain(manifest.Everyday, p => p.Key == PayloadManifest.LmxxfSmallWeightsComponent);
        var pins = manifest.Newest(PayloadManifest.OptiScalerComponent).Pins();
        foreach (var path in SmallPaths) Assert.Contains(path, pins.OptiFiles.Keys);
        var elsewhere = manifest.Newest(PayloadManifest.OptiScalerComponent).WithoutLmxxf().Pins();
        Assert.DoesNotContain(elsewhere.OptiFiles.Keys, k => k.StartsWith(Work.LmxxfSmallPayload, StringComparison.Ordinal));
        // Every installer checks every path of every release the same way, so the folder below is spelled with a dot.
        Assert.Throws<InstallException>(() => OptiScalerVersionTests.Payload(Releases(["native-game-tiled-assets/small/block0-ffn.f16"])));
    }

    [Fact]
    public void TheSmallFolderIsAllowedAndNothingElseBelowTheWeights()
    {
        Assert.Equal("native-game-tiled-assets/small/block0-ffn.f16",
            Work.OptiDestination("native-game-tiled-assets.small/block0-ffn.f16", "dxgi.dll"));
        Assert.True(Engine.IsAllowed("native-game-tiled-assets/small/block0-ffn.f16"));
        Assert.True(Engine.IsAllowed("native-game-tiled-assets/small/model-manifest.json"));
        foreach (var name in new[]
                 {
                     "native-game-tiled-assets/small/sub/x.f16", "native-game-tiled-assets/other/x.f16",
                     "native-game-tiled-assets/small/../dxgi.dll", "native-game-tiled-assets/small/",
                 })
            Assert.False(Engine.IsAllowed(name), name);
    }

    [Fact]
    public void ItGoesInBelowTheWeightsAndUninstallTakesItAndItsFolder()
    {
        var game = Fixture.Temp("lmxxf-small-install");
        File.WriteAllBytes(Path.Combine(game, "Game.exe"), Fixture.PeWithImports(true, ["d3d12.dll"]));
        var manifest = OptiScalerVersionTests.Payload(Releases(SmallPaths)).Newest(PayloadManifest.OptiScalerComponent);
        var src = OptiScalerVersionTests.Stage("lmxxf-small", manifest, PayloadManifest.OptiScalerComponent,
            PayloadManifest.LmxxfWeightsComponent, PayloadManifest.LmxxfSmallWeightsComponent);
        var pins = manifest.Pins();
        Assert.False(Work.Preflight(game, src, Preset.OptiScaler, pins).Failed);
        var report = Work.Install(game, src, Preset.OptiScaler, pins);
        Assert.False(report.Failed, report.ToLog("install"));
        var small = Path.Combine(game, "native-game-tiled-assets", "small");
        Assert.Equal("stand-in native-game-tiled-assets.small/block0-ffn.f16", File.ReadAllText(Path.Combine(small, "block0-ffn.f16")));
        Assert.Equal("stand-in native-game-tiled-assets/block0-ffn.f16",
            File.ReadAllText(Path.Combine(game, "native-game-tiled-assets", "block0-ffn.f16")));
        Assert.False(Work.PayloadMovedOn(game, OptiScalerVersionTests.Payload(Releases(SmallPaths))));

        var gone = Work.Uninstall(game, Preset.OptiScaler);
        Assert.False(gone.Failed, gone.ToLog("uninstall"));
        Assert.False(Directory.Exists(Path.Combine(game, "native-game-tiled-assets")), string.Join(", ", Fixture.Walk(game)));
    }
}
