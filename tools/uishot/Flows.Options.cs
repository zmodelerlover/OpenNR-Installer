// `uishot <out> options <payload folder>`: the opt-in tiles under the NR runtimes (WrongGPU, OpenRR, danielblnc's
// DLSS-FG) and the REFramework card, on the sheet of a listed RE Engine game with NVIDIA's nvngx_dlssd.dll beside it
// and of a game with neither, wide, at the usual width and a narrow one, and narrow in Portuguese. The payload folder holds a payload.json and an
// api-db.json whose OptiScaler carries those components, plus an nvngx_dlssd.dll 310.9.1; they are copied into
// AMDNR_HOME, which has to be a folder of its own like the rest.

using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using AmdNr.App;
using AmdNr.Core;
using static Seed;

internal static class OptionsShots
{
    public static void Run(string from, Func<Func<bool>, int, bool> until, Action<Window, string> save)
    {
        File.WriteAllText(Path.Combine(AppPaths.Root, "config.json"), """
            { "App": { "Owner": "", "Repo": "" }, "Addon": { "Owner": "", "Repo": "" },
              "Payload": { "ManifestUrl": "https://127.0.0.1:1/payload.json",
                           "ApiDbUrl": "https://127.0.0.1:1/api-db.json" } }
            """);
        File.Copy(Path.Combine(from, "payload.json"), Path.Combine(AppPaths.Root, "payload.json"), true);
        File.Copy(Path.Combine(from, "api-db.json"), Path.Combine(AppPaths.Root, "api-db.json"), true);
        var root = Path.Combine(AppPaths.Root, "option-games");
        var games = new List<GameEntry>();
        foreach (var (name, exe, dlssd) in new[] { ("Resident Evil Requiem", "re9.exe", true), ("D3D12 Game", "Game.exe", false) })
        {
            var dir = Path.Combine(root, name);
            Directory.CreateDirectory(dir);
            File.WriteAllBytes(Path.Combine(dir, exe), PeImporting(true, "d3d12.dll"));
            File.WriteAllText(Path.Combine(dir, "nvngx_dlss.dll"), "dlss");
            if (dlssd) File.Copy(Path.Combine(from, "nvngx_dlssd.dll"), Path.Combine(dir, "nvngx_dlssd.dll"), true);
            games.Add(new GameEntry { Path = dir, Name = name, Preset = Preset.OptiScaler, PresetChosen = true });
        }
        GameStore.Save(games);

        // Portuguese last: it runs longer than English.
        foreach (var (w, h, lang) in new[] { (1920, 1040, "en"), (1240, 900, "en"), (980, 760, "en"), (980, 760, "pt-BR") })
        {
            App.ChangeLanguage(lang);
            var main = new MainWindow { Width = w, Height = h };
            main.Show();
            until(() => main.Session.Manifest is not null && main.Library.Cards.All(c => c.Graphics is not null), 30);
            var sheet = main.GetVisualDescendants().OfType<GameSheet>().First();
            foreach (var (card, list) in main.Library.Cards.ToList().SelectMany(c => new[] { (c, false), (c, w == 1920) }).Distinct())
            {
                // The list view docks the sheet beside the list, wider than over the grid.
                if (list) main.GetVisualDescendants().OfType<LibraryPage>().First().FindControl<Button>("ListViewButton")!
                    .RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                if (list) main.OpenSheet(card); else sheet.Open(card);
                until(() => false, 2);
                // danielblnc 0.8.0 on the game without its own DLSS-FG, so its FG tile shows too.
                if (card.Name == "D3D12 Game" && sheet.FindControl<ComboBox>("DanielVersionBox") is { } daniel
                    && daniel.Items.OfType<RuntimeOption>().ToList().FindIndex(o => o.Version.StartsWith("0.8")) is >= 0 and var at8)
                    daniel.SelectedIndex = at8;
                until(() => false, 2);
                var extras = sheet.FindControl<Control>("OptiExtras")!;
                if (extras.FindAncestorOfType<ScrollViewer>() is { Content: Visual content } scroll
                    && extras.TranslatePoint(new Point(0, 0), content) is { } at)
                    scroll.Offset = new Vector(0, Math.Max(0, at.Y - 260));
                until(() => false, 1);
                var tag = $"options-{(list ? "list-" : "")}{card.Name.Replace(' ', '-')}-{w}x{h}{(lang == "en" ? "" : "-" + lang)}";
                save(main, tag);
                Console.WriteLine($"{tag}: rdna4 {main.Session.Machine?.Rdna4}, extras {extras.IsVisible}");
                if (card.Name != "D3D12 Game" && !list && w != 980)
                {
                    // Ticked, WrongGPU's tile is lit and its "off by default" chip goes; unticked again after.
                    var box = sheet.FindControl<OptionTile>("WrongGpuSection")!.GetVisualDescendants().OfType<CheckBox>().First();
                    foreach (var on in new[] { true, false })
                    {
                        box.IsChecked = on;
                        box.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                        until(() => false, 3);
                        if (on) save(main, tag + "-ticked");
                    }
                    Console.WriteLine($"  WrongGPU ticked and unticked: entry {card.Entry.WrongGpu}");
                }
                if (list) main.GetVisualDescendants().OfType<LibraryPage>().First().FindControl<Button>("GridViewButton")!
                    .RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            }
            main.Close();
        }
    }
}
