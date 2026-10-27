using System.IO.Compression;
using System.Text;
using AmdNr.Core;

namespace AmdNr.Core.Tests;

/// <summary>danielblnc 0.8.0's DLSS-FG in a D3D12 game without its own: NVIDIA's Streamline in OptiScaler\streamline\,
/// nvngx_dlssg.dll in OptiScaler\, and the two FrameGen keys, only where it can work (Work.HostFg.cs).</summary>
public class HostFgTests
{
    private static readonly string[] Streamline =
        ["sl.interposer.dll", "sl.common.dll", "sl.dlss_g.dll", "sl.reflex.dll", "sl.pcl.dll"];

    private static readonly string Dlssg = Path.Combine("OptiScaler", "nvngx_dlssg.dll");

    private static (string Dir, PayloadPins Pins) Payloads(string tag, string runtime = "0.8.0")
    {
        var (dir, pins) = OptiScalerRouteTests.Payloads(tag, "0.5.8-opennr", runtime);
        var streamline = new Dictionary<string, string>(StringComparer.Ordinal);
        Directory.CreateDirectory(Path.Combine(dir, "streamline"));
        Directory.CreateDirectory(Path.Combine(dir, "dlssg"));
        foreach (var name in Streamline)
        {
            var bytes = Encoding.UTF8.GetBytes($"stand-in {name}");
            File.WriteAllBytes(Path.Combine(dir, "streamline", name), bytes);
            streamline["streamline/" + name] = Engine.Sha(bytes);
        }
        var dlssg = Encoding.UTF8.GetBytes("stand-in nvngx_dlssg.dll");
        File.WriteAllBytes(Path.Combine(dir, "dlssg", "nvngx_dlssg.dll"), dlssg);
        pins.StreamlineFiles = streamline;
        (pins.Dlssg, pins.DlssgSha) = ("dlssg/nvngx_dlssg.dll", Engine.Sha(dlssg));
        pins.HostFg = true;
        return (dir, pins);
    }

    /// <summary>A 64-bit game on this API with a DLSS 310 of its own, and whatever else is named.</summary>
    private static string Game(string tag, string api = "d3d12.dll", bool dlss = true, params string[] more)
    {
        var game = Fixture.Temp(tag);
        File.WriteAllBytes(Path.Combine(game, "Game.exe"), Fixture.PeWithImports(true, [api]));
        if (dlss) Versioned.Write(Path.Combine(game, "nvngx_dlss.dll"), "NVIDIA DLSS", "310.4.0.0");
        foreach (var path in more)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(game, path))!);
            File.WriteAllText(Path.Combine(game, path), "x");
        }
        return game;
    }

    private static string Ini(string game) => File.ReadAllText(Path.Combine(game, "OptiScaler.ini"));

    private static string Key(string game, string key) => Engine.GetIni(Ini(game), "FrameGen", key).Trim();

    private static Report Install(string game, string src, PayloadPins pins)
    {
        var report = Work.Install(game, src, Preset.OptiScaler, pins);
        Assert.False(report.Failed, report.ToLog("install"));
        return report;
    }

    [Fact]
    public void OfferedOnlyOnD3D12WithTheHostedRuntimeAndAnUpscaler()
    {
        Assert.Equal(HostFgBlock.None, Work.HostFgBlocked(Game("fg-d3d12"), "0.8.0"));
        Assert.Equal(HostFgBlock.Runtime, Work.HostFgBlocked(Game("fg-060"), "0.6.0"));
        Assert.Equal(HostFgBlock.Api, Work.HostFgBlocked(Game("fg-vk", "vulkan-1.dll"), "0.8.0"));
        Assert.Equal(HostFgBlock.Api, Work.HostFgBlocked(Game("fg-d3d11", "d3d11.dll"), "0.8.0"));
        Assert.Equal(HostFgBlock.Api, Work.HostFgBlocked(Game("fg-api"), "0.8.0", GraphicsApi.Vulkan));
        Assert.Equal(HostFgBlock.OwnFg, Work.HostFgBlocked(Game("fg-own", more: "nvngx_dlssg.dll"), "0.8.0"));
        Assert.Equal(HostFgBlock.OwnFg, Work.HostFgBlocked(Game("fg-own-sl", more: Path.Combine("bin", "sl.dlss_g.dll")), "0.8.0"));
        // Unreal's Streamline plugin sits eight folders down (Hogwarts Legacy).
        var unreal = Path.Combine("Engine", "Plugins", "Runtime", "Nvidia", "Streamline", "Binaries", "ThirdParty", "Win64", "sl.dlss_g.dll");
        Assert.Equal(HostFgBlock.OwnFg, Work.HostFgBlocked(Game("fg-own-ue", more: unreal), "0.8.0"));
        // Copies kept in a backup folder are nobody's to load (Red Dead Redemption 2 after a test by hand).
        var kept = Path.Combine("_optiscaler_backup_2026-10-24", "manual-copies", "dlssnr_amd_host", "nvngx_dlssg.dll");
        Assert.Equal(HostFgBlock.None, Work.HostFgBlocked(Game("fg-backup", more: [kept, Path.Combine(".amd-nr-x86bridge-backups", "1", "nvngx_dlssg.dll")]), "0.8.0"));
        // What this app and the host put in are not the game's: no upscaler in OptiScaler\, no DLSS-FG in dlssnr_amd_host\.
        var bare = Game("fg-bare", dlss: false, more: [Path.Combine("OptiScaler", "nvngx_dlss.dll"),
            Path.Combine("OptiScaler", "amd_fidelityfx_upscaler_dx12.dll"), Path.Combine("dlssnr_amd_host", "nvngx_dlssg.dll")]);
        Assert.Equal(HostFgBlock.NoUpscaler, Work.HostFgBlocked(bare, "0.8.0"));
        Assert.Equal(HostFgBlock.None, Work.HostFgBlocked(Game("fg-host", more: Path.Combine("dlssnr_amd_host", "nvngx_dlssg.dll")), "0.8.0"));
    }

    [Fact]
    public void TickedItGoesInWithTheTwoKeysAndUnticksBack()
    {
        var game = Game("fg-install");
        var (src, pins) = Payloads("fg-install");
        Assert.True(Fixture.HasAny(Work.Preflight(game, src, Preset.OptiScaler, pins), "danielblnc's DLSS-FG goes in"));

        var report = Install(game, src, pins);
        foreach (var name in Streamline)
            Assert.Equal($"stand-in {name}", File.ReadAllText(Path.Combine(game, "OptiScaler", "streamline", name)));
        Assert.Equal("stand-in nvngx_dlssg.dll", File.ReadAllText(Path.Combine(game, Dlssg)));
        Assert.Equal("upscaler", Key(game, "FGInput"));
        Assert.Equal("dlssg", Key(game, "FGOutput"));
        Assert.True(Work.HasHostFg(game));
        Assert.True(Fixture.HasAny(report, "its own NR is off"), report.ToLog("install"));

        // The ini went in with the keys, so it is still this install's own: an update writes it again.
        Install(game, src, pins);
        Assert.True(Fixture.HasAny(Work.Preflight(game, src, Preset.OptiScaler, pins), "Every OptiScaler file is there"));

        pins.HostFg = false;
        Install(game, src, pins);
        Assert.False(File.Exists(Path.Combine(game, Dlssg)));
        Assert.False(Directory.Exists(Path.Combine(game, "OptiScaler", "streamline")));
        Assert.Equal("", Key(game, "FGInput"));
        Assert.False(Work.HasHostFg(game));
    }

    [Fact]
    public void AKeptIniGetsOnlyTheTwoKeysAndGivesBackOnlyOurs()
    {
        var game = Game("fg-kept");
        var (src, pins) = Payloads("fg-kept");
        File.WriteAllText(Path.Combine(game, "OptiScaler.ini"), "[FrameGen]\nEnabled=true\nFGInput=auto\nFGOutput=auto\n[DlssNr]\nNrBackend=mochizuki\n");

        var report = Install(game, src, pins);
        Assert.False(Fixture.HasAny(report, "its own NR is off"), report.ToLog("install"));
        Assert.Equal("upscaler", Key(game, "FGInput"));
        Assert.Equal("dlssg", Key(game, "FGOutput"));
        Assert.Equal("true", Key(game, "Enabled"));

        // Uninstall gives the keys back while they still say what this set.
        Assert.False(Work.Uninstall(game, Preset.OptiScaler).Failed);
        Assert.Equal("auto", Key(game, "FGInput"));
        Assert.Equal("auto", Key(game, "FGOutput"));
        Assert.Equal("true", Key(game, "Enabled"));

        // One the person changed since stays as they left it.
        Install(game, src, pins);
        File.WriteAllText(Path.Combine(game, "OptiScaler.ini"), Ini(game).Replace("FGOutput=dlssg", "FGOutput=xefg"));
        pins.HostFg = false;
        Install(game, src, pins);
        Assert.Equal("upscaler", Key(game, "FGInput"));
        Assert.Equal("xefg", Key(game, "FGOutput"));
        Assert.False(File.Exists(Path.Combine(game, Dlssg)));
    }

    [Theory]
    [InlineData("own", "d3d12.dll", true, "its own DLSS frame generation")]
    [InlineData("vk", "vulkan-1.dll", false, "D3D12 games only")]
    [InlineData("bare", "d3d12.dll", false, "no DLSS, FSR or XeSS")]
    public void WhereItCannotWorkNothingGoesIn(string tag, string api, bool ownFg, string why)
    {
        var game = Game("fg-no-" + tag, api, dlss: tag != "bare", more: ownFg ? ["nvngx_dlssg.dll"] : []);
        var (src, pins) = Payloads("fg-no-" + tag);
        var report = Install(game, src, pins);
        Assert.True(Fixture.HasAny(report, why), report.ToLog("install"));
        Assert.False(File.Exists(Path.Combine(game, Dlssg)));
        Assert.False(Directory.Exists(Path.Combine(game, "OptiScaler", "streamline")));
        Assert.Equal("", Key(game, "FGOutput"));
    }

    [Fact]
    public void AnOlderRuntimeOrNoneGetsNothing()
    {
        var game = Game("fg-old");
        var (src, pins) = Payloads("fg-old", "0.6.0");
        Assert.True(Fixture.HasAny(Install(game, src, pins), "needs danielblnc 0.8.0"));
        Assert.False(File.Exists(Path.Combine(game, Dlssg)));

        var (src080, pins080) = Payloads("fg-old-080");
        Install(game, src080, pins080);
        Assert.True(File.Exists(Path.Combine(game, Dlssg)));
        var report = Work.Install(game, src080, Preset.OptiScaler, pins080, daniel: false);
        Assert.False(report.Failed, report.ToLog("install"));
        Assert.False(File.Exists(Path.Combine(game, Dlssg)));
    }

    [Fact]
    public void TheShippedPayloadPinsStreamlineAndDlssgForTheNewestOptiScaler()
    {
        var shipped = PayloadManifest.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "payload.json")));
        var pins = shipped.Newest(PayloadManifest.OptiScalerComponent).Pins();
        Assert.Equal(Streamline.Select(n => "streamline/" + n).Order(), pins.StreamlineFiles.Keys.Order());
        Assert.Equal("dlssg/nvngx_dlssg.dll", pins.Dlssg);
        Assert.Contains(PayloadManifest.StreamlineComponent, PayloadManifest.OnDemand);
        Assert.Contains(PayloadManifest.DlssgComponent, PayloadManifest.OnDemand);
    }

    /// <summary>NVIDIA's SDK holds a development sl.dlss_g.dll ahead of the production one: the entry of the pinned size
    /// is the one taken.</summary>
    [Fact]
    public void ExtractTakesTheEntryOfThePinnedSize()
    {
        var dir = Fixture.Temp("fg-zip");
        var archive = Path.Combine(dir, "sdk.zip");
        var production = Encoding.UTF8.GetBytes("production");
        using (var zip = ZipFile.Open(archive, ZipArchiveMode.Create))
        {
            foreach (var (path, text) in new[] { ("_artifacts/Develop_x64/sl.dlss_g.dll", "development build"), ("bin/x64/sl.dlss_g.dll", "production") })
                using (var stream = zip.CreateEntry(path).Open()) stream.Write(Encoding.UTF8.GetBytes(text));
        }
        var entry = new PayloadFile { Name = "sl.dlss_g.dll", Path = "streamline/sl.dlss_g.dll", Size = (ulong)production.Length, Sha256 = Engine.Sha(production) };
        PayloadCache.ExtractVerified(archive, [entry], dir);
        Assert.Equal(production, File.ReadAllBytes(Path.Combine(dir, "streamline", "sl.dlss_g.dll")));
    }
}
