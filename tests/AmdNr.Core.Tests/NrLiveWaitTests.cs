using AmdNr.Core;

namespace AmdNr.Core.Tests;

/// <summary>Scale with a library game that is not running yet waits for its window, then starts NRLive, and gives up
/// after five minutes.</summary>
public class NrLiveWaitTests
{
    [Fact]
    public void ItWaitsForTheGamesWindowAndThenIsReady()
    {
        var now = new DateTime(2026, 11, 3, 14, 0, 0, DateTimeKind.Utc);
        var open = new List<string> { "explorer.exe", "MyRe4.exe" };
        var wait = new NrLiveWait(NrLiveProfile.ExactProcess("re4.exe"), () => open, () => now);
        Assert.Equal("re4.exe", wait.Exe);
        Assert.Equal(NrLiveWaitState.Waiting, wait.Poll());
        now = now.AddMinutes(4);
        Assert.Equal(NrLiveWaitState.Waiting, wait.Poll());
        open.Add("re4.exe");
        Assert.Equal(NrLiveWaitState.Ready, wait.Poll());
    }

    [Fact]
    public void ItGivesUpAfterFiveMinutes()
    {
        var now = new DateTime(2026, 11, 3, 14, 0, 0, DateTimeKind.Utc);
        var wait = new NrLiveWait(NrLiveProfile.ExactProcess("re4.exe"), () => [], () => now);
        Assert.Equal(NrLiveWaitState.Waiting, wait.Poll());
        now += NrLiveWait.Limit - TimeSpan.FromSeconds(1);
        Assert.Equal(NrLiveWaitState.Waiting, wait.Poll());
        now += TimeSpan.FromSeconds(1);
        Assert.Equal(NrLiveWaitState.GaveUp, wait.Poll());
    }

    [Fact]
    public void ItMatchesAsNRLiveDoesAndABadPatternMatchesNothing()
    {
        Assert.True(NrLiveWait.Matches(@"^re4\.exe$", "re4.exe"));
        Assert.False(NrLiveWait.Matches(@"^re4\.exe$", "re4_exe"));
        Assert.True(NrLiveWait.Matches("Game", "MyGame.exe"));
        Assert.False(NrLiveWait.Matches("(", "re4.exe"));
    }
}
