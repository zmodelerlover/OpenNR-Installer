// Settings per game (AmdNr.Core\NrLive\NrLiveProfiles.cs): whose settings the page shows and NRLive's files hold,
// the "Settings for" menu at the head of the session card, and the moments a copy changes hands -- the target
// picked, a setting changed for a game that had none of its own, the countdown ending on a window, a session over.

using Avalonia.Controls;
using Avalonia.Interactivity;
using AmdNr.Core;

namespace AmdNr.App;

/// <summary>An entry of the "Settings for" menu: the global settings (no game) or a library game's.</summary>
public sealed record ProfileChoice(NrLiveGame? Game, string Label)
{
    public override string ToString() => Label;
}

public partial class NrLivePage
{
    /// <summary>The process in front and its executable, read as the countdown ends. The headless flows put a
    /// stand-in here, so the window in front is a game of theirs.</summary>
    public static Func<(uint Pid, string? Exe)> Foreground { get; set; } = NrLiveNative.Foreground;

    private static NrLiveProfiles Profiles => new(NrLivePackage.Folder);

    /// <summary>Whose settings the page shows: a library game, or null for the global ones.</summary>
    private NrLiveGame? _context;

    /// <summary>The game the target names, kept in the menu even while the global settings are shown.</summary>
    private NrLiveGame? _targetGame;

    /// <summary>Set when the countdown ended on a game whose upscaler, capture or keys differ from those NRLive was
    /// started with: those are on its command line, which an elevated NRLive cannot be given again.</summary>
    private bool _launchKept;

    private List<NrLiveGame> Games() => _shell.Library.Cards.Select(Game).ToList();

    private static NrLiveGame Game(GameCard card) =>
        new(NrLiveGame.IdFor(card.Path), card.Name, card.Path, card.Graphics?.Executable ?? card.Entry.Executable);

    /// <summary>The game the target is: the library game picked, the one whose executable runs the window picked,
    /// or none for the window in front when the countdown ends, which is only known then.</summary>
    private NrLiveGame? TargetGame() => _targetGame = TargetItemBox.SelectedItem switch
    {
        LibraryGame g => Game(g.Card),
        OpenWindow w => NrLiveProfiles.Match(NrLiveNative.ProcessPath(w.Pid), Games()),
        _ => null,
    };

    /// <summary>Shows <paramref name="game"/>'s settings (null: the global ones), NRLive's files given them first.
    /// During a session the files stay with the session's copy.</summary>
    private void UseContext(NrLiveGame? game)
    {
        _context = game;
        if (!Active)
        {
            var before = Profiles.Holder;
            Try(() => Profiles.Use(Profiles.Effective(game)));
            if (Profiles.Holder != before) Reload();
        }
        ShowSettings();
        ShowContext();
        ShowState();
    }

    /// <summary>The launch profile read again, after a copy was written into it.</summary>
    private void Reload()
    {
        _profile = NrLiveProfile.Load(NrLivePackage.Folder);
        ShowSession();
    }

    private void ShowContext()
    {
        if (_shell is null) return;
        _setting = true;
        var profiles = Profiles;
        ProfileBox.Items.Clear();
        ProfileBox.Items.Add(new ProfileChoice(null, Ui.Text("Str.NrLiveProfileGlobal")));
        foreach (var game in Games().Where(g => profiles.Has(g) || g.Id == _context?.Id || g.Id == _targetGame?.Id).OrderBy(g => g.Name))
            ProfileBox.Items.Add(new ProfileChoice(game, profiles.Has(game) ? game.Name : Ui.Format("Str.NrLiveProfileShared", game.Name)));
        ProfileBox.SelectedItem = ProfileBox.Items.OfType<ProfileChoice>().FirstOrDefault(c => c.Game?.Id == _context?.Id) ?? ProfileBox.Items[0];
        ProfileBox.IsEnabled = !Active;
        ResetProfileButton.IsVisible = _context is not null && profiles.Has(_context);
        ResetProfileButton.IsEnabled = !Active;
        if (_context is not null) ToolTip.SetTip(ResetProfileButton, Ui.Format("Str.NrLiveProfileResetHint", _context.Name));
        _setting = false;
    }

    /// <summary>The name of the settings in use, for the status line.</summary>
    private string ProfileInUse() => Profiles.Effective(_context) == NrLiveProfiles.Global || _context is null
        ? Ui.Text("Str.NrLiveProfileGlobal") : _context.Name;

    private void OnProfileChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_setting || _shell is null || ProfileBox.SelectedItem is not ProfileChoice choice) return;
        // After the menu's own selection is done: the menu is filled again on the way.
        Avalonia.Threading.Dispatcher.UIThread.Post(() => UseContext(choice.Game));
    }

    private void OnResetProfile(object? sender, RoutedEventArgs e)
    {
        if (_context is null || Active) return;
        Try(() => Profiles.Reset(_context));
        Reload();
        UseContext(_context);
    }

    /// <summary>Before a setting is written: a game shown on the global settings gets its own copy first, so the
    /// change is the game's and the global settings keep theirs.</summary>
    private void Own()
    {
        if (_context is null || Profiles.Has(_context)) return;
        Try(() => Profiles.Fork(_context));
        ShowContext();
        ShowState();
    }

    /// <summary>After a setting is written: NRLive's files saved into the copy they hold.</summary>
    private static void Keep() => Try(() => Profiles.Capture());

    /// <summary>The countdown ended on the window in front: its game's settings, if it is a library game with its
    /// own, into NRLive's files. A running NRLive reads scaleconfig.ini again within half a second; its launch
    /// settings it only reads when it starts.</summary>
    private void PickForeground(string? exe, bool started)
    {
        var launch = NrLiveLaunch.From(_profile);
        _context = null;
        Try(() => _context = Profiles.UseForeground(() => exe, Games()));
        _targetGame = _context;
        _profile = NrLiveProfile.Load(NrLivePackage.Folder);
        _launchKept = started && NrLiveLaunch.From(_profile) != launch;
        ShowSession();
        ShowSettings();
        ShowContext();
    }

    private static void Try(Action write)
    {
        try { write(); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }
}
