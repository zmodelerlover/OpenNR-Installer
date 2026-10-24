// The NRLive page and the anti-cheat warning that leads to it. NRLive is offered only once the payload list pins a
// real build -- a placeholder is not -- and a stand-in package, already in the cache, is what gets "downloaded" and
// unpacked into the data folder. Then a setting written to scaleconfig.ini, a key a newer NRLive reads showing up
// by itself, a hotkey conflict that holds Scale back, and a game with Easy Anti-Cheat whose sheet says so, leads
// to the page with the game picked, and stops saying it once told to. Nothing is started: Scale would run NRLive.

using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using AmdNr.App;
using AmdNr.Core;

internal static class NrLiveFlow
{
    public static void Run(MainWindow main, byte[] exe, Action<bool, string> check, Func<Func<bool>, int, bool> until,
        Action<Button> click, Action<Window, string> save)
    {
        string S(string key) => main.FindResource(key) as string ?? key;
        var page = main.GetVisualDescendants().OfType<NrLivePage>().First();
        T Named<T>(string name) where T : Control => page.FindControl<T>(name)!;
        var title = Named<TextBlock>("PackageTitle");
        var button = Named<Button>("PackageButton");
        var payload = Path.Combine(AppPaths.Root, "payload.json");
        var original = File.ReadAllText(payload);

        main.ShowPage(MainWindow.Page.NrLive);
        check(until(() => page.IsEffectivelyVisible, 5) && title.Text == S("Str.NrLiveUnavailable") && !button.IsVisible
              && !Named<StackPanel>("Controls").IsEnabled, "a payload list without NRLive says it is not published, and locks the page");
        check(Named<ToggleSwitch>("AdminSwitch").IsChecked == true, "run as administrator starts on, as in NRLive's launcher");
        save(main, "flow-nrlive-unavailable");

        Seed(payload, original, placeholder: true);
        var reload = main.Session.LoadManifestAsync();
        check(until(() => reload.IsCompleted, 10) && title.Text == S("Str.NrLiveUnavailable") && !button.IsVisible,
            "a release still pinned to the placeholder is not offered");

        Seed(payload, original, placeholder: false);
        reload = main.Session.LoadManifestAsync();
        check(until(() => reload.IsCompleted && button.IsVisible, 10) && title.Text == string.Format(S("Str.NrLiveNotDownloaded"), "9.9.9"),
            "a pinned release is offered for download");
        save(main, "flow-nrlive-offered");
        click(button);
        check(until(() => NrLivePackage.InstalledVersion() == "9.9.9" && !main.Session.Busy, 20)
              && until(() => title.Text == string.Format(S("Str.NrLiveReady"), "9.9.9"), 5) && Named<StackPanel>("Controls").IsEnabled,
            $"Download unpacks it into the data folder and unlocks the page ({title.Text})");
        check(Named<Border>("ModelBox").IsVisible, "and says NR has no model without one");

        // A setting is a row, written to scaleconfig.ini when it changes.
        var picture = Named<StackPanel>("PictureRows");
        var frameGeneration = (ToggleSwitch)((Grid)picture.Children[1]).Children[1];
        frameGeneration.IsChecked = true;
        until(() => false, 1);
        check(NrLiveConfig.Load(NrLivePackage.Folder).Get(NrLiveConfig.Settings.First(s => s.Id == "FrameGeneration.enabled")) == 1,
            "switching frame generation on writes it to scaleconfig.ini");
        save(main, "flow-nrlive-ready");

        // A key only a newer NRLive reads shows up once the NRLive.exe in the folder reads it.
        check(picture.Children.Count == 4, $"the upscaler choice is hidden while NRLive does not read it ({picture.Children.Count} rows)");
        File.WriteAllBytes(NrLivePackage.Exe, [.. exe, .. Encoding.Unicode.GetBytes("--upscaler\0")]);
        page.Show();
        check(Named<StackPanel>("PictureRows").Children.Count == 5, "and appears once it does");

        // An NRLive with OpenNR's runtime beside it: the [OpenNR] rows in place of the bridge's, the HUD's preset, and
        // the model looked for where the runtime reads it.
        File.WriteAllBytes(NrLivePackage.Exe, [.. exe, .. Encoding.Unicode.GetBytes("--upscaler\0DLSSNR\0runtime\0OpenNR\0FPS\0preset\0position\0pipeline\0gpu_load\0gpu_temp\0"),
            .. "MochizukiOriginalModel\0MochizukiRenoStyle\0MochizukiRenoIntensity\0MochizukiRenoPasses\0"u8]);
        var runtime = Path.Combine(NrLivePackage.Folder, "opennr", "MochizukiNrRuntime.dll");
        Directory.CreateDirectory(Path.GetDirectoryName(runtime)!);
        File.WriteAllBytes(runtime, exe);
        page.Show();
        var nr = Named<StackPanel>("NrRows");
        check(nr.Children.Count == 6 && Named<TextBlock>("ModelText").Text == S("Str.NrLiveModelMissingOpenNr"),
            $"with OpenNR's runtime the NR card has its rows and asks for the model in opennr ({nr.Children.Count} rows)");
        check(Named<StackPanel>("OverlayRows").Children.Count == 7, "and the HUD has its preset, position and items");
        save(main, "flow-nrlive-opennr");
        Directory.Delete(Path.GetDirectoryName(runtime)!, recursive: true);
        page.Show();

        // The stop key made the same as the scale key: Scale is held back, and the page says why.
        var stop = Named<TextBox>("StopKeyBox");
        Press(stop, Key.S, KeyModifiers.Control | KeyModifiers.Alt);
        check(stop.Text == "ctrl+alt+s" && !Named<Button>("ScaleButton").IsEnabled
              && Named<TextBlock>("StatusText").Text == S("Str.NrLiveProblemConflict"), "a hotkey conflict holds Scale back and says so");
        save(main, "flow-nrlive-conflict");
        Press(stop, Key.A, KeyModifiers.Control | KeyModifiers.Shift);
        check(stop.Text == "ctrl+shift+a" && Named<Button>("ScaleButton").IsEnabled, "and a different key lets it go");

        // A game with Easy Anti-Cheat: the warning on its sheet, the way to NRLive, and Ignore remembered.
        var game = Path.Combine(AppPaths.Root, "flow-anticheat", "Guarded Game");
        Directory.CreateDirectory(Path.Combine(game, "EasyAntiCheat"));
        File.WriteAllBytes(Path.Combine(game, "Guarded.exe"), exe);
        var card = main.Library.Add(new GameEntry { Path = game, Name = "Guarded Game" });
        main.OpenSheet(card);
        var sheet = main.GetVisualDescendants().OfType<GameSheet>().First();
        var warning = sheet.FindControl<AntiCheatCard>("AntiCheatWarning")!;
        check(until(() => sheet.IsOpen && warning.IsVisible, 10)
              && warning.FindControl<TextBlock>("Title")!.Text == string.Format(S("Str.AntiCheatTitle"), "Easy Anti-Cheat"),
            "a game with Easy Anti-Cheat says so on its sheet");
        save(main, "flow-anticheat-warning");
        click(warning.FindControl<Button>("UseButton")!);
        check(until(() => page.IsEffectivelyVisible && !sheet.IsVisible, 5) && Named<ComboBox>("TargetModeBox").SelectedIndex == 2
              && Named<ComboBox>("TargetItemBox").SelectedItem is LibraryGame { Card: var picked } && picked == card, "Use NRLive opens the page with that game picked to capture");
        save(main, "flow-nrlive-game-picked");

        main.OpenSheet(card);
        check(until(() => sheet.IsOpen && warning.IsVisible, 10), "the warning is there again until it is ignored");
        click(warning.FindControl<Button>("IgnoreButton")!);
        check(!warning.IsVisible && GameStore.Load().Any(e => e.AntiCheatIgnored && Engine.SamePath(e.Path, game)),
            "Ignore hides it and saves that with the game");
        sheet.Close();
        until(() => !sheet.IsOpen, 5);
        main.OpenSheet(card);
        check(until(() => sheet.IsOpen, 5) && !until(() => warning.IsVisible, 2), "and it stays hidden on the next visit");
        sheet.Close();
        until(() => !sheet.IsOpen, 5);

        // Out of the list again, and off the ignored games a removal puts it on, so the flows after this find what they left.
        main.Library.Remove(card);
        var settings = Settings.Load();
        settings.Ignored.RemoveAll(i => Engine.SamePath(i.Path, game));
        settings.Save();
        File.WriteAllText(payload, original);
        var back = main.Session.LoadManifestAsync();
        until(() => back.IsCompleted, 10);
        main.ShowPage(MainWindow.Page.Games);
    }

    private static void Press(TextBox box, Key key, KeyModifiers modifiers) =>
        box.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key, KeyModifiers = modifiers });

    /// <summary>The flows' payload list with NRLive 9.9.9 under releases, and its stand-in package in the cache.</summary>
    private static void Seed(string payload, string original, bool placeholder)
    {
        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
            foreach (var (name, text) in new[] { ("NRLive.exe", "MZ stand-in"), ("version.txt", "9.9.9"), ("dlssnr-amd/shaders/a.spv", "spv") })
            {
                using var w = new StreamWriter(zip.CreateEntry(name).Open());
                w.Write(text);
            }
        var bytes = buffer.ToArray();
        var cached = Path.Combine(PayloadCache.FolderFor(PayloadManifest.NrLiveComponent, "9.9.9"), "NRLiveUI.zip");
        Directory.CreateDirectory(Path.GetDirectoryName(cached)!);
        File.WriteAllBytes(cached, bytes);
        var manifest = JsonNode.Parse(original)!.AsObject();
        var releases = manifest["releases"] as JsonObject ?? new JsonObject();
        manifest["releases"] = releases;
        releases["nrlive"] = new JsonArray(new JsonObject
        {
            ["version"] = "9.9.9",
            ["components"] = new JsonObject
            {
                ["nrlive"] = new JsonObject
                {
                    ["version"] = "9.9.9",
                    ["files"] = new JsonArray(new JsonObject
                    {
                        ["name"] = "NRLiveUI.zip", ["size"] = bytes.Length, ["url"] = "https://127.0.0.1:1/NRLiveUI.zip",
                        ["sha256"] = placeholder ? PayloadManifest.PlaceholderSha : Convert.ToHexStringLower(SHA256.HashData(bytes)),
                    }),
                },
            },
        });
        File.WriteAllText(payload, manifest.ToJsonString());
    }
}
