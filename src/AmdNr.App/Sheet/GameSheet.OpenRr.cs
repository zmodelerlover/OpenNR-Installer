// OpenRR on the OptiScaler route: a box, ticked unless unticked, on an RX 9000 card, in a 64-bit D3D12 game, where the
// OptiScaler version chosen carries it. The line under it says what the game's nvngx_dlssd.dll means for it. What it
// installs is Work.OpenRr.cs.

using Avalonia.Interactivity;
using AmdNr.Core;

namespace AmdNr.App;

public partial class GameSheet
{
    private static bool CarriesOpenRr(ComponentRelease opti) => opti.Components.ContainsKey(PayloadManifest.OpenRrComponent);

    private bool OffersOpenRr(GameCard card) =>
        card.Entry.Preset.IsOptiScaler() && !RouteUndecided(card) && _version?.Opti is { } opti && CarriesOpenRr(opti) && Rdna4
        && Work.OpenRrBlocked(TargetFor(card), OptiApi(card)) == OpenRrBlock.None;

    /// <summary>The box's answer for an install, null where it is not offered (OptiScaler.ini is then left alone).</summary>
    private bool? WantsOpenRr(GameCard card) => OffersOpenRr(card) ? card.Entry.OpenRr ?? true : null;

    /// <summary>OpenRR's download when ticked, and NVIDIA's nvngx_dlssd.dll 310.9.1 when the game needs it.</summary>
    private static string[] OpenRrComponents(GameCard card, PayloadPins pins) =>
        pins.OpenRr != true ? []
        : Work.OpenRrNeedsDlssd(TargetFor(card)) ? [PayloadManifest.OpenRrComponent, PayloadManifest.OpenRrDlssdComponent]
        : [PayloadManifest.OpenRrComponent];

    private void ShowOpenRr()
    {
        if (_card is not { } card) return;
        var offered = OffersOpenRr(card);
        _setting = true;
        OpenRrSection.IsVisible = offered;
        OpenRrBox.IsChecked = offered && card.Entry.OpenRr != false;
        OpenRrLine.Text = offered ? OpenRrLineFor(TargetFor(card)) : "";
        OpenRrLine.IsVisible = OpenRrLine.Text.Length > 0;
        _setting = false;
    }

    private static string OpenRrLineFor(string target)
    {
        var found = Work.OpenRrDlssd(target);
        if (found.Count == 0) return Ui.Text("Str.OpenRrHint");
        if (found.FirstOrDefault(d => !d.Works) is not { } off) return Ui.Text("Str.OpenRrReady");
        return Ui.Format(Work.SupplyOpenRrDlssd ? "Str.OpenRrSupplied" : "Str.OpenRrOtherDlssd", off.Path, off.Version?.ToString() ?? "?");
    }

    private void OnOpenRrChanged(object? sender, RoutedEventArgs e)
    {
        if (_setting || _card is not { } card) return;
        card.Entry.OpenRr = OpenRrBox.IsChecked == true;
        Library.Save();
        ShowInstallLabel();
        // A different set of files: the pre-flight says so again.
        _ = RefreshAsync();
    }
}
