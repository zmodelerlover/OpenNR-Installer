// The games NVIDIA lists with DLSS Ray Reconstruction, for the OpenRR card: api-db.json's "rr" key, written by hand
// like "avoid" and kept by tools/ApiDbBuilder. Each game is matched by Steam app id, then by executable name, then by
// title. To add one, add an entry there and publish api-db.json: apps pick it up on their next start, an app older
// than this key ignores it.

using System.Text.Json.Serialization;

namespace AmdNr.Core;

// Settable, not init: see ApiDatabase.
public sealed class RrGame : IListedGame
{
    [JsonPropertyName("t")] public required string Title { get; init; }
    [JsonPropertyName("steam")] public List<string> SteamAppIds { get; set; } = [];

    /// <summary>Executable file names, matched without case.</summary>
    [JsonPropertyName("exe")] public List<string> Executables { get; set; } = [];

    /// <summary>What its Ray Reconstruction runs on: "D3D12", "Vulkan", or "Remix" for RTX Remix.</summary>
    [JsonPropertyName("api")] public string Api { get; set; } = "D3D12";

    /// <summary>"no" for a game OpenRR does not work in whatever its API, with a short note on why in "why" (for
    /// whoever keeps the list, not shown).</summary>
    [JsonPropertyName("openrr")] public string? OpenRr { get; set; }
    [JsonPropertyName("why")] public string? Why { get; set; }

    [JsonIgnore] public bool NotForOpenRr => string.Equals(OpenRr, "no", StringComparison.OrdinalIgnoreCase);

    public bool Is(string api) => string.Equals(Api, api, StringComparison.OrdinalIgnoreCase);
}

/// <summary>What the OpenRR card shows: nothing, the box, or a line saying why this game cannot have it.</summary>
public enum OpenRrCard { Hidden, Offered, Vulkan, Remix, NotCompatible }

public sealed partial class ApiDatabase
{
    [JsonPropertyName("rr")] public List<RrGame> RayReconstruction { get; set; } = [];

    /// <summary>The listed game this one is, or null: by Steam app id, then executable name, then any of its titles.</summary>
    public RrGame? RrGameFor(string? steamAppId, string? executable, params string?[] titles) =>
        Listed(RayReconstruction, steamAppId, executable, titles);

    /// <summary>The game of a hand-written list this one is: by Steam app id, then executable name, then any of its titles.</summary>
    internal static T? Listed<T>(IReadOnlyList<T> games, string? steamAppId, string? executable, string?[] titles) where T : class, IListedGame
    {
        if (!string.IsNullOrWhiteSpace(steamAppId)
            && games.FirstOrDefault(g => g.SteamAppIds.Contains(steamAppId.Trim())) is { } bySteam)
            return bySteam;
        var exe = Path.GetFileName(executable);
        if (!string.IsNullOrEmpty(exe)
            && games.FirstOrDefault(g => g.Executables.Contains(exe, StringComparer.OrdinalIgnoreCase)) is { } byExe)
            return byExe;
        var keys = titles.Select(PcgwParser.NormaliseTitle).Where(k => k.Length >= 2).ToHashSet(StringComparer.Ordinal);
        return games.FirstOrDefault(g => keys.Contains(PcgwParser.NormaliseTitle(g.Title)));
    }
}

public static partial class Work
{
    /// <summary>OpenRR runs in Vulkan games (from OpenRR 0.1.0): listed Vulkan games get the box like D3D12 ones.</summary>
    public const bool OpenRrVulkanSupported = true;

    /// <summary>The line a listed game gets whatever its files say, or null when the files decide.</summary>
    public static OpenRrCard? OpenRrRefusal(RrGame? listed, bool vulkan = OpenRrVulkanSupported) =>
        listed is null ? null
        : listed.NotForOpenRr ? OpenRrCard.NotCompatible
        : listed.Is("Remix") ? OpenRrCard.Remix
        : listed.Is("Vulkan") && !vulkan ? OpenRrCard.Vulkan
        : null;

    /// <summary>The OpenRR card for a game: a listed one gets the box, or the reason it cannot have it (marked not
    /// compatible, RTX Remix, Vulkan without <see cref="OpenRrVulkanSupported"/>); an unlisted one only when it has an
    /// nvngx_dlssd.dll. A listed game the files show on another API still gets it when it renders D3D12 as well
    /// (<paramref name="hasD3D12"/>), or when the list says Vulkan.</summary>
    public static OpenRrCard OpenRrCardFor(RrGame? listed, OpenRrBlock block, bool hasDlssd, bool hasD3D12,
        bool vulkan = OpenRrVulkanSupported) =>
        OpenRrRefusal(listed, vulkan)
        ?? (block == OpenRrBlock.Width ? OpenRrCard.Hidden
        : block == OpenRrBlock.None && (listed is not null || hasDlssd) ? OpenRrCard.Offered
        : listed is not null && (hasD3D12 || listed.Is("Vulkan")) ? OpenRrCard.Offered
        : OpenRrCard.Hidden);
}
