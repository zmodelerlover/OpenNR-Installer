// The session: what NRLive captures, the countdown to switch to the game in, NRLive.exe itself, and the hotkey
// that scales and stops from inside the game. It works the way NRLive's own launcher does -- the same command
// line, the same countdown, the same profile -- so moving between the two changes nothing.

using System.ComponentModel;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using AmdNr.Core;

namespace AmdNr.App;

public partial class NrLivePage
{
    private NrLiveProfile _profile = new();
    private Process? _process;
    private int _secondsLeft;
    private string? _stateKey;
    private Level? _stateLevel;
    private bool _setting;
    private GlobalHotkey? _hotkey;
    private readonly DispatcherTimer _tick = new() { Interval = TimeSpan.FromSeconds(1) };

    private bool Running => _process is { HasExited: false };

    /// <summary>The page with one game of the library picked to capture, from the warning on its sheet.</summary>
    public void ShowGame(GameCard card)
    {
        _profile.TargetMode = "pname";
        _profile.TargetText = card.Graphics?.Executable is { } exe ? NrLiveProfile.ExactProcess(Path.GetFileName(exe)) : "";
        SaveProfile();
        ShowSession();
        TargetItemBox.SelectedItem = TargetItemBox.Items.OfType<LibraryGame>().FirstOrDefault(g => g.Card == card);
    }

    private void ShowSession()
    {
        _setting = true;
        TargetModeBox.SelectedIndex = _profile.TargetMode switch { "pid" => 1, "pname" => 2, _ => 0 };
        DelayBox.SelectedItem = DelayBox.Items.OfType<int>().OrderBy(s => Math.Abs(s - _profile.Delay)).First();
        AdminSwitch.IsChecked = _profile.RunAsAdmin;
        CaptureBox.SelectedIndex = _profile.CaptureMode == "dxgi" ? 1 : 0;
        MotionBox.SelectedIndex = _profile.Motion == "amdof" ? 1 : 0;
        ScaleKeyBox.Text = _profile.ScaleHotkey;
        StopKeyBox.Text = _profile.StopKey;
        OverlayKeyBox.Text = _profile.OverlayKey;
        BypassBox.Text = _profile.BindBypass;
        _setting = false;
        ShowTargetItems();
        ShowState();
    }

    /// <summary>The second menu: the windows open now, or the library's games that have an executable to match.</summary>
    private void ShowTargetItems()
    {
        _setting = true;
        var mode = TargetModeBox.SelectedIndex;
        TargetItemSection.IsVisible = mode > 0;
        RefreshButton.IsVisible = mode == 1;
        TargetItemLabel.Text = Ui.Text(mode == 1 ? "Str.NrLiveTargetWindow" : "Str.NrLiveTargetGame");
        TargetItemBox.Items.Clear();
        if (mode == 1)
        {
            foreach (var window in NrLiveNative.Windows()) TargetItemBox.Items.Add(window);
            TargetItemBox.SelectedItem = TargetItemBox.Items.OfType<OpenWindow>().FirstOrDefault(w => w.Pid.ToString() == _profile.TargetText);
        }
        else if (mode == 2)
        {
            foreach (var card in _shell.Library.Cards.Where(c => c.Graphics?.Executable is not null).OrderBy(c => c.Name))
                TargetItemBox.Items.Add(new LibraryGame(card));
            TargetItemBox.SelectedItem = TargetItemBox.Items.OfType<LibraryGame>().FirstOrDefault(g =>
                NrLiveProfile.ExactProcess(Path.GetFileName(g.Card.Graphics!.Executable!)) == _profile.TargetText);
        }
        _setting = false;
    }

    private void OnTargetModeChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_setting || _shell is null) return;
        _profile.TargetMode = TargetModeBox.SelectedIndex switch { 1 => "pid", 2 => "pname", _ => "picker" };
        _profile.TargetText = "";
        SaveProfile();
        ShowTargetItems();
        ShowState();
    }

    private void OnTargetItemChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_setting) return;
        _profile.TargetText = TargetItemBox.SelectedItem switch
        {
            OpenWindow window => window.Pid.ToString(),
            LibraryGame { Card.Graphics.Executable: { } exe } => NrLiveProfile.ExactProcess(Path.GetFileName(exe)),
            _ => "",
        };
        SaveProfile();
        ShowState();
    }

    private void OnRefreshWindows(object? sender, RoutedEventArgs e) => ShowTargetItems();

    private void OnDelayChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_setting || DelayBox.SelectedItem is not int seconds) return;
        _profile.Delay = seconds;
        SaveProfile();
    }

    private void OnAdminChanged(object? sender, RoutedEventArgs e)
    {
        if (_setting) return;
        _profile.RunAsAdmin = AdminSwitch.IsChecked == true;
        SaveProfile();
    }

    private void OnCaptureChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_setting || _shell is null) return;
        _profile.CaptureMode = CaptureBox.SelectedIndex == 1 ? "dxgi" : "wgc";
        _profile.Motion = MotionBox.SelectedIndex == 1 ? "amdof" : "fast";
        SaveProfile();
    }

    private void SaveProfile()
    {
        try { _profile.Save(NrLivePackage.Folder); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }

    // -- The session -----------------------------------------------------------------------------------

    private void OnScale(object? sender, RoutedEventArgs e) => Toggle();

    private void Toggle()
    {
        if (Running) Stop();
        else Start();
    }

    private void Start()
    {
        if (!Installed || Session.Busy) return;
        if (_profile.Problem() is { } problem)
        {
            SetState(Level.Warn, ProblemKey(problem));
            return;
        }
        var start = new ProcessStartInfo(NrLivePackage.Exe)
        {
            WorkingDirectory = NrLivePackage.Folder,
            UseShellExecute = _profile.RunAsAdmin,
            Verb = _profile.RunAsAdmin ? "runas" : "",
        };
        foreach (var arg in _profile.Arguments(NrLiveConfig.Supported(NrLivePackage.Exe).Contains(NrLiveConfig.UpscalerId))) start.ArgumentList.Add(arg);
        try
        {
            _process = Process.Start(start);
        }
        catch (Win32Exception x)
        {
            // 1223: the person said no to Windows' administrator prompt, which is an answer and not a failure.
            SetState(Level.Warn, x.NativeErrorCode == 1223 ? "Str.NrLiveStatusReady" : "Str.NrLiveStartFailed", x.Message);
            return;
        }
        if (_process is null) return;
        _secondsLeft = Math.Clamp(_profile.Delay, 1, 30);
        _tick.Start();
        SetState(null, "Str.NrLiveStatusStarting");
        ShowPackage();
    }

    /// <summary>Ends NRLive. Elevated, it is out of this app's reach, so its own stop key is pressed instead.</summary>
    private void Stop()
    {
        if (_process is null) return;
        try
        {
            _process.Kill(entireProcessTree: true);
            Reset("Str.NrLiveStatusStopped", Level.Info);
        }
        catch (Exception x) when (x is Win32Exception or InvalidOperationException)
        {
            // The next tick sees it gone and says it stopped; if the key could not even be pressed, the person has to.
            if (NrLiveNative.Press(_profile.StopKey)) _stateKey = "Str.NrLiveStatusStopped";
            else _shell.Toast(Ui.Format("Str.NrLiveStopFailed", _profile.StopKey), Level.Warn);
        }
    }

    /// <summary>Once a second while NRLive runs: the countdown, then whether it is still there.</summary>
    private void Tick()
    {
        if (_process is null || _process.HasExited)
        {
            Reset(_stateKey == "Str.NrLiveStatusStopped" ? "Str.NrLiveStatusStopped" : "Str.NrLiveStatusExited", Level.Info);
            return;
        }
        if (_secondsLeft > 0 && --_secondsLeft == 0) SetState(Level.Ok, "Str.NrLiveStatusRunning", _profile.ScaleHotkey);
        else if (_secondsLeft > 0) ShowState();
    }

    private void Reset(string key, Level level)
    {
        _tick.Stop();
        _process?.Dispose();
        _process = null;
        _secondsLeft = 0;
        SetState(level, key);
        ShowPackage();
    }

    /// <summary>Stops a session the window is closing on: NRLive's own launcher does the same, and an elevated
    /// capture window left behind with nothing to stop it but a key nobody remembers is worse.</summary>
    public void Shutdown()
    {
        _hotkey?.Dispose();
        if (Running) Stop();
    }

    private string[] _stateArgs = [];

    private void SetState(Level? level, string key, params string[] args)
    {
        _stateLevel = level;
        _stateKey = key;
        _stateArgs = args;
        ShowState();
    }

    /// <summary>The status line and the button, from the state the session is in.</summary>
    private void ShowState()
    {
        var starting = Running && _secondsLeft > 0;
        var key = Running ? (starting ? "Str.NrLiveStatusStarting" : "Str.NrLiveStatusRunning")
            : _profile.Problem() is { } problem ? ProblemKey(problem)
            : _stateKey ?? "Str.NrLiveStatusReady";
        var level = Running ? (starting ? (Level?)null : Level.Ok) : _profile.Problem() is not null ? Level.Warn : _stateLevel;
        Ui.SetLevel(StatusBox, level);
        Ui.SetLevel(StatusTile, level);
        StatusTile.Classes.Set("neutral", level is null);
        StatusIcon.Data = Ui.Icon(Running ? "IconScale" : level is { } l ? l switch { Level.Ok => "IconOk", Level.Warn => "IconWarn", Level.Err => "IconErr", _ => "IconInfo" } : "IconInfo");
        StatusTitle.Text = Ui.Text(Running ? (starting ? "Str.NrLiveTitleStarting" : "Str.NrLiveTitleRunning") : "Str.NrLiveTitleReady");
        StatusText.Text = Ui.Format(key, key == "Str.NrLiveStatusRunning" ? [_profile.ScaleHotkey] : _stateArgs.Cast<object?>().ToArray());
        ScaleLabel.Text = starting ? Ui.Format("Str.NrLiveStarting", _secondsLeft) : Ui.Text(Running ? "Str.NrLiveStop" : "Str.NrLiveScale");
        ScaleIcon.Data = Ui.Icon(Running ? "IconStop" : "IconScale");
        ScaleButton.Classes.Set("primary", !Running);
        ScaleButton.Classes.Set("ghost", Running);
        ScaleButton.Classes.Set("danger", Running);
        ScaleButton.IsEnabled = Running || Installed && _profile.Problem() is null;
        DelayBox.IsEnabled = TargetModeBox.IsEnabled = TargetItemBox.IsEnabled = !Running;
    }

    private static string ProblemKey(NrLiveProblem problem) => problem switch
    {
        NrLiveProblem.HotkeyConflict => "Str.NrLiveProblemConflict",
        NrLiveProblem.HotkeyInvalid => "Str.NrLiveProblemHotkey",
        NrLiveProblem.BypassInvalid => "Str.NrLiveProblemBypass",
        _ => "Str.NrLiveProblemTarget",
    };

    /// <summary>The scale hotkey, registered with Windows while NRLive is here: pressed in the game, it starts a
    /// session or ends the one running, as the button does.</summary>
    private void RegisterHotkey()
    {
        _hotkey?.Dispose();
        _hotkey = null;
        HotkeyProblem.IsVisible = false;
        if (!GlobalHotkeys || !Installed || !NrLiveHotkey.TryParse(_profile.ScaleHotkey, out var mods, out var vk)) return;
        _hotkey = new GlobalHotkey(mods, vk, () => Dispatcher.UIThread.Post(Toggle));
        if (_hotkey.Registered) return;
        HotkeyProblem.Text = Ui.Format("Str.NrLiveHotkeyTaken", _profile.ScaleHotkey);
        HotkeyProblem.IsVisible = true;
    }
}
