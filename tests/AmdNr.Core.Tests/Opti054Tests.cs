using System.Text;
using AmdNr.Core;

namespace AmdNr.Core.Tests;

/// <summary>OptiScaler 0.5.4: 32-bit D3D11 and OpenGL games through the package's x86\dxgi.dll and x86\opengl32.dll,
/// person protection's folder, mochizuki's second model and the paths its new runtime reads and writes.</summary>
public class Opti054Tests
{
    private const string Wide = "0.5.4-amd-nr";

    private static (string Dir, PayloadPins Pins) Payloads(string tag, string version = Wide)
    {
        var (dir, pins) = OptiScalerRouteTests.Payloads(tag, version);
        var opti = (Dictionary<string, string>)pins.OptiFiles;
        foreach (var path in new[]
                 {
                     "x86/d3d9.dll", "x86/dxgi.dll", "x86/opengl32.dll", "x86/OptiScalerDx9Host.exe",
                     "person-model/person-worker.exe", "person-model/onnxruntime.dll", "person-model/YOLO11_AGPL-3.0.txt",
                 })
        {
            var bytes = Encoding.UTF8.GetBytes($"stand-in {path}");
            Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(dir, path))!);
            File.WriteAllBytes(Path.Combine(dir, path), bytes);
            opti[path] = Engine.Sha(bytes);
        }
        return (dir, pins);
    }

    private static string Game(string tag, bool x64, params string[] imports)
    {
        var game = Fixture.Temp(tag);
        File.WriteAllBytes(Path.Combine(game, "Game.exe"), Fixture.PeWithImports(x64, imports));
        return game;
    }

    private static string Text(string dir, string name) => File.ReadAllText(Path.Combine(dir, name));

    [Fact]
    public void The32BitD3D11AndOpenGLGamesAreOptiScalersFrom054OnAndD3D8StaysOnReShade()
    {
        var dx11 = GraphicsDetector.Detect(Game("x86w-detect-11", false, "d3d11.dll"));
        Assert.Equal(Preset.OptiScaler, dx11.PresetFor(true, true, x86Wide: true));
        Assert.NotEqual(Preset.OptiScaler, dx11.PresetFor(true, true));
        Assert.Equal(GraphicsApi.D3D11, dx11.OptiApi(true, true, true));
        Assert.True(dx11.CanRunOptiScalerWith(true, true, true));

        var gl = GraphicsDetector.Detect(Game("x86w-detect-gl", false, "opengl32.dll"));
        Assert.Equal(Preset.OptiScaler, gl.PresetFor(true, true, true));
        Assert.Equal(GraphicsApi.OpenGL, gl.OptiApi(true, true, true));

        var dx8 = GraphicsDetector.Detect(Game("x86w-detect-8", false, "d3d8.dll"));
        Assert.NotEqual(Preset.OptiScaler, dx8.PresetFor(true, true, true));

        Assert.True(Work.OptiRunsX86Wide("0.5.4-amd-nr"));
        Assert.False(Work.OptiRunsX86Wide("0.5.3-amd-nr"));
    }

    [Fact]
    public void A32BitD3D11GameGetsTheDxgiProxyAndAnIniWithNrOnWithoutAnUpscaler()
    {
        var game = Game("x86w-install-11", false, "d3d11.dll");
        var (src, pins) = Payloads("x86w-install-11");
        Assert.False(Work.Preflight(game, src, Preset.OptiScaler, pins).Failed);
        var report = Work.Install(game, src, Preset.OptiScaler, pins);
        Assert.False(report.Failed, report.ToLog("install"));

        Assert.Equal("stand-in x86/dxgi.dll", Text(game, "dxgi.dll"));
        var host = Path.Combine(game, "OptiScaler64");
        Assert.Equal("stand-in OptiScaler.dll", Text(host, "OptiScaler.dll"));
        Assert.Equal("stand-in x86/OptiScalerDx9Host.exe", Text(host, "OptiScalerDx9Host.exe"));
        Assert.Equal("stand-in person-model/onnxruntime.dll", Text(host, "person-model/onnxruntime.dll"));
        var ini = Text(host, Work.OptiScalerIni);
        Assert.Equal("true", Engine.Trim(Engine.GetIni(ini, "DlssNr", "Enabled")));
        Assert.Equal("true", Engine.Trim(Engine.GetIni(ini, "DlssNr", "PresentWithoutUpscaler")));
        Assert.False(File.Exists(Path.Combine(game, "opengl32.dll")));
        Assert.Equal(RouteFamily.OptiScaler, GameScanner.InstalledAs(game));
    }

    [Fact]
    public void A32BitOpenGLGameGetsTheOpenGLProxyAndUninstallTakesItsLogsAndPersonModel()
    {
        var game = Game("x86w-install-gl", false, "opengl32.dll");
        var (src, pins) = Payloads("x86w-install-gl");
        var report = Work.Install(game, src, Preset.OptiScaler, pins);
        Assert.False(report.Failed, report.ToLog("install"));
        Assert.Equal("stand-in x86/opengl32.dll", Text(game, "opengl32.dll"));
        Assert.False(File.Exists(Path.Combine(game, "dxgi.dll")));

        var host = Path.Combine(game, "OptiScaler64");
        foreach (var log in new[] { "opengl32-x86.log", "dx9-host.log", "person-model/person-worker.log" })
            File.WriteAllText(Path.Combine(host, log), "written while the game ran");
        var gone = Work.Uninstall(game, Preset.OptiScaler);
        Assert.False(gone.Failed, gone.ToLog("uninstall"));
        Assert.False(File.Exists(Path.Combine(game, "opengl32.dll")));
        Assert.False(Directory.Exists(Path.Combine(host, "person-model")));
        Assert.False(File.Exists(Path.Combine(host, "opengl32-x86.log")));
    }

    [Fact]
    public void A32BitD3D11GameOnAnOlderOptiScalerIsRefused()
    {
        var game = Game("x86w-old", false, "d3d11.dll");
        var (src, pins) = Payloads("x86w-old", "0.5.3-amd-nr");
        Assert.True(Work.Preflight(game, src, Preset.OptiScaler, pins).Failed);
        var report = Work.Install(game, src, Preset.OptiScaler, pins);
        Assert.True(Fixture.HasErr(report, "0.5.4"), report.ToLog("install"));
        Assert.False(File.Exists(Path.Combine(game, "dxgi.dll")));
    }

    [Fact]
    public void A64BitGameGetsPersonModelBesideOptiScaler()
    {
        var game = Game("x64-person", true, "d3d12.dll");
        var (src, pins) = Payloads("x64-person");
        var report = Work.Install(game, src, Preset.OptiScaler, pins);
        Assert.False(report.Failed, report.ToLog("install"));
        Assert.Equal("stand-in person-model/person-worker.exe", Text(game, "person-model/person-worker.exe"));
        Assert.Equal("stand-in person-model/YOLO11_AGPL-3.0.txt", Text(game, "person-model/YOLO11_AGPL-3.0.txt"));
        Assert.False(Work.Uninstall(game, Preset.OptiScaler).Failed);
        Assert.False(Directory.Exists(Path.Combine(game, "person-model")));
    }

    [Fact]
    public void TheNewPathsAreAllowedAndNothingDeeperIs()
    {
        foreach (var name in new[]
                 {
                     "dlssnr-amd/shaders/renodx/nr_renodx.spv", "dlssnr-amd/dlssnr-small.bin", "dlssnr-amd/int4/pipeline.cache",
                     "person-model/onnxruntime.dll", "OptiScaler64/person-model/yunet.onnx", "plugins/WrongGPU.asi",
                     "wronggpu_ffx_dx12.dll", "wronggpu.ini", "opengl32.dll",
                 })
            Assert.True(Engine.IsAllowed(name), name);
        foreach (var name in new[] { "person-model/sub/x.dll", "plugins/other.asi", "person-model/../dxgi.dll" })
            Assert.False(Engine.IsAllowed(name), name);
        Assert.True(Engine.IsConfig("wronggpu.ini"));
    }

    [Fact]
    public void UninstallTakesTheMochizukiBuildMarkersAndInt4Caches()
    {
        var game = Game("mz-markers", true, "d3d12.dll");
        var (src, pins) = Payloads("mz-markers");
        Assert.False(Work.Install(game, src, Preset.OptiScaler, pins).Failed);
        var data = Path.Combine(game, Engine.MochizukiFolder);
        Directory.CreateDirectory(Path.Combine(data, "int4", "pipeline-binaries"));
        foreach (var f in new[] { "build.pending", "build.failed", "pipeline.cache", "int4/pipeline.cache", "int4/pipeline-binaries/a.bin" })
            File.WriteAllText(Path.Combine(data, f), "written by the runtime");
        var report = Work.Uninstall(game, Preset.OptiScaler);
        Assert.False(report.Failed, report.ToLog("uninstall"));
        Assert.False(Directory.Exists(data), string.Join(", ", Fixture.Walk(game)));
    }

    [Fact]
    public void ThePayloadCarriesTheSecondModelWithMochizukiAndLeavesPersonProtectionOutWithLmxxf()
    {
        var file = (string name, string path) => new PayloadFile { Name = name, Path = path, Size = 1, Sha256 = new string('a', 64) };
        var component = (string version, PayloadFile f) => new PayloadComponent { Version = version, Files = [f] };
        var release = new ComponentRelease
        {
            Version = Wide,
            Components = new Dictionary<string, PayloadComponent>(StringComparer.Ordinal)
            {
                [PayloadManifest.OptiScalerComponent] = component(Wide, file("OptiScaler.dll", "OptiScaler.dll")),
                [PayloadManifest.MochizukiComponent] = component(Wide, file("MochizukiNrRuntime.dll", "MochizukiNrRuntime.dll")),
                [PayloadManifest.MochizukiModelComponent] = component("1", file("dlssnr.bin", "dlssnr-amd/dlssnr.bin")),
                [PayloadManifest.MochizukiSmallModelComponent] = component("1", file("dlssnr-small.bin", "dlssnr-amd/dlssnr-small.bin")),
                [PayloadManifest.PersonModelComponent] = component(Wide, file("yunet.onnx", "person-model/yunet.onnx")),
                [PayloadManifest.WrongGpuComponent] = component("0.5.5-ea", file("wronggpu_ffx_dx12.dll", "wronggpu/wronggpu_ffx_dx12.dll")),
            },
        };
        var manifest = new PayloadManifest
        {
            Schema = 1,
            Components = new Dictionary<string, PayloadComponent>(StringComparer.Ordinal)
            {
                [PayloadManifest.AddonComponent] = component("0.7.13", file(Work.AddonName, Work.AddonName)),
                [PayloadManifest.RuntimeComponent] = new PayloadComponent
                {
                    Version = "0.6.0", Files = [file(Work.RuntimeName, Work.RuntimeName), file(Work.WeightsName, Work.WeightsName)],
                },
            },
            Releases = new Dictionary<string, List<ComponentRelease>> { [PayloadManifest.OptiScalerComponent] = [release] },
        };

        var reshade = manifest.WithMochizuki().Pins();
        Assert.Contains("dlssnr-amd/dlssnr-small.bin", reshade.MochizukiFiles.Keys);
        var opti = manifest.With(release).Pins();
        Assert.Contains("dlssnr-amd/dlssnr-small.bin", opti.MochizukiFiles.Keys);
        Assert.Contains("person-model/yunet.onnx", opti.OptiFiles.Keys);
        Assert.Equal("0.5.5-ea", opti.WrongGpuVersion);
        var elsewhere = manifest.With(release).WithoutLmxxf().Pins();
        Assert.DoesNotContain("person-model/yunet.onnx", elsewhere.OptiFiles.Keys);
        Assert.Empty(elsewhere.WrongGpuFiles);
    }
    [Fact]
    public void TheShippedManifestCarriesTheNewComponentsIn054AndEveryNewFileMayBeInstalled()
    {
        var shipped = PayloadManifest.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "payload.json")));
        var newest = shipped.Offered(PayloadManifest.OptiScalerComponent)[0];
        Assert.Equal(Wide, newest.Version);
        var pins = shipped.With(newest).Pins();
        Assert.Equal("7e966e2639dbec6596aa0bac3b8761137d2c40ca8cb4aca30df7ca10043c6e33", pins.MochizukiFiles["dlssnr-amd/dlssnr-small.bin"]);
        Assert.Contains(pins.MochizukiFiles.Keys, k => k.StartsWith("dlssnr-amd.shaders.renodx/", StringComparison.Ordinal));
        foreach (var licence in new[] { "onnxruntime_LICENSE.txt", "YuNet_MIT.txt", "PPHumanSeg_Apache-2.0.txt", "YOLO11_AGPL-3.0.txt" })
            Assert.Contains($"person-model/{licence}", pins.OptiFiles.Keys);
        Assert.Contains("x86/opengl32.dll", pins.OptiFiles.Keys);
        Assert.Equal("0.5.5-ea", pins.WrongGpuVersion);
        Assert.Equal("f7b167b5ac1c9cff19984b520e85370119a8ccd12bf14f73b2d392e918bf6363",
            pins.WrongGpuFiles["wronggpu/WrongGPU-Setup-0.5.5-ea.exe"]);
        Assert.Equal("wronggpu/nvngx_dlss.dll", pins.WrongGpuDlss);
        Assert.StartsWith("https://raw.githubusercontent.com/NVIDIA/DLSS/", shipped.With(newest)
            .Component(PayloadManifest.WrongGpuDlssComponent).Files.Single().Url);
        foreach (var path in pins.OptiFiles.Keys.Where(k => !k.StartsWith("x86/", StringComparison.Ordinal)))
            Assert.True(Engine.IsAllowed(Work.OptiDestination(path, "dxgi.dll"))
                        && Engine.IsAllowed("OptiScaler64/" + Work.OptiDestination(path, "OptiScaler.dll")), path);
        foreach (var path in pins.MochizukiFiles.Keys)
            Assert.True(Engine.IsAllowed(Work.MochizukiDestination(path)), path);
        foreach (var path in pins.WrongGpuFiles.Keys)
            Assert.True(Work.WrongGpuDestination(path) is not { } at || Engine.IsAllowed(at), path);
        // The add-on and its 32-bit bridge move together.
        Assert.Equal("0.7.13", pins.AddonVersion);
        Assert.Equal("0.7.13", pins.BridgeVersion);
    }
}
