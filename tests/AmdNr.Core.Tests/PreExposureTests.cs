using AmdNr.Core;

namespace AmdNr.Core.Tests;

/// <summary>danielblnc 0.6.0 reads a pre-exposure past the end of OptiScaler 0.5.0's record and turned the frame into
/// green noise: the OptiScaler route sets [DlssNrOnAmd] UsePreExposure=0 in the runtime's ini, keeping the rest of it.</summary>
public class PreExposureTests
{
    [Theory]
    [InlineData("[DlssNrOnAmd]\r\nScale=0.5\r\n")]
    [InlineData("")]
    public void OptiScalerWithRuntime060TurnsPreExposureOff(string existing)
    {
        var game = Fixture.Temp("pre-exposure");
        File.WriteAllBytes(Path.Combine(game, "Game.exe"), Fixture.PeWithImports(true, ["d3d12.dll"]));
        var ini = Path.Combine(game, "dlssnr_on_amd.ini");
        File.WriteAllText(ini, existing);
        var (src, pins) = OptiScalerRouteTests.Payloads("pre-exposure", runtimeVersion: "0.6.0");

        var report = Work.Install(game, src, Preset.OptiScaler, pins);
        Assert.False(report.Failed, report.ToLog("install"));
        Assert.True(Fixture.HasAny(report, "UsePreExposure=0"), report.ToLog("install"));
        var text = File.ReadAllText(ini);
        Assert.Equal("0", Engine.Trim(Engine.GetIni(text, "DlssNrOnAmd", "UsePreExposure")));
        if (existing.Length > 0) Assert.Equal("0.5", Engine.Trim(Engine.GetIni(text, "DlssNrOnAmd", "Scale")));
    }

    /// <summary>0.5.1 never reads it; 0.8.0 runs hosted and reads its ini from dlssnr_amd_host\ only.</summary>
    [Theory]
    [InlineData("0.5.1")]
    [InlineData("0.8.0")]
    public void ARuntimeThatDoesNotReadItLeavesTheIniAlone(string version)
    {
        var game = Fixture.Temp($"pre-exposure-{version}");
        File.WriteAllBytes(Path.Combine(game, "Game.exe"), Fixture.PeWithImports(true, ["d3d12.dll"]));
        var (src, pins) = OptiScalerRouteTests.Payloads($"pre-exposure-{version}", runtimeVersion: version);
        var report = Work.Install(game, src, Preset.OptiScaler, pins);
        Assert.False(report.Failed, report.ToLog("install"));
        Assert.False(File.Exists(Path.Combine(game, "dlssnr_on_amd.ini")));
    }
}
