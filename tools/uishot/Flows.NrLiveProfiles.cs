// Settings per game on the NRLive page, with a library game picked as the target: shown on the global settings
// until one is changed, its own copy from then on, the global ones a menu entry away, the page still fitting the
// default window with the menu and its reset button in the header, the window in front as the countdown ends
// bringing its game's settings, and the reset back to the global ones. The countdown runs with "Run as
// administrator" off, so it runs here and NRLive is started only when it ends: the stand-in exe fails to start.

using Avalonia.Controls;
using Avalonia.LogicalTree;
using AmdNr.App;
using AmdNr.Core;

internal static class NrLiveProfilesFlow
{
    public static void Run(MainWindow main, NrLivePage page, GameCard card, string gameExe, Action<bool, string> check,
        Func<Func<bool>, int, bool> until, Action<Button> click, Action<Window, string> save)
    {
        string S(string key) => main.FindResource(key) as string ?? key;
        T Named<T>(string name) where T : Control => page.FindControl<T>(name)!;
        Control? Setting(string id) => page.GetLogicalDescendants().OfType<Control>().FirstOrDefault(c => c.Tag as string == id);
        var menu = Named<ComboBox>("ProfileBox");
        var reset = Named<Button>("ResetProfileButton");
        var file = Path.Combine(NrLivePackage.Folder, "opennr-profiles", NrLiveGame.IdFor(card.Path) + ".json");
        var frameGeneration = NrLiveConfig.Settings.First(s => s.Id == "FrameGeneration.enabled");
        int Fg() => NrLiveConfig.Load(NrLivePackage.Folder).Get(frameGeneration);
        string? Shown() => (menu.SelectedItem as ProfileChoice)?.Label;

        check(Shown() == string.Format(S("Str.NrLiveProfileShared"), card.Name) && !reset.IsVisible && !File.Exists(file) && Fg() == 1,
            $"the game picked shows the global settings until one is changed ({Shown()})");
        ((ToggleSwitch)Setting("FrameGeneration.enabled")!).IsChecked = false;
        until(() => false, 1);
        check(File.Exists(file) && Fg() == 0 && Shown() == card.Name && reset.IsVisible,
            "the first change gives the game its own settings, in opennr-profiles");

        menu.SelectedItem = menu.Items.OfType<ProfileChoice>().First(c => c.Game is null);
        until(() => false, 1);
        check(Fg() == 1 && ((ToggleSwitch)Setting("FrameGeneration.enabled")!).IsChecked == true && !reset.IsVisible,
            "switching to Global puts the global settings back in scaleconfig.ini and on the page");
        menu.SelectedItem = menu.Items.OfType<ProfileChoice>().First(c => c.Game?.Name == card.Name);
        until(() => false, 1);
        check(Fg() == 0 && ((ToggleSwitch)Setting("FrameGeneration.enabled")!).IsChecked == false, "and back to the game's");

        // The menu and the reset button sit in the session card's header: the page still fits the default window.
        var model = Path.Combine(NrLivePackage.Folder, "dlssnr-amd", "dlssnr.bin");
        File.WriteAllBytes(model, [0]);
        page.Show();
        (main.Width, main.Height) = (1240, 800);
        until(() => false, 1);
        var scroll = Named<ScrollViewer>("PageScroll");
        check(reset.IsVisible && scroll.Extent.Height <= scroll.Viewport.Height + 1,
            $"with a game's settings shown the page still fits 1240x800 (extent {scroll.Extent.Height:0}, viewport {scroll.Viewport.Height:0})");
        save(main, "flow-nrlive-profiles");

        // The window in front when the countdown ends: on the global settings until then, its game's after.
        var admin = Named<ToggleSwitch>("AdminSwitch");
        admin.IsChecked = false;
        Named<ComboBox>("TargetModeBox").SelectedIndex = 0;
        check(Fg() == 1 && Shown() == S("Str.NrLiveProfileGlobal"), "the window in front starts on the global settings");
        NrLivePage.Foreground = () => (0, gameExe);
        Named<ComboBox>("DelayBox").SelectedItem = 3;
        click(Named<Button>("ScaleButton"));
        var status = Named<TextBlock>("StatusText");
        var inUse = string.Format(S("Str.NrLiveProfileInUse"), card.Name);
        check(until(() => Fg() == 0 && status.Text?.EndsWith(inUse) == true && menu.IsEnabled, 10) && Shown() == card.Name,
            $"as the countdown ends on the game's window its settings are applied and named on the status line ({status.Text})");
        save(main, "flow-nrlive-profile-foreground");
        NrLivePage.Foreground = () => (0, null);

        click(reset);
        until(() => false, 1);
        check(!File.Exists(file) && Fg() == 1 && !reset.IsVisible && Shown() == string.Format(S("Str.NrLiveProfileShared"), card.Name),
            "Use global settings deletes the game's and puts the global ones back");
        admin.IsChecked = true;
        Named<ComboBox>("DelayBox").SelectedItem = 5;
        File.Delete(model);
        (main.Width, main.Height) = (1240, 820);
    }
}
