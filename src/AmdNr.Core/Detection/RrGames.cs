// The games NVIDIA lists with DLSS Ray Reconstruction, for the OpenRR card: api-db.json's "rr" key, written by hand
// like "avoid" and kept by tools/ApiDbBuilder. Each game is matched by Steam app id, then by executable name, then by
// title. To add one, add an entry there and publish api-db.json: apps pick it up on their next start, an app older
// than this key ignores it.

using System.Text.Json.Serialization;

namespace AmdNr.Core;

// Settable, not init: see ApiDatabase.
public sealed class RrGame
{
    [JsonPropertyName("t")] public required string Title { get; init; }
    [JsonPropertyName("steam")] public List<string> SteamAppIds { get; set; } = [];

    /// <summary>Executable file names, matched without case.</summary>
    [JsonPropertyName("exe")] public List<string> Executables { get; set; } = [];

    /// <summary>What its Ray Reconstruction runs on: "D3D12", "Vulkan", or "Remix" for RTX Remix.</summary>
    [JsonPropertyName("api")] public string Api { get; set; } = "D3D12";

    public bool Is(string api) => string.Equals(Api, api, StringComparison.OrdinalIgnoreCase);
}

/// <summary>What the OpenRR card shows: nothing, the box, or a line saying why this game cannot have it.</summary>
public enum OpenRrCard { Hidden, Offered, Vulkan, Remix }

public sealed partial class ApiDatabase
{
    [JsonPropertyName("rr")] public List<RrGame> RayReconstruction { get; set; } = [];

    /// <summary>The listed game this one is, or null: by Steam app id, then executable name, then any of its titles.</summary>
    public RrGame? RrGameFor(string? steamAppId, string? executable, params string?[] titles)
    {
        if (!string.IsNullOrWhiteSpace(steamAppId)
            && RayReconstruction.FirstOrDefault(g => g.SteamAppIds.Contains(steamAppId.Trim())) is { } bySteam)
            return bySteam;
        var exe = Path.GetFileName(executable);
        if (!string.IsNullOrEmpty(exe)
            && RayReconstruction.FirstOrDefault(g => g.Executables.Contains(exe, StringComparer.OrdinalIgnoreCase)) is { } byExe)
            return byExe;
        var keys = titles.Select(PcgwParser.NormaliseTitle).Where(k => k.Length >= 2).ToHashSet(StringComparer.Ordinal);
        return RayReconstruction.FirstOrDefault(g => keys.Contains(PcgwParser.NormaliseTitle(g.Title)));
    }
}

public static partial class Work
{
    /// <summary>The OpenRR card for a game: a listed one gets the box, or the reason it cannot have it (Vulkan, RTX
    /// Remix); an unlisted one only when it has an nvngx_dlssd.dll. A listed D3D12 game the files show on another API
    /// still gets it when it renders D3D12 as well (<paramref name="hasD3D12"/>).</summary>
    public static OpenRrCard OpenRrCardFor(RrGame? listed, OpenRrBlock block, bool hasDlssd, bool hasD3D12) =>
        listed?.Is("Vulkan") == true ? OpenRrCard.Vulkan
        : listed?.Is("Remix") == true ? OpenRrCard.Remix
        : block == OpenRrBlock.Width ? OpenRrCard.Hidden
        : block == OpenRrBlock.None && (listed is not null || hasDlssd) ? OpenRrCard.Offered
        : listed is not null && hasD3D12 ? OpenRrCard.Offered
        : OpenRrCard.Hidden;
}
