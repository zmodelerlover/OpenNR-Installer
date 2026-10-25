using AmdNr.Core;

namespace AmdNr.Core.Tests;

/// <summary>Settings per game for NRLive: a copy of scaleconfig.ini's keys and the launch profile's, per library game
/// and one global, swapped into NRLive's files as the target says. Each test in a folder of its own.</summary>
public class NrLiveProfilesTests
{
    private static readonly NrLiveSetting Sharpness = NrLiveConfig.Settings.First(s => s.Id == "FSR.sharpness");
    private static readonly NrLiveSetting LimitFps = NrLiveConfig.Settings.First(s => s.Id == "FrameLimiter.fps");

    private static (string Folder, NrLiveProfiles Profiles, NrLiveGame Game) Setup()
    {
        var folder = Fixture.Temp("nrlive-profiles");
        File.WriteAllText(Path.Combine(folder, NrLiveConfig.FileName), "[FSR]\r\nsharpness=65\r\n[General]\r\nscreenshot_path=C:\\Shots\r\n");
        new NrLiveProfile { TargetMode = "pname", TargetText = "^Game\\.exe$" }.Save(folder);
        var game = Path.Combine(folder, "games", "Some Game");
        return (folder, new NrLiveProfiles(folder), new NrLiveGame(NrLiveGame.IdFor(game), "Some Game", game, Path.Combine(game, "bin", "Game.exe")));
    }

    private static int Get(string folder, NrLiveSetting s) => NrLiveConfig.Load(folder).Get(s);

    /// <summary>A change the page makes: written to scaleconfig.ini, then into the copy the files hold.</summary>
    private static void Change(string folder, NrLiveProfiles profiles, NrLiveSetting s, int value)
    {
        var config = NrLiveConfig.Load(folder);
        config.Set(s, value);
        config.Save();
        profiles.Capture();
    }

    [Fact]
    public void AGameGetsItsOwnCopyOnTheFirstChangeAndTheGlobalOneKeepsItsValues()
    {
        var (folder, profiles, game) = Setup();
        profiles.Use(profiles.Effective(game));
        Assert.False(profiles.Has(game));
        Assert.Equal(NrLiveProfiles.Global, profiles.Holder);

        profiles.Fork(game);
        Change(folder, profiles, Sharpness, 30);
        var launch = NrLiveProfile.Load(folder);
        launch.Upscaler = "fsr31";
        launch.Save(folder);
        profiles.Capture();
        Assert.True(profiles.Has(game));
        Assert.Equal(game.Id, profiles.Holder);
        Assert.True(File.Exists(Path.Combine(folder, "opennr-profiles", game.Id + ".json")));

        profiles.Use(NrLiveProfiles.Global);
        Assert.Equal(65, Get(folder, Sharpness));
        Assert.Equal("fsr4", NrLiveProfile.Load(folder).Upscaler);
        profiles.Use(profiles.Effective(game));
        Assert.Equal(30, Get(folder, Sharpness));
        Assert.Equal("fsr31", NrLiveProfile.Load(folder).Upscaler);
        // What to capture is one setting for all, and keys the page does not manage are left alone.
        Assert.Equal("pname", NrLiveProfile.Load(folder).TargetMode);
        Assert.Equal("C:\\Shots", NrLiveConfig.Load(folder).Raw("General", "screenshot_path"));
    }

    [Fact]
    public void ARunningExecutableIsMatchedToItsLibraryGameByPath()
    {
        var a = new NrLiveGame("a", "A", @"D:\Games\A", @"D:\Games\A\bin\a.exe");
        var b = new NrLiveGame("b", "B", @"D:\Games\A\Tools\B", null);
        var c = new NrLiveGame("c", "C", @"D:\Games\C", @"E:\Elsewhere\c.exe");
        NrLiveGame[] games = [a, b, c];
        Assert.Equal(a, NrLiveProfiles.Match(@"d:\games\a\BIN\A.EXE", games));
        Assert.Equal(a, NrLiveProfiles.Match(@"D:\Games\A\launcher.exe", games));
        Assert.Equal(b, NrLiveProfiles.Match(@"D:\Games\A\Tools\B\b.exe", games));
        Assert.Equal(c, NrLiveProfiles.Match(@"E:\Elsewhere\c.exe", games));
        Assert.Null(NrLiveProfiles.Match(@"D:\Games\AB\x.exe", games));
        Assert.Null(NrLiveProfiles.Match(null, games));
        Assert.Equal(NrLiveGame.IdFor(@"D:\Games\A\"), NrLiveGame.IdFor(@"d:\games\a"));
        Assert.NotEqual(NrLiveGame.IdFor(@"D:\Games\A"), NrLiveGame.IdFor(@"E:\Games\A"));
    }

    [Fact]
    public void TheWindowInFrontWhenTheCountdownEndsBringsItsGamesSettings()
    {
        var (folder, profiles, game) = Setup();
        var other = new NrLiveGame(NrLiveGame.IdFor(Path.Combine(folder, "games", "Other")), "Other", Path.Combine(folder, "games", "Other"), null);
        profiles.Fork(game);
        Change(folder, profiles, Sharpness, 20);
        profiles.Use(NrLiveProfiles.Global);

        var asked = 0;
        Assert.Equal(game, profiles.UseForeground(() => { asked++; return game.Exe; }, [game, other]));
        Assert.Equal(1, asked);
        Assert.Equal(20, Get(folder, Sharpness));
        Assert.Equal(game.Id, profiles.Holder);

        // A library game without settings of its own, and a window of no game at all: the global settings.
        Assert.Equal(other, profiles.UseForeground(() => Path.Combine(other.Folder, "o.exe"), [game, other]));
        Assert.Equal(65, Get(folder, Sharpness));
        Assert.Null(profiles.UseForeground(() => @"C:\Windows\explorer.exe", [game, other]));
        Assert.Equal(NrLiveProfiles.Global, profiles.Holder);
    }

    [Fact]
    public void WhatTheOverlaySavedDuringASessionGoesBackIntoTheCopyItRanWith()
    {
        var (folder, profiles, game) = Setup();
        profiles.Fork(game);
        profiles.Use(NrLiveProfiles.Global);
        Change(folder, profiles, LimitFps, 90);
        profiles.Use(game.Id);
        // The game's copy was made before the limit was set: the key is taken out, NRLive's default applies.
        Assert.Null(NrLiveConfig.Load(folder).Raw("FrameLimiter", "fps"));

        // NRLive's overlay writes the file itself during the session; the session's end saves it back.
        File.WriteAllText(Path.Combine(folder, NrLiveConfig.FileName), "[FSR]\r\nsharpness=10\r\n");
        profiles.Capture();
        profiles.Use(NrLiveProfiles.Global);
        Assert.Equal(65, Get(folder, Sharpness));
        Assert.Equal(90, Get(folder, LimitFps));
        profiles.Use(game.Id);
        Assert.Equal(10, Get(folder, Sharpness));
    }

    [Fact]
    public void ResetGoesBackToTheGlobalSettings()
    {
        var (folder, profiles, game) = Setup();
        profiles.Fork(game);
        Change(folder, profiles, Sharpness, 30);

        profiles.Reset(game);
        Assert.False(profiles.Has(game));
        Assert.Equal(NrLiveProfiles.Global, profiles.Holder);
        Assert.Equal(65, Get(folder, Sharpness));
        // Not held: only its copy goes, NRLive's files stay with what they hold.
        profiles.Fork(game);
        Change(folder, profiles, Sharpness, 40);
        profiles.Use(NrLiveProfiles.Global);
        Change(folder, profiles, Sharpness, 70);
        profiles.Reset(game);
        Assert.Equal(70, Get(folder, Sharpness));
        Assert.Equal(NrLiveProfiles.Global, profiles.Effective(game));
    }
}
