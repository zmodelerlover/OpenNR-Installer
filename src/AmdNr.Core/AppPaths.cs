// Where this app keeps everything it writes for itself: settings, the list of games, the download
// cache, the logs and the runtime builds a person supplied.

namespace AmdNr.Core;

public static class AppPaths
{
    /// <summary>%AppData%\AmdNrInstaller, created on first use -- or wherever AMDNR_HOME says.
    /// The override is what keeps the test suite out of the real cache, and it is also how a
    /// portable install would keep everything beside the executable. Always here, wherever the data
    /// went: the pointer to a data folder moved elsewhere (<see cref="DataLocation"/>), and the Vulkan
    /// layer this app registers, which games load from the path it was registered with.</summary>
    public static string Home { get; } = Create(
        Environment.GetEnvironmentVariable("AMDNR_HOME") is { Length: > 0 } home
            ? home
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData,
                    Environment.SpecialFolderOption.Create),
                "AmdNrInstaller"));

    private static DataLocation.Found _found = DataLocation.Resolve(Home);
    private static string? _root;

    /// <summary>The data folder the pointer names when it is not there -- a drive unplugged. Settled with
    /// <see cref="Reload"/> or <see cref="UseDefault"/> before anything reads <see cref="Root"/>: an empty
    /// folder made in its place would read as no games and no settings.</summary>
    public static string? Missing => _found.Missing ? _found.Root : null;

    /// <summary>Reads the pointer again: a drive plugged back in, or a move that just pointed it somewhere
    /// else -- whatever this process still writes on its way out goes to the new folder, not a deleted one.</summary>
    public static void Reload()
    {
        _found = DataLocation.Resolve(Home);
        _root = null;
    }

    /// <summary>Moves the data to <paramref name="target"/> (<see cref="DataMove.Move"/>) and reads the pointer
    /// again, so every path after it is in the new folder: the first run goes on there without a restart.
    /// <see cref="Home"/> as the target is the way back to the default.</summary>
    public static int MoveTo(string target, bool withCache, IProgress<double>? progress = null,
        CancellationToken cancel = default)
    {
        var left = DataMove.Move(Root, target, Home, withCache, progress, cancel);
        Reload();
        return left;
    }

    public static void UseDefault()
    {
        try { DataLocation.Write(Home, null); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        Reload();
    }

    /// <summary>The data folder: <see cref="Home"/>, or the folder someone moved it to.</summary>
    public static string Root => _found.Missing
        ? throw new InvalidOperationException($"The data folder {_found.Root} is not there.")
        : _root ??= Create(_found.Root);

    public static string Cache => Create(Path.Combine(Root, "cache"));
    public static string Logs => Create(Path.Combine(Root, "logs"));

    /// <summary>danielblnc's builds a person supplied (UserRuntime), kept by hash. Outside the cache on
    /// purpose: emptying the cache brings everything in it back by download, and these never come back
    /// that way.</summary>
    public static string Runtimes => Create(Path.Combine(Root, "runtimes"));
    public static string GamesFile => Path.Combine(Root, "games.json");
    public static string CrashLog => Path.Combine(Logs, "crash.log");

    private static string Create(string dir)
    {
        Directory.CreateDirectory(dir);
        return dir;
    }
}
