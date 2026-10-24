// Which NR runtimes a game's OptiScaler install brings: any can stay out, game by game (a player who runs open source
// only leaves danielblnc's, which is closed, out). What is left out is neither downloaded nor installed.

using Avalonia.Controls;
using Avalonia.Interactivity;
using AmdNr.Core;

namespace AmdNr.App;

public partial class GameSheet
{
    private const string LmxxfRuntime = "lmxxf";
    private const string MochizukiRuntime = "mochizuki";
    private const string DanielRuntime = "daniel";

    // Made here rather than in GameSheet.axaml, which is at the line limit: one tile per runtime, with its download size,
    // and danielblnc's version on a branch under its own tile.
    private CheckBox? _lmxxfBox, _mochizukiBox, _danielBox;
    private Grid? _danielBranch, _runtimeRow;
    // On an RX 7000 the mochizuki tile is the opt-in to its RX 7000 network (GameSheet.Rdna3.cs), with what that costs.
    private TextBlock? _rdna3Note;
    // lmxxf, mochizuki, danielblnc: each label on one line, and room under danielblnc's for its version and badge.
    private static readonly double[] Weights = [1, 1.15, 1.25];

    /// <summary>Whether this game's OptiScaler install leaves the runtime out. The ReShade routes take what they need.</summary>
    private static bool Skips(GameCard? card, string runtime) =>
        card is not null && card.Entry.Preset.IsOptiScaler() && card.Entry.SkippedRuntimes?.Contains(runtime) == true;

    private CheckBox Tile(Panel column, string label)
    {
        var box = new CheckBox { Content = label, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch };
        box.Click += OnRuntimesChanged;
        var tile = new Border { Child = box };
        tile.Classes.Add("runtimeTile");
        box.IsCheckedChanged += (_, _) => tile.Classes.Set("on", box.IsChecked == true);
        column.Children.Add(tile);
        return box;
    }

    private void BuildRuntimeTiles()
    {
        // danielblnc's column is the widest: its version box hangs under it. A column whose runtime is not offered
        // is given no width (ShowRuntimes).
        var row = _runtimeRow = new Grid { Margin = new Avalonia.Thickness(-4, 0) };
        foreach (var weight in Weights) row.ColumnDefinitions.Add(new ColumnDefinition(weight, GridUnitType.Star));
        StackPanel Column()
        {
            var column = new StackPanel { Spacing = 6, Margin = new Avalonia.Thickness(4, 0) };
            Grid.SetColumn(column, row.Children.Count);
            row.Children.Add(column);
            return column;
        }
        _lmxxfBox = Tile(Column(), "lmxxf (330 MB)");
        _mochizukiBox = Tile(Column(), "mochizuki (350 MB)");
        var daniel = Column();
        _danielBox = Tile(daniel, "danielblnc (205 MB)");
        _danielBranch = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
        var line = new Border();
        line.Classes.Add("branch");
        _danielBranch.Children.Add(line);
        daniel.Children.Add(_danielBranch);
        OptiRuntimes.Children.Add(row);
        _rdna3Note = new TextBlock { Name = "MochizukiRdna3Note", TextWrapping = Avalonia.Media.TextWrapping.Wrap, IsVisible = false };
        _rdna3Note.Classes.Add("caption");
        OptiRuntimes.Children.Add(_rdna3Note);
    }

    /// <summary>danielblnc's version box, under its tile on the OptiScaler route and in its own row on the ReShade ones.</summary>
    private void PlaceDanielVersion(Panel to, int column)
    {
        if (DanielVersionBox.Parent != to && DanielVersionBox.Parent is Panel from)
        {
            from.Children.Remove(DanielVersionBox);
            to.Children.Add(DanielVersionBox);
        }
        Grid.SetColumn(DanielVersionBox, column);
    }

    /// <summary>Called at the end of ShowMochizuki, which lays out the same runtimes for the ReShade routes.</summary>
    private void ShowRuntimes()
    {
        if (_card is not { } card) return;
        if (_lmxxfBox is null) BuildRuntimeTiles();
        var opti = card.Entry.Preset.IsOptiScaler() && !RouteUndecided(card);
        _setting = true;
        // lmxxf and mochizuki run on RX 9000 only, and are not downloaded anywhere else either; mochizuki also on an
        // RX 7000 where the version carries its RX 7000 network, off until ticked.
        var rx7000 = OffersMochizukiRdna3(card);
        var offered = new[] { Rdna4, Rdna4 && OffersMochizuki(card) || rx7000, true };
        for (var i = 0; i < offered.Length; i++)
        {
            _runtimeRow!.Children[i].IsVisible = offered[i];
            _runtimeRow.ColumnDefinitions[i].Width = offered[i] ? new GridLength(Weights[i], GridUnitType.Star) : new GridLength(0);
        }
        _lmxxfBox!.IsChecked = !Skips(card, LmxxfRuntime);
        _mochizukiBox!.Content = rx7000 ? "mochizuki, experimental (380 MB)" : "mochizuki (350 MB)";
        _mochizukiBox.IsChecked = rx7000 ? WantsMochizukiRdna3(card) : !Skips(card, MochizukiRuntime);
        _rdna3Note!.Text = Ui.Text("Str.MochizukiRdna3Note");
        _rdna3Note.IsVisible = rx7000;
        _danielBox!.IsChecked = !Skips(card, DanielRuntime);
        _setting = false;
        OptiRuntimes.IsVisible = opti;
        if (opti)
        {
            // The tiles say which runtimes go in, so the section that names them for ReShade stays shut.
            _danielBranch!.IsVisible = DanielVersionRow.IsVisible && !Skips(card, DanielRuntime);
            MochizukiSection.IsVisible = false;
            PlaceDanielVersion(_danielBranch, 1);
        }
        else PlaceDanielVersion(DanielVersionRow, 3);
    }

    private void OnRuntimesChanged(object? sender, RoutedEventArgs e)
    {
        if (_setting || _card is not { } card || _lmxxfBox is null || _mochizukiBox is null || _danielBox is null) return;
        var skipped = new List<string>();
        if (Rdna4 && _lmxxfBox.IsChecked != true) skipped.Add(LmxxfRuntime);
        if (Rdna4 && _mochizukiBox.IsChecked != true) skipped.Add(MochizukiRuntime);
        if (_danielBox.IsChecked != true) skipped.Add(DanielRuntime);
        card.Entry.SkippedRuntimes = skipped.Count > 0 ? skipped : null;
        if (OffersMochizukiRdna3(card)) card.Entry.MochizukiRdna3 = _mochizukiBox.IsChecked == true;
        Library.Save();
        ShowMochizuki();
        ShowInstallLabel();
        // A different set of files: the pre-flight says so again.
        _ = RefreshAsync();
    }
}
