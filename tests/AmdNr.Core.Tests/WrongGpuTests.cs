using System.Text;
using AmdNr.Core;

namespace AmdNr.Core.Tests;

/// <summary>WrongGPU beside OptiScaler 0.5.4: its files, the model its setup converts in a folder of its own, the two
/// OptiScaler.ini keys, and what comes out when it is left off. Its setup is a stand-in here.</summary>
public class WrongGpuTests
{
    private const string Setup = "wronggpu/WrongGPU-Setup-0.5.5-ea.exe";

    private static readonly string[] Files =
    [
        "wronggpu/wronggpu_ffx_dx12.dll", "wronggpu/wronggpu_runtime.dll", "wronggpu/WrongGPU.asi",
        "wronggpu/wronggpu-license.txt", "wronggpu/wronggpu-notices.txt", Setup,
    ];

    private static (string Dir, PayloadPins Pins) Payloads(string tag)
    {
        var (dir, pins) = OptiScalerRouteTests.Payloads(tag, "0.5.4-amd-nr");
        var wg = (Dictionary<string, string>)pins.WrongGpuFiles;
        Directory.CreateDirectory(Path.Combine(dir, "wronggpu"));
        foreach (var path in Files)
        {
            var bytes = Encoding.UTF8.GetBytes($"stand-in {path}");
            File.WriteAllBytes(Path.Combine(dir, path), bytes);
            wg[path] = Engine.Sha(bytes);
        }
        return (dir, pins);
    }

    private static string Game(string tag)
    {
        var game = Fixture.Temp(tag);
        File.WriteAllBytes(Path.Combine(game, "Game.exe"), Fixture.PeWithImports(true, ["d3d12.dll"]));
        return game;
    }

    /// <summary>A setup that leaves a model where WrongGPU keeps it, or fails, and the calls it was given.</summary>
    private static List<string[]> FakeSetup(string tag, bool converts = true)
    {
        var calls = new List<string[]>();
        Work.WrongGpuModel = Path.Combine(Fixture.Temp($"wg-model-{tag}"), "model.wgcm");
        Work.RunWrongGpuSetup = (exe, arguments) =>
        {
            calls.Add([exe, .. arguments]);
            var dir = arguments.First(a => a.StartsWith("/dir=", StringComparison.Ordinal))[5..];
            Assert.True(File.Exists(Path.Combine(dir, "Game.exe")));
            if (arguments.Contains("/uninstall")) return 0;
            var package = arguments.First(a => a.StartsWith("/optiscaler=", StringComparison.Ordinal))[12..];
            Assert.True(File.Exists(Path.Combine(package, "OptiScaler.dll")));
            Assert.True(File.Exists(Path.Combine(package, "OptiScaler.ini")));
            if (!converts) return 3;
            File.WriteAllText(Work.WrongGpuModel, "model");
            return 0;
        };
        return calls;
    }

    [Fact]
    public void TickedItGoesInWithItsModelAndOptiScalerIniNamesIt()
    {
        var game = Game("wg-install");
        var (src, pins) = Payloads("wg-install");
        var calls = FakeSetup("install");

        Assert.False(Work.Preflight(game, src, Preset.OptiScaler, pins, wrongGpu: true).Failed);
        var report = Work.Install(game, src, Preset.OptiScaler, pins, wrongGpu: true);
        Assert.False(report.Failed, report.ToLog("install"));

        Assert.Equal(2, calls.Count);
        Assert.Contains("/experimental", calls[0]);
        Assert.Contains("/silent", calls[0]);
        Assert.Contains("/uninstall", calls[1]);
        Assert.Equal("stand-in wronggpu/wronggpu_ffx_dx12.dll", File.ReadAllText(Path.Combine(game, "wronggpu_ffx_dx12.dll")));
        Assert.Equal("stand-in wronggpu/WrongGPU.asi", File.ReadAllText(Path.Combine(game, "plugins", "WrongGPU.asi")));
        Assert.Equal("stand-in OptiScaler/amd_fidelityfx_upscaler_dx12.dll",
            File.ReadAllText(Path.Combine(game, "wronggpu-amd-compare.dll")));
        Assert.False(File.Exists(Path.Combine(game, "WrongGPU-Setup-0.5.5-ea.exe")));
        Assert.Contains("runtime_options=model=M;executor=dx12", File.ReadAllText(Path.Combine(game, Work.WrongGpuIni)));
        var ini = File.ReadAllText(Path.Combine(game, Work.OptiScalerIni));
        Assert.Equal(Path.Combine(game, Work.WrongGpuFfx), Engine.Trim(Engine.GetIni(ini, "Libraries", "FfxDx12SRPath")));
        Assert.Equal("true", Engine.Trim(Engine.GetIni(ini, "Plugins", "LoadAsiPlugins")));
        Assert.True(Work.HasWrongGpu(game));
        Assert.False(Work.WrongGpuNeedsDlss(game));

        // Left off, the next install takes it back out and OptiScaler.ini goes back to its own FFX upscaler.
        File.WriteAllText(Path.Combine(game, "wronggpu-fsr.log"), "written while the game ran");
        var off = Work.Install(game, src, Preset.OptiScaler, pins);
        Assert.False(off.Failed, off.ToLog("off"));
        foreach (var name in Work.WrongGpuNames.Where(n => n != Work.WrongGpuIni).Append("wronggpu-fsr.log"))
            Assert.False(File.Exists(Path.Combine(game, name)), name);
        Assert.False(Directory.Exists(Path.Combine(game, "plugins")));
        ini = File.ReadAllText(Path.Combine(game, Work.OptiScalerIni));
        Assert.Equal("auto", Engine.Trim(Engine.GetIni(ini, "Libraries", "FfxDx12SRPath")));
        Assert.False(Work.HasWrongGpu(game));
    }

    [Fact]
    public void UninstallTakesItBackAndAModelThatCannotBeMadeWritesNothing()
    {
        var game = Game("wg-uninstall");
        var (src, pins) = Payloads("wg-uninstall");
        FakeSetup("uninstall");
        Assert.False(Work.Install(game, src, Preset.OptiScaler, pins, wrongGpu: true).Failed);
        var gone = Work.Uninstall(game, Preset.OptiScaler);
        Assert.False(gone.Failed, gone.ToLog("uninstall"));
        Assert.False(File.Exists(Path.Combine(game, "wronggpu_runtime.dll")));
        Assert.False(File.Exists(Path.Combine(game, "plugins", "WrongGPU.asi")));

        var fresh = Game("wg-nomodel");
        FakeSetup("nomodel", converts: false);
        Assert.True(Work.WrongGpuNeedsDlss(fresh));
        var report = Work.Install(fresh, src, Preset.OptiScaler, pins, wrongGpu: true);
        Assert.True(Fixture.HasErr(report, "could not convert"), report.ToLog("install"));
        Assert.False(File.Exists(Path.Combine(fresh, "dxgi.dll")));
        Assert.False(File.Exists(Path.Combine(fresh, "wronggpu_ffx_dx12.dll")));
    }

    [Fact]
    public void AVersionWithoutItOrA32BitGameIsRefused()
    {
        var game = Game("wg-none");
        var (src, pins) = OptiScalerRouteTests.Payloads("wg-none", "0.5.3-amd-nr");
        Assert.True(Fixture.HasErr(Work.Preflight(game, src, Preset.OptiScaler, pins, wrongGpu: true), "no WrongGPU"));
        Assert.True(Work.Install(game, src, Preset.OptiScaler, pins, wrongGpu: true).Failed);
        Assert.False(File.Exists(Path.Combine(game, "dxgi.dll")));
    }
}
