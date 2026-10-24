// The list view with the open game installed while the list is scrolled past the top: the game moves up
// among the installed ones, above the rows on screen, and the list scrolls to it. Avalonia's panel had no
// position for its first row then, and the scroll arranged it at NaN: "Invalid Arrange rectangle" at the
// end of every install (BeamNG.drive, 2026-10-23), and on every layout after it.

using Avalonia.Controls;
using Avalonia.VisualTree;
using AmdNr.App;
using AmdNr.Core;

internal static class ListViewFlow
{
    public static void Run(MainWindow main, byte[] exe, Action<bool, string> check, Func<Func<bool>, int, bool> until,
        Action<Button> click)
    {
        var page = main.GetVisualDescendants().OfType<LibraryPage>().First();
        var lib = Path.Combine(AppPaths.Root, "flow-list");
        for (var i = 0; i < 40; i++)
        {
            var name = $"List Game {i:00}";
            Directory.CreateDirectory(Path.Combine(lib, name));
            File.WriteAllBytes(Path.Combine(lib, name, $"ListGame{i:00}.exe"), exe);
        }
        var search = page.SearchFolderAsync(lib);
        check(until(() => search.IsCompleted && !main.Session.Busy && main.Library.Cards.Count(c => c.Name.StartsWith("List Game")) == 40, 20),
            "forty games for the list view");

        click(page.FindControl<Button>("ListViewButton")!);
        var list = page.FindControl<ListBox>("GameList")!;
        check(until(() => list.IsEffectivelyVisible && list.SelectedItem is not null, 10), "the list view opens a game");
        var last = main.Library.Cards.Last(c => c.Name.StartsWith("List Game"));
        main.OpenSheet(last);
        list.ScrollIntoView(last);
        until(() => false, 1);

        // The app writes a UI exception down and carries on, so the crash log is what says it happened.
        string Crashes() => File.Exists(AppPaths.CrashLog) ? File.ReadAllText(AppPaths.CrashLog) : "";
        var before = Crashes().Length;
        last.Installed = true;
        until(() => false, 2);
        var crash = Crashes()[before..];
        check(!crash.Contains("Invalid Arrange") && list.SelectedItem == last,
            $"installing the open game moves it up and the list follows it{(crash.Length > 0 ? ": " + crash.Split('\n')[1].Trim() : "")}");
        check(list.GetVisualDescendants().OfType<ListBoxItem>().All(i => double.IsFinite(i.Bounds.Y)), "every row has a place");

        last.Installed = false;
        click(page.FindControl<Button>("GridViewButton")!);
        until(() => false, 1);
    }
}
