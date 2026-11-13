// WrongGPU on the OptiScaler route: an opt-in box, on an RX 9000 card, in a 64-bit game, where the OptiScaler version
// chosen carries its kit (0.6.0 on); and under it, once ticked, its Ray Reconstruction in a game it can reach, in place
// of OpenRR. What it installs and how its model is made is Work.WrongGpu.cs. Its launcher, which converts the model, is
// downloaded from WrongGPU's release page only after the player said yes, and deleted after the install.

using Avalonia.Interactivity;
using AmdNr.Core;

namespace AmdNr.App;

public partial class GameSheet
{
    /// <summary>Whether this OptiScaler version brings WrongGPU's kit.</summary>
    private static bool CarriesWrongGpu(ComponentRelease opti) =>
        opti.Components.TryGetValue(PayloadManifest.WrongGpuComponent, out var wg) && Work.IsWrongGpuKit(wg);

    /// <summary>Whether the box is offered here: the OptiScaler route at a version that carries WrongGPU, an RX 9000
    /// card (WrongGPU runs on RDNA4 only), and a 64-bit game.</summary>
    private bool OffersWrongGpu(GameCard card) =>
        card.Entry.Preset.IsOptiScaler() && _version?.Opti is { } opti && CarriesWrongGpu(opti) && Rdna4
        && (card.Graphics ?? GraphicsDetector.Detect(card.Path)).Width != Route.X86;

    /// <summary>Whether an install from this sheet brings WrongGPU: ticked, or with nothing said on this machine, what
    /// the folder holds -- so a game added again does not lose it on the next update.</summary>
    private bool WantsWrongGpu(GameCard card) =>
        OffersWrongGpu(card) && (card.Entry.WrongGpu ?? Work.HasWrongGpu(TargetFor(card)));

    /// <summary>Whether its Ray Reconstruction box shows: WrongGPU ticked, in a game with Ray Reconstruction that its
    /// plugin can reach.</summary>
    private bool OffersWrongGpuRr(GameCard card)
    {
        if (!WantsWrongGpu(card)) return false;
        var target = TargetFor(card);
        return (ListedRr(card) is not null || Work.OpenRrDlssd(target).Count > 0) && Work.WrongGpuRrFits(target);
    }

    /// <summary>Whether its Ray Reconstruction goes in, which leaves OpenRR out of this game.</summary>
    private bool WantsWrongGpuRr(GameCard card) =>
        OffersWrongGpuRr(card) && (card.Entry.WrongGpuRr ?? Work.HasWrongGpuRr(TargetFor(card)));

    /// <summary>Whether NVIDIA's DLSS file has to come down for OptiScaler\ under danielblnc 0.8.0 in a game whose own
    /// DLSS is too old (Work.HostNeedsDlss). Never for WrongGPU.</summary>
    private static bool NeedsDlss(GameCard card, PayloadPins pins) =>
        !Skips(card, DanielRuntime) && card.Entry.Preset.IsOptiScaler() && Work.HostNeedsDlss(TargetFor(card), pins.OptiRuntimeVersion);

    private void ShowWrongGpu()
    {
        if (_card is not { } card) return;
        _setting = true;
        WrongGpuSection.IsVisible = OffersWrongGpu(card) && !RouteUndecided(card);
        WrongGpuSection.IsChecked = WantsWrongGpu(card);
        WrongGpuSection.Chip(Ui.Text("Str.OffByDefault"), "off");
        WrongGpuSection.Line = Ui.Text("Str.WrongGpuLine");
        WrongGpuRrSection.IsVisible = WrongGpuSection.IsVisible && OffersWrongGpuRr(card);
        WrongGpuRrSection.IsChecked = WantsWrongGpuRr(card);
        WrongGpuRrSection.Chip(Ui.Text("Str.OffByDefault"), "off");
        WrongGpuRrSection.Line = Ui.Text("Str.WrongGpuLine");
        _setting = false;
        ShowHostFg();
        ShowOpenRr();
        ShowReFramework();
    }

    private void OnWrongGpuChanged(object? sender, RoutedEventArgs e)
    {
        if (_setting || _card is not { } card) return;
        card.Entry.WrongGpu = WrongGpuSection.IsChecked == true;
        Library.Save();
        ShowWrongGpu();
        ShowInstallLabel();
        // A different set of files: the pre-flight says so again.
        _ = RefreshAsync();
    }

    /// <summary>One Ray Reconstruction per game: ticking WrongGPU's unticks OpenRR's, and the other way round
    /// (OnOpenRrChanged).</summary>
    private void OnWrongGpuRrChanged(object? sender, RoutedEventArgs e)
    {
        if (_setting || _card is not { } card) return;
        card.Entry.WrongGpuRr = WrongGpuRrSection.IsChecked == true;
        if (card.Entry.WrongGpuRr == true) card.Entry.OpenRr = false;
        Library.Save();
        ShowOpenRr();
        ShowInstallLabel();
        _ = RefreshAsync();
    }

    /// <summary>Before an install with WrongGPU: when its model or RR data has to be converted on this PC, asks before
    /// it downloads WrongGPU's launcher from its release page into <paramref name="into"/>. False when the install
    /// should stop: the player said no while the model is needed, or the download failed. <paramref name="into"/> is
    /// only for this install and goes after it (DropWrongGpuLauncher).</summary>
    private async Task<bool> FetchWrongGpuAsync(GameCard card, PayloadPins pins, string into)
    {
        var target = TargetFor(card);
        var others = Library.Cards.Where(c => c != card).Select(TargetFor).ToList();
        Work.WrongGpuOtherGames = () => others;
        var needed = await Task.Run(() => Work.WrongGpuToConvert(target, pins.WrongGpuRr));
        if (needed.Count == 0) return true;
        var version = pins.WrongGpuVersion;
        var yes = await ChoiceDialog.ShowAsync(_shell, Ui.Format("Str.WrongGpuFetchTitle", version),
            Ui.Format("Str.WrongGpuFetchBody", version),
            [
                ("fetch", Ui.Text("Str.WrongGpuFetchYes"), Work.WrongGpuZipUrl(version)),
                ("no", Ui.Text("Str.Dismiss"), Ui.Text("Str.WrongGpuFetchNo")),
            ], primary: "fetch") == "fetch";
        if (!yes)
        {
            if (!needed.Contains(WrongGpuData.Model)) return true;
            ShowStep(StepDownload, failed: true);
            ShowResult(Level.Warn, Ui.Text("Str.WrongGpuDeclined"), "");
            return false;
        }
        Status(Ui.Format("Str.WrongGpuFetchTitle", version));
        try
        {
            pins.WrongGpuLauncher = await Work.FetchWrongGpuLauncherAsync(Session.Http, version, into);
            return true;
        }
        catch (Exception e) when (e is HttpRequestException or InstallException or IOException or TaskCanceledException
                                      or InvalidDataException or UnauthorizedAccessException)
        {
            ShowStep(StepDownload, failed: true);
            ShowResult(Level.Err, Ui.Text("Str.WrongGpuFetchFailed"), e.Message);
            return false;
        }
    }

    private static void DropWrongGpuLauncher(string folder)
    {
        try { if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }
}
