using System.IO.Compression;
using System.Net;
using System.Text;
using AmdNr.Core;

namespace AmdNr.Core.Tests;

/// <summary>WrongGPU 0.6.0 beside OptiScaler: the kit's files under route C's names, the model and RR data its launcher
/// converts only from a game's own DLSS file named on its command line, its RR plugin in place of OpenRR, OptiScaler.ini's
/// keys, and what comes out when it is left off. Its launcher is a stand-in here.</summary>
public class WrongGpuTests
{
    private static readonly string[] Files =
    [
        "wronggpu/amd_fidelityfx_upscaler_dx12.dll", "wronggpu/wronggpu_runtime.dll", "wronggpu/WrongGPU-RR.asi",
        "wronggpu/wronggpu-notices.txt",
    ];

    private static (string Dir, PayloadPins Pins) Payloads(string tag)
    {
        var (dir, pins) = OptiScalerRouteTests.Payloads(tag, "0.6.7-opennr");
        var wg = (Dictionary<string, string>)pins.WrongGpuFiles;
        Directory.CreateDirectory(Path.Combine(dir, "wronggpu"));
        foreach (var path in Files)
        {
            var bytes = Encoding.UTF8.GetBytes($"stand-in {path}");
            File.WriteAllBytes(Path.Combine(dir, path), bytes);
            wg[path] = Engine.Sha(bytes);
        }
        pins.WrongGpuLauncher = Path.Combine(dir, "WrongGPU-0.6.0.exe");
        return (dir, pins);
    }

    /// <summary>A D3D12 game, with its own DLSS 310.9.1 unless told otherwise, and Streamline when it has RR.</summary>
    private static string Game(string tag, bool dlss = true, bool rr = false)
    {
        var game = Fixture.Temp(tag);
        File.WriteAllBytes(Path.Combine(game, "Game.exe"), Fixture.PeWithImports(true, ["d3d12.dll"]));
        if (dlss) Versioned.Write(Path.Combine(game, "nvngx_dlss.dll"), "NVIDIA DLSS", "310.9.1.0");
        if (rr)
        {
            Versioned.Write(Path.Combine(game, "nvngx_dlssd.dll"), "NVIDIA DLSSD", "310.9.1.0");
            File.WriteAllText(Path.Combine(game, "sl.interposer.dll"), "streamline");
        }
        return game;
    }

    /// <summary>A launcher that converts from the file it is given (or from <paramref name="instead"/>), the calls it got.</summary>
    private static List<string[]> FakeLauncher(string tag, bool model = false, string? instead = null)
    {
        var calls = new List<string[]>();
        Work.WrongGpuOtherGames = () => [];
        Work.WrongGpuModelDir = Fixture.Temp($"wg-model-{tag}");
        if (model) Made("", "elsewhere");
        Work.RunWrongGpuLauncher = (exe, arguments) =>
        {
            calls.Add(arguments);
            var from = instead ?? arguments[2][(arguments[2].IndexOf('=') + 1)..];
            Made(arguments[0] == "model" ? "" : "F", from);
            return (0, "data: ready");
        };
        return calls;
    }

    private static void Made(string folder, string from)
    {
        var dir = Path.Combine(Work.WrongGpuModelDir, folder);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "model.wgcm"), "model");
        File.WriteAllText(Path.Combine(dir, "model-source.txt"), $"source\t{from}\nversion\t310.9.1.0\n");
    }

    private static string Ini(string game) => File.ReadAllText(Path.Combine(game, Work.OptiScalerIni));
    private static string Key(string game, string section, string key) => Engine.Trim(Engine.GetIni(Ini(game), section, key));

    [Fact]
    public void TickedItsModelIsConvertedFromTheGamesOwnDlssAndOptiScalerIniNamesIt()
    {
        var game = Game("wg-install");
        var (src, pins) = Payloads("wg-install");
        var calls = FakeLauncher("install");

        Assert.False(Work.Preflight(game, src, Preset.OptiScaler, pins, wrongGpu: true).Failed);
        var report = Work.Install(game, src, Preset.OptiScaler, pins, wrongGpu: true);
        Assert.False(report.Failed, report.ToLog("install"));

        Assert.Equal(["model", "convert", "--model=" + Path.Combine(game, "nvngx_dlss.dll")], Assert.Single(calls));
        Assert.Equal("stand-in wronggpu/amd_fidelityfx_upscaler_dx12.dll", File.ReadAllText(Path.Combine(game, Work.WrongGpuFfx)));
        Assert.True(File.Exists(Path.Combine(game, "wronggpu_runtime.dll")));
        Assert.True(File.Exists(Path.Combine(game, "wronggpu-notices.txt")));
        Assert.False(File.Exists(Path.Combine(game, "plugins", "WrongGPU-RR.asi")));
        Assert.False(File.Exists(Path.Combine(game, Work.WrongGpuIni)));
        Assert.Equal(Path.Combine(game, Work.WrongGpuFfx), Key(game, "Libraries", "FfxDx12SRPath"));
        Assert.Equal("ffx", Key(game, "Upscalers", "Dx12Upscaler"));
        Assert.Equal("false", Key(game, "FSR", "Fsr4Update"));
        Assert.Equal("0", Key(game, "FSR", "UpscalerIndex"));
        Assert.True(Work.HasWrongGpu(game));

        // Left off, the next install takes it back out and OptiScaler.ini goes back to its own choice.
        File.WriteAllText(Path.Combine(game, "wronggpu-fsr.log"), "written while the game ran");
        var off = Work.Install(game, src, Preset.OptiScaler, pins);
        Assert.False(off.Failed, off.ToLog("off"));
        foreach (var name in new[] { Work.WrongGpuFfx, "wronggpu_runtime.dll", "wronggpu-notices.txt", "wronggpu-fsr.log" })
            Assert.False(File.Exists(Path.Combine(game, name)), name);
        Assert.Equal("auto", Key(game, "Libraries", "FfxDx12SRPath"));
        Assert.Equal("auto", Key(game, "Upscalers", "Dx12Upscaler"));
        Assert.False(Work.HasWrongGpu(game));
        Assert.True(File.Exists(Path.Combine(game, "nvngx_dlss.dll")));
    }

    [Fact]
    public void WithoutAGamesOwnDlssNothingIsConvertedAndNothingIsInstalled()
    {
        // Only DLSS files this app or the host put in: OptiScaler\'s and dlssnr_amd_host\'s.
        var game = Game("wg-nodlss", dlss: false);
        foreach (var folder in new[] { "OptiScaler", "dlssnr_amd_host" })
        {
            Directory.CreateDirectory(Path.Combine(game, folder));
            Versioned.Write(Path.Combine(game, folder, "nvngx_dlss.dll"), "NVIDIA DLSS", "310.9.1.0");
        }
        var old = Fixture.Temp("wg-nodlss-old");
        Versioned.Write(Path.Combine(old, "nvngx_dlss.dll"), "NVIDIA DLSS", "310.3.0.0");
        var (src, pins) = Payloads("wg-nodlss");
        var calls = FakeLauncher("nodlss");
        Work.WrongGpuOtherGames = () => [old];

        Assert.Equal([], Work.WrongGpuToConvert(game, rr: false));
        var report = Work.Install(game, src, Preset.OptiScaler, pins, wrongGpu: true);
        Assert.True(Fixture.HasErr(report, Work.NoWrongGpuDlss), report.ToLog("install"));
        Assert.Empty(calls);
        Assert.False(File.Exists(Path.Combine(game, Work.WrongGpuFfx)));
        Assert.False(File.Exists(Path.Combine(game, "dxgi.dll")));

        // Another game's own DLSS does.
        var other = Fixture.Temp("wg-nodlss-other");
        Versioned.Write(Path.Combine(other, "nvngx_dlss.dll"), "NVIDIA DLSS", "310.5.0.0");
        Work.WrongGpuOtherGames = () => [old, other];
        Assert.Equal(Path.Combine(other, "nvngx_dlss.dll"), Work.WrongGpuSource(game, WrongGpuData.Model));
    }

    [Fact]
    public void AModelMadeFromAnotherFileThanTheOneNamedIsTakenOutAgain()
    {
        var game = Game("wg-elsewhere");
        var (src, pins) = Payloads("wg-elsewhere");
        FakeLauncher("elsewhere", instead: @"E:\Some Game\dlssnr_amd_host\nvngx_dlss.dll");
        var report = Work.Install(game, src, Preset.OptiScaler, pins, wrongGpu: true);
        Assert.True(Fixture.HasErr(report, "could not convert its model"), report.ToLog("install"));
        Assert.False(Work.WrongGpuReady(WrongGpuData.Model));
        Assert.False(File.Exists(Path.Combine(game, Work.WrongGpuFfx)));
    }

    [Fact]
    public void WithoutTheLauncherAModelThatIsNotThereStopsTheInstall()
    {
        var game = Game("wg-declined");
        var (src, pins) = Payloads("wg-declined");
        FakeLauncher("declined");
        pins.WrongGpuLauncher = null;
        Assert.Equal([WrongGpuData.Model], Work.WrongGpuToConvert(game, rr: false));
        Assert.True(Work.Install(game, src, Preset.OptiScaler, pins, wrongGpu: true).Failed);
        Assert.False(File.Exists(Path.Combine(game, "dxgi.dll")));
    }

    [Fact]
    public void ItsRayReconstructionGoesInFromTheGamesOwnDlssdAndNeverBesideOpenRr()
    {
        var game = Game("wg-rr", rr: true);
        var (src, pins) = Payloads("wg-rr");
        var calls = FakeLauncher("rr", model: true);
        pins.WrongGpuRr = true;
        pins.OpenRr = true;
        Assert.True(Fixture.HasErr(Work.Preflight(game, src, Preset.OptiScaler, pins, wrongGpu: true), "not both"));

        pins.OpenRr = false;
        Assert.True(Work.WrongGpuRrFits(game));
        var report = Work.Install(game, src, Preset.OptiScaler, pins, wrongGpu: true);
        Assert.False(report.Failed, report.ToLog("install"));
        Assert.Equal(["rr-data", "convert", "--dll=" + Path.Combine(game, "nvngx_dlssd.dll")], Assert.Single(calls));
        Assert.Equal("stand-in wronggpu/WrongGPU-RR.asi", File.ReadAllText(Path.Combine(game, "plugins", "WrongGPU-RR.asi")));
        Assert.Contains("rr_mode=fast", File.ReadAllText(Path.Combine(game, "WrongGPU", "wronggpu.ini")));
        Assert.Equal("true", Key(game, "Plugins", "LoadAsiPlugins"));
        Assert.True(Work.HasWrongGpuRr(game));

        // Unticked, the plugin and its folder go; Model M stays.
        pins.WrongGpuRr = false;
        Assert.False(Work.Install(game, src, Preset.OptiScaler, pins, wrongGpu: true).Failed);
        Assert.False(File.Exists(Path.Combine(game, "plugins", "WrongGPU-RR.asi")));
        Assert.True(File.Exists(Path.Combine(game, Work.WrongGpuFfx)));
        Assert.Equal("auto", Key(game, "Plugins", "LoadAsiPlugins"));
    }

    [Fact]
    public void WithoutAGamesOwnDlssdItsRayReconstructionStaysOffAndModelMGoesIn()
    {
        var game = Game("wg-nodlssd");
        File.WriteAllText(Path.Combine(game, "sl.interposer.dll"), "streamline");
        // A copy this app put in for OpenRR does not count.
        Directory.CreateDirectory(Path.Combine(game, "opennr-rr"));
        Versioned.Write(Path.Combine(game, "opennr-rr", "nvngx_dlssd.dll"), "NVIDIA DLSSD", "310.9.1.0");
        var (src, pins) = Payloads("wg-nodlssd");
        var calls = FakeLauncher("nodlssd", model: true);
        pins.WrongGpuRr = true;
        var report = Work.Install(game, src, Preset.OptiScaler, pins, wrongGpu: true);
        Assert.False(report.Failed, report.ToLog("install"));
        Assert.True(Fixture.HasAny(report, Work.NoWrongGpuDlssd), report.ToLog("install"));
        Assert.Empty(calls);
        Assert.True(File.Exists(Path.Combine(game, Work.WrongGpuFfx)));
        Assert.False(File.Exists(Path.Combine(game, "plugins", "WrongGPU-RR.asi")));
    }

    [Fact]
    public void AVersionWithoutTheKitA32BitGameOrAnUnrealOneIsRefused()
    {
        var game = Game("wg-none");
        var (src, pins) = OptiScalerRouteTests.Payloads("wg-none", "0.5.3-amd-nr");
        Assert.True(Fixture.HasErr(Work.Preflight(game, src, Preset.OptiScaler, pins, wrongGpu: true), "no WrongGPU"));
        Assert.True(Work.Install(game, src, Preset.OptiScaler, pins, wrongGpu: true).Failed);
        Assert.False(File.Exists(Path.Combine(game, "dxgi.dll")));

        var unreal = Path.Combine(Fixture.Temp("wg-unreal"), "Binaries", "Win64");
        Directory.CreateDirectory(unreal);
        File.WriteAllText(Path.Combine(unreal, "sl.interposer.dll"), "streamline");
        Assert.False(Work.WrongGpuRrFits(unreal));

        var ea = new PayloadComponent { Version = "0.5.5-ea2", Files = [new PayloadFile { Name = "WrongGPU-Setup-0.5.5-ea2.exe",
            Path = "wronggpu/WrongGPU-Setup-0.5.5-ea2.exe", Size = 1, Sha256 = new string('a', 64) }] };
        Assert.False(Work.IsWrongGpuKit(ea));
    }

    /// <summary>The launcher comes from the release zip only when it matches the .sha256 published beside it.</summary>
    [Fact]
    public async Task TheLauncherIsTakenOnlyFromAZipThatMatchesItsPublishedSha()
    {
        var exe = Encoding.UTF8.GetBytes("launcher");
        using var zip = new MemoryStream();
        using (var archive = new ZipArchive(zip, ZipArchiveMode.Create, leaveOpen: true))
        {
            await using (var e = archive.CreateEntry("WrongGPU-0.6.0.exe").Open()) e.Write(exe);
            await using (var s = archive.CreateEntry("SHA256SUMS.txt").Open())
                s.Write(Encoding.UTF8.GetBytes($"{Engine.Sha(exe)}  WrongGPU-0.6.0.exe\n"));
        }
        var bytes = zip.ToArray();
        foreach (var good in new[] { true, false })
        {
            var sha = good ? Engine.Sha(bytes) : new string('b', 64);
            using var http = new HttpClient(new Serve(url => url.EndsWith(".sha256", StringComparison.Ordinal)
                ? Encoding.UTF8.GetBytes($"{sha}  WrongGPU-0.6.0.zip\n") : bytes));
            var into = Fixture.Temp($"wg-fetch-{good}");
            if (good)
            {
                var path = await Work.FetchWrongGpuLauncherAsync(http, "0.6.0", into);
                Assert.Equal(exe, File.ReadAllBytes(path));
                Assert.False(File.Exists(Path.Combine(into, "WrongGPU-0.6.0.zip")));
            }
            else await Assert.ThrowsAsync<InstallException>(() => Work.FetchWrongGpuLauncherAsync(http, "0.6.0", into));
        }
        Assert.Equal("https://github.com/phantoslayer21/wronggpu/releases/download/v0.6.0/WrongGPU-0.6.0.zip",
            Work.WrongGpuZipUrl("0.6.0"));
    }

    private sealed class Serve(Func<string, byte[]> body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancel) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(body(request.RequestUri!.ToString())) });
    }
}
