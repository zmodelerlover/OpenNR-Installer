namespace AmdNr.Core.Tests;

[CollectionDefinition("AppPathsMove", DisableParallelization = true)]
public class AppPathsMoveCollection;

/// <summary>The first run's data folder step: AppPaths.MoveTo on the real AppPaths (this run's AMDNR_HOME), which
/// every other test reads, so it runs alone and always ends back in Home.</summary>
[Collection("AppPathsMove")]
public sealed class FirstRunDataFolderTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "amdnr-tests", "firstrun-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (!string.Equals(AppPaths.Root, AppPaths.Home, StringComparison.OrdinalIgnoreCase))
            AppPaths.MoveTo(AppPaths.Home, withCache: true);
        try { Directory.Delete(_dir, recursive: true); }
        catch (IOException) { }
    }

    [Fact]
    public void WhatTheWizardWroteFollowsTheFolderAndEveryPathWithItThenComesBack()
    {
        var home = AppPaths.Home;
        Assert.Equal(home, AppPaths.Root);
        // What the steps before it leave behind: the language, a log line, the api list.
        File.WriteAllText(Path.Combine(AppPaths.Root, "settings.json"), """{ "Language": "pt-BR" }""");
        File.WriteAllText(Path.Combine(AppPaths.Logs, "amd-nr-installer.log"), "started");
        File.WriteAllText(Path.Combine(AppPaths.Root, "api-db.json"), "{}");

        var first = Path.Combine(_dir, "D", "OpenNR");
        Assert.Equal(0, AppPaths.MoveTo(first, withCache: true));
        Assert.Equal(first, AppPaths.Root);
        Assert.Equal(first, DataLocation.Read(home));
        Assert.StartsWith(first, AppPaths.Cache);
        Assert.StartsWith(first, AppPaths.Logs);
        Assert.StartsWith(first, AppPaths.GamesFile);
        Assert.Equal("""{ "Language": "pt-BR" }""", File.ReadAllText(Path.Combine(first, "settings.json")));
        Assert.Equal("started", File.ReadAllText(Path.Combine(first, "logs", "amd-nr-installer.log")));
        Assert.True(File.Exists(Path.Combine(first, "api-db.json")));
        Assert.False(File.Exists(Path.Combine(home, "settings.json")));
        Assert.False(Directory.Exists(Path.Combine(home, "logs")));

        // Back in the wizard, another folder: from the first one, which goes.
        var second = Path.Combine(_dir, "E", "OpenNR");
        AppPaths.MoveTo(second, withCache: true);
        Assert.Equal(second, AppPaths.Root);
        Assert.False(Directory.Exists(first));
        Assert.True(File.Exists(Path.Combine(second, "settings.json")));

        // And the default again: the pointer goes, the files come home, the other folder goes.
        AppPaths.MoveTo(home, withCache: true);
        Assert.Equal(home, AppPaths.Root);
        Assert.False(File.Exists(Path.Combine(home, DataLocation.FileName)));
        Assert.Equal("""{ "Language": "pt-BR" }""", File.ReadAllText(Path.Combine(home, "settings.json")));
        Assert.True(File.Exists(Path.Combine(home, "logs", "amd-nr-installer.log")));
        Assert.False(Directory.Exists(second));
    }

    /// <summary>A move that cannot finish leaves AppPaths where it was.</summary>
    [Fact]
    public void ACancelledMoveKeepsEveryPathWhereItWas()
    {
        File.WriteAllText(Path.Combine(AppPaths.Root, "settings.json"), "{}");
        using var cancel = new CancellationTokenSource();
        cancel.Cancel();
        var target = Path.Combine(_dir, "F", "OpenNR");
        Assert.ThrowsAny<OperationCanceledException>(() => AppPaths.MoveTo(target, withCache: true, cancel: cancel.Token));
        Assert.Equal(AppPaths.Home, AppPaths.Root);
        Assert.Null(DataLocation.Read(AppPaths.Home));
        Assert.False(Directory.Exists(target));
    }
}
