// mochizuki's RX 7000 network on the OptiScaler route: an opt-in box in the NR runtime section, on an RX 7000 card,
// where the OptiScaler version chosen carries it (mochizuki-rdna3). Ticked, mochizuki goes in with it as it does on an
// RX 9000; unticked, nothing of mochizuki goes in, as on any card it does not run on. Selected() leaves the component
// out of every other install, so nothing of it is downloaded there.

using Avalonia.Interactivity;
using AmdNr.Core;

namespace AmdNr.App;

public partial class GameSheet
{
    /// <summary>Whether this OptiScaler version brings the RX 7000 network beside mochizuki.</summary>
    private static bool CarriesMochizukiRdna3(ComponentRelease opti) =>
        CarriesMochizuki(opti) && opti.Components.ContainsKey(PayloadManifest.MochizukiRdna3Component);

    /// <summary>Whether the box is offered here: the OptiScaler route at a version that carries the RX 7000 network, on
    /// an RX 7000 card.</summary>
    private bool OffersMochizukiRdna3(GameCard card) =>
        card.Entry.Preset.IsOptiScaler() && _version?.Opti is { } opti && CarriesMochizukiRdna3(opti)
        && Session.Machine?.Rx7000 == true;

    /// <summary>Whether an install from this sheet brings it: ticked, or with nothing said on this machine, what the
    /// folder holds -- so a game added again does not lose it on the next update.</summary>
    private bool WantsMochizukiRdna3(GameCard card) =>
        OffersMochizukiRdna3(card) && (card.Entry.MochizukiRdna3 ?? Work.HasMochizukiRdna3(TargetFor(card)));

    private void OnMochizukiRdna3Changed(object? sender, RoutedEventArgs e)
    {
        if (_setting || _card is not { } card) return;
        card.Entry.MochizukiRdna3 = MochizukiRdna3Box.IsChecked == true;
        Library.Save();
        ShowMochizuki();
        ShowInstallLabel();
        // mochizuki and the RX 7000 network go in or come out: the pre-flight says so again.
        _ = RefreshAsync();
    }
}
