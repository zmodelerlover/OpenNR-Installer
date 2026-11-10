// The data folder card: open it, move it somewhere else, or back to the default. The move itself is
// DataMove's; this asks where, checks the folder, asks what to bring, and restarts into the new one.

using System.Diagnostics;
using Avalonia.Interactivity;
using AmdNr.Core;

namespace AmdNr.App;

public partial class SettingsPage
{
    private void OnOpenData(object? sender, RoutedEventArgs e) => AppUpdate.OpenFolder(AppPaths.Root);

    private void OnChangeData(object? sender, RoutedEventArgs e) => _shell.Run("data folder", async () =>
    {
        if (Blocked()) return;
        if (await DataFolderChoice.PickAsync(_shell) is { } target) await MoveDataAsync(target);
    });

    private void OnDefaultData(object? sender, RoutedEventArgs e) => _shell.Run("data folder", async () =>
    {
        if (!Blocked()) await MoveDataAsync(AppPaths.Home);
    });

    /// <summary>Not while anything else runs: an install writes logs and reads the cache, and NRLive runs from here.</summary>
    private bool Blocked()
    {
        if (_shell.Session.Busy) _shell.Toast(Ui.Text("Str.Working"), Level.Info);
        else if (_shell.LivePage.InUse) _shell.Toast(Ui.Text("Str.DataFolderNrLive"), Level.Warn);
        else return false;
        return true;
    }

    private async Task MoveDataAsync(string target)
    {
        var from = AppPaths.Root;
        var all = await Task.Run(() => DataMove.Size(from, withCache: true));
        var essentials = await Task.Run(() => DataMove.Size(from, withCache: false));
        var games = _shell.Library.Cards.Select(c => c.Entry.Path).ToList();
        if (await DataFolderChoice.CheckAsync(target, games, essentials) is { } problem)
        {
            _shell.Toast(problem, Level.Warn);
            return;
        }

        var answer = await ChoiceDialog.ShowAsync(_shell, Ui.Text("Str.DataFolderMoveTitle"),
            Ui.Format("Str.DataFolderMoveBody", target),
        [
            ("all", Ui.Text("Str.DataFolderMoveAll"), Ui.Format("Str.DataFolderMoveAllBody", Ui.Megabytes(all))),
            ("empty", Ui.Text("Str.DataFolderStartEmpty"), Ui.Format("Str.DataFolderStartEmptyBody", Ui.Megabytes(essentials))),
            ("keep", Ui.Text("Str.Dismiss"), Ui.Text("Str.DataFolderKeepBody")),
        ], primary: "all");
        if (answer is not ("all" or "empty") || Blocked()) return;
        var withCache = answer == "all";
        if (withCache && Engine.FreeBytes(target) is { } free && free < all)
        {
            _shell.Toast(DataFolderChoice.Problem(DataFolderProblem.NoSpace, target, null, all), Level.Warn);
            return;
        }

        _shell.Session.Busy = true;
        DataChangeButton.IsEnabled = DataDefaultButton.IsEnabled = false;
        DataMoving.IsVisible = true;
        DataCancelButton.IsEnabled = true;
        _moveCancel = new CancellationTokenSource();
        var cancel = _moveCancel.Token;
        DataProgress.Value = 0;
        DataMovingText.Text = Ui.Format("Str.DataFolderMoving", 0);
        try
        {
            // Made here, on the UI thread, so its reports come back to it: made inside Task.Run they ran on the
            // thread pool and the progress bar threw "Call from invalid thread", which closed the app.
            var progress = new Progress<double>(p =>
            {
                DataProgress.Value = p;
                DataMovingText.Text = Ui.Format("Str.DataFolderMoving", (int)(p * 100));
            });
            var left = await Task.Run(() => AppPaths.MoveTo(target, withCache, progress, cancel));
            InstallLog.Append($"{DateTime.Now:s} data folder moved from {from} to {target}"
                              + (withCache ? "" : ", cache left to come back by download")
                              + (left > 0 ? $"; {left} old entries could not be deleted" : ""));
        }
        catch (OperationCanceledException)
        {
            InstallLog.Append($"{DateTime.Now:s} data folder move to {target} cancelled, nothing changed");
            _shell.Toast(Ui.Format("Str.DataFolderMoveCancelled", from), Level.Info);
            return;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            InstallLog.Append($"{DateTime.Now:s} data folder move to {target} failed, nothing changed: {e.Message}");
            _shell.Toast(Ui.Format("Str.DataFolderMoveFailed", e.Message, from), Level.Err);
            return;
        }
        finally
        {
            _shell.Session.Busy = false;
            DataChangeButton.IsEnabled = DataDefaultButton.IsEnabled = true;
            DataMoving.IsVisible = false;
            _moveCancel?.Dispose();
            _moveCancel = null;
        }
        Restart();
    }

    private CancellationTokenSource? _moveCancel;

    /// <summary>Stops the copy at the next buffer; what was copied goes and the data stays where it was.</summary>
    private void OnCancelMove(object? sender, RoutedEventArgs e)
    {
        DataCancelButton.IsEnabled = false;
        _moveCancel?.Cancel();
    }

    /// <summary>The pointer already names the new folder; this process still has the old one, so it hands over.</summary>
    private void Restart()
    {
        try
        {
            if (Environment.ProcessPath is { } exe)
                Process.Start(new ProcessStartInfo(exe, Program.AfterUpdate) { UseShellExecute = true });
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or FileNotFoundException
                                      or InvalidOperationException)
        {
            // Started by hand next time, it opens on the new folder all the same.
        }
        _shell.Close();
    }
}
