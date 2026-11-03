using AmdNr.Core;

namespace AmdNr.Core.Tests;

/// <summary>RTSS hooked into Cities: Skylines II stuttered with NR on (2026-11): the pre-flight warns while it runs and
/// notes it when it is only installed.</summary>
public class RtssTests
{
    [Fact]
    public void RunningWarnsInstalledNotesNeitherSaysNothing()
    {
        var running = new Report();
        Work.CheckRtss(running, running: true, installed: true);
        Assert.Equal([(Level.Warn, Work.RtssRunning)], running.Lines);

        var installed = new Report();
        Work.CheckRtss(installed, running: false, installed: true);
        Assert.Equal([(Level.Info, Work.RtssInstalled)], installed.Lines);

        var none = new Report();
        Work.CheckRtss(none, running: false, installed: false);
        Assert.Empty(none.Lines);
        Assert.False(running.Failed);
    }
}
