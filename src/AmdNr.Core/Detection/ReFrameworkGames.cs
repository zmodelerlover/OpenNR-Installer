// The RE Engine games REFramework runs in, for the REFramework card: api-db.json's "reframework" key, written by hand
// like "rr" and kept by tools/ApiDbBuilder. Each game names the REFramework build that fits it ("RE9" is the
// reframework-re9 component in payload.json), so a game patch that needs another build is a change to this list or to
// the payload, not a new app. Matched by Steam app id, then executable name, then title; an app older than this key
// ignores it.

using System.Text.Json.Serialization;

namespace AmdNr.Core;

/// <summary>A game of a hand-written list in api-db.json.</summary>
public interface IListedGame
{
    string Title { get; }
    List<string> SteamAppIds { get; }
    List<string> Executables { get; }
}

// Settable, not init: see ApiDatabase.
public sealed class ReFrameworkGame : IListedGame
{
    [JsonPropertyName("t")] public required string Title { get; init; }
    [JsonPropertyName("steam")] public List<string> SteamAppIds { get; set; } = [];

    /// <summary>Executable file names, matched without case.</summary>
    [JsonPropertyName("exe")] public List<string> Executables { get; set; } = [];

    /// <summary>The REFramework build for it, as REFramework names its release zips: "RE9", "DMC5", "RE2_TDB66".</summary>
    [JsonPropertyName("build")] public string Build { get; set; } = "";

    /// <summary>Something this project installs needs REFramework in this game: the box starts ticked. None yet.</summary>
    [JsonPropertyName("needed")] public bool Needed { get; set; }

    /// <summary>Why a mod here can cost the player ("online", "anti-cheat"): the card warns and the box starts unticked,
    /// <see cref="Needed"/> or not.</summary>
    [JsonPropertyName("risk")] public string? Risk { get; set; }

    /// <summary>A note for whoever keeps the list, not shown.</summary>
    [JsonPropertyName("why")] public string? Why { get; set; }

    [JsonIgnore] public bool Risky => !string.IsNullOrWhiteSpace(Risk);

    /// <summary>The payload component that carries its build.</summary>
    [JsonIgnore] public string Component => PayloadManifest.ReFrameworkBuildComponent(Build);
}

public sealed partial class ApiDatabase
{
    [JsonPropertyName("reframework")] public List<ReFrameworkGame> ReFramework { get; set; } = [];

    /// <summary>The listed RE Engine game this one is, or null.</summary>
    public ReFrameworkGame? ReFrameworkGameFor(string? steamAppId, string? executable, params string?[] titles) =>
        Listed(ReFramework.Where(g => g.Build.Length > 0).ToList(), steamAppId, executable, titles);
}
