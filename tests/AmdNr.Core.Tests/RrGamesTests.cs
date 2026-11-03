using AmdNr.Core;

namespace AmdNr.Core.Tests;

/// <summary>NVIDIA's Ray Reconstruction games in api-db.json ("rr"): how a game is matched, and what the OpenRR card
/// shows for listed, unlisted, Vulkan and RTX Remix games.</summary>
public class RrGamesTests
{
    private static ApiDatabase Shipped() => ApiDatabase.Parse(PayloadCache.Embedded("api-db.json")!);

    [Fact]
    public void TheShippedListMatchesBySteamIdExecutableAndTitle()
    {
        var db = Shipped();
        Assert.Equal(35, db.RayReconstruction.Count);
        Assert.All(db.RayReconstruction, g => Assert.True(g.Is("D3D12") || g.Is("Vulkan") || g.Is("Remix"), g.Title));
        // Every Steam id and executable names one game only.
        Assert.Equal(db.RayReconstruction.SelectMany(g => g.SteamAppIds).Count(),
            db.RayReconstruction.SelectMany(g => g.SteamAppIds).Distinct().Count());
        Assert.Equal(db.RayReconstruction.SelectMany(g => g.Executables).Count(),
            db.RayReconstruction.SelectMany(g => g.Executables).Distinct(StringComparer.OrdinalIgnoreCase).Count());

        Assert.Equal("Cyberpunk 2077", db.RrGameFor("1091500", null)?.Title);
        Assert.Equal("Cyberpunk 2077", db.RrGameFor(null, @"E:\Games\Cyberpunk2077\bin\x64\CYBERPUNK2077.exe")?.Title);
        Assert.Equal("Resident Evil Requiem", db.RrGameFor("9", @"C:\G\re9.exe", "Something Else")?.Title);
        // Epic, no Steam id: by its executable, or by its title.
        Assert.Equal("Alan Wake 2", db.RrGameFor(null, @"C:\Epic\AlanWake2\AlanWake2.exe")?.Title);
        Assert.Equal("Alan Wake 2", db.RrGameFor(null, null, null, "Alan Wake 2")?.Title);
        Assert.Equal("Marvel's Spider-Man 2", db.RrGameFor(null, null, "Marvels Spider-Man 2")?.Title);

        Assert.True(db.RrGameFor("3017860", null)!.Is("Vulkan"));
        Assert.True(db.RrGameFor(null, "TheGreatCircle.exe")!.Is("Vulkan"));
        Assert.True(db.RrGameFor("2012840", null, "Portal with RTX")!.Is("Remix"));

        // Close titles are other games.
        Assert.Null(db.RrGameFor("400", "portal2.exe", "Portal"));
        Assert.Null(db.RrGameFor("870780", "Control_DX12.exe", "Control"));
        Assert.Null(db.RrGameFor(null, @"C:\G\Game.exe", "My Game", "G"));

        // An app that writes the file back keeps the list.
        Assert.Equal(35, ApiDatabase.Parse(db.Serialise()).RayReconstruction.Count);
        Assert.Empty(ApiDatabase.Parse("{\"schema\":1,\"games\":{}}").RayReconstruction);
    }

    [Fact]
    public void TheCardFollowsTheList()
    {
        var d3d12 = new RrGame { Title = "Listed" };
        var vulkan = new RrGame { Title = "Vk", Api = "vulkan" };
        var remix = new RrGame { Title = "Remix", Api = "Remix" };

        Assert.Equal(OpenRrCard.Offered, Work.OpenRrCardFor(d3d12, OpenRrBlock.None, hasDlssd: false, hasD3D12: true));
        Assert.Equal(OpenRrCard.Hidden, Work.OpenRrCardFor(null, OpenRrBlock.None, hasDlssd: false, hasD3D12: true));
        Assert.Equal(OpenRrCard.Offered, Work.OpenRrCardFor(null, OpenRrBlock.None, hasDlssd: true, hasD3D12: true));
        Assert.Equal(OpenRrCard.Vulkan, Work.OpenRrCardFor(vulkan, OpenRrBlock.Api, hasDlssd: true, hasD3D12: false));
        Assert.Equal(OpenRrCard.Remix, Work.OpenRrCardFor(remix, OpenRrBlock.Width, hasDlssd: false, hasD3D12: false));
        Assert.Equal(OpenRrCard.Hidden, Work.OpenRrCardFor(d3d12, OpenRrBlock.Width, hasDlssd: true, hasD3D12: true));
        // A listed game the files show on D3D11 that renders D3D12 as well; one with no D3D12 at all.
        Assert.Equal(OpenRrCard.Offered, Work.OpenRrCardFor(d3d12, OpenRrBlock.Api, hasDlssd: false, hasD3D12: true));
        Assert.Equal(OpenRrCard.Hidden, Work.OpenRrCardFor(d3d12, OpenRrBlock.Api, hasDlssd: false, hasD3D12: false));
        Assert.Equal(OpenRrCard.Hidden, Work.OpenRrCardFor(null, OpenRrBlock.Api, hasDlssd: true, hasD3D12: false));
    }

    [Fact]
    public void AGameFolderGetsTheCardItsFilesAndTheListSay()
    {
        var db = Shipped();
        OpenRrCard Card(string tag, string exe, string api, bool x64 = true, string? dlssd = null)
        {
            var game = Fixture.Temp(tag);
            File.WriteAllBytes(Path.Combine(game, exe), Fixture.PeWithImports(x64, [api]));
            if (dlssd is not null) Versioned.Write(Path.Combine(game, "nvngx_dlssd.dll"), "NVIDIA DLSS-RR", dlssd);
            var detected = GraphicsDetector.Detect(game);
            var block = Work.OpenRrBlocked(game);
            return Work.OpenRrCardFor(db.RrGameFor(null, detected.Executable), block, Work.OpenRrDlssd(game).Count > 0,
                detected.All.Contains(GraphicsApi.D3D12));
        }

        Assert.Equal(OpenRrCard.Offered, Card("rrlist-hogwarts", "HogwartsLegacy.exe", "d3d12.dll"));
        Assert.Equal(OpenRrCard.Hidden, Card("rrlist-plain", "Game.exe", "d3d12.dll"));
        Assert.Equal(OpenRrCard.Offered, Card("rrlist-dlssd", "Game.exe", "d3d12.dll", dlssd: "310.3.0.0"));
        Assert.Equal(OpenRrCard.Vulkan, Card("rrlist-doom", "DOOMTheDarkAges.exe", "vulkan-1.dll"));
        Assert.Equal(OpenRrCard.Hidden, Card("rrlist-32", "Game.exe", "d3d9.dll", x64: false, dlssd: "310.9.1.0"));
    }
}
