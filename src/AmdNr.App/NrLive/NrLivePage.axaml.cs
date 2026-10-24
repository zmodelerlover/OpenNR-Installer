// The NRLive page: Caliel's window scaler, run from here instead of from its own launcher, for the games whose
// anti-cheat rules out anything injected. Split like the game sheet: getting NRLive and the page itself here,
// the session -- what to capture, the countdown, the process and its hotkey -- in NrLivePage.Session.cs, its
// settings and hotkey boxes in NrLivePage.Settings.cs, and the pieces the settings are built of in
// NrLivePage.Rows.cs. What it writes and runs is in AmdNr.Core\NrLive.

using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;
using AmdNr.Core;

namespace AmdNr.App;

public partial class NrLivePage : UserControl
{
    private MainWindow _shell = null!;
    private Session Session => _shell.Session;

    /// <summary>Whether the page registers its scale hotkey with Windows. Off in the headless renders, which
    /// should not take a key combination from the machine they run on.</summary>
    public static bool GlobalHotkeys { get; set; } = true;

    public const string SourceUrl = "https://github.com/Caliel666/NRLive";

    public NrLivePage()
    {
        InitializeComponent();
        _tick.Tick += (_, _) => Tick();
        foreach (var seconds in new[] { 3, 5, 10, 15, 20, 30 }) DelayBox.Items.Add(seconds);
        foreach (var box in new[] { ScaleKeyBox, StopKeyBox, OverlayKeyBox })
            box.AddHandler(KeyDownEvent, OnHotkeyKey, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        // The four hotkey boxes across when each gets room for a combination, two by two when not.
        HotkeyGrid.SizeChanged += (_, e) => HotkeyGrid.Columns = e.NewSize.Width >= 720 ? 4 : 2;
        SettingsGrid.SizeChanged += (_, e) => Arrange(e.NewSize.Width);
        IsVisible = false;
        PropertyChanged += (_, e) =>
        {
            // Back on the page: NRLive's overlay may have saved its settings meanwhile, and windows came and went.
            if (e.Property == IsVisibleProperty && IsVisible) Show();
        };
    }

    public void Attach(MainWindow shell)
    {
        _shell = shell;
        Session.ManifestChanged += ShowPackage;
        Session.BusyChanged += ShowPackage;
        Show();
    }

    /// <summary>The card the machine read found, on the upscaling card: FSR 4 is an RX 9000's.</summary>
    public void ShowMachine(SystemState state)
    {
        GpuText.Text = state.Rdna4 == true ? "RDNA 4" : state.Gpu;
        ToolTip.SetTip(GpuBadge, state.Gpu);
        GpuBadge.IsVisible = state.Gpu != "unknown";
    }

    private int _columns;

    /// <summary>The settings cards three across when each gets room for a menu beside its label, two across with
    /// the overlay under both when not, one under another on a window narrower still. The capture strip's menus go
    /// under its title at the same point, where they would no longer fit beside it.</summary>
    private void Arrange(double width)
    {
        var columns = width >= 1000 ? 3 : width >= 620 ? 2 : 1;
        if (columns == _columns) return;
        _columns = columns;
        SettingsGrid.ColumnDefinitions = new(columns switch { 3 => "*,16,*,16,*", 2 => "*,16,*", _ => "*" });
        SettingsGrid.RowDefinitions = new(columns switch { 3 => "Auto", 2 => "Auto,16,Auto", _ => "Auto,16,Auto,16,Auto" });
        Control[] cards = [PictureCard, NrCard, OverlayCard];
        for (var i = 0; i < cards.Length; i++)
        {
            var (column, row, span) = columns switch { 3 => (i * 2, 0, 1), 2 => i < 2 ? (i * 2, 0, 1) : (0, 2, 3), _ => (0, i * 2, 1) };
            Grid.SetColumn(cards[i], column);
            Grid.SetRow(cards[i], row);
            Grid.SetColumnSpan(cards[i], span);
        }
        var wide = columns == 3;
        Grid.SetRow(CaptureGroup, wide ? 0 : 1);
        Grid.SetRow(MotionGroup, wide ? 0 : 1);
        Grid.SetColumn(CaptureGroup, wide ? 2 : 1);
        CaptureGroup.Margin = MotionGroup.Margin = new Avalonia.Thickness(0, wide ? 0 : 10, 0, 0);
    }

    /// <summary>The installed release, or null.</summary>
    private static bool Installed => NrLivePackage.InstalledVersion() is not null;

    /// <summary>Everything on the page, read again from the disk.</summary>
    public void Show()
    {
        if (_shell is null) return;
        ShowPackage();
        _profile = NrLiveProfile.Load(NrLivePackage.Folder);
        ShowSession();
        ShowSettings();
        RegisterHotkey();
    }

    /// <summary>Written in code, so written again in the new language.</summary>
    public void Relabel()
    {
        ShowPackage();
        ShowTargetItems();
        ShowState();
        ShowSettings();
    }

    // -- Getting NRLive ------------------------------------------------------------------------------

    private bool _downloading;

    private void ShowPackage()
    {
        var release = Session.Manifest?.NrLive();
        var installed = NrLivePackage.InstalledVersion();
        var current = release is not null && NrLivePackage.IsCurrent(release);
        Controls.IsEnabled = installed is not null;
        // Here and current, it is one line beside the title; the card is for something to do.
        var slim = installed is not null && (current || release is null) && !_downloading;
        PackageCard.IsVisible = !slim;
        PackageSlim.IsVisible = slim;
        if (slim) PackageSlimText.Text = Ui.Format("Str.NrLiveReady", installed);
        ToolTip.SetTip(PackageSlimText, NrLivePackage.Folder);
        FolderButton.IsVisible = installed is not null;
        ReleasesButton.IsVisible = release is null && installed is null;
        PackageButton.IsVisible = release is not null && !current;
        PackageButton.IsEnabled = !Session.Busy && !Running;
        PackageProgress.IsVisible = _downloading;
        if (_downloading) return;

        var (level, icon) = installed is not null && (current || release is null) ? (Level.Ok, "IconOk")
            : release is not null ? (Level.Warn, "IconDownload") : ((Level?)null, "IconInfo");
        Ui.SetLevel(PackageTile, level);
        PackageTile.Classes.Set("neutral", level is null);
        PackageIcon.Data = Ui.Icon(icon);
        if (installed is not null)
        {
            PackageTitle.Text = Ui.Format("Str.NrLiveReady", installed);
            PackageText.Text = release is not null && !current ? Ui.Format("Str.NrLiveUpdateHint", release.Version) : NrLivePackage.Folder;
            PackageButton.Content = release is not null ? Ui.Format("Str.NrLiveUpdate", release.Version) : null;
        }
        else if (release is not null)
        {
            PackageTitle.Text = Ui.Format("Str.NrLiveNotDownloaded", release.Version);
            var size = release.Components.Values.SelectMany(c => c.Files).Aggregate(0UL, (sum, f) => sum + f.Size);
            PackageText.Text = Ui.Format("Str.NrLiveDownloadHint", Ui.Megabytes(size));
            PackageButton.Content = Ui.Text("Str.NrLiveDownload");
        }
        else
        {
            PackageTitle.Text = Ui.Text("Str.NrLiveUnavailable");
            PackageText.Text = Ui.Text("Str.NrLiveUnavailableHint");
        }
    }

    /// <summary>Downloads the release into the cache, each file checked against its pin, and unpacks it into
    /// NRLive's folder. Busy for the length of it, like any download: two writers on one cache lose files.</summary>
    private void OnPackage(object? sender, RoutedEventArgs e) => _shell.Run("nrlive download", async () =>
    {
        if (Session.Busy || Running || Session.Manifest?.NrLive() is not { } release) return;
        _downloading = true;
        Session.Busy = true;
        PackageProgress.Value = 0;
        PackageTitle.Text = Ui.Format("Str.DownloadingPercent", 0);
        var progress = new Progress<DownloadProgress>(p =>
        {
            if (p.Fraction is not { } f) return;
            PackageProgress.Value = f * 100;
            PackageTitle.Text = Ui.Format("Str.DownloadingPercent", (int)(f * 100));
        });
        try
        {
            await NrLivePackage.InstallAsync(Session.Cache(), Session.Manifest, release, progress);
            _shell.Toast(Ui.Format("Str.NrLiveReady", release.Version), Level.Ok);
        }
        catch (Exception x) when (x is HttpRequestException or InstallException or IOException
                                      or UnauthorizedAccessException or TaskCanceledException
                                      or System.IO.InvalidDataException)
        {
            _shell.Toast(Ui.Format("Str.NrLiveDownloadFailed", x.Message), Level.Err);
        }
        finally
        {
            _downloading = false;
            Session.Busy = false;
            Show();
        }
    });

    private void OnReleases(object? sender, RoutedEventArgs e) => Open(SourceUrl + "/releases");

    private void OnSource(object? sender, RoutedEventArgs e) => Open(SourceUrl);

    private void OnFolder(object? sender, RoutedEventArgs e) => Open(NrLivePackage.Folder);

    private static void Open(string target)
    {
        try { Process.Start(new ProcessStartInfo(target) { UseShellExecute = true })?.Dispose(); }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException) { }
    }
}
