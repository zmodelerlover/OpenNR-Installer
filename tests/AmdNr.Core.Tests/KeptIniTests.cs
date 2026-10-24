using AmdNr.Core;

namespace AmdNr.Core.Tests;

/// <summary>An ini this route wrote and the player changed since is kept on a reinstall, and the report says what it
/// names instead of the runtime a fresh one would have.</summary>
public class KeptIniTests
{
    [Fact]
    public void ReinstallReportsTheKeptIni()
    {
        var game = Fixture.Temp("kept-ini");
        File.WriteAllBytes(Path.Combine(game, "Game.exe"), Fixture.PeWithImports(true, ["d3d12.dll"]));
        File.WriteAllText(Path.Combine(game, "nvngx_dlss.dll"), "dlss");
        var (src, pins) = OptiScalerRouteTests.Payloads("kept-ini");

        var report = Work.Install(game, src, Preset.OptiScaler, pins);
        Assert.False(report.Failed, report.ToLog("install"));
        var ini = Path.Combine(game, Work.OptiScalerIni);
        File.WriteAllText(ini, Engine.SetIni(File.ReadAllText(ini), "DlssNr", "NrBackend", "auto"));

        report = Work.Install(game, src, Preset.OptiScaler, pins);
        Assert.False(report.Failed, report.ToLog("reinstall"));
        Assert.Contains(report.Lines, l => l.Text.Contains("stays as it is: its NR runtime is auto, which is danielblnc's"));
        Assert.Equal("auto", Engine.Trim(Engine.GetIni(File.ReadAllText(ini), "DlssNr", "NrBackend")));
    }
}
