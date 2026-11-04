using System.Text;
using AmdNr.Core;

namespace AmdNr.Core.Tests;

/// <summary>mochizuki's RX 6000 network (mochizuki-rdna2): shaders-rdna2 and its prewarm list in a component of their
/// own, the same way as the RX 7000 one, and what its runtime writes taken back on uninstall.</summary>
public partial class MochizukiTests
{
    private static readonly string[] Rdna2Paths =
    [
        "dlssnr-amd.prewarm/manifest-rdna2.txt", "dlssnr-amd.shaders-rdna2/g_attn.spv",
        "dlssnr-amd.shaders-rdna2.runtime/runtime_blit.spv", "dlssnr-amd.shaders-rdna2.temporal/temporal_post_fp32h16.spv",
    ];

    private static (string Src, PayloadManifest Manifest) Rdna2Staged(string label)
    {
        var manifest = PayloadManifest.Parse($$$"""
            {"schema":1,"components":{
              "addon":{"version":"1","files":[{"name":"amd-nr.addon64","size":1,"sha256":"{{{new string('1', 64)}}}","url":"https://x/a"}]},
              "runtime":{"version":"1","files":[
                {"name":"dlssnr_amd_pass1.dll","size":1,"sha256":"{{{new string('2', 64)}}}","url":"https://x/r"},
                {"name":"dlssnr_on_amd_weights.bin","size":1,"sha256":"{{{Sha("stand-in weights")}}}","url":"https://x/w"}]},
              "optiscaler":{"version":"0.1.1-amd-nr","files":{{{Zip("o11.zip")}}},"extract":[{{{Pinned(OptiPaths.Take(2), "0.1.1")}}}]},
              "opti-runtime":{"version":"0.3.1","files":[{"name":"{{{RuntimeFile}}}","size":1,"sha256":"{{{Sha("stand-in 0.3.1 runtime")}}}","url":"https://x/p"}]}
            },"releases":{"optiscaler":[
              {"version":"0.6.0-opennr","components":{
                "optiscaler":{"version":"0.6.0-opennr","files":{{{Zip("o60.zip")}}},"extract":[{{{Pinned(OptiPaths, "0.4.0")}}}]},
                "mochizuki":{"version":"0.6.0-opennr","files":{{{Zip("mochizuki-0.6.0-opennr.zip")}}},"extract":[{{{Pinned(MochizukiPaths, "0.4.0")}}}]},
                "mochizuki-model":{"version":"1","files":[{"name":"dlssnr.bin","path":"{{{ModelPath}}}","size":{{{Encoding.UTF8.GetByteCount(Content(ModelPath, "model"))}}},"sha256":"{{{Sha(Content(ModelPath, "model"))}}}","url":"https://x/m"}]},
                "mochizuki-rdna3":{"version":"0.6.0-opennr","files":{{{Zip("mochizuki-rdna3-0.6.0-opennr.zip")}}},"extract":[{{{Pinned(Rdna3Paths, "rdna3")}}}]},
                "mochizuki-rdna2":{"version":"0.6.0-opennr","files":{{{Zip("mochizuki-rdna2-0.6.0-opennr.zip")}}},"extract":[{{{Pinned(Rdna2Paths, "rdna2")}}}]}
              }}
            ]}}
            """).Newest(PayloadManifest.OptiScalerComponent);
        var src = Fixture.Temp($"mochizuki-rdna2-src-{label}");
        foreach (var name in new[] { PayloadManifest.OptiScalerComponent, PayloadManifest.MochizukiComponent,
                     PayloadManifest.MochizukiModelComponent, PayloadManifest.MochizukiRdna3Component, PayloadManifest.MochizukiRdna2Component })
            foreach (var file in manifest.Component(name).Installed)
            {
                var full = Path.Combine(src, file.RelativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(full)!);
                File.WriteAllText(full, Content(file.RelativePath, name switch
                {
                    PayloadManifest.MochizukiModelComponent => "model",
                    PayloadManifest.MochizukiRdna3Component => "rdna3",
                    PayloadManifest.MochizukiRdna2Component => "rdna2",
                    _ => "0.4.0",
                }));
            }
        File.WriteAllText(Path.Combine(src, RuntimeFile), "stand-in 0.3.1 runtime");
        File.WriteAllText(Path.Combine(src, Work.WeightsName), "stand-in weights");
        return (src, manifest);
    }

    [Fact]
    public void TheRx6000NetworkIsPinnedWithMochizukiOnlyWhereTheManifestKeepsIt()
    {
        var (_, manifest) = Rdna2Staged("pins");
        var rx6000 = manifest.Without(PayloadManifest.MochizukiRdna3Component).Pins().MochizukiFiles.Keys;
        foreach (var path in Rdna2Paths) Assert.Contains(path, rx6000);
        Assert.DoesNotContain(rx6000, k => k.Contains("shaders-rdna3", StringComparison.Ordinal));
        Assert.Equal(MochizukiPaths.Append(ModelPath).Order(StringComparer.Ordinal),
            manifest.Without(PayloadManifest.MochizukiRdna3Component, PayloadManifest.MochizukiRdna2Component).Pins().MochizukiFiles.Keys
                .Order(StringComparer.Ordinal));
        Assert.Contains(PayloadManifest.MochizukiRdna2Component, PayloadManifest.OnDemand);
        Assert.DoesNotContain(manifest.Everyday, p => p.Key == PayloadManifest.MochizukiRdna2Component);
    }

    [Fact]
    public void ItsFilesLandInShadersRdna2AndItsListBesideManifestTxt()
    {
        var destinations = Rdna2Paths.Select(Work.MochizukiDestination).ToList();
        Assert.Equal(["dlssnr-amd/prewarm/manifest-rdna2.txt", "dlssnr-amd/shaders-rdna2/g_attn.spv",
            "dlssnr-amd/shaders-rdna2/runtime/runtime_blit.spv", "dlssnr-amd/shaders-rdna2/temporal/temporal_post_fp32h16.spv"],
            destinations);
        foreach (var destination in destinations) Assert.True(Engine.IsAllowed(destination), destination);
    }

    [Fact]
    public void TickedOnAnRx6000ItGoesInBesideMochizukiAndUntickedItComesOut()
    {
        var game = Game("rdna2");
        var (src, manifest) = Rdna2Staged("install");
        var rx6000 = manifest.Without(PayloadManifest.MochizukiRdna3Component);

        var report = Work.Install(game, src, Preset.OptiScaler, rx6000.Pins(), mochizuki: true);
        Assert.False(report.Failed, report.ToLog("install"));
        Assert.True(Fixture.HasAny(report, "RX 6000 network went in too"), report.ToLog("install"));
        Assert.False(Fixture.HasAny(report, "RX 7000 network went in too"), report.ToLog("install"));
        foreach (var path in Rdna2Paths)
            Assert.Equal(Content(path, "rdna2"), File.ReadAllText(Path.Combine(game, Work.MochizukiDestination(path))));
        Assert.True(Work.HasMochizukiRdna2(game));
        Assert.False(Work.HasMochizukiRdna3(game));

        var again = Work.Install(game, src, Preset.OptiScaler, rx6000.Without(PayloadManifest.MochizukiRdna2Component).Pins(), mochizuki: true);
        Assert.False(again.Failed, again.ToLog("unticked"));
        Assert.False(Fixture.HasAny(again, "RX 6000 network went in too"), again.ToLog("unticked"));
        Assert.False(Fixture.HasAny(again, "mochizuki was left off"), again.ToLog("unticked"));
        Assert.False(File.Exists(Path.Combine(game, "dlssnr-amd", "prewarm", "manifest-rdna2.txt")), again.ToLog("unticked"));
        Assert.False(Work.HasMochizukiRdna2(game));
        Assert.True(File.Exists(Path.Combine(game, "MochizukiNrRuntime.dll")));
    }

    [Fact]
    public void UninstallTakesTheRx6000NetworkBackWithWhatItsRuntimeWrote()
    {
        var game = Game("rdna2-uninstall");
        var before = Snapshot(game);
        var (src, manifest) = Rdna2Staged("uninstall");
        Assert.False(Work.Install(game, src, Preset.OptiScaler, manifest.Without(PayloadManifest.MochizukiRdna3Component).Pins(),
            mochizuki: true).Failed);

        // A game ran on an RX 6000: the check's verdict, the child's files and machine code, the machine code kept for
        // the build, the pipeline cache and a rewritten prewarm list; and an RX 7000's leftovers from another card.
        var data = Path.Combine(game, "dlssnr-amd");
        File.WriteAllText(Path.Combine(data, "rdna2-check.txt"), "73ff-00800196 pipelines PASS\n");
        foreach (var folder in new[] { "rdna2-check", Path.Combine("rdna2-check", "binaries"), "binaries-rdna2", "binaries-rdna3" })
            Directory.CreateDirectory(Path.Combine(data, folder));
        File.WriteAllText(Path.Combine(data, "rdna2-check", "pipelines.log"), "start 0\nok 0 12\ndone\n");
        File.WriteAllText(Path.Combine(data, "rdna2-check", "binaries", "g_attn.nrb"), "machine code");
        File.WriteAllText(Path.Combine(data, "binaries-rdna2", "g_attn.nrb"), "machine code");
        File.WriteAllText(Path.Combine(data, "binaries-rdna2", "identity.txt"), "73ff-00800196");
        File.WriteAllText(Path.Combine(data, "binaries-rdna3", "identity.txt"), "744c-00800196");
        File.WriteAllText(Path.Combine(data, "pipeline-rdna2.cache"), "compiled pipelines");
        File.WriteAllText(Path.Combine(data, "prewarm", "manifest-rdna2.txt"), "mochizuki-prewarm 2\nrewritten\n");
        File.WriteAllText(Path.Combine(data, "prewarm", "manifest-rdna2.txt.123.1.tmp"), "cut short");

        var gone = Work.Uninstall(game, Preset.OptiScaler);
        Assert.False(gone.Failed, gone.ToLog("uninstall"));
        Assert.False(Directory.Exists(data), gone.ToLog("uninstall"));
        Assert.False(Work.Uninstall(game, Preset.OptiScaler, removeConfig: true).Failed);
        Assert.Equal(before, Snapshot(game));
    }

    [Theory]
    [InlineData(0x1002u, 0x73BFu, "AMD Radeon RX 6900 XT", true)]
    [InlineData(0x1002u, 0x73DFu, "AMD Radeon RX 6700 XT", true)]
    [InlineData(0x1002u, 0x73FFu, "AMD Radeon RX 6600", true)]
    [InlineData(0x1002u, 0x743Fu, "AMD Radeon RX 6500 XT", true)]
    [InlineData(0x1002u, 0x0000u, "AMD Radeon RX 6800", true)]
    [InlineData(0x1002u, 0x1681u, "AMD Radeon 680M", true)]
    [InlineData(0x1002u, 0x164Eu, "AMD Radeon(TM) Graphics", true)]
    [InlineData(0x1002u, 0x163Fu, "AMD Custom GPU 0405", true)]
    [InlineData(0x1002u, 0x744Cu, "AMD Radeon RX 7900 XTX", false)]
    [InlineData(0x1002u, 0x7550u, "AMD Radeon RX 9070 XT", false)]
    [InlineData(0x1002u, 0x15BFu, "AMD Radeon 780M Graphics", false)]
    [InlineData(0x1002u, 0x731Fu, "AMD Radeon RX 5700 XT", false)]
    [InlineData(0x10DEu, 0x2684u, "NVIDIA GeForce RTX 4090", false)]
    public void TheRx6000NetworkIsOfferedOnRx6000CardsAndRdna2Apus(uint vendor, uint device, string name, bool rx6000) =>
        Assert.Equal(rx6000, Work.IsRx6000(vendor, device, name));
}
