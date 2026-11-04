// OpenRR on the OptiScaler route, on an RX 9000 card: a box, ticked unless unticked, in a 64-bit D3D12 or Vulkan game
// NVIDIA lists with Ray Reconstruction (api-db.json's "rr", RrGames.cs) or a D3D12 one with an nvngx_dlssd.dll; a
// listed game marked not compatible or on RTX Remix gets the line saying why it cannot have it, every other game no
// card at all. The line under the box says what the game's nvngx_dlssd.dll means for it. What it installs is
// Work.OpenRr.cs.

using Avalonia.Interactivity;
using AmdNr.Core;

namespace AmdNr.App;

public partial class GameSheet
{
    private static bool CarriesOpenRr(ComponentRelease opti) => opti.Components.ContainsKey(PayloadManifest.OpenRrComponent);

    private bool OffersOpenRr(GameCard card) => OpenRrCardFor(card) == OpenRrCard.Offered;

    /// <summary>The listed Ray Reconstruction game this is, or null.</summary>
    private RrGame? ListedRr(GameCard card) =>
        Session.ApiDb?.RrGameFor(card.Entry.AppId, card.Graphics?.Executable ?? card.Entry.Executable, card.Entry.CustomName,
            card.Entry.Name, Path.GetFileName(card.Path.TrimEnd('\\', '/')));

    private OpenRrCard OpenRrCardFor(GameCard card)
    {
        if (!card.Entry.Preset.IsOptiScaler() || RouteUndecided(card) || !Rdna4) return OpenRrCard.Hidden;
        var listed = ListedRr(card);
        // The lines saying why a listed game cannot have it do not depend on the version chosen.
        if (Work.OpenRrRefusal(listed) is { } refused) return refused;
        if (_version?.Opti is not { } opti || !CarriesOpenRr(opti)) return OpenRrCard.Hidden;
        var target = TargetFor(card);
        var block = Work.OpenRrBlocked(target, OptiApi(card));
        // The dlssd scan only decides an unlisted game.
        var hasDlssd = listed is null && block == OpenRrBlock.None && Work.OpenRrDlssd(target).Count > 0;
        return Work.OpenRrCardFor(listed, block, hasDlssd, (card.Graphics ?? GraphicsDetector.Detect(card.Path)).All.Contains(GraphicsApi.D3D12));
    }

    /// <summary>The box's answer for an install, null where it is not offered (OptiScaler.ini is then left alone).</summary>
    private bool? WantsOpenRr(GameCard card) => OffersOpenRr(card) ? card.Entry.OpenRr ?? true : null;

    /// <summary>OpenRR's download when ticked, and NVIDIA's nvngx_dlssd.dll 310.9.1 when the game needs it.</summary>
    private string[] OpenRrComponents(GameCard card, PayloadPins pins) =>
        pins.OpenRr != true ? []
        : Work.OpenRrNeedsDlssd(TargetFor(card), OptiApi(card)) ? [PayloadManifest.OpenRrComponent, PayloadManifest.OpenRrDlssdComponent]
        : [PayloadManifest.OpenRrComponent];

    private void ShowOpenRr()
    {
        if (_card is not { } card) return;
        var shown = OpenRrCardFor(card);
        var offered = shown == OpenRrCard.Offered;
        var listed = ListedRr(card);
        _setting = true;
        OpenRrSection.IsVisible = shown != OpenRrCard.Hidden;
        OpenRrBox.IsEnabled = offered;
        OpenRrBox.IsChecked = offered && card.Entry.OpenRr != false;
        OpenRrLine.Text = shown switch
        {
            OpenRrCard.Vulkan or OpenRrCard.NotCompatible => Ui.Format("Str.OpenRrNotCompatible", listed!.Title),
            OpenRrCard.Remix => Ui.Format("Str.OpenRrRemix", listed!.Title),
            OpenRrCard.Offered when listed is not null =>
                Ui.Format("Str.OpenRrListed", listed.Title) + "\n" + OpenRrLineFor(TargetFor(card), OptiApi(card)),
            OpenRrCard.Offered => OpenRrLineFor(TargetFor(card), OptiApi(card)),
            _ => "",
        };
        OpenRrLine.IsVisible = OpenRrLine.Text.Length > 0;
        _setting = false;
    }

    /// <summary>What the game's nvngx_dlssd.dll means for OpenRR. None is a listed game: 310.9.1 is supplied.</summary>
    private static string OpenRrLineFor(string target, GraphicsApi? api)
    {
        var found = Work.OpenRrDlssd(target);
        if (found.Count == 0) return Ui.Text("Str.OpenRrHint");
        // A Vulkan game: opennr-rr\'s copy is read first and the game keeps its own
        var vulkan = Work.OpenRrVulkan(target, api);
        if (vulkan && found[0].Works || found.FirstOrDefault(d => !d.Works) is not { } off) return Ui.Text("Str.OpenRrReady");
        return Ui.Format(!Work.SupplyOpenRrDlssd ? "Str.OpenRrOtherDlssd" : vulkan ? "Str.OpenRrSuppliedVulkan" : "Str.OpenRrSupplied",
            off.Path, off.Version?.ToString() ?? "?");
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
