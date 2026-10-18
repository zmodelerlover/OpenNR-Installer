// WrongGPU on the OptiScaler route: an opt-in box, on an RX 9000 card, in a 64-bit game, where the OptiScaler version
// chosen carries it (0.5.4 on). What it installs and how its model is made is Work.WrongGpu.cs.

using Avalonia.Interactivity;
using AmdNr.Core;

namespace AmdNr.App;

public partial class GameSheet
{
    /// <summary>Whether this OptiScaler version brings WrongGPU.</summary>
    private static bool CarriesWrongGpu(ComponentRelease opti) => opti.Components.ContainsKey(PayloadManifest.WrongGpuComponent);

    /// <summary>Whether the box is offered here: the OptiScaler route at a version that carries WrongGPU, an RX 9000
    /// card (WrongGPU runs on RDNA4 only), and a 64-bit game.</summary>
    private bool OffersWrongGpu(GameCard card) =>
        card.Entry.Preset.IsOptiScaler() && _version?.Opti is { } opti && CarriesWrongGpu(opti) && Rdna4
        && (card.Graphics ?? GraphicsDetector.Detect(card.Path)).Width != Route.X86;

    /// <summary>Whether an install from this sheet brings WrongGPU: ticked, or with nothing said on this machine, what
    /// the folder holds -- so a game added again does not lose it on the next update.</summary>
    private bool WantsWrongGpu(GameCard card) =>
        OffersWrongGpu(card) && (card.Entry.WrongGpu ?? Work.HasWrongGpu(TargetFor(card)));

    /// <summary>Whether NVIDIA's DLSS file has to come down for WrongGPU's setup (Work.WrongGpuNeedsDlss).</summary>
    private static bool NeedsDlss(GameCard card, bool wrongGpu) => wrongGpu && Work.WrongGpuNeedsDlss(TargetFor(card));

    private void ShowWrongGpu()
    {
        if (_card is not { } card) return;
        _setting = true;
        WrongGpuSection.IsVisible = OffersWrongGpu(card) && !RouteUndecided(card);
        WrongGpuBox.IsChecked = WantsWrongGpu(card);
        _setting = false;
    }

    private void OnWrongGpuChanged(object? sender, RoutedEventArgs e)
    {
        if (_setting || _card is not { } card) return;
        card.Entry.WrongGpu = WrongGpuBox.IsChecked == true;
        Library.Save();
        ShowInstallLabel();
        // A different set of files: the pre-flight says so again.
        _ = RefreshAsync();
    }
}
