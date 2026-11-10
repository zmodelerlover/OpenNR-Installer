// Picking a data folder and saying why one will not do: the same for the Settings card and the first run's step.

using Avalonia.Controls;
using Avalonia.Platform.Storage;
using AmdNr.Core;

namespace AmdNr.App;

internal static class DataFolderChoice
{
    /// <summary>The folder the data would go into (<see cref="DataMove.Target"/>), or null when nothing was picked.</summary>
    public static async Task<string?> PickAsync(TopLevel owner)
    {
        var picked = await owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = Ui.Text("Str.DataFolderPick"),
            AllowMultiple = false,
        });
        var folder = picked.Count > 0 ? picked[0].TryGetLocalPath() : null;
        return string.IsNullOrWhiteSpace(folder) ? null : DataMove.Target(folder);
    }

    /// <summary>Why <paramref name="target"/> will not take <paramref name="need"/> bytes from the folder in use,
    /// in words, or null when it will.</summary>
    public static async Task<string?> CheckAsync(string target, IEnumerable<string> games, ulong need)
    {
        var from = AppPaths.Root;
        var (problem, game) = await Task.Run(() => DataMove.Check(target, from, AppPaths.Home, games, need));
        return problem == DataFolderProblem.None ? null : Problem(problem, target, game, need);
    }

    public static string Problem(DataFolderProblem problem, string target, string? game, ulong need) => problem switch
    {
        DataFolderProblem.Nested => Ui.Text("Str.DataFolderNested"),
        DataFolderProblem.SystemFolder => Ui.Text("Str.DataFolderSystem"),
        DataFolderProblem.GameFolder => Ui.Format("Str.DataFolderGame", game ?? ""),
        DataFolderProblem.NotEmpty => Ui.Format("Str.DataFolderNotEmpty", target),
        DataFolderProblem.NotWritable => Ui.Format("Str.DataFolderNotWritable", target),
        _ => Ui.Format("Str.DataFolderNoSpace", Ui.Megabytes(need), Ui.Megabytes(Engine.FreeBytes(target) ?? 0)),
    };
}
