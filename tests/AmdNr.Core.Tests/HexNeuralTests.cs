using System.Text;
using AmdNr.Core;

namespace AmdNr.Core.Tests;

/// <summary>HexNeural beside OptiScaler (Work.HexNeural.cs): the RX 6000 graphics cards it is offered on, where its
/// files land, NrBackend=rdna2 when it goes in and auto when it comes out, its ini kept as the player's settings, and
/// uninstall taking it all back.</summary>
public class HexNeuralTests
{
    private static readonly string[] Files =
    [
        Work.HexNeuralRuntimeName, "nr_runtime.dll", "Rdna2NrRuntime.ini", "HexNeural-NOTICE.txt",
        "nr_data_h15.1ff9997855c80dd5.1280x720/meta.json", "nr_data_h15.1ff9997855c80dd5.1280x720.kernels/native_io.co",
    ];

    private static (string Dir, PayloadPins Pins) Payloads(string tag)
    {
        var (dir, pins) = OptiScalerRouteTests.Payloads(tag, "0.5.4-amd-nr");
        var files = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var path in Files)
        {
            var bytes = Encoding.UTF8.GetBytes(path == "Rdna2NrRuntime.ini" ? "[nr]\r\ndata_dir=.\\nr_data_h15\r\n" : $"stand-in {path}");
            Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(dir, path))!);
            File.WriteAllBytes(Path.Combine(dir, path), bytes);
            files[path] = Engine.Sha(bytes);
        }
        pins.HexNeuralFiles = files;
        pins.HexNeuralVersion = "2026.11.16";
        return (dir, pins);
    }

    private static string Game(string tag, bool x64 = true)
    {
        var game = Fixture.Temp(tag);
        File.WriteAllBytes(Path.Combine(game, "Game.exe"), Fixture.PeWithImports(x64, [x64 ? "d3d12.dll" : "d3d9.dll"]));
        return game;
    }

    private static string Backend(string game) =>
        Engine.Trim(Engine.GetIni(File.ReadAllText(Path.Combine(game, Work.OptiScalerIni)), "DlssNr", "NrBackend"));

    [Fact]
    public void OnlyAnRx6000GraphicsCardAndItsTreeArePlaced()
    {
        Assert.True(Work.IsRx6000Card(0x1002, 0x73BF, "AMD Radeon RX 6900 XT"));
        Assert.True(Work.IsRx6000Card(0x1002, 0x7422, "AMD Radeon RX 6400"));
        Assert.True(Work.IsRx6000Card(0x1002, 0, "AMD Radeon RX 6700 XT"));
        // The RDNA2 APUs mochizuki's RX 6000 network counts are not: the runtime refuses them.
        Assert.False(Work.IsRx6000Card(0x1002, 0x1681, "AMD Radeon 680M"));
        Assert.True(Work.IsRx6000(0x1002, 0x1681, "AMD Radeon 680M"));
        Assert.False(Work.IsRx6000Card(0x1002, 0x7550, "AMD Radeon RX 9070 XT"));
        Assert.False(Work.IsRx6000Card(0x10DE, 0x73BF, "RX 6900 XT"));

        Assert.True(Engine.IsAllowed("nr_data_h15/1ff9997855c80dd5/1280x720/kernels/native_io.co"));
        Assert.True(Engine.IsAllowed("Rdna2NrRuntime.dll"));
        Assert.False(Engine.IsAllowed("nr_data_h15/a/b/c/d/e.co"));
        Assert.False(Engine.IsAllowed("nr_data_h15/../x.dll"));
        Assert.True(Engine.IsConfig("Rdna2NrRuntime.ini"));
        Assert.Equal("nr_data_h15/1ff9997855c80dd5/1280x720/kernels/native_io.co",
            Work.HexNeuralDestination("nr_data_h15.1ff9997855c80dd5.1280x720.kernels/native_io.co"));
        Assert.Equal("nr_runtime.dll", Work.HexNeuralDestination("nr_runtime.dll"));
        Assert.Null(Work.HexNeuralDestination("dlssnr-amd.shaders/x.spv"));
        Assert.Null(Work.HexNeuralDestination("nvngx_dlssnr.dll"));
    }

    [Fact]
    public void TickedItGoesInAsTheRuntimeAndUntickedItComesOut()
    {
        var game = Game("hex-tick");
        var (src, pins) = Payloads("hex-tick");
        pins.HexNeural = true;
        Assert.True(Fixture.HasAny(Work.Preflight(game, src, Preset.OptiScaler, pins), "HexNeural 2026.11.16 is there"));
        var report = Work.Install(game, src, Preset.OptiScaler, pins);
        Assert.False(report.Failed, report.ToLog("install"));
        Assert.Equal("stand-in nr_runtime.dll", File.ReadAllText(Path.Combine(game, "nr_runtime.dll")));
        Assert.True(File.Exists(Path.Combine(game, "nr_data_h15", "1ff9997855c80dd5", "1280x720", "kernels", "native_io.co")));
        Assert.True(File.Exists(Path.Combine(game, "HexNeural-NOTICE.txt")));
        Assert.Equal("rdna2", Backend(game));
        Assert.True(Work.HasHexNeural(game));

        // The menu's settings are the player's: kept on the next install.
        var ini = Path.Combine(game, "Rdna2NrRuntime.ini");
        File.WriteAllText(ini, "[nr]\r\ndata_dir=.\\nr_data_h15\r\nvit_every=1\r\n");
        Assert.False(Work.Install(game, src, Preset.OptiScaler, pins).Failed);
        Assert.Contains("vit_every=1", File.ReadAllText(ini));

        File.WriteAllText(Path.Combine(game, "Rdna2NrRuntime.log"), "written while the game ran");
        pins.HexNeural = false;
        Assert.True(Fixture.HasAny(Work.Preflight(game, src, Preset.OptiScaler, pins), "left off this time"));
        var off = Work.Install(game, src, Preset.OptiScaler, pins);
        Assert.False(off.Failed, off.ToLog("off"));
        Assert.False(File.Exists(Path.Combine(game, Work.HexNeuralRuntimeName)));
        Assert.False(File.Exists(Path.Combine(game, "nr_runtime.dll")));
        Assert.False(File.Exists(Path.Combine(game, "Rdna2NrRuntime.log")));
        Assert.False(Directory.Exists(Path.Combine(game, "nr_data_h15")));
        // The ini this app wrote goes back to the package's, which names no rdna2.
        Assert.NotEqual("rdna2", Backend(game));
        Assert.False(Work.HasHexNeural(game));
    }

    [Fact]
    public void AKeptIniIsPointedAtItAndNotOfferedLeavesEverythingAlone()
    {
        var game = Game("hex-kept");
        var (src, pins) = Payloads("hex-kept");
        File.WriteAllText(Path.Combine(game, Work.OptiScalerIni), "[DlssNr]\r\nEnabled=true\r\nNrBackend=daniel\r\n");
        pins.HexNeural = true;
        var report = Work.Install(game, src, Preset.OptiScaler, pins);
        Assert.False(report.Failed, report.ToLog("install"));
        Assert.Equal("rdna2", Backend(game));
        Assert.Contains("Enabled=true", File.ReadAllText(Path.Combine(game, Work.OptiScalerIni)));

        pins.HexNeural = null;
        Assert.False(Work.Install(game, src, Preset.OptiScaler, pins).Failed);
        Assert.True(File.Exists(Path.Combine(game, Work.HexNeuralRuntimeName)));
        Assert.Equal("rdna2", Backend(game));

        // A runtime picked in the menu since stays picked on the next install.
        pins.HexNeural = true;
        File.WriteAllText(Path.Combine(game, Work.OptiScalerIni), "[DlssNr]\r\nEnabled=true\r\nNrBackend=mochizuki\r\n");
        Assert.False(Work.Install(game, src, Preset.OptiScaler, pins).Failed);
        Assert.Equal("mochizuki", Backend(game));
        File.WriteAllText(Path.Combine(game, Work.OptiScalerIni), "[DlssNr]\r\nEnabled=true\r\nNrBackend=hexneural\r\n");

        // Unticked, the kept ini stops naming it.
        pins.HexNeural = false;
        Assert.False(Work.Install(game, src, Preset.OptiScaler, pins).Failed);
        Assert.Equal("auto", Backend(game));
    }

    [Fact]
    public void NotInA32BitGameAndUninstallTakesItBack()
    {
        var (src, pins) = Payloads("hex-x86");
        pins.HexNeural = true;
        Assert.True(Fixture.HasAny(Work.Preflight(Game("hex-x86", x64: false), src, Preset.OptiScaler, pins), "64-bit games only"));

        var game = Game("hex-uninstall");
        Assert.False(Work.Install(game, src, Preset.OptiScaler, pins).Failed);
        File.WriteAllText(Path.Combine(game, "Rdna2NrRuntime.log"), "log");
        var gone = Work.Uninstall(game, Preset.OptiScaler);
        Assert.False(gone.Failed, gone.ToLog("uninstall"));
        Assert.False(File.Exists(Path.Combine(game, Work.HexNeuralRuntimeName)));
        Assert.False(File.Exists(Path.Combine(game, "Rdna2NrRuntime.log")));
        Assert.False(File.Exists(Path.Combine(game, "nr_data_h15", "1ff9997855c80dd5", "1280x720", "meta.json")));
    }

    [Fact]
    public void ThePayloadCarriesItInsideAnOptiScalerReleaseOnly()
    {
        var shipped = PayloadManifest.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "payload.json")));
        Assert.False(shipped.Has(PayloadManifest.HexNeuralComponent));
        Assert.Contains(PayloadManifest.HexNeuralComponent, PayloadManifest.OnDemand);
        foreach (var release in shipped.Releases![PayloadManifest.OptiScalerComponent])
            if (release.Components.TryGetValue(PayloadManifest.HexNeuralComponent, out var hex))
            {
                Assert.All(hex.Installed, f => Assert.NotNull(Work.HexNeuralDestination(f.RelativePath)));
                Assert.All(hex.Installed, f => Assert.DoesNotContain("nvngx", f.Name, StringComparison.OrdinalIgnoreCase));
                Assert.All(hex.Files, f => Assert.StartsWith("https://huggingface.co/datasets/zmodelerlover/amd-nr/resolve/main/hexneural/", f.Url));
            }
    }
}
