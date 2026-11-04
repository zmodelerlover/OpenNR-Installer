// mochizuki's RX 7000 and RX 6000 networks on the OptiScaler route: the mochizuki tile as an opt-in, on an RX 7000
// (mochizuki-rdna3) or RX 6000 (mochizuki-rdna2) card, where the OptiScaler version chosen carries that network. Ticked,
// mochizuki goes in with it as it does on an RX 9000; unticked, nothing of mochizuki goes in, as on any card it does not
// run on. Selected() leaves each component out of every other install, so nothing of it is downloaded there.

using AmdNr.Core;

namespace AmdNr.App;

public partial class GameSheet
{
    /// <summary>Whether this OptiScaler version brings the RX 7000 network beside mochizuki.</summary>
    private static bool CarriesMochizukiRdna3(ComponentRelease opti) =>
        CarriesMochizuki(opti) && opti.Components.ContainsKey(PayloadManifest.MochizukiRdna3Component);

    /// <summary>Whether the tile offers it here: the OptiScaler route at a version that carries the RX 7000 network, on
    /// an RX 7000 card.</summary>
    private bool OffersMochizukiRdna3(GameCard card) =>
        card.Entry.Preset.IsOptiScaler() && _version?.Opti is { } opti && CarriesMochizukiRdna3(opti)
        && Session.Machine?.Rx7000 == true;

    /// <summary>Whether an install from this sheet brings it: ticked, or with nothing said on this machine, what the
    /// folder holds -- so a game added again does not lose it on the next update.</summary>
    private bool WantsMochizukiRdna3(GameCard card) =>
        OffersMochizukiRdna3(card) && (card.Entry.MochizukiRdna3 ?? Work.HasMochizukiRdna3(TargetFor(card)));

    /// <summary>Whether this OptiScaler version brings the RX 6000 network beside mochizuki.</summary>
    private static bool CarriesMochizukiRdna2(ComponentRelease opti) =>
        CarriesMochizuki(opti) && opti.Components.ContainsKey(PayloadManifest.MochizukiRdna2Component);

    /// <summary>As <see cref="OffersMochizukiRdna3"/>, on an RX 6000 card.</summary>
    private bool OffersMochizukiRdna2(GameCard card) =>
        card.Entry.Preset.IsOptiScaler() && _version?.Opti is { } opti && CarriesMochizukiRdna2(opti)
        && Session.Machine?.Rx6000 == true;

    /// <summary>As <see cref="WantsMochizukiRdna3"/>, for the RX 6000 network.</summary>
    private bool WantsMochizukiRdna2(GameCard card) =>
        OffersMochizukiRdna2(card) && (card.Entry.MochizukiRdna2 ?? Work.HasMochizukiRdna2(TargetFor(card)));

    /// <summary>A Radeon read as neither RX 9000 nor RX 7000: RX 6000 and older, where danielblnc 0.8.0 is not offered
    /// (Work.RuntimeOffer). Unknown counts as not.</summary>
    private bool BeforeRdna3 => Session.Machine is { LooksLikeRadeon: true, Rdna4: false, Rx7000: false };
}
