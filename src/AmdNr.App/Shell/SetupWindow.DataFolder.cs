// -- 2. Where the data goes -------------------------------------------------------------------------
// The default folder, or another one picked with the Settings card's picker and its rules. What the steps before
// wrote there (settings.json, a log) moves with it and the pointer is written; AppPaths reads it again, so the
// rest of the wizard and the main window carry on in the new folder without a restart. Picking the default
// again moves it all back. Only here: an installed instance moves its data from Settings.

using Avalonia.Controls;
using Avalonia.Interactivity;
using AmdNr.Core;

namespace AmdNr.App;

public partial class SetupWindow
{
    private CancellationTokenSource? _dataCancel;

    private void ShowDataFolder()
    {
        DataPathText.Text = AppPaths.Root;
        ToolTip.SetTip(DataPathText, AppPaths.Root);
        var isDefault = AppPaths.Root == AppPaths.Home;
        DataDefaultNote.IsVisible = isDefault;
        DataDefaultButton.IsVisible = !isDefault;
    }

    private async void OnPickData(object? sender, RoutedEventArgs e)
    {
        if (_busy) return;
        if (await DataFolderChoice.PickAsync(this) is { } target) await UseDataFolderAsync(target);
    }

    private async void OnDefaultData(object? sender, RoutedEventArgs e)
    {
        if (!_busy) await UseDataFolderAsync(AppPaths.Home);
    }

    private void OnCancelData(object? sender, RoutedEventArgs e)
    {
        DataCancelButton.IsEnabled = false;
        _dataCancel?.Cancel();
    }

    /// <summary>Moves what is there now, downloads included when the person came back from the files step, into
    /// <paramref name="target"/> (the folder <see cref="DataMove.Target"/> made of the one picked, or Home).
    /// Public for the UI flows, which cannot drive a folder picker.</summary>
    public async Task UseDataFolderAsync(string target)
    {
        DataProblemBox.IsVisible = false;
        var from = AppPaths.Root;
        Busy(true);
        try
        {
            var need = await Task.Run(() => DataMove.Size(from, withCache: true));
            var games = GameStore.Load().Select(g => g.Path).ToList();
            if (await DataFolderChoice.CheckAsync(target, games, need) is { } problem)
            {
                ShowDataProblem(problem);
                return;
            }

            DataMoving.IsVisible = true;
            DataCancelButton.IsEnabled = true;
            DataProgress.Value = 0;
            DataMovingText.Text = Ui.Format("Str.DataFolderMoving", 0);
            _dataCancel = new CancellationTokenSource();
            var cancel = _dataCancel.Token;
            var progress = new Progress<double>(p =>
            {
                DataProgress.Value = p;
                DataMovingText.Text = Ui.Format("Str.DataFolderMoving", (int)(p * 100));
            });
            var left = await Task.Run(() => AppPaths.MoveTo(target, withCache: true, progress, cancel));
            InstallLog.Append($"{DateTime.Now:s} first run: data folder moved from {from} to {target}"
                              + (left > 0 ? $"; {left} old entries could not be deleted" : ""));
        }
        catch (OperationCanceledException)
        {
            ShowDataProblem(Ui.Format("Str.DataFolderMoveCancelled", from));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            InstallLog.Append($"{DateTime.Now:s} first run: data folder move to {target} failed, nothing changed: {e.Message}");
            ShowDataProblem(Ui.Format("Str.DataFolderMoveFailed", e.Message, from));
        }
        finally
        {
            DataMoving.IsVisible = false;
            _dataCancel?.Dispose();
            _dataCancel = null;
            Busy(false);
            ShowDataFolder();
        }
    }

    private void ShowDataProblem(string text)
    {
        DataProblem.Text = text;
        DataProblemBox.IsVisible = true;
    }
}
