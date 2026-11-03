using System.Text;
using AmdNr.Core;

namespace AmdNr.Core.Tests;

/// <summary>lmxxf's runtime, kernels and shaders as a component of their own (lmxxf-runtime) beside OptiScaler's package
/// without them (optiscaler-lean): a game without lmxxf downloads neither them nor the full package, which stays in the
/// optiscaler component for the installers already published.</summary>
public class LmxxfRuntimeSplitTests
{
    private static readonly string[] Opti = ["OptiScaler.dll", "OptiScaler.ini", "OptiScaler/libxess.dll"];
    private static readonly string[] Lmxxf = ["LmxxfNrRuntime.dll", "lmxxf-modules/c32_fast.hsaco", "shaders/native_nr.hlsl"];

    private static string Sha(string s) => Engine.Sha(Encoding.UTF8.GetBytes(s));

    private static string Extract(IEnumerable<string> paths) => string.Join(",", paths.Select(p =>
        $"{{\"name\":\"{Path.GetFileName(p)}\",\"path\":\"{p}\",\"size\":1,\"sha256\":\"{Sha($"stand-in {p}")}\"}}"));

    private static string Zip(string name, char sha) =>
        $"\"files\":[{{\"name\":\"{name}\",\"size\":1,\"sha256\":\"{new string(sha, 64)}\",\"url\":\"https://x/{name}\"}}]";

    private static string Releases => $$$"""
        ,"releases":{"optiscaler":[
          {"version":"0.6.0-opennr","components":{
            "optiscaler":{"version":"0.6.0-opennr",{{{Zip("OptiScaler-0.6.0-opennr.zip", '4')}}},"extract":[{{{Extract(Opti.Concat(Lmxxf))}}}]},
            "optiscaler-lean":{"version":"0.6.0-opennr",{{{Zip("optiscaler-lean-0.6.0-opennr.zip", '5')}}},"extract":[{{{Extract(Opti)}}}]},
            "lmxxf-runtime":{"version":"0.6.0-opennr",{{{Zip("lmxxf-runtime-0.6.0-opennr.zip", '6')}}},"extract":[{{{Extract(Lmxxf)}}}]},
            "lmxxf-weights":{"version":"1",{{{Zip("native-game-tiled-assets.zip", '7')}}},"extract":[{{{Extract(["native-game-tiled-assets/block0-ffn.f16"])}}}]} } },
          {"version":"0.5.9-opennr","components":{
            "optiscaler":{"version":"0.5.9-opennr",{{{Zip("OptiScaler-0.5.9-opennr.zip", '8')}}},"extract":[{{{Extract(Opti.Concat(Lmxxf))}}}]},
            "lmxxf-weights":{"version":"1",{{{Zip("native-game-tiled-assets.zip", '7')}}},"extract":[{{{Extract(["native-game-tiled-assets/block0-ffn.f16"])}}}]} } }
        ]}
        """;

    [Fact]
    public void TheLeanPackageStandsInForTheFullOneAndLmxxfComesOnlyWithLmxxf()
    {
        var newest = OptiScalerVersionTests.Payload(Releases).Newest(PayloadManifest.OptiScalerComponent);
        Assert.Equal("optiscaler-lean-0.6.0-opennr.zip", newest.Component(PayloadManifest.OptiScalerComponent).Files[0].Name);
        Assert.False(newest.Has(PayloadManifest.OptiScalerLeanComponent));
        Assert.DoesNotContain(newest.Everyday, p => p.Key == PayloadManifest.LmxxfRuntimeComponent);
        foreach (var path in Lmxxf) Assert.Contains(path, newest.Pins().OptiFiles.Keys);

        // Left out of the game, or a card lmxxf does not run on: nothing of lmxxf is pinned, so nothing of it is fetched.
        foreach (var without in new[] { newest.Without(PayloadManifest.LmxxfComponents), newest.WithoutLmxxf() })
        {
            Assert.False(without.Has(PayloadManifest.LmxxfRuntimeComponent));
            Assert.Equal("optiscaler-lean-0.6.0-opennr.zip", without.Component(PayloadManifest.OptiScalerComponent).Files[0].Name);
            Assert.DoesNotContain(without.Pins().OptiFiles.Keys, k => Lmxxf.Contains(k));
        }

        // A version from before the split keeps its whole package, and no other version's lmxxf goes with it.
        var older = newest.With(newest.Offered(PayloadManifest.OptiScalerComponent).Single(r => r.Version == "0.5.9-opennr"));
        Assert.Equal("OptiScaler-0.5.9-opennr.zip", older.Component(PayloadManifest.OptiScalerComponent).Files[0].Name);
        Assert.False(older.Has(PayloadManifest.LmxxfRuntimeComponent));
        foreach (var path in Lmxxf) Assert.Contains(path, older.Pins().OptiFiles.Keys);
    }

    [Fact]
    public void ItGoesInWithLmxxfAndStaysOutWithout()
    {
        var newest = OptiScalerVersionTests.Payload(Releases).Newest(PayloadManifest.OptiScalerComponent);
        var src = OptiScalerVersionTests.Stage("lmxxf-split", newest, PayloadManifest.OptiScalerComponent,
            PayloadManifest.LmxxfRuntimeComponent, PayloadManifest.LmxxfWeightsComponent);

        var game = Fixture.Temp("lmxxf-split-on");
        File.WriteAllBytes(Path.Combine(game, "Game.exe"), Fixture.PeWithImports(true, ["d3d12.dll"]));
        var report = Work.Install(game, src, Preset.OptiScaler, newest.Pins());
        Assert.False(report.Failed, report.ToLog("install"));
        foreach (var path in Lmxxf)
            Assert.Equal($"stand-in {path}", File.ReadAllText(Path.Combine(game, path)));

        var off = Fixture.Temp("lmxxf-split-off");
        File.WriteAllBytes(Path.Combine(off, "Game.exe"), Fixture.PeWithImports(true, ["d3d12.dll"]));
        report = Work.Install(off, src, Preset.OptiScaler, newest.Without(PayloadManifest.LmxxfComponents).Pins());
        Assert.False(report.Failed, report.ToLog("install"));
        Assert.True(File.Exists(Path.Combine(off, "OptiScaler", "libxess.dll")));
        Assert.False(File.Exists(Path.Combine(off, "LmxxfNrRuntime.dll")));
        Assert.False(Directory.Exists(Path.Combine(off, "lmxxf-modules")));
        Assert.False(Directory.Exists(Path.Combine(off, "shaders")));
    }

    [Fact]
    public void APayloadWithTheLeanPackageAtTheTopReadsItThereToo()
    {
        var manifest = PayloadManifest.Parse($$$"""
            {"schema":1,"components":{
              "addon":{"version":"1",{{{Zip("amd-nr.addon64", '1')}}}},
              "optiscaler":{"version":"0.6.0-opennr",{{{Zip("OptiScaler-0.6.0-opennr.zip", '4')}}},"extract":[{{{Extract(Opti.Concat(Lmxxf))}}}]},
              "optiscaler-lean":{"version":"0.6.0-opennr",{{{Zip("optiscaler-lean-0.6.0-opennr.zip", '5')}}},"extract":[{{{Extract(Opti)}}}]}
            }}
            """);
        Assert.Equal("optiscaler-lean-0.6.0-opennr.zip", manifest.Component(PayloadManifest.OptiScalerComponent).Files[0].Name);
        Assert.False(manifest.Has(PayloadManifest.OptiScalerLeanComponent));
    }
}
