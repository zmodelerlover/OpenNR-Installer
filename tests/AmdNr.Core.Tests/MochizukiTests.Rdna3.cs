using System.Text;
using AmdNr.Core;

namespace AmdNr.Core.Tests;

/// <summary>mochizuki's RX 7000 network (mochizuki-rdna3): a component of its own beside the mochizuki archive, pinned
/// with mochizuki only where the manifest keeps it, laid out where the installers already published allow it, and
/// taken back on uninstall with what its runtime writes.</summary>
public partial class MochizukiTests
{
    /// <summary>shaders-rdna3 as the archive keeps it: the network, runtime\ and temporal\, spelled with dots.</summary>
    private static readonly string[] Rdna3Paths =
    [
        "dlssnr-amd.shaders-rdna3/g_attn.spv", "dlssnr-amd.shaders-rdna3/shader-constants.txt",
        "dlssnr-amd.shaders-rdna3.runtime/cascade_blur.spv", "dlssnr-amd.shaders-rdna3.temporal/temporal_post_fp32_grade.spv",
    ];

    /// <summary>A release that carries mochizuki and its RX 7000 network, with the bytes of each file staged.</summary>
    private static (string Src, PayloadManifest Manifest) Rdna3Staged(string label)
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
              {"version":"0.5.6-opennr","components":{
                "optiscaler":{"version":"0.5.6-opennr","files":{{{Zip("o56.zip")}}},"extract":[{{{Pinned(OptiPaths, "0.4.0")}}}]},
                "mochizuki":{"version":"0.5.6-opennr","files":{{{Zip("mochizuki-0.5.6-opennr.zip")}}},"extract":[{{{Pinned(MochizukiPaths, "0.4.0")}}}]},
                "mochizuki-model":{"version":"1","files":[{"name":"dlssnr.bin","path":"{{{ModelPath}}}","size":{{{Encoding.UTF8.GetByteCount(Content(ModelPath, "model"))}}},"sha256":"{{{Sha(Content(ModelPath, "model"))}}}","url":"https://x/m"}]},
                "mochizuki-rdna3":{"version":"0.5.6-opennr","files":{{{Zip("mochizuki-rdna3-0.5.6-opennr.zip")}}},"extract":[{{{Pinned(Rdna3Paths, "rdna3")}}}]}
              }}
            ]}}
            """).Newest(PayloadManifest.OptiScalerComponent);
        var src = Fixture.Temp($"mochizuki-rdna3-src-{label}");
        foreach (var name in new[] { PayloadManifest.OptiScalerComponent, PayloadManifest.MochizukiComponent,
                     PayloadManifest.MochizukiModelComponent, PayloadManifest.MochizukiRdna3Component })
            foreach (var file in manifest.Component(name).Installed)
            {
                var full = Path.Combine(src, file.RelativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(full)!);
                File.WriteAllText(full, Content(file.RelativePath, name switch
                {
                    PayloadManifest.MochizukiModelComponent => "model",
                    PayloadManifest.MochizukiRdna3Component => "rdna3",
                    _ => "0.4.0",
                }));
            }
        File.WriteAllText(Path.Combine(src, RuntimeFile), "stand-in 0.3.1 runtime");
        File.WriteAllText(Path.Combine(src, Work.WeightsName), "stand-in weights");
        return (src, manifest);
    }

    [Fact]
    public void TheRx7000NetworkIsPinnedWithMochizukiOnlyWhereTheManifestKeepsIt()
    {
        var (_, manifest) = Rdna3Staged("pins");
        var ticked = manifest.Pins().MochizukiFiles.Keys;
        foreach (var path in Rdna3Paths) Assert.Contains(path, ticked);
        Assert.Equal(MochizukiPaths.Append(ModelPath).Order(StringComparer.Ordinal),
            manifest.WithoutMochizukiRdna3().Pins().MochizukiFiles.Keys.Order(StringComparer.Ordinal));
        // Kept apart from OptiScaler's own files, which go in whatever the card.
        Assert.DoesNotContain(manifest.Pins().OptiFiles.Keys, k => k.Contains("shaders-rdna3", StringComparison.Ordinal));

        // Fetched only by an install that asks for it, never in the first-run wizard or Download all.
        Assert.Contains(PayloadManifest.MochizukiRdna3Component, PayloadManifest.OnDemand);
        Assert.DoesNotContain(manifest.Everyday, p => p.Key == PayloadManifest.MochizukiRdna3Component);
    }

    /// <summary>What the installers already out there do with a payload that carries it: they parse it (a component
    /// they do not know is checked like any other and never asked for), and the folder it lands in is one their
    /// Engine.IsAllowed takes, so a manifest that names it is never refused.</summary>
    [Fact]
    public void ItsFilesLandInShadersRdna3WhereEveryInstallerAllowsThem()
    {
        var destinations = Rdna3Paths.Select(Work.MochizukiDestination).ToList();
        Assert.Equal(["dlssnr-amd/shaders-rdna3/g_attn.spv", "dlssnr-amd/shaders-rdna3/shader-constants.txt",
            "dlssnr-amd/shaders-rdna3/runtime/cascade_blur.spv", "dlssnr-amd/shaders-rdna3/temporal/temporal_post_fp32_grade.spv"],
            destinations);
        foreach (var destination in destinations) Assert.True(Engine.IsAllowed(destination), destination);
        // And what its runtime writes there: the verdict of its checks, the child's list and log, its pipeline cache.
        foreach (var written in new[] { "dlssnr-amd/rdna3-check.txt", "dlssnr-amd/rdna3-check/pipelines.log",
                     "dlssnr-amd/pipeline-rdna3.cache", "dlssnr-amd/prewarm/manifest-rdna3.txt" })
            Assert.True(Engine.IsAllowed(written), written);
    }

    [Fact]
    public void TickedItGoesInBesideMochizukiAndUntickedItComesOut()
    {
        var game = Game("rdna3");
        var (src, manifest) = Rdna3Staged("install");

        var report = Work.Install(game, src, Preset.OptiScaler, manifest.Pins(), mochizuki: true);
        Assert.False(report.Failed, report.ToLog("install"));
        Assert.True(Fixture.HasAny(report, "RX 7000 network went in too"), report.ToLog("install"));
        foreach (var path in Rdna3Paths)
            Assert.Equal(Content(path, "rdna3"), File.ReadAllText(Path.Combine(game, Work.MochizukiDestination(path))));
        Assert.True(Work.HasMochizukiRdna3(game));
        Assert.True(Work.HasMochizuki(game));

        // Unticked on the next install: mochizuki stays, the RX 7000 network comes out.
        var again = Work.Install(game, src, Preset.OptiScaler, manifest.WithoutMochizukiRdna3().Pins(), mochizuki: true);
        Assert.False(again.Failed, again.ToLog("unticked"));
        Assert.False(Fixture.HasAny(again, "RX 7000 network went in too"), again.ToLog("unticked"));
        var rdna3 = Path.Combine(game, "dlssnr-amd", "shaders-rdna3");
        Assert.False(Directory.Exists(rdna3) && Directory.EnumerateFiles(rdna3, "*", SearchOption.AllDirectories).Any(),
            again.ToLog("unticked"));
        Assert.False(Work.HasMochizukiRdna3(game));
        Assert.True(File.Exists(Path.Combine(game, "MochizukiNrRuntime.dll")));
    }

    [Fact]
    public void UninstallTakesTheRx7000NetworkBackWithWhatItsRuntimeWrote()
    {
        var game = Game("rdna3-uninstall");
        var before = Snapshot(game);
        var (src, manifest) = Rdna3Staged("uninstall");
        Assert.False(Work.Install(game, src, Preset.OptiScaler, manifest.Pins(), mochizuki: true).Failed);

        // A game ran on an RX 7000: the checks' verdicts and the child's files, its own cache and prewarm list.
        var data = Path.Combine(game, "dlssnr-amd");
        File.WriteAllText(Path.Combine(data, "rdna3-check.txt"), "744c-00800196 layout PASS\n");
        Directory.CreateDirectory(Path.Combine(data, "rdna3-check"));
        File.WriteAllText(Path.Combine(data, "rdna3-check", "pipelines.txt"), "g_attn.spv\n");
        File.WriteAllText(Path.Combine(data, "rdna3-check", "pipelines.log"), "start 0\nok 0 12\ndone\n");
        File.WriteAllText(Path.Combine(data, "pipeline-rdna3.cache"), "compiled pipelines");
        File.WriteAllText(Path.Combine(data, "prewarm", "manifest-rdna3.txt"), "mochizuki-prewarm 2\n");

        var gone = Work.Uninstall(game, Preset.OptiScaler);
        Assert.False(gone.Failed, gone.ToLog("uninstall"));
        Assert.False(Directory.Exists(data), gone.ToLog("uninstall"));
        Assert.False(Work.Uninstall(game, Preset.OptiScaler, removeConfig: true).Failed);
        Assert.Equal(before, Snapshot(game));
    }

    [Theory]
    [InlineData(0x1002u, 0x744Cu, "AMD Radeon RX 7900 XTX", true)]
    [InlineData(0x1002u, 0x747Eu, "AMD Radeon RX 7800 XT", true)]
    [InlineData(0x1002u, 0x7480u, "AMD Radeon RX 7600", true)]
    [InlineData(0x1002u, 0x0000u, "AMD Radeon RX 7700 XT", true)]
    [InlineData(0x1002u, 0x7448u, "AMD Radeon PRO W7900", true)]
    [InlineData(0x1002u, 0x7550u, "AMD Radeon RX 9070 XT", false)]
    [InlineData(0x1002u, 0x73BFu, "AMD Radeon RX 6900 XT", false)]
    [InlineData(0x1002u, 0x15BFu, "AMD Radeon 780M Graphics", false)]
    [InlineData(0x10DEu, 0x2684u, "NVIDIA GeForce RTX 4090", false)]
    public void TheRx7000NetworkIsOfferedOnRx7000CardsOnly(uint vendor, uint device, string name, bool rx7000) =>
        Assert.Equal(rx7000, Work.IsRx7000(vendor, device, name));
}
