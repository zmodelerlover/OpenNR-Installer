// Scale with a library game picked (-pname) before the game runs: NRLive exits at once with "No process name matches
// regex", so the page waits here for the game's window instead, once a second, and starts NRLive when it is up.

using System.Text.RegularExpressions;

namespace AmdNr.Core;

public enum NrLiveWaitState { Waiting, Ready, GaveUp }

/// <summary>Waiting for a process that matches a -pname pattern to have a window. <paramref name="windows"/> gives the
/// executable names of the processes with a visible window now; <paramref name="clock"/> is the time, for a test.</summary>
public sealed class NrLiveWait(string pattern, Func<IEnumerable<string>> windows, Func<DateTime>? clock = null)
{
    public static readonly TimeSpan Limit = TimeSpan.FromMinutes(5);

    private readonly Func<DateTime> _clock = clock ?? (() => DateTime.UtcNow);
    private DateTime? _since;

    /// <summary>The executable the pattern names, for the status line: "re4.exe" for ^re4\.exe$.</summary>
    public string Exe { get; } = Regex.Unescape(pattern.TrimStart('^').TrimEnd('$'));

    /// <summary>Whether a process of this name would be one NRLive takes: it searches with the pattern, as ECMAScript.</summary>
    public static bool Matches(string pattern, string exe)
    {
        try { return Regex.IsMatch(exe, pattern, RegexOptions.ECMAScript, TimeSpan.FromMilliseconds(100)); }
        catch (Exception e) when (e is ArgumentException or RegexMatchTimeoutException) { return false; }
    }

    /// <summary>Ready once a window of the game is up, GaveUp <see cref="Limit"/> after the first poll, Waiting until then.</summary>
    public NrLiveWaitState Poll()
    {
        var now = _clock();
        _since ??= now;
        if (windows().Any(exe => Matches(pattern, exe))) return NrLiveWaitState.Ready;
        return now - _since >= Limit ? NrLiveWaitState.GaveUp : NrLiveWaitState.Waiting;
    }
}
