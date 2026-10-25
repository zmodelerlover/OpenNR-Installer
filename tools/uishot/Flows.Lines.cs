// The game sheet's lines as laid out: each route's chips on its name's line where the card has room, and the last
// session's and Detected's (i) on their first line, with no caption over either box. Read docked beside the list,
// where the sheet is wide enough for both. The flow's game has no API to recommend a route by, so Chips shows
// OptiScaler's Recommended beside its Installed for a picture, as a game read as D3D12 with it in would.

using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using AmdNr.App;

internal static class SheetLines
{
    public static void Chips(GameSheet sheet, Func<Func<bool>, int, bool> until, Action shot)
    {
        var chips = new[] { sheet.FindControl<Border>("OptiRecommended")!, sheet.FindControl<Border>("OptiInstalled")! };
        var was = chips.Select(c => c.IsVisible).ToArray();
        foreach (var c in chips) c.IsVisible = true;
        until(() => false, 1);
        shot();
        for (var i = 0; i < chips.Length; i++) chips[i].IsVisible = was[i];
    }

    public static void Check(GameSheet sheet, Action<bool, string> check)
    {
        double Mid(Visual v, Visual to) => v.TranslatePoint(new Point(0, v.Bounds.Height / 2), to)?.Y ?? double.NaN;
        foreach (var name in new[] { "RouteReShade", "RouteOpti" })
        {
            var route = sheet.FindControl<RadioButton>(name)!;
            var label = route.GetVisualDescendants().OfType<WrapPanel>().First().Children[0];
            var chips = route.GetVisualDescendants().OfType<Border>().Where(b => b.Classes.Contains("status") && b.IsEffectivelyVisible).ToList();
            check(chips.All(c => Math.Abs(Mid(c, route) - Mid(label, route)) < 3),
                $"{name}: its {chips.Count} chip(s) on the name's line");
        }
        foreach (var (box, line) in new[] { ("LastSession", "SessionTitle"), ("DetectedPart", "ExeLine") })
        {
            var frame = sheet.GetVisualDescendants().OfType<Control>().First(c => c.Name == box);
            var text = sheet.GetVisualDescendants().OfType<TextBlock>().First(c => c.Name == line);
            var info = (text.Parent as Panel)?.Children.OfType<Border>().FirstOrDefault(b => b.Classes.Contains("info"));
            var top = text.TranslatePoint(new Point(0, 0), frame)?.Y ?? double.NaN;
            check(info is not null && top < 20 && (!info.IsEffectivelyVisible || Math.Abs(Mid(info, frame) - Mid(text, frame)) < 12),
                $"{box} opens on its own line, the (i) after it, no caption ({top:0} px down)");
        }
    }
}
