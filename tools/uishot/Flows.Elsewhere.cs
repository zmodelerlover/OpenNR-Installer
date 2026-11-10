// Phantom Blade Zero (2026-11-10): v0.9.5 read the 7-Zip in ProjectPBZ\Binaries\Win64 as the game and installed
// beside it; the game is platform\steam\PBZ.exe. Driven as a person gets there: the install goes in beside the
// tool, the game is pointed back at what detection finds now, and the sheet has to say an earlier install is in
// the other folder, offer Uninstall for it, and take it out when Install is pressed.

using Avalonia.Controls;
using Avalonia.VisualTree;
using AmdNr.App;
using AmdNr.Core;

internal static class ElsewhereFlow
{
    public static void Run(Action<bool, string> check, Func<Func<bool>, int, bool> until, Action<Button> click,
        Action<Window, string> save)
    {
        var game = Path.Combine(AppPaths.Root, "PBZ");
        var old = Directory.CreateDirectory(Path.Combine(game, "ProjectPBZ", "Binaries", "Win64")).FullName;
        var steam = Directory.CreateDirectory(Path.Combine(game, "platform", "steam")).FullName;
        var tool = Path.Combine(old, "8za_v2.exe");
        File.WriteAllBytes(tool, [.. Seed.Pe64(), .. new byte[64_000]]);
        File.WriteAllBytes(Path.Combine(steam, "PBZ.exe"), Seed.PeImporting(true, "d3d12.dll"));
        var entries = GameStore.Load();
        entries.Add(new GameEntry { Path = game, Name = "Phantom Blade Zero", Preset = Preset.OptiScaler, PresetChosen = true, Executable = tool });
        GameStore.Save(entries);

        var main = new MainWindow { Width = 1240, Height = 820 };
        main.Show();
        check(until(() => main.Session.Manifest is not null && main.Library.Cards.Any(c => c.Path == game && c.Graphics is not null), 30),
            "the game is read, pointed at the tool");
        var card = main.Library.Cards.First(c => c.Path == game);
        main.OpenSheet(card);
        var sheet = main.GetVisualDescendants().OfType<GameSheet>().First();
        T Named<T>(string name) where T : Control => sheet.FindControl<T>(name)!;
        var install = Named<Button>("InstallButton");
        var earlier = Named<Border>("EarlierBox");
        check(until(() => sheet.IsOpen && Named<TextBlock>("VerdictTitle").Text is { Length: > 0 } && !main.Session.Busy, 15)
              && !earlier.IsVisible, "its sheet opens with no earlier install to speak of");
        click(install);
        check(until(() => !main.Session.Busy && File.Exists(Path.Combine(old, Engine.ManifestNameX64)), 30)
              && File.Exists(Path.Combine(old, "dxgi.dll")), "the install goes in beside the tool");

        // Back to what detection finds, as the sheet's own button does it.
        card.Entry.Executable = null;
        main.Library.Redetect(card);
        sheet.Show(card);
        check(card.Graphics?.Executable == Path.Combine(steam, "PBZ.exe"), $"detection finds the game ({card.Graphics?.Executable})");
        check(until(() => earlier.IsVisible, 15) && Named<TextBlock>("Earlier").Text!.Contains(Path.Combine("ProjectPBZ", "Binaries", "Win64")),
            $"and the sheet says an earlier install is in the other folder ({Named<TextBlock>("Earlier").Text})");
        check(!card.Installed && Named<Button>("UninstallButton").IsVisible, "nothing is installed where the game is, and Uninstall is offered for the earlier one");
        save(main, "flow-elsewhere");

        click(install);
        check(until(() => !main.Session.Busy && card.InstalledVia == RouteFamily.OptiScaler, 30)
              && File.Exists(Path.Combine(steam, "dxgi.dll")), $"Install puts it where the game is ({Named<TextBlock>("ResultTitle").Text})");
        check(!File.Exists(Path.Combine(old, "dxgi.dll")) && !File.Exists(Path.Combine(old, Work.WeightsName)) && File.Exists(tool),
            "and takes the earlier one out, leaving the folder's own files");
        check(!earlier.IsVisible, "the sheet stops saying so");
        main.Close();
    }
}
