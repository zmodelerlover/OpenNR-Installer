using System.Text;
using AmdNr.Core;

namespace AmdNr.Core.Tests;

/// <summary>danielblnc 0.8.0 reads NVIDIA's networks from the nvngx_dlss.dll OptiScaler hands it: a game whose own is
/// older than 310, or missing, gets NVIDIA's 310.9.1 in OptiScaler\ (Work.HostDlss.cs).</summary>
public class HostDlssTests
{
    private static readonly string Library = Path.Combine("OptiScaler", "nvngx_dlss.dll");

    private static (string Dir, PayloadPins Pins) Payloads(string tag, string runtime)
    {
        var (dir, pins) = OptiScalerRouteTests.Payloads(tag, "0.5.7-opennr", runtime);
        Directory.CreateDirectory(Path.Combine(dir, "wronggpu"));
        var dlss = Path.Combine(dir, "wronggpu", "nvngx_dlss.dll");
        Versioned.Write(dlss, "NVIDIA DLSS", "310.9.1.0");
        return (dir, new PayloadPins
        {
            AddonSha = pins.AddonSha, AddonSize = pins.AddonSize, WeightsSha = pins.WeightsSha, WeightsSize = pins.WeightsSize,
            OptiFiles = pins.OptiFiles, OptiRuntimeName = pins.OptiRuntimeName, OptiRuntimeSha = pins.OptiRuntimeSha,
            OptiRuntimeSize = pins.OptiRuntimeSize, OptiScalerVersion = pins.OptiScalerVersion,
            OptiRuntimeVersion = pins.OptiRuntimeVersion,
            WrongGpuDlss = "wronggpu/nvngx_dlss.dll", WrongGpuDlssSha = Engine.Sha(File.ReadAllBytes(dlss)),
        });
    }

    /// <summary>A 64-bit D3D12 game with the DLSS named, or none.</summary>
    private static string Game(string tag, string? dlss)
    {
        var game = Fixture.Temp(tag);
        File.WriteAllBytes(Path.Combine(game, "Game.exe"), Fixture.PeWithImports(true, ["d3d12.dll"]));
        if (dlss is not null) Versioned.Write(Path.Combine(game, "nvngx_dlss.dll"), "NVIDIA DLSS", dlss);
        return game;
    }

    private static Report Install(string game, string src, PayloadPins pins)
    {
        var report = Work.Install(game, src, Preset.OptiScaler, pins);
        Assert.False(report.Failed, report.ToLog("install"));
        return report;
    }

    [Fact]
    public void AnOldDlssGets310InOptiScalerAndUninstallTakesIt()
    {
        var game = Game("hd-old", "2.2.10.0");
        var (src, pins) = Payloads("hd-old", "0.8.0");
        Assert.True(Work.HostNeedsDlss(game, "0.8.0"));
        Assert.True(Fixture.HasAny(Work.Preflight(game, src, Preset.OptiScaler, pins), "NVIDIA's 310 in OptiScaler"));

        var report = Install(game, src, pins);
        Assert.True(Fixture.HasAny(report,
            "The game's DLSS 2.2.10 is too old for danielblnc 0.8.0's DLSS-SR; OptiScaler hands it 310.9.1"), report.ToLog("install"));
        Assert.Equal(File.ReadAllBytes(Path.Combine(src, "wronggpu", "nvngx_dlss.dll")), File.ReadAllBytes(Path.Combine(game, Library)));

        var gone = Work.Uninstall(game, Preset.OptiScaler);
        Assert.False(gone.Failed, gone.ToLog("uninstall"));
        Assert.False(File.Exists(Path.Combine(game, Library)));
        Assert.True(File.Exists(Path.Combine(game, "nvngx_dlss.dll")));
    }

    [Fact]
    public void ANewDlssGetsNothingAndNoDlssGets310()
    {
        var (src, pins) = Payloads("hd-new", "0.8.0");
        var fresh = Game("hd-new", "310.4.0.0");
        Assert.False(Work.HostNeedsDlss(fresh, "0.8.0"));
        Install(fresh, src, pins);
        Assert.False(File.Exists(Path.Combine(fresh, Library)));

        var none = Game("hd-none", null);
        Assert.True(Work.HostNeedsDlss(none, "0.8.0"));
        Assert.True(Fixture.HasAny(Install(none, src, pins), "has no nvngx_dlss.dll beside it"));
        Assert.True(File.Exists(Path.Combine(none, Library)));
    }

    [Fact]
    public void AnOlderRuntimeGetsNothingAndTakesOutWhatTheHostedOneHad()
    {
        var game = Game("hd-060", "2.2.10.0");
        var (src060, pins060) = Payloads("hd-060", "0.6.0");
        Assert.False(Work.HostNeedsDlss(game, "0.6.0"));
        Install(game, src060, pins060);
        Assert.False(File.Exists(Path.Combine(game, Library)));

        var (src080, pins080) = Payloads("hd-080", "0.8.0");
        Install(game, src080, pins080);
        Assert.True(File.Exists(Path.Combine(game, Library)));
        Install(game, src060, pins060);
        Assert.False(File.Exists(Path.Combine(game, Library)));

        // Nor without danielblnc's runtime at all.
        var report = Work.Install(game, src080, Preset.OptiScaler, pins080, daniel: false);
        Assert.False(report.Failed, report.ToLog("install"));
        Assert.False(File.Exists(Path.Combine(game, Library)));
    }

    [Fact]
    public void APersonsOwnFileStays()
    {
        var game = Game("hd-own", "2.2.10.0");
        var (src, pins) = Payloads("hd-own", "0.8.0");
        Directory.CreateDirectory(Path.Combine(game, "OptiScaler"));
        var own = Encoding.UTF8.GetBytes("somebody's DLSS");
        File.WriteAllBytes(Path.Combine(game, Library), own);
        Assert.False(Work.HostNeedsDlss(game, "0.8.0"));

        Assert.True(Fixture.HasAny(Install(game, src, pins), "already here, which stays as it is"));
        Assert.Equal(own, File.ReadAllBytes(Path.Combine(game, Library)));
        Assert.False(Work.Uninstall(game, Preset.OptiScaler).Failed);
        Assert.Equal(own, File.ReadAllBytes(Path.Combine(game, Library)));
    }
}
