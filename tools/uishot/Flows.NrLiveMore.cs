// The NRLive cards at the window's default size: with every More closed the three as tall as one another, as tall as
// the tallest one's rows and no taller, each More right under its rows; each More opened on its own makes its card
// taller and no other, down to the window's foot at most, its bar at the box's edge with the rows up to it. Then the
// pieces in the well, shot to be looked at and their pixels read: a More header and a menu under the pointer and an
// open drop-down; and a HUD tile switched, whose switch is the same control afterwards (laying the rows out again
// replaced it mid-slide). Run from Flows.NrLive.cs with the OpenNR stand-in and its model in place.

using System.Runtime.InteropServices;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.VisualTree;
using AmdNr.App;
using AmdNr.Core;

internal static class NrLiveMoreFlow
{
    private static string S(string key) => Application.Current!.FindResource(key) as string ?? key;

    public static void Run(MainWindow main, NrLivePage page, Action<bool, string> check,
        Func<Func<bool>, int, bool> until, Action<Window, string> save)
    {
        T Named<T>(string name) where T : Control => page.FindControl<T>(name)!;
        Control? Setting(string id) => page.GetLogicalDescendants().OfType<Control>().FirstOrDefault(c => c.Tag as string == id);
        // NR's advanced [OpenNR] keys too, so all three cards have a More, the NR one longer than its room.
        string[] advanced = NrLiveConfig.Settings.Where(s => s.Section == "OpenNR" && s.Advanced).Select(s => s.Key).ToArray();
        File.WriteAllBytes(NrLivePackage.Exe, [.. File.ReadAllBytes(NrLivePackage.Exe), .. Encoding.ASCII.GetBytes(string.Concat(advanced.Select(k => k + "\0")))]);
        page.Show();
        until(() => false, 1);
        string[] names = ["picture", "nr", "overlay"];
        Border[] cards = [Named<Border>("PictureCard"), Named<Border>("NrCard"), Named<Border>("OverlayCard")];
        Expander[] mores = [Named<Expander>("PictureMore"), Named<Expander>("NrMore"), Named<Expander>("OverlayMore")];
        CardBody[] bodies = [Named<CardBody>("PictureBody"), Named<CardBody>("NrBody"), Named<CardBody>("OverlayBody")];
        var scroll = Named<ScrollViewer>("PageScroll");
        string Heights() => string.Join("/", cards.Select(c => c.Bounds.Height.ToString("0")));
        bool Fits() => scroll.Extent.Height <= scroll.Viewport.Height + 1;
        double Bottom(Control c) => c.TranslatePoint(new Point(0, c.Bounds.Height), scroll)?.Y ?? double.MaxValue;
        check(mores.All(m => m.IsVisible), "with NR's advanced keys every card has a More");

        var nrMore = Named<StackPanel>("NrMoreRows").Children;
        var ungrouped = nrMore.Where(c => c is not Expander).ToList();
        var lastSlider = ungrouped.Select((c, i) => (c, i)).Where(x => x.c.GetVisualDescendants().OfType<Slider>().Any()).Max(x => x.i);
        var firstSwitch = ungrouped.Select((c, i) => (c, i)).Where(x => x.c.GetVisualDescendants().OfType<ToggleSwitch>().Any()).Min(x => x.i);
        check(lastSlider < firstSwitch, "NR's More lists its sliders first, then its switches");
        // Under them, closed, the groups OptiScaler's menu has for the same keys, and a group's rows are its own.
        string?[] groups = [.. nrMore.OfType<Expander>().Select(e => e.Tag as string)];
        check(groups.SequenceEqual(["fast", "composition", "look", "encoding", "preprocess"]) && nrMore.OfType<Expander>().All(e => !e.IsExpanded),
            $"and then its groups, closed: fast, composition, look, encoding, preprocess ({string.Join(", ", groups)})");
        var groupOf = (string id) => nrMore.OfType<Expander>().FirstOrDefault(e => e.GetLogicalDescendants().OfType<Control>().Any(c => c.Tag as string == id))?.Tag as string;
        check(groupOf("OpenNR.MochizukiPreprocessCurve") == "preprocess" && groupOf("OpenNR.MochizukiRenoLookBrighten") == "look"
              && groupOf("OpenNR.MochizukiRenoSourceEncoding") == "encoding" && groupOf("OpenNR.MochizukiRenoCodecMode") == "composition",
            "with AgX's curve under preprocess, brightening under the look, source encoding under encoding");
        // A switch that heads its group: off, the rest of the group is grey.
        var prepBody = ((Control)Setting("OpenNR.MochizukiPreprocessCurve")!).GetLogicalAncestors().OfType<StackPanel>().First(p => p.Parent is StackPanel);
        check(!prepBody.IsEnabled, "preprocess off leaves its rows grey");
        // The encoding rows wait for a Source encoding, which itself stays free.
        check(!((Control)Setting("OpenNR.MochizukiRenoFeedMode")!).IsEnabled && !((Control)Setting("OpenNR.MochizukiWhitePoint")!).IsEnabled
              && ((Control)Setting("OpenNR.MochizukiRenoSourceEncoding")!).IsEnabled,
            "with Source encoding at From the format the rest of its group is grey");
        ((ComboBox)Setting("OpenNR.MochizukiRenoSourceEncoding")!).SelectedIndex = 2;
        until(() => false, 1);
        check(((Control)Setting("OpenNR.MochizukiRenoFeedMode")!).IsEnabled && ((Control)Setting("OpenNR.MochizukiWhitePoint")!).IsEnabled
              && ((Control)Setting("OpenNR.MochizukiRenoCodecMode")!).IsEnabled,
            "and Linear takes the grey off it and off Codec under Composition");
        ((ComboBox)Setting("OpenNR.MochizukiRenoSourceEncoding")!).SelectedIndex = 0;
        until(() => false, 1);

        // int4 mixed: grey with the reason on its row while the Small model is in use, what is stored untouched.
        var folder = NrLivePackage.Folder;
        var model = NrLiveConfig.Settings.First(s => s.Id == "OpenNR.MochizukiOriginalModel");
        var int4Setting = NrLiveConfig.Settings.First(s => s.Id == "OpenNR.MochizukiInt4");
        var stored = NrLiveConfig.Load(folder);
        stored.Set(int4Setting, 1);
        stored.Set(model, 0);
        stored.Save();
        page.Show();
        until(() => false, 1);
        var int4 = (ToggleSwitch)Setting(int4Setting.Id)!;
        var int4Row = (Grid)int4.Parent!;
        check(!int4.IsEnabled && int4.IsChecked == true && Equals(ToolTip.GetTip(int4Row), S(int4Setting.OffKey)) && int4Row.Children[0].Opacity < 1,
            "with the Small model int4 mixed is grey, says why on its tooltip and keeps its stored value");
        ((ToggleSwitch)Setting(model.Id)!).IsChecked = true;
        until(() => false, 1);
        check(int4.IsEnabled && ToolTip.GetTip(int4Row) is null && NrLiveConfig.Load(folder).Get(int4Setting) == 1,
            "the Original model takes the grey off, the value as it was");
        ((ToggleSwitch)Setting(model.Id)!).IsChecked = false;
        until(() => false, 1);
        check(!int4.IsEnabled && NrLiveConfig.Load(folder).Get(int4Setting) == 1, "and the Small model again puts it back");
        stored = NrLiveConfig.Load(folder);
        stored.Set(int4Setting, 0);
        stored.Save();
        page.Show();
        until(() => false, 1);

        // All closed: one height, the tallest body's own, and the three More headers on one line at the foot.
        var heights = cards.Select(c => c.Bounds.Height).ToArray();
        check(heights.Max() - heights.Min() < 1 && Math.Abs(bodies.Max(b => b.Closed) - bodies[0].Bounds.Height) < 1,
            $"with every More closed the three cards are as tall as one another and as the tallest one's rows ({Heights()})");
        double Top(Control c) => c.TranslatePoint(new Point(0, 0), scroll)?.Y ?? double.MaxValue;
        check(mores.Max(Top) - mores.Min(Top) < 1 && mores.Zip(bodies).All(x => Math.Abs(x.First.Bounds.Bottom - x.Second.Bounds.Height) < 1),
            $"and the closed More headers line up at the foot of their cards ({string.Join("/", mores.Select(m => Top(m).ToString("0")))})");
        check(Fits(), $"and the page fits 1240x800 (extent {scroll.Extent.Height:0}, viewport {scroll.Viewport.Height:0})");
        save(main, "flow-nrlive-closed");

        for (var i = 0; i < mores.Length; i++)
        {
            mores[i].IsExpanded = true;
            until(() => false, 1);
            var others = Enumerable.Range(0, cards.Length).Where(j => j != i).ToArray();
            check(others.All(j => Math.Abs(cards[j].Bounds.Height - heights[j]) < 1) && cards[i].Bounds.Height > heights[i] + 1,
                $"{names[i]}'s More open makes its card taller and no other ({Heights()})");
            var inner = mores[i].GetVisualDescendants().OfType<ScrollViewer>().First();
            // Down to the window's foot; a window with less room than MinMore under the card lets the page scroll instead.
            var least = inner.Viewport.Height <= bodies[i].MinMore;
            check((Fits() && Bottom(cards[i]) <= scroll.Viewport.Height + 1) || least,
                $"and the card stops at the window's foot (card bottom {Bottom(cards[i]):0}, viewport {scroll.Viewport.Height:0}, More shows {inner.Viewport.Height:0} of {inner.Extent.Height:0})");
            var rows = (Control)inner.Content!;
            var box = mores[i].GetVisualDescendants().OfType<Border>().First(b => b.Name == "ExpanderContent");
            var bar = inner.GetVisualDescendants().OfType<ScrollBar>().First(b => b.Orientation == Avalonia.Layout.Orientation.Vertical);
            double Right(Control c) => c.TranslatePoint(new Point(c.Bounds.Width, 0), main)?.X ?? 0;
            if (inner.Extent.Height > inner.Viewport.Height + 1)
            {
                var thumb = bar.GetVisualDescendants().OfType<Thumb>().First();
                check(bar.IsVisible && bar.Bounds.Width <= 10 && Math.Abs(Right(bar) - (Right(box) - box.BorderThickness.Right)) < 1
                      && rows.Margin.Right == 0 && Math.Abs(Right(rows) - (Right(bar) - bar.Bounds.Width)) < 1,
                    $"its rows scroll inside, the thin bar ({bar.Bounds.Width:0} px) at the box's edge and the rows up to its lane " +
                    $"(rows end {Right(rows):0}, bar {Right(bar) - bar.Bounds.Width:0}-{Right(bar):0}, box {Right(box):0})");
                check(thumb.Bounds.Height >= 24, "with a thumb to grab");
            }
            else check(rows.Margin.Right == 14 && Right(rows) <= Right(box) - 14,
                $"its rows fit, and keep the box's padding with no bar beside them ({inner.Viewport.Height:0} of {inner.Extent.Height:0})");
            save(main, $"flow-nrlive-more-{names[i]}");
            if (i == 1)
            {
                // Each group of NR's More opened, shot to be looked at, and scrolling to its last row.
                foreach (var group in nrMore.OfType<Expander>())
                {
                    group.IsExpanded = true;
                    until(() => false, 1);
                    inner.Offset = new Vector(0, inner.Extent.Height);
                    until(() => false, 1);
                    check(inner.Offset.Y + inner.Viewport.Height >= inner.Extent.Height - 1, $"the {group.Tag} group scrolls to its end ({inner.Extent.Height:0})");
                    save(main, $"flow-nrlive-group-{group.Tag}");
                    group.IsExpanded = false;
                    until(() => false, 1);
                    inner.Offset = default;
                }
                // The pointer on NR's More header: the well and its edge; and the bar under it stays as thin.
                var header = mores[1].GetVisualDescendants().OfType<ToggleButton>().First();
                main.MouseMove(header.TranslatePoint(new Point(header.Bounds.Width - 60, header.Bounds.Height / 2), main) ?? default);
                until(() => false, 1);
                check(Near(Pixel(main, header, header.Bounds.Width - 80, header.Bounds.Height / 2), main.FindResource("Well") as IBrush),
                    "a More header under the pointer is in the well");
                save(main, "flow-nrlive-more-hover");
                if (bar.IsVisible)
                {
                    main.MouseMove(bar.TranslatePoint(new Point(bar.Bounds.Width / 2, 20), main) ?? default);
                    until(() => false, 1);
                    check(bar.Bounds.Width <= 10, "and its bar stays thin under the pointer");
                }
                main.MouseMove(default);
            }
            mores[i].IsExpanded = false;
            until(() => false, 1);
        }
        check(cards.Zip(heights).All(x => Math.Abs(x.First.Bounds.Height - x.Second) < 1), $"closed again, the cards are back to one height ({Heights()})");

        // A taller window, a maximised one on a 1080p screen: the cards keep their own height with the room under them
        // free, and NR's More open grows its card down to the window's foot and scrolls inside it.
        (main.Width, main.Height) = (1600, 1000);
        until(() => false, 1);
        var tall = cards.Select(c => c.Bounds.Height).ToArray();
        check(tall.Max() - tall.Min() < 1 && Bottom(cards[0]) < scroll.Viewport.Height - 100,
            $"on a taller window the cards keep one height and leave the room under them free ({Heights()}, viewport {scroll.Viewport.Height:0})");
        save(main, "flow-nrlive-tall-closed");
        mores[1].IsExpanded = true;
        until(() => false, 1);
        var nrInner = mores[1].GetVisualDescendants().OfType<ScrollViewer>().First();
        var foot = scroll.Viewport.Height - Named<StackPanel>("PageStack").Margin.Bottom;
        check(Fits() && Math.Abs(Bottom(cards[1]) - foot) < 2 && nrInner.Extent.Height > nrInner.Viewport.Height + 1
              && Math.Abs(cards[0].Bounds.Height - tall[0]) < 1 && Math.Abs(cards[2].Bounds.Height - tall[2]) < 1,
            $"and NR's More open takes its card alone to the window's foot, scrolling inside ({Bottom(cards[1]):0} of {foot:0}, {nrInner.Viewport.Height:0} of {nrInner.Extent.Height:0})");
        save(main, "flow-nrlive-tall-nr");
        // With the room to see them: the grey int4 row, and the look and preprocess groups opened.
        Setting("OpenNR.MochizukiInt4")!.BringIntoView();
        until(() => false, 1);
        save(main, "flow-nrlive-tall-int4");
        foreach (var group in nrMore.OfType<Expander>().Where(e => e.Tag as string is "look" or "preprocess"))
        {
            group.IsExpanded = true;
            until(() => false, 1);
            group.BringIntoView();
            until(() => false, 1);
            save(main, $"flow-nrlive-tall-{group.Tag}");
            group.IsExpanded = false;
            until(() => false, 1);
        }
        mores[1].IsExpanded = false;
        (main.Width, main.Height) = (1240, 800);
        until(() => false, 1);

        // A menu under the pointer: the well inside its edge, not Fluent's near black inside near white.
        var target = Named<ComboBox>("TargetModeBox");
        main.MouseMove(target.TranslatePoint(new Point(target.Bounds.Width / 2, target.Bounds.Height / 2), main) ?? default);
        until(() => false, 1);
        var inside = Pixel(main, target, 24, 4);
        var edge = new[] { 0.0, 1.0 }.Select(x => Pixel(main, target, x, target.Bounds.Height / 2)).ToArray();
        check(Near(inside, main.FindResource("Well") as IBrush) && edge.Any(c => Near(c, main.FindResource("WellEdge") as IBrush, 10)),
            $"a menu under the pointer is in the well with its edge (#{inside.R:X2}{inside.G:X2}{inside.B:X2}, edge {string.Join("/", edge.Select(c => $"#{c.R:X2}{c.G:X2}{c.B:X2}"))})");
        save(main, "flow-nrlive-combo-hover");
        main.MouseMove(default);

        // A drop-down open, laid over the window so the window's shot carries it.
        target.GetVisualDescendants().OfType<Popup>().First().ShouldUseOverlayLayer = true;
        target.IsDropDownOpen = true;
        until(() => false, 1);
        save(main, "flow-nrlive-dropdown");
        target.IsDropDownOpen = false;
        until(() => false, 1);

        // A HUD tile switched: the preset turns custom in its menu, and the switch is the same control afterwards,
        // so its knob slides to the end instead of a new switch appearing at it.
        var preset = (ComboBox)Setting("FPS.preset")!;
        var was = preset.SelectedIndex;
        var toggle = (ToggleSwitch)Setting("FPS.gpu_temp")!;
        var on = toggle.IsChecked == true;
        toggle.IsChecked = !on;
        until(() => false, 1);
        check(toggle.GetVisualRoot() is not null && Setting("FPS.gpu_temp") == toggle && preset.SelectedIndex == 0
              && toggle.FindAncestorOfType<Border>()!.Classes.Contains("on") == !on,
            "a HUD tile switched keeps its switch, tints or clears, and the preset menu says custom");
        save(main, "flow-nrlive-tiles");
        preset.SelectedIndex = was;
        until(() => false, 1);
    }

    private static bool Near(Color c, IBrush? brush, int tolerance = 3) =>
        brush is ISolidColorBrush b && Math.Abs(c.R - b.Color.R) <= tolerance && Math.Abs(c.G - b.Color.G) <= tolerance && Math.Abs(c.B - b.Color.B) <= tolerance;

    /// <summary>The colour the window's last frame has at a point of a control.</summary>
    private static Color Pixel(Window window, Visual of, double x, double y)
    {
        var p = of.TranslatePoint(new Point(x, y), window) ?? default;
        using var frame = window.CaptureRenderedFrame()!;
        using var bits = frame.Lock();
        var v = (uint)Marshal.ReadInt32(bits.Address, (int)p.Y * bits.RowBytes + (int)p.X * 4);
        var (r, b) = bits.Format == PixelFormat.Rgba8888 ? (v & 0xFF, (v >> 16) & 0xFF) : ((v >> 16) & 0xFF, v & 0xFF);
        return Color.FromRgb((byte)r, (byte)((v >> 8) & 0xFF), (byte)b);
    }
}
