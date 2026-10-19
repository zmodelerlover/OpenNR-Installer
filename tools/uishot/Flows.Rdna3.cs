// mochizuki's RX 7000 network as a person with an RX 7000 meets it: an OptiScaler version that carries it shows a box
// in the NR runtime section, off until ticked, with what it costs; ticked, Install puts mochizuki in with
// dlssnr-amd\shaders-rdna3 and a fresh OptiScaler.ini runs it. On the machine as it really is the box is not there
// (it is an RX 7000 only for this flow), and an install takes the RX 7000 network back out. Run after the mochizuki
// flow; it leaves 1.0.4 installed as the real card has it, for the uninstall flow to take back.

using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using AmdNr.App;
using AmdNr.Core;

internal static class Rdna3Flow
{
    public static void Run(MainWindow main, GameSheet sheet, GameCard card, string game,
        Action<bool, string> check, Func<Func<bool>, int, bool> until, Action<Button> click, Action<Window, string> save)
    {
        T Named<T>(string name) where T : Control => sheet.FindControl<T>(name)!;
        var section = Named<StackPanel>("MochizukiSection");
        var box = Named<CheckBox>("MochizukiRdna3Box");
        var note = Named<TextBlock>("MochizukiNote");
        var versions = Named<ComboBox>("OptiVersionBox");
        var install = Named<Button>("InstallButton");
        var rdna3 = Path.Combine(game, "dlssnr-amd", "shaders-rdna3", "g_attn.spv");

        // The machine this app read, as an RX 7000 for a while: Session.Machine has no other way in.
        var machineProperty = typeof(Session).GetProperty(nameof(Session.Machine))!;
        var real = main.Session.Machine;
        void Machine(SystemState? state) => machineProperty.GetSetMethod(nonPublic: true)!.Invoke(main.Session, [state]);
        void Reselect()
        {
            versions.SelectedIndex = 1;
            versions.SelectedIndex = 0;
        }

        var count = versions.ItemCount;
        MochizukiFlow.Seed("1.0.2", "1.0.4", rdna3: true);
        var reload = main.Session.LoadManifestAsync();
        check(until(() => reload.IsCompleted && versions.ItemCount == count + 1, 10), $"the menu lists the version that carries it ({versions.ItemCount})");
        Machine(new SystemState("AMD Radeon RX 7900 XTX", real?.Driver ?? "unknown", true, true, Rdna4: false, Rx7000: true));
        Reselect();
        check(until(() => section.IsVisible && box.IsVisible, 5), "on an RX 7000 the NR runtime section offers the RX 7000 network");
        check(box.IsChecked != true && note.Text == main.FindResource("Str.MochizukiRdna3Note") as string, $"off until ticked, with what it costs: {note.Text}");
        until(() => false, 1);
        if (section.FindAncestorOfType<ScrollViewer>() is { Content: Visual content } scroll
            && section.TranslatePoint(new Point(0, 0), content) is { } at)
            scroll.Offset = new Vector(0, Math.Max(0, at.Y - 160));
        until(() => false, 1);
        save(main, "flow-2-optiscaler-rx7000");

        box.IsChecked = true;
        box.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        check(card.Entry.MochizukiRdna3 == true, "ticking it is remembered for the game");
        until(() => false, 1);
        save(main, "flow-2-optiscaler-rx7000-ticked");
        click(install);
        check(until(() => !main.Session.Busy, 30) && File.Exists(rdna3) && File.Exists(Path.Combine(game, "MochizukiNrRuntime.dll")),
            "Install brings mochizuki with dlssnr-amd\\shaders-rdna3");
        check(Engine.GetIni(File.ReadAllText(Path.Combine(game, Work.OptiScalerIni)), "DlssNr", "NrBackend").Trim() == "mochizuki",
            "and OptiScaler.ini runs mochizuki");

        // The card as it really is: no box, and an install takes the RX 7000 network out again.
        Machine(real);
        Reselect();
        check(until(() => !box.IsVisible, 5), $"on the real card ({real?.Gpu}) there is no box");
        click(install);
        check(until(() => !main.Session.Busy, 30) && !File.Exists(rdna3), "and an install there takes the RX 7000 network out");
        card.Entry.MochizukiRdna3 = null;
    }
}
