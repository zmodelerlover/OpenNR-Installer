// Settings per game for NRLive. NRLive itself has one scaleconfig.ini and one launch profile; the page keeps a copy
// of both for each library game that was given settings of its own, and one of the global setup, under
// nrlive\opennr-profiles, and swaps the copy a session needs into NRLive's files. Which copy those files hold now is
// written down (holder.txt), so whatever NRLive's overlay saves during a session goes back into that copy and no
// other, and a session cut off by a crash is still put back where it belongs the next time.
//
// Only what is a matter of taste is copied: the upscaler, capture, motion vectors, the overlay and NRLive's own keys
// from the launch profile, and the scaleconfig.ini keys the page shows. What to capture, the countdown, the scale
// hotkey and running elevated are needed before the game is known, so they stay one setting for all.

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AmdNr.Core;

/// <summary>A library game as the profiles know it: <paramref name="Id"/> from its folder (<see cref="IdFor"/>), the
/// name for the menu, and the executable the library found in it, when it found one.</summary>
public sealed record NrLiveGame(string Id, string Name, string Folder, string? Exe)
{
    /// <summary>The folder's name and a hash of its full path: the same game keeps its file when renamed in the
    /// library, and two games in folders of the same name do not share one.</summary>
    public static string IdFor(string folder)
    {
        var full = Path.GetFullPath(folder).TrimEnd('\\', '/').ToLowerInvariant();
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(full)))[..12];
        var slug = new string(Path.GetFileName(full).Select(c => char.IsAsciiLetterOrDigit(c) ? c : '-').ToArray()).Trim('-');
        return slug.Length > 0 ? $"{slug[..Math.Min(slug.Length, 40)]}-{hash}" : hash;
    }
}

/// <summary>The launch profile's part of a copy (see <see cref="NrLiveProfile"/> for each field).</summary>
public sealed record NrLiveLaunch
{
    public string Upscaler { get; set; } = "fsr4";
    public string CaptureMode { get; set; } = "wgc";
    public string Motion { get; set; } = "fast";
    public bool NoOverlay { get; set; }
    public string StopKey { get; set; } = "ctrl+shift+a";
    public string OverlayKey { get; set; } = "ctrl+home";
    public string BindBypass { get; set; } = "home,insert,end,pageup,pagedown";

    public static NrLiveLaunch From(NrLiveProfile p) => new()
    {
        Upscaler = p.Upscaler, CaptureMode = p.CaptureMode, Motion = p.Motion, NoOverlay = p.NoOverlay,
        StopKey = p.StopKey, OverlayKey = p.OverlayKey, BindBypass = p.BindBypass,
    };

    public void ApplyTo(NrLiveProfile p)
    {
        (p.Upscaler, p.CaptureMode, p.Motion, p.NoOverlay) = (Upscaler, CaptureMode, Motion, NoOverlay);
        (p.StopKey, p.OverlayKey, p.BindBypass) = (StopKey, OverlayKey, BindBypass);
    }
}

/// <summary>One copy: whose it is (empty for the global one), its launch part, and the scaleconfig.ini keys
/// (<see cref="NrLiveConfig.ProfileKeys"/>) as the file had them, "Section.key" to the text; a key left out was
/// not in the file, so NRLive's default applied.</summary>
public sealed class NrLiveProfileCopy
{
    public string Name { get; set; } = "";
    public string Folder { get; set; } = "";
    public NrLiveLaunch Launch { get; set; } = new();
    public Dictionary<string, string> Settings { get; set; } = [];
}

public sealed class NrLiveProfiles(string folder)
{
    public const string Global = "global";

    private string Dir => Path.Combine(folder, "opennr-profiles");
    private string FileOf(string id) => Path.Combine(Dir, id + ".json");
    private string HolderFile => Path.Combine(Dir, "holder.txt");

    /// <summary>Whose copy NRLive's files hold now.</summary>
    public string Holder
    {
        get
        {
            try { return File.Exists(HolderFile) && File.ReadAllText(HolderFile).Trim() is { Length: > 0 } id ? id : Global; }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return Global; }
        }
        private set
        {
            Engine.MakeParent(HolderFile);
            File.WriteAllText(HolderFile, value);
        }
    }

    public bool Has(NrLiveGame game) => File.Exists(FileOf(game.Id));

    /// <summary>The copy a game runs with: its own once it has one, the global one until then and for no game.</summary>
    public string Effective(NrLiveGame? game) => game is not null && Has(game) ? game.Id : Global;

    /// <summary>NRLive's files given the copy of <paramref name="id"/>, the copy they held saved from them first.</summary>
    public void Use(string id)
    {
        if (Holder == id) return;
        Capture();
        if (Read(id) is { } copy) Write(copy);
        Holder = id;
    }

    /// <summary>NRLive's files as they are now, saved into the copy they hold: after a session, whatever its overlay
    /// saved; after a change on the page, that change. A game copy deleted meanwhile is not made again.</summary>
    public void Capture()
    {
        var holder = Holder;
        if (holder != Global && !File.Exists(FileOf(holder))) return;
        var old = Read(holder);
        Save(holder, Snapshot(old?.Name ?? "", old?.Folder ?? ""));
    }

    /// <summary>The game's own copy, made from NRLive's files as they are: called before the first change made for a
    /// game shown on the global settings, so the change lands in its copy and the global one keeps what it had.</summary>
    public void Fork(NrLiveGame game)
    {
        if (Has(game)) return;
        Capture();
        Save(game.Id, Snapshot(game.Name, game.Folder));
        Holder = game.Id;
    }

    /// <summary>Back to the global settings: the game's copy deleted, and NRLive's files given the global copy if they
    /// held the game's.</summary>
    public void Reset(NrLiveGame game)
    {
        var held = Holder == game.Id;
        File.Delete(FileOf(game.Id));
        if (!held) return;
        if (Read(Global) is { } global) Write(global);
        Holder = Global;
    }

    /// <summary>As a countdown ends on the window in front: the copy of the library game whose executable runs it, or
    /// the global one. NRLive reads scaleconfig.ini again when it changes, so a running NRLive follows.</summary>
    public NrLiveGame? UseForeground(Func<string?> foregroundExe, IEnumerable<NrLiveGame> games)
    {
        var game = Match(foregroundExe(), games);
        Use(Effective(game));
        return game;
    }

    /// <summary>The library game a running executable belongs to: the one that names that executable, else the one
    /// whose folder holds it (the innermost, for a game inside another's folder); null for none.</summary>
    public static NrLiveGame? Match(string? exe, IEnumerable<NrLiveGame> games)
    {
        if (string.IsNullOrEmpty(exe)) return null;
        var list = games.ToList();
        return list.FirstOrDefault(g => g.Exe is not null && Engine.SamePath(g.Exe, exe))
               ?? list.Where(g => Engine.IsInside(g.Folder, exe)).MaxBy(g => g.Folder.Length);
    }

    private NrLiveProfileCopy? Read(string id)
    {
        try
        {
            var path = FileOf(id);
            return File.Exists(path) ? JsonSerializer.Deserialize(File.ReadAllText(path), NrLiveJson.Default.NrLiveProfileCopy) : null;
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException) { return null; }
    }

    private void Save(string id, NrLiveProfileCopy copy)
    {
        Engine.MakeParent(FileOf(id));
        File.WriteAllText(FileOf(id), JsonSerializer.Serialize(copy, NrLiveJson.Default.NrLiveProfileCopy));
    }

    private NrLiveProfileCopy Snapshot(string name, string gameFolder)
    {
        var config = NrLiveConfig.Load(folder);
        var copy = new NrLiveProfileCopy { Name = name, Folder = gameFolder, Launch = NrLiveLaunch.From(NrLiveProfile.Load(folder)) };
        foreach (var (section, key) in NrLiveConfig.ProfileKeys)
            if (config.Raw(section, key) is { } value) copy.Settings[$"{section}.{key}"] = value;
        return copy;
    }

    private void Write(NrLiveProfileCopy copy)
    {
        var config = NrLiveConfig.Load(folder);
        foreach (var (section, key) in NrLiveConfig.ProfileKeys)
            config.SetRaw(section, key, copy.Settings.GetValueOrDefault($"{section}.{key}"));
        config.Save();
        var profile = NrLiveProfile.Load(folder);
        copy.Launch.ApplyTo(profile);
        profile.Save(folder);
    }
}
