using AmdNr.Core;

namespace AmdNr.Core.Tests;

/// <summary>danielblnc 0.6.0, which OptiScaler runs inline, on an RX 9070 XT stalled the GPU for seconds until the driver
/// reset it. The sheet says so when that build is picked on OptiScaler, and the problem report flags it in the folder.</summary>
public class InstallStateTests
{
    private static string Folder(string tag, string backend, string extra = "")
    {
        var dir = Fixture.Temp(tag);
        File.WriteAllText(Path.Combine(dir, Work.OptiScalerIni), $"[DlssNr]\nNrBackend = {backend}\n{extra}");
        return dir;
    }

    [Fact]
    public void AnUnstableRuntimeInlineOnAnRx9000IsFlagged()
    {
        var inline = Folder("state-inline", "daniel");
        Assert.Contains("unstable runtime inline", InstallState.OptiNr(inline, "0.6.0", rdna4: true));
        Assert.DoesNotContain("unstable", InstallState.OptiNr(inline, Work.Rdna4Recommended, rdna4: true));
        Assert.DoesNotContain("unstable", InstallState.OptiNr(inline, "0.6.0", rdna4: false));
        // AmdAsync left from an older install: OptiScaler no longer reads it, so the runtime still runs inline.
        Assert.Contains("unstable runtime inline",
            InstallState.OptiNr(Folder("state-old-async", "daniel", "AmdAsync = true\n"), "0.6.0", rdna4: true));
        Assert.DoesNotContain("unstable", InstallState.OptiNr(Folder("state-lmxxf", "lmxxf"), "0.6.0", rdna4: true));
        Assert.Null(InstallState.OptiNr(Fixture.Temp("state-none"), "0.6.0", rdna4: true));

        var lines = InstallState.Lines(inline, null, true);
        Assert.Contains(lines, l => l.Contains("optiscaler    NrBackend=daniel", StringComparison.Ordinal));
    }

    [Fact]
    public void TheSheetWarnsOfAnUnstableBuildOnlyOnAnRx9000()
    {
        Assert.Contains("DEVICE_HUNG", Work.UnstableOnRdna4("0.6.0", rdna4: true));
        Assert.Null(Work.UnstableOnRdna4(Work.Rdna4Recommended, rdna4: true));
        Assert.Null(Work.UnstableOnRdna4("0.6.0", rdna4: false));
        Assert.Null(Work.UnstableOnRdna4("", rdna4: true));
    }
}
