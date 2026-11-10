// The first run's data folder step: the default shown, another folder taken (what the language step wrote goes
// with it, and the files step after it shows the cache there, with no restart), Back shows the folder chosen, the
// default again brings it all home, and a folder that will not do says why and moves nothing. Run by the flows in
// Program.cs right after the payload is seeded, while the home is still small; it ends back in the default.
// The folder picker cannot be driven headless, so the flow hands the step the folder it would have returned.

using Avalonia.Controls;
using AmdNr.App;
using AmdNr.Core;

internal static class FirstRunDataFlow
{
    public static void Run(Action<bool, string> check, Func<Func<bool>, int, bool> until, Action<Button> click,
        Action<Window, string> save)
    {
        var home = AppPaths.Home;
        var settings = Settings.Load();
        settings.Language = "en";
        settings.Save();

        var setup = new SetupWindow { Width = 820, Height = 900 };
        setup.Show();
        T Named<T>(string name) where T : Control => setup.FindControl<T>(name)!;
        var next = Named<Button>("NextButton");
        var back = Named<Button>("BackButton");
        var path = Named<TextBlock>("DataPathText");
        click(next);
        until(() => Named<Control>("PageData").IsVisible, 5);
        check(Named<Control>("PageData").IsVisible && path.Text == home && Named<Control>("DataDefaultNote").IsVisible
              && !Named<Control>("DataDefaultButton").IsVisible, "the first run's second step offers the default data folder");

        var target = DataMove.Target(Path.Combine(Path.GetDirectoryName(home)!, "flow-data-" + Guid.NewGuid().ToString("N")[..8]));
        var move = setup.UseDataFolderAsync(target);
        check(until(() => move.IsCompleted, 30) && AppPaths.Root == target && DataLocation.Read(home) == target,
            $"another folder is taken and pointed at ({AppPaths.Root})");
        check(File.Exists(Path.Combine(target, "settings.json")) && !File.Exists(Path.Combine(home, "settings.json"))
              && File.Exists(Path.Combine(target, "payload.json")), "and what was written before it went there");
        check(path.Text == target && Named<Control>("DataDefaultButton").IsVisible && !Named<Control>("DataDefaultNote").IsVisible,
            "the step shows it, with the way back to the default");
        save(setup, "flow-firstrun-data-moved");

        click(next);
        click(next);
        var files = Named<PayloadPanel>("Payloads");
        check(until(() => files.FindControl<TextBlock>("CacheFolder")!.Text == AppPaths.Cache, 10)
              && AppPaths.Cache.StartsWith(target), $"the files step after it downloads there ({AppPaths.Cache})");
        until(() => back.IsEnabled, 20);
        click(back);
        click(back);
        check(Named<Control>("PageData").IsVisible && path.Text == target, "Back to the step shows the folder chosen");

        move = setup.UseDataFolderAsync(Environment.GetFolderPath(Environment.SpecialFolder.Windows));
        check(until(() => move.IsCompleted, 10) && Named<Control>("DataProblemBox").IsVisible && AppPaths.Root == target,
            "a folder that will not do says why and nothing moves");

        move = setup.UseDataFolderAsync(home);
        check(until(() => move.IsCompleted, 30) && AppPaths.Root == home && DataLocation.Read(home) is null
              && File.Exists(Path.Combine(home, "settings.json")) && !Directory.Exists(target),
            "the default again brings it all home and the other folder goes");
        check(path.Text == home && !Named<Control>("DataProblemBox").IsVisible, "and the step says the default");
        setup.Close();
    }
}
