// The NRLive page: Caliel's window scaler, run from here instead of from its own launcher, for the games whose
// anti-cheat rules out anything injected. Split like the game sheet: getting NRLive and the page itself here,
// the session -- what to capture, the countdown, the process and its hotkey -- in NrLivePage.Session.cs, and
// its settings and hotkey boxes in NrLivePage.Settings.cs. What it writes and runs is in AmdNr.Core\NrLive.

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
