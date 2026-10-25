// The NRLive cards with every More open at the window's default size: the cards keep their height, the page still
// fits, More lists its sliders before its switches and scrolls inside its card where its rows do not fit. Then the
// pieces the visual round put in the well, shot to be looked at: a More header under the pointer and an open
// drop-down. Run from Flows.NrLive.cs with the OpenNR stand-in and its model in place.

using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.VisualTree;
using AmdNr.App;
using AmdNr.Core;

internal static class NrLiveMoreFlow
{
    public static void Run(MainWindow main, NrLivePage page, Action<bool, string> check,
        Func<Func<bool>, int, bool> until, Action<Window, string> save)
    {
        T Named<T>(string name) where T : Control => page.FindControl<T>(name)!;
        // NR's advanced [OpenNR] keys too, so all three cards have a More, the NR one longer than its room.
        string[] advanced = NrLiveConfig.Settings.Where(s => s.Section == "OpenNR" && s.Advanced).Select(s => s.Key).ToArray();
        File.WriteAllBytes(NrLivePackage.Exe, [.. File.ReadAllBytes(NrLivePackage.Exe), .. Encoding.ASCII.GetBytes(string.Concat(advanced.Select(k => k + "\0")))]);
        page.Show();
        until(() => false, 1);
        Border[] cards = [Named<Border>("PictureCard"), Named<Border>("NrCard"), Named<Border>("OverlayCard")];
        Expander[] mores = [Named<Expander>("PictureMore"), Named<Expander>("NrMore"), Named<Expander>("OverlayMore")];
        var heights = cards.Select(c => c.Bounds.Height).ToArray();
        check(mores.All(m => m.IsVisible), "with NR's advanced keys every card has a More");

        var nrMore = Named<StackPanel>("NrMoreRows").Children;
        var lastSlider = nrMore.Select((c, i) => (c, i)).Where(x => x.c.GetVisualDescendants().OfType<Slider>().Any()).Max(x => x.i);
        var firstSwitch = nrMore.Select((c, i) => (c, i)).Where(x => x.c.GetVisualDescendants().OfType<ToggleSwitch>().Any()).Min(x => x.i);
        check(lastSlider < firstSwitch, "NR's More lists its sliders first, then its switches");

        foreach (var more in mores) more.IsExpanded = true;
        until(() => false, 1);
        var scroll = Named<ScrollViewer>("PageScroll");
        check(cards.Zip(heights).All(x => Math.Abs(x.First.Bounds.Height - x.Second) < 1),
            $"opening every More leaves the cards as tall as they were ({string.Join("/", heights.Select(h => h.ToString("0")))} -> " +
            $"{string.Join("/", cards.Select(c => c.Bounds.Height.ToString("0")))})");
        check(scroll.Extent.Height <= scroll.Viewport.Height + 1,
            $"and the page still fits 1240x800 (extent {scroll.Extent.Height:0}, viewport {scroll.Viewport.Height:0})");
        var inner = mores.Select(m => m.GetVisualDescendants().OfType<ScrollViewer>().First()).ToArray();
        check(inner.All(s => s.Viewport.Height >= Math.Min(s.Extent.Height, 40) - 1) && inner[1].Extent.Height > inner[1].Viewport.Height + 1,
            $"each open More has room in its card, and NR's scrolls inside it ({string.Join("/", inner.Select(s => $"{s.Viewport.Height:0} of {s.Extent.Height:0}"))})");
        var bar = inner[1].GetVisualDescendants().OfType<ScrollBar>().First(b => b.Orientation == Avalonia.Layout.Orientation.Vertical);
        check(bar.IsVisible && bar.Bounds.Width <= 10, $"with a thin scroll bar ({bar.Bounds.Width:0} px)");
        save(main, "flow-nrlive-more-open");

        // The pointer on NR's More header: the well, not black, the chevron's square the same; and the bar under it
        // stays as thin.
        var header = mores[1].GetVisualDescendants().OfType<ToggleButton>().First();
        main.MouseMove(header.TranslatePoint(new Point(header.Bounds.Width - 20, header.Bounds.Height / 2), main) ?? default);
        until(() => false, 1);
        save(main, "flow-nrlive-more-hover");
        main.MouseMove(bar.TranslatePoint(new Point(bar.Bounds.Width / 2, 20), main) ?? default);
        until(() => false, 1);
        check(bar.Bounds.Width <= 10, "and stays thin under the pointer");
        main.MouseMove(default);
        foreach (var more in mores) more.IsExpanded = false;

        // A drop-down open, laid over the window so the window's shot carries it.
        var target = Named<ComboBox>("TargetModeBox");
        target.GetVisualDescendants().OfType<Popup>().First().ShouldUseOverlayLayer = true;
        target.IsDropDownOpen = true;
        until(() => false, 1);
        save(main, "flow-nrlive-dropdown");
        target.IsDropDownOpen = false;
        until(() => false, 1);
    }
}
