namespace AmdNr.Core.Tests;

/// <summary>The data folder moved somewhere else: the pointer in Home, which folders will do, and the move.
/// Everything here is in a temp folder of its own, passed in as Home; AppPaths itself is only read.</summary>
public sealed class DataFolderTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "amdnr-tests", "datafolder-" + Guid.NewGuid().ToString("N"));
    private string Home => Path.Combine(_dir, "home");

    public DataFolderTests() => Directory.CreateDirectory(Home);

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); }
        catch (IOException) { }
    }

    private static void Put(string path, string text = "x")
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    /// <summary>A data folder as the app leaves one, with the two things that never move.</summary>
    private void Fill(string root)
    {
        Put(Path.Combine(root, "settings.json"), "{}");
        Put(Path.Combine(root, "games.json"), "[]");
        Put(Path.Combine(root, "runtimes", "abc.dll"), "runtime");
        Put(Path.Combine(root, "logs", "kept", "123", "uninstall.log"), "log");
        Put(Path.Combine(root, "cache", "opti", "0.6.4", "OptiScaler.dll"), new string('o', 300_000));
        Put(Path.Combine(Home, "vulkan-layer", "ReShade64.json"), "{}");
    }

    [Fact]
    public void NoPointerIsHome()
    {
        Assert.Equal(new DataLocation.Found(Home, false), DataLocation.Resolve(Home));
    }

    [Fact]
    public void PointerToAFolderThatIsThereIsThatFolder()
    {
        var custom = Directory.CreateDirectory(Path.Combine(_dir, "D", "data")).FullName;
        DataLocation.Write(Home, custom);
        Assert.Equal(new DataLocation.Found(custom, false), DataLocation.Resolve(Home));
    }

    /// <summary>An unplugged drive: the folder is reported, not made.</summary>
    [Fact]
    public void PointerToAFolderThatIsGoneIsMissingAndNotMade()
    {
        var gone = Path.Combine(_dir, "unplugged", "data");
        DataLocation.Write(Home, gone);
        Assert.Equal(new DataLocation.Found(gone, true), DataLocation.Resolve(Home));
        Assert.False(Directory.Exists(gone));
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("""{ "root": "relative\\path" }""")]
    [InlineData("""{ "other": 1 }""")]
    public void ABrokenPointerIsHome(string text)
    {
        File.WriteAllText(Path.Combine(Home, DataLocation.FileName), text);
        Assert.Equal(new DataLocation.Found(Home, false), DataLocation.Resolve(Home));
    }

    [Fact]
    public void PointingBackAtHomeTakesThePointerAway()
    {
        DataLocation.Write(Home, Path.Combine(_dir, "elsewhere"));
        DataLocation.Write(Home, Home + "\\");
        Assert.False(File.Exists(Path.Combine(Home, DataLocation.FileName)));
    }

    /// <summary>AMDNR_HOME is Home, so a pointer in the real %AppData% is never read while it is set.</summary>
    [Fact]
    public void AmdNrHomeWins()
    {
        var env = Environment.GetEnvironmentVariable("AMDNR_HOME");
        Assert.False(string.IsNullOrEmpty(env));
        Assert.Equal(env, AppPaths.Home);
        Assert.Null(AppPaths.Missing);
        Assert.Equal(env, AppPaths.Root);
    }

    [Fact]
    public void TargetIsThePickedFolderWhenEmptyAndAFolderInsideItOtherwise()
    {
        var empty = Directory.CreateDirectory(Path.Combine(_dir, "empty")).FullName;
        Assert.Equal(empty, DataMove.Target(empty));
        Assert.Equal(Path.Combine(_dir, "new"), DataMove.Target(Path.Combine(_dir, "new")));
        Put(Path.Combine(_dir, "busy", "something.txt"));
        Assert.Equal(Path.Combine(_dir, "busy", DataMove.OwnFolder), DataMove.Target(Path.Combine(_dir, "busy")));
    }

    [Fact]
    public void MoveCopiesEverythingPointsAtItAndDeletesTheOld()
    {
        Fill(Home);
        var to = Path.Combine(_dir, "D", "OpenNR");
        var seen = new List<double>();

        var left = DataMove.Move(Home, to, Home, withCache: true, new SyncProgress(seen.Add));

        Assert.Equal(0, left);
        Assert.Equal(new DataLocation.Found(to, false), DataLocation.Resolve(Home));
        Assert.Equal("runtime", File.ReadAllText(Path.Combine(to, "runtimes", "abc.dll")));
        Assert.True(File.Exists(Path.Combine(to, "logs", "kept", "123", "uninstall.log")));
        Assert.Equal(300_000, new FileInfo(Path.Combine(to, "cache", "opti", "0.6.4", "OptiScaler.dll")).Length);
        // Home keeps only the pointer and the layer games load from where it was registered.
        Assert.Equal([DataLocation.FileName, "vulkan-layer"],
            Directory.GetFileSystemEntries(Home).Select(p => Path.GetFileName(p)!).Order().ToArray());
        Assert.False(Directory.Exists(Path.Combine(to, "vulkan-layer")));
        Assert.Equal(1.0, seen.Last(), 3);
    }

    [Fact]
    public void StartingEmptyMovesAllButTheCacheAndDropsTheOldCache()
    {
        Fill(Home);
        var to = Path.Combine(_dir, "D", "OpenNR");
        Assert.True(DataMove.Size(Home, withCache: false) < DataMove.Size(Home, withCache: true));

        DataMove.Move(Home, to, Home, withCache: false);

        Assert.True(File.Exists(Path.Combine(to, "games.json")));
        Assert.True(File.Exists(Path.Combine(to, "settings.json")));
        Assert.True(File.Exists(Path.Combine(to, "runtimes", "abc.dll")));
        Assert.False(Directory.Exists(Path.Combine(to, "cache")));
        Assert.False(Directory.Exists(Path.Combine(Home, "cache")));
    }

    /// <summary>A file that cannot be read halfway through: what was copied goes, the old folder and the pointer
    /// stay as they were.</summary>
    [Fact]
    public void AFailedMoveLeavesTheOldFolderAndThePointerAlone()
    {
        Fill(Home);
        var to = Path.Combine(_dir, "D", "OpenNR");
        var before = Directory.GetFiles(Home, "*", SearchOption.AllDirectories).Order().ToArray();

        using (new FileStream(Path.Combine(Home, "runtimes", "abc.dll"), FileMode.Open, FileAccess.Read, FileShare.None))
            Assert.ThrowsAny<IOException>(() => DataMove.Move(Home, to, Home, withCache: true));

        Assert.Equal(before, Directory.GetFiles(Home, "*", SearchOption.AllDirectories).Order().ToArray());
        Assert.Equal("runtime", File.ReadAllText(Path.Combine(Home, "runtimes", "abc.dll")));
        Assert.False(File.Exists(Path.Combine(Home, DataLocation.FileName)));
        Assert.False(Directory.Exists(to));
    }

    /// <summary>Into a folder that was there already: only what the move wrote is taken back.</summary>
    [Fact]
    public void AFailedMoveIntoAnExistingFolderLeavesThatFolderAsItWas()
    {
        Fill(Home);
        var to = Directory.CreateDirectory(Path.Combine(_dir, "D", "OpenNR")).FullName;

        using (new FileStream(Path.Combine(Home, "settings.json"), FileMode.Open, FileAccess.Read, FileShare.None))
            Assert.ThrowsAny<IOException>(() => DataMove.Move(Home, to, Home, withCache: true));

        Assert.True(Directory.Exists(to));
        Assert.Empty(Directory.GetFiles(to, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public void MovingBackToHomeTakesThePointerAwayAndTheOtherFolder()
    {
        var custom = Path.Combine(_dir, "D", "OpenNR");
        Fill(custom);
        DataLocation.Write(Home, custom);

        DataMove.Move(custom, Home, Home, withCache: true);

        Assert.Equal(new DataLocation.Found(Home, false), DataLocation.Resolve(Home));
        Assert.True(File.Exists(Path.Combine(Home, "games.json")));
        Assert.True(File.Exists(Path.Combine(Home, "vulkan-layer", "ReShade64.json")));
        Assert.False(Directory.Exists(custom));
    }

    private (DataFolderProblem Problem, string? Game) Check(string target, string? current = null, ulong need = 0,
        params string[] games) => DataMove.Check(target, current ?? Home, Home, games, need);

    [Fact]
    public void AnEmptyFolderElsewhereWillDo()
    {
        Assert.Equal(DataFolderProblem.None, Check(Path.Combine(_dir, "D", "OpenNR")).Problem);
        Assert.False(Directory.Exists(Path.Combine(_dir, "D", "OpenNR")), "the write test leaves no folder behind");
        Assert.Equal(DataFolderProblem.None, Check(Directory.CreateDirectory(Path.Combine(_dir, "E")).FullName).Problem);
    }

    [Fact]
    public void TheFolderInUseInsideItOrAroundItWillNot()
    {
        Assert.Equal(DataFolderProblem.Nested, Check(Home).Problem);
        Assert.Equal(DataFolderProblem.Nested, Check(Path.Combine(Home, "sub")).Problem);
        var custom = Path.Combine(_dir, "D", "OpenNR");
        Assert.Equal(DataFolderProblem.Nested, Check(Path.Combine(_dir, "D"), current: custom).Problem);
        // Inside Home is out even when the data is elsewhere; Home itself is the way back.
        Assert.Equal(DataFolderProblem.Nested, Check(Path.Combine(Home, "sub"), current: custom).Problem);
        Assert.Equal(DataFolderProblem.None, Check(Home, current: custom).Problem);
    }

    [Fact]
    public void ProgramFilesAndWindowsWillNot()
    {
        Assert.Equal(DataFolderProblem.SystemFolder,
            Check(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "OpenNR")).Problem);
        Assert.Equal(DataFolderProblem.SystemFolder,
            Check(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp", "OpenNR")).Problem);
    }

    [Fact]
    public void AGameFolderWillNot()
    {
        var game = Path.Combine(_dir, "Games", "Cyberpunk 2077");
        Assert.Equal((DataFolderProblem.GameFolder, game), Check(Path.Combine(game, "data"), games: game));
        Assert.Equal(DataFolderProblem.None, Check(Path.Combine(_dir, "Games", "other"), games: game).Problem);
    }

    [Fact]
    public void AFolderWithFilesInItWillNot()
    {
        Put(Path.Combine(_dir, "busy", "something.txt"));
        Assert.Equal(DataFolderProblem.NotEmpty, Check(Path.Combine(_dir, "busy")).Problem);
    }

    [Fact]
    public void AFolderThatCannotBeWrittenWillNot()
    {
        var used = DriveInfo.GetDrives().Select(d => char.ToUpperInvariant(d.Name[0])).ToHashSet();
        var free = Enumerable.Range('D', 23).Select(c => (char)c).First(c => !used.Contains(c));
        Assert.Equal(DataFolderProblem.NotWritable, Check($@"{free}:\OpenNR").Problem);
    }

    [Fact]
    public void ADriveWithoutRoomWillNot()
    {
        Assert.Equal(DataFolderProblem.NoSpace, Check(Path.Combine(_dir, "D"), need: ulong.MaxValue).Problem);
    }

    /// <summary>Progress&lt;T&gt; reports on the thread pool; the test reads what came in right after.</summary>
    private sealed class SyncProgress(Action<double> report) : IProgress<double>
    {
        public void Report(double value) => report(value);
    }
}
