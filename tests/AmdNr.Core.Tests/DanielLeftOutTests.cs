using AmdNr.Core;

namespace AmdNr.Core.Tests;

/// <summary>A player who runs open source only leaves danielblnc's runtime out in Settings. OptiScaler then goes in without
/// its passes and weights, an earlier install's come out, and a fresh ini does not name a runtime that is not there.</summary>
public class DanielLeftOutTests
{
    [Fact]
    public void OptiScalerGoesInWithoutDanielblncsFiles()
    {
        var game = Fixture.Temp("daniel-out");
        File.WriteAllBytes(Path.Combine(game, "Game.exe"), Fixture.PeWithImports(true, ["d3d12.dll"]));
        File.WriteAllText(Path.Combine(game, "nvngx_dlss.dll"), "dlss");
        var (src, pins) = OptiScalerRouteTests.Payloads("daniel-out");

        var report = Work.Install(game, src, Preset.OptiScaler, pins);
        Assert.False(report.Failed, report.ToLog("install"));
        Assert.True(File.Exists(Path.Combine(game, Work.WeightsName)));

        report = Work.Install(game, src, Preset.OptiScaler, pins, daniel: false);
        Assert.False(report.Failed, report.ToLog("install without danielblnc"));
        Assert.False(File.Exists(Path.Combine(game, Work.WeightsName)));
        Assert.DoesNotContain(Work.OptiPasses, p => File.Exists(Path.Combine(game, p)));
        Assert.True(Work.Preflight(game, src, Preset.OptiScaler, pins, daniel: false).Lines.All(l => l.Level != Level.Err));

        var backend = Engine.Trim(Engine.GetIni(File.ReadAllText(Path.Combine(game, Work.OptiScalerIni)), "DlssNr", "NrBackend"));
        Assert.Equal("off", backend);
    }
}
