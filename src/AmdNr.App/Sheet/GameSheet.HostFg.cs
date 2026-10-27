// danielblnc's DLSS frame generation for a game without its own, on the OptiScaler route: an opt-in box where the
// OptiScaler version chosen carries NVIDIA's Streamline (0.5.8 on) and the game runs danielblnc 0.8.0, in a 64-bit
// D3D12 game. A game with its own DLSS-FG, or with no upscaler, sees the box greyed with the reason. What it installs
// is Work.HostFg.cs.

using Avalonia.Interactivity;
using AmdNr.Core;

namespace AmdNr.App;

public partial class GameSheet
{
    private static bool CarriesHostFg(ComponentRelease opti) =>
        opti.Components.ContainsKey(PayloadManifest.StreamlineComponent) && opti.Components.ContainsKey(PayloadManifest.DlssgComponent);

    /// <summary>What keeps the box from this game, None when it is offered. A version without Streamline, or
    /// danielblnc's runtime left out, counts as the runtime's.</summary>
    private HostFgBlock HostFgFor(GameCard card, PayloadPins pins) =>
        !card.Entry.Preset.IsOptiScaler() || RouteUndecided(card) || _version?.Opti is not { } opti || !CarriesHostFg(opti)
        || Skips(card, DanielRuntime)
            ? HostFgBlock.Runtime
            : Work.HostFgBlocked(TargetFor(card), pins.OptiRuntimeVersion, OptiApi(card));

    /// <summary>Whether an install from this sheet brings it: ticked, or with nothing said on this machine, what the
    /// folder holds, as WrongGPU does.</summary>
    private bool WantsHostFg(GameCard card, PayloadPins pins) =>
        HostFgFor(card, pins) == HostFgBlock.None && (card.Entry.HostFg ?? Work.HasHostFg(TargetFor(card)));

    /// <summary>The pins of the version chosen, with the box's answer on them.</summary>
    private PayloadPins InstallPins(GameCard card)
    {
        var pins = Pins();
        pins.HostFg = WantsHostFg(card, pins);
        return pins;
    }

    private void ShowHostFg()
    {
        if (_card is not { } card) return;
        var pins = Pins();
        var block = HostFgFor(card, pins);
        var on = block == HostFgBlock.None && WantsHostFg(card, pins);
        _setting = true;
        HostFgSection.IsVisible = block is HostFgBlock.None or HostFgBlock.OwnFg or HostFgBlock.NoUpscaler;
        HostFgBox.IsEnabled = block == HostFgBlock.None;
        HostFgBox.IsChecked = on;
        HostFgLine.Text = block switch
        {
            HostFgBlock.OwnFg => Ui.Text("Str.HostFgOwnFg"),
            HostFgBlock.NoUpscaler => Ui.Text("Str.HostFgNoUpscaler"),
            _ => on ? Ui.Text("Str.HostFgNrOff") : "",
        };
        HostFgLine.IsVisible = HostFgLine.Text.Length > 0;
        _setting = false;
    }

    private void OnHostFgChanged(object? sender, RoutedEventArgs e)
    {
        if (_setting || _card is not { } card) return;
        card.Entry.HostFg = HostFgBox.IsChecked == true;
        Library.Save();
        ShowHostFg();
        ShowInstallLabel();
        // A different set of files: the pre-flight says so again.
        _ = RefreshAsync();
    }
}
