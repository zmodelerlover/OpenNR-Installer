// HexNeural on the OptiScaler route: a fourth runtime tile, on an RX 6000 graphics card only, where the OptiScaler
// version chosen carries it. Experimental and off unless ticked; ticked, it goes in and becomes the NR runtime
// (NrBackend=rdna2), unticked an earlier install's comes out. What it installs is Work.HexNeural.cs.

using AmdNr.Core;

namespace AmdNr.App;

public partial class GameSheet
{
    private static bool CarriesHexNeural(ComponentRelease opti) => opti.Components.ContainsKey(PayloadManifest.HexNeuralComponent);

    /// <summary>Whether the tile offers it here: the OptiScaler route at a version that carries it, on an RX 6000 graphics
    /// card.</summary>
    private bool OffersHexNeural(GameCard card) =>
        card.Entry.Preset.IsOptiScaler() && !RouteUndecided(card) && _version?.Opti is { } opti && CarriesHexNeural(opti)
        && Session.Machine?.Rx6000Card == true;

    /// <summary>Whether an install from this sheet brings it: ticked, or with nothing said on this machine, what the
    /// folder holds.</summary>
    private bool WantsHexNeural(GameCard card) =>
        OffersHexNeural(card) && (card.Entry.HexNeural ?? Work.HasHexNeural(TargetFor(card)));

    /// <summary>Its download, when ticked.</summary>
    private static string[] HexNeuralComponents(PayloadPins pins) =>
        pins.HexNeural == true ? [PayloadManifest.HexNeuralComponent] : [];
}
