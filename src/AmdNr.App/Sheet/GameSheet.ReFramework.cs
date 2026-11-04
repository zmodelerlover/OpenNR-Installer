// REFramework on an RE Engine game's sheet, on either 64-bit route: a card for a game on api-db.json's "reframework"
// list (ReFrameworkGames.cs), or one this app put REFramework in, so it can be taken out. Unticked unless the list
// says the game needs it; a game that plays online gets the warning and stays unticked until the person ticks it.
// What it installs is Work.ReFramework.cs.

using AmdNr.Core;

namespace AmdNr.App;

public partial class GameSheet
{
    /// <summary>The listed RE Engine game this is, or null.</summary>
    private ReFrameworkGame? ListedReFramework(GameCard card) =>
        Session.ApiDb?.ReFrameworkGameFor(card.Entry.AppId, card.Graphics?.Executable ?? card.Entry.Executable,
            card.Entry.CustomName, card.Entry.Name, Path.GetFileName(card.Path.TrimEnd('\\', '/')));

    /// <summary>Whether the card shows: a route picked that REFramework goes beside, a 64-bit game, and the game on the
    /// list or REFramework of this app's already here.</summary>
    private bool OffersReFramework(GameCard card) =>
        !RouteUndecided(card) && Work.ReFrameworkRoute(card.Entry.Preset)
        && (card.Graphics ?? GraphicsDetector.Detect(card.Path)).Width != Route.X86
        && (ListedReFramework(card) is not null || Work.HasReFramework(TargetFor(card)));

    /// <summary>The box's answer: ticked or unticked by hand, or else what the folder holds, or ticked where the list
    /// says the game needs it and it does not play online.</summary>
    private bool WantsReFramework(GameCard card) =>
        card.Entry.ReFramework ?? (Work.HasReFramework(TargetFor(card))
                                   || ListedReFramework(card) is { Needed: true, Risky: false });

    /// <summary>The manifest with this game's REFramework build in it, when the list names one.</summary>
    private PayloadManifest WithReFramework(PayloadManifest manifest) =>
        _card is { } card && ListedReFramework(card) is { } game ? manifest.WithReFramework(game.Build) : manifest;

    /// <summary>REFramework's download when ticked: its licence and the game's build.</summary>
    private static string[] ReFrameworkComponents(PayloadPins pins) =>
        pins.ReFramework == true && pins.ReFrameworkBuild.Length > 0 ? [PayloadManifest.ReFrameworkComponent, pins.ReFrameworkBuild] : [];

    private void ShowReFramework()
    {
        if (_card is not { } card || !OffersReFramework(card))
        {
            ReFrameworkPanel.IsVisible = false;
            return;
        }
        var listed = ListedReFramework(card);
        var target = TargetFor(card);
        var here = Work.HasReFramework(target);
        var release = listed is null ? null : Session.Manifest?.ReFrameworkRelease(listed.Build);
        var version = release?.Components.GetValueOrDefault(listed!.Component)?.Version;
        var blocked = here ? null : Work.ReFrameworkBlocked(target, card.Entry.Preset, _proxy);
        var line = here ? Ui.Text("Str.ReFwInstalled")
            : version is null ? Ui.Format("Str.ReFwNoBuild", listed?.Title ?? card.Name)
            : blocked is not null ? Ui.Text("Str.ReFwBlocked")
            : listed is { Needed: true } ? Ui.Format("Str.ReFwNeeded", listed.Title)
            : Ui.Format("Str.ReFwListed", listed!.Title, version);
        _setting = true;
        ReFrameworkPanel.Show(WantsReFramework(card), version is not null || here,
            listed is { Risky: true } ? Ui.Format("Str.ReFwRisk", listed.Title) : "", line);
        _setting = false;
    }

    private void OnReFrameworkChanged(bool on)
    {
        if (_setting || _card is not { } card) return;
        card.Entry.ReFramework = on;
        Library.Save();
        ShowInstallLabel();
        // A different set of files: the pre-flight says so again.
        _ = RefreshAsync();
    }
}
