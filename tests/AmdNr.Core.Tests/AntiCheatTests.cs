using AmdNr.Core;

namespace AmdNr.Core.Tests;

public class AntiCheatTests
{
    private static string Game(params string[] files)
    {
        var dir = Fixture.Temp("anticheat");
        foreach (var file in files)
        {
            var path = Path.Combine(dir, file);
            if (file.EndsWith('/')) Directory.CreateDirectory(path);
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, "x");
            }
        }
        return dir;
    }

    [Theory]
    [InlineData("Easy Anti-Cheat", "EasyAntiCheat/")]
    [InlineData("Easy Anti-Cheat", "start_protected_game.exe")]
    [InlineData("Easy Anti-Cheat", "FortniteGame/Binaries/Win64/FortniteClient-Win64-Shipping_EAC_EOS.exe")]
    [InlineData("BattlEye", "DayZ_BE.exe")]
    [InlineData("BattlEye", "Game/Binaries/Win64/BattlEye/")]
    [InlineData("BattlEye", "BEService_x64.exe")]
    [InlineData("Riot Vanguard", "live/VALORANT.exe")]
    [InlineData("nProtect GameGuard", "GameGuard/GameMon64.des")]
    [InlineData("XIGNCODE3", "XIGNCODE/x3.xem")]
    [InlineData("Ricochet", "atvi-randgrid_sr.sys")]
    [InlineData("ACE", "AntiCheatExpert/")]
    [InlineData("mhyprot", "mhyprot3.sys")]
    [InlineData("EA Javelin", "EAAntiCheat.GameServiceLauncher.exe")]
    [InlineData("PunkBuster", "pb/PnkBstrA.exe")]
    public void FindsEachAntiCheatByItsNames(string name, string file) =>
        Assert.Equal([name], AntiCheat.Find(Game(file, "Game.exe")));

    [Fact]
    public void AGameWithoutOneHasNone() =>
        Assert.Empty(AntiCheat.Find(Game("Game.exe", "data/textures.pak", "Engine/Binaries/Win64/Game-Win64-Shipping.exe",
            "BE/readme.txt", "eacute.txt", "pb/config.cfg", "model.bin")));

    [Fact]
    public void SeveralAreAllFoundInTheTablesOrder() =>
        Assert.Equal(["Easy Anti-Cheat", "BattlEye"], AntiCheat.Find(Game("BattlEye/", "EasyAntiCheat/", "Game.exe")));

    [Fact]
    public void LooksNoDeeperThanFourLevels()
    {
        Assert.Equal(["BattlEye"], AntiCheat.Find(Game("a/b/c/BattlEye/")));
        Assert.Empty(AntiCheat.Find(Game("a/b/c/d/BattlEye/")));
    }

    [Fact]
    public void LooksBesideAnExecutableOutsideTheFolder()
    {
        var folder = Game("readme.txt");
        var elsewhere = Game("Game.exe", "EasyAntiCheat/");
        Assert.Equal(["Easy Anti-Cheat"], AntiCheat.Find(folder, Path.Combine(elsewhere, "Game.exe")));
    }

    [Fact]
    public void AFolderThatIsGoneHasNone() =>
        Assert.Empty(AntiCheat.Find(Path.Combine(Path.GetTempPath(), "amdnr-no-such-" + Guid.NewGuid().ToString("N"))));
}
