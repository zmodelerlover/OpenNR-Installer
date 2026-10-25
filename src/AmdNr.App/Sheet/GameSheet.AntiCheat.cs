// The anti-cheat warning on the sheet: an install puts a DLL in the game, which is what an anti-cheat bans
// for, so a game whose folder carries one (AntiCheat.Find) is pointed at NRLive, which installs nothing. Said
// once per game: "Ignore" is remembered in games.json, and nothing is refused either way.

using AmdNr.Core;

namespace AmdNr.App;

public partial class GameSheet
{
    /// <summary>What each folder was found to carry this session, so reopening a sheet does not walk the folder again.</summary>
    private static readonly Dictionary<string, IReadOnlyList<string>> AntiCheats = new(StringComparer.OrdinalIgnoreCase);

    private void WireAntiCheat()
    {
        AntiCheatWarning.Use += () =>
        {
            if (_card is not { } card) return;
            if (!Docked && !NrLivePage.ComingSoon) Close();
            _shell.ShowNrLive(card);
        };
        AntiCheatWarning.Ignore += () =>
        {
            if (_card is not { } card) return;
            card.Entry.AntiCheatIgnored = true;
            Library.Save();
            AntiCheatWarning.IsVisible = false;
        };
    }

    private async void ShowAntiCheat(GameCard card)
    {
        AntiCheatWarning.IsVisible = false;
        if (card.Entry.AntiCheatIgnored) return;
        if (!AntiCheats.TryGetValue(card.Path, out var found))
        {
            var exe = card.Graphics?.Executable;
            found = await Task.Run(() => AntiCheat.Find(card.Path, exe));
            AntiCheats[card.Path] = found;
        }
        // Another game's sheet may be open by now, or this one ignored meanwhile.
        if (_card == card && !card.Entry.AntiCheatIgnored) AntiCheatWarning.Show(found);
    }
}
