using System.Text;
using AmdNr.Core;

namespace AmdNr.Core.Tests;

/// <summary>OpenRR beside OptiScaler: where it is offered, its files, [OpenNR-RR] Enabled when it is left off, the
/// game's nvngx_dlssd.dll version, and NVIDIA's 310.9.1 in its place with the game's own put back by uninstall.</summary>
public class OpenRrTests
{
    private const string DlssdPayload = "openrr-dlssd/nvngx_dlssd.dll";

    private static readonly string[] Files =
        [Work.OpenRrRuntimeName, "opennr-rr.shaders/rr_enc0.spv", "opennr-rr.shaders/rr_post_fl.spv"];

    private static (string Dir, PayloadPins Pins) Payloads(string tag, bool dlssd = true)
    {
        var (dir, pins) = OptiScalerRouteTests.Payloads(tag, "0.5.4-amd-nr");
        var files = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var path in Files)
        {
            var bytes = Encoding.UTF8.GetBytes($"stand-in {path}");
            Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(dir, path))!);
            File.WriteAllBytes(Path.Combine(dir, path), bytes);
            files[path] = Engine.Sha(bytes);
        }
        pins.OpenRrFiles = files;
        pins.OpenRrVersion = "0.1.0-perf18";
        if (dlssd)
        {
            var at = Path.Combine(dir, DlssdPayload);
            Directory.CreateDirectory(Path.GetDirectoryName(at)!);
            Versioned.Write(at, "NVIDIA DLSS-RR", "310.9.1.0");
            (pins.OpenRrDlssd, pins.OpenRrDlssdSha) = (DlssdPayload, Engine.HashFile(at));
        }
        return (dir, pins);
    }

    private static string Game(string tag, string? dlssd = null, bool x64 = true, string api = "d3d12.dll")
    {
        var game = Fixture.Temp(tag);
        File.WriteAllBytes(Path.Combine(game, "Game.exe"), Fixture.PeWithImports(x64, [api]));
        if (dlssd is not null) Versioned.Write(Path.Combine(game, "nvngx_dlssd.dll"), "NVIDIA DLSS-RR", dlssd);
        return game;
    }

    private static string Enabled(string game) =>
        Engine.Trim(Engine.GetIni(File.ReadAllText(Path.Combine(game, Work.OptiScalerIni)), "OpenNR-RR", "Enabled"));

    private static string Version(string path) =>
        System.Diagnostics.FileVersionInfo.GetVersionInfo(path).FileVersion ?? "";

    [Fact]
    public void ItIsOfferedIn64BitD3D12GamesOnly()
    {
        Assert.Equal(OpenRrBlock.None, Work.OpenRrBlocked(Game("rr-sel-12")));
        Assert.Equal(OpenRrBlock.Api, Work.OpenRrBlocked(Game("rr-sel-vk", api: "vulkan-1.dll")));
        Assert.Equal(OpenRrBlock.Api, Work.OpenRrBlocked(Game("rr-sel-11", api: "d3d11.dll"), GraphicsApi.D3D11));
        Assert.Equal(OpenRrBlock.Width, Work.OpenRrBlocked(Game("rr-sel-32", x64: false, api: "d3d9.dll")));
        Assert.True(Engine.IsAllowed("opennr-rr/shaders/rr_enc0.spv"));
        Assert.False(Engine.IsAllowed("opennr-rr/shaders/x/rr_enc0.spv"));
        Assert.False(Engine.IsAllowed("opennr-rr/shaders/notes.txt"));
        Assert.Equal("opennr-rr/shaders/rr_enc0.spv", Work.OpenRrDestination("opennr-rr.shaders/rr_enc0.spv"));
        Assert.Null(Work.OpenRrDestination(DlssdPayload));
    }

    [Fact]
    public void TickedItGoesInAndUntickedItComesOutWithEnabledFalse()
    {
        var game = Game("rr-tick", "310.9.1.0");
        var (src, pins) = Payloads("rr-tick");
        pins.OpenRr = true;
        Assert.False(Work.Preflight(game, src, Preset.OptiScaler, pins).Failed);
        var report = Work.Install(game, src, Preset.OptiScaler, pins);
        Assert.False(report.Failed, report.ToLog("install"));
        Assert.Equal("stand-in OpenNrRrRuntime.dll", File.ReadAllText(Path.Combine(game, Work.OpenRrRuntimeName)));
        Assert.True(File.Exists(Path.Combine(game, "opennr-rr", "shaders", "rr_post_fl.spv")));
        Assert.Equal("", Enabled(game));
        Assert.True(Fixture.HasAny(report, "310.9.1 is here"), report.ToLog("install"));
        // The game's own is the one that works: nothing of NVIDIA's goes over it.
        Assert.False(Work.OpenRrNeedsDlssd(game));

        File.WriteAllText(Path.Combine(game, "opennr_rr.log"), "written while the game ran");
        pins.OpenRr = false;
        Assert.True(Fixture.HasAny(Work.Preflight(game, src, Preset.OptiScaler, pins), "left off this time"));
        var off = Work.Install(game, src, Preset.OptiScaler, pins);
        Assert.False(off.Failed, off.ToLog("off"));
        Assert.False(File.Exists(Path.Combine(game, Work.OpenRrRuntimeName)));
        Assert.False(File.Exists(Path.Combine(game, "opennr_rr.log")));
        Assert.False(Directory.Exists(Path.Combine(game, "opennr-rr")));
        Assert.Equal("false", Enabled(game));

        pins.OpenRr = true;
        Assert.False(Work.Install(game, src, Preset.OptiScaler, pins).Failed);
        Assert.NotEqual("false", Enabled(game));
        Assert.True(File.Exists(Path.Combine(game, "opennr-rr", "shaders", "rr_enc0.spv")));

        // An ini that is the person's keeps everything but the key: false goes back to auto.
        var kept = Path.Combine(game, Work.OptiScalerIni);
        File.WriteAllText(kept, "[OpenNR-RR]\r\nEnabled=false\r\n[DlssNr]\r\nNrBackend=mochizuki\r\n");
        Assert.False(Work.Install(game, src, Preset.OptiScaler, pins).Failed);
        Assert.Equal("auto", Enabled(game));
        Assert.Contains("NrBackend=mochizuki", File.ReadAllText(kept));
    }

    [Fact]
    public void AFreshUntickedInstallSaysEnabledFalseAndOneNotOfferedLeavesTheIniAlone()
    {
        var game = Game("rr-fresh-off");
        var (src, pins) = Payloads("rr-fresh-off");
        pins.OpenRr = false;
        Assert.False(Work.Install(game, src, Preset.OptiScaler, pins).Failed);
        Assert.Equal("false", Enabled(game));
        Assert.False(File.Exists(Path.Combine(game, Work.OpenRrRuntimeName)));
        Assert.False(File.Exists(Path.Combine(game, "nvngx_dlssd.dll")));

        var other = Game("rr-fresh-none");
        pins.OpenRr = null;
        Assert.False(Work.Install(other, src, Preset.OptiScaler, pins).Failed);
        Assert.Equal("", Enabled(other));
        Assert.False(File.Exists(Path.Combine(other, Work.OpenRrRuntimeName)));
    }

    [Fact]
    public void AnotherDlssdIsReplacedBy31091AndUninstallPutsTheGamesBack()
    {
        var game = Game("rr-supply", "310.3.0.0");
        var (src, pins) = Payloads("rr-supply");
        pins.OpenRr = true;
        Assert.True(Work.OpenRrNeedsDlssd(game));
        Assert.False(Work.OpenRrDlssd(game)[0].Works);
        var report = Work.Install(game, src, Preset.OptiScaler, pins);
        Assert.False(report.Failed, report.ToLog("install"));
        Assert.True(Fixture.HasAny(report, "310.3.0"), report.ToLog("install"));
        Assert.Equal("310.9.1.0", Version(Path.Combine(game, "nvngx_dlssd.dll")));
        // Ours now, so it stays on the next install.
        Assert.True(Work.OpenRrNeedsDlssd(game));
        Assert.False(Work.Install(game, src, Preset.OptiScaler, pins).Failed);
        Assert.Equal("310.9.1.0", Version(Path.Combine(game, "nvngx_dlssd.dll")));

        var gone = Work.Uninstall(game, Preset.OptiScaler);
        Assert.False(gone.Failed, gone.ToLog("uninstall"));
        Assert.Equal("310.3.0.0", Version(Path.Combine(game, "nvngx_dlssd.dll")));
        Assert.False(File.Exists(Path.Combine(game, Work.OpenRrRuntimeName)));
        Assert.False(Directory.Exists(Path.Combine(game, "opennr-rr")));
    }

    [Fact]
    public void AVulkanGameGets31091InOpennrRrAndKeepsItsOwn()
    {
        // Indiana Jones: Vulkan, its own nvngx_dlssd.dll in streamline\
        var game = Game("rr-vk", api: "vulkan-1.dll");
        var own = Path.Combine(game, "streamline", "nvngx_dlssd.dll");
        Directory.CreateDirectory(Path.GetDirectoryName(own)!);
        Versioned.Write(own, "NVIDIA DLSS-RR", "310.2.1.0");
        var (src, pins) = Payloads("rr-vk");
        pins.OpenRr = true;
        Assert.True(Work.OpenRrVulkan(game, null));
        Assert.True(Work.OpenRrNeedsDlssd(game));
        Assert.True(Fixture.HasAny(Work.Preflight(game, src, Preset.OptiScaler, pins), "opennr-rr"));
        var report = Work.Install(game, src, Preset.OptiScaler, pins);
        Assert.False(report.Failed, report.ToLog("install"));
        Assert.Equal("310.9.1.0", Version(Path.Combine(game, "opennr-rr", "nvngx_dlssd.dll")));
        Assert.Equal("310.2.1.0", Version(own));
        Assert.False(File.Exists(Path.Combine(game, "nvngx_dlssd.dll")));
        Assert.True(File.Exists(Path.Combine(game, Work.OpenRrRuntimeName)));
        Assert.True(File.Exists(Path.Combine(game, "opennr-rr", "shaders", "rr_enc0.spv")));
        Assert.Equal(Path.Combine("opennr-rr", "nvngx_dlssd.dll"), Work.OpenRrDlssd(game)[0].Path);
        // Ours, so it stays on the next install.
        Assert.True(Work.OpenRrNeedsDlssd(game));
        Assert.False(Work.Install(game, src, Preset.OptiScaler, pins).Failed);
        Assert.Equal("310.9.1.0", Version(Path.Combine(game, "opennr-rr", "nvngx_dlssd.dll")));

        var gone = Work.Uninstall(game, Preset.OptiScaler);
        Assert.False(gone.Failed, gone.ToLog("uninstall"));
        Assert.False(Directory.Exists(Path.Combine(game, "opennr-rr")));
        Assert.False(File.Exists(Path.Combine(game, Work.OpenRrRuntimeName)));
        Assert.Equal("310.2.1.0", Version(own));
    }

    [Fact]
    public void LeftOffTheGamesOwnDlssdComesBack()
    {
        var game = Game("rr-supply-off", "310.3.0.0");
        var (src, pins) = Payloads("rr-supply-off");
        pins.OpenRr = true;
        Assert.False(Work.Install(game, src, Preset.OptiScaler, pins).Failed);
        pins.OpenRr = false;
        var off = Work.Install(game, src, Preset.OptiScaler, pins);
        Assert.False(off.Failed, off.ToLog("off"));
        Assert.Equal("310.3.0.0", Version(Path.Combine(game, "nvngx_dlssd.dll")));
    }

    [Fact]
    public void TheVersionNoteSaysWhatTheGamesDlssdMeans()
    {
        // Not downloaded: the game's 310.3.0 is all there is, and the report says it will not run.
        var game = Game("rr-note", "310.3.0.0");
        var (src, pins) = Payloads("rr-note", dlssd: false);
        pins.OpenRr = true;
        var report = Work.Install(game, src, Preset.OptiScaler, pins);
        Assert.False(report.Failed, report.ToLog("install"));
        Assert.True(Fixture.HasAny(report, "nvngx_dlssd.dll is 310.3.0"), report.ToLog("install"));
        Assert.True(Fixture.HasAny(report, "310.9.1 only"), report.ToLog("install"));
        Assert.Equal("310.3.0.0", Version(Path.Combine(game, "nvngx_dlssd.dll")));

        // None at all: it may not use Ray Reconstruction, and NVIDIA's 310.9.1 goes in for when it does.
        var none = Game("rr-note-none");
        Assert.Empty(Work.OpenRrDlssd(none));
        Assert.True(Work.OpenRrNeedsDlssd(none));
        // A deeper one is found too, under its path in the game.
        var deep = Game("rr-note-deep");
        Directory.CreateDirectory(Path.Combine(deep, "Engine", "Plugins"));
        Versioned.Write(Path.Combine(deep, "Engine", "Plugins", "nvngx_dlssd.dll"), "NVIDIA DLSS-RR", "310.9.1.0");
        Assert.True(Assert.Single(Work.OpenRrDlssd(deep)).Works);
        Assert.False(Work.OpenRrNeedsDlssd(deep));
    }

    [Fact]
    public void AVersionWithoutItIsRefusedWhenTicked()
    {
        var game = Game("rr-none");
        var (src, pins) = OptiScalerRouteTests.Payloads("rr-none", "0.5.4-amd-nr");
        pins.OpenRr = true;
        Assert.True(Fixture.HasErr(Work.Preflight(game, src, Preset.OptiScaler, pins), "no OpenRR"));
        Assert.True(Work.Install(game, src, Preset.OptiScaler, pins).Failed);
        Assert.False(File.Exists(Path.Combine(game, "dxgi.dll")));
    }

    [Fact]
    public void ThePayloadPinsItAndAnInstallComparesAgainstIt()
    {
        // The shipped list pins it in the newest release, and both of its components come on demand.
        var manifest = PayloadManifest.Parse(PayloadCache.Embedded("payload.json"));
        Assert.NotEmpty(manifest.Newest(PayloadManifest.OptiScalerComponent).Pins().OpenRrFiles);
        Assert.Contains(PayloadManifest.OpenRrComponent, PayloadManifest.OnDemand);
        Assert.Contains(PayloadManifest.OpenRrDlssdComponent, PayloadManifest.OnDemand);
    }
}
