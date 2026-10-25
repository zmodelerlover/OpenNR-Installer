// The room a scroller's content keeps on its right, for a scroller that reaches the edge of its box. Without a bar
// the content keeps that room as its margin, so it does not touch the box's border; once the bar shows, its lane
// (Styles/Details.axaml) is that room, and the margin is given back so the content runs up to the bar instead of
// stopping short of it with an empty strip beside the bar.

using Avalonia;
using Avalonia.Controls;

namespace AmdNr.App;

public static class ScrollGutter
{
    public static readonly AttachedProperty<double> SizeProperty =
        AvaloniaProperty.RegisterAttached<ScrollViewer, double>("Size", typeof(ScrollGutter));

    public static double GetSize(ScrollViewer scroll) => scroll.GetValue(SizeProperty);

    public static void SetSize(ScrollViewer scroll, double value) => scroll.SetValue(SizeProperty, value);

    static ScrollGutter() => SizeProperty.Changed.AddClassHandler<ScrollViewer>((scroll, _) =>
    {
        scroll.ScrollChanged -= Fit;
        scroll.ScrollChanged += Fit;
        Fit(scroll, null);
    });

    private static void Fit(object? sender, ScrollChangedEventArgs? e)
    {
        if (sender is not ScrollViewer scroll || scroll.Content is not Control content) return;
        var right = scroll.Extent.Height > scroll.Viewport.Height + 0.5 ? 0 : GetSize(scroll);
        var m = content.Margin;
        if (Math.Abs(m.Right - right) >= 0.5) content.Margin = new Thickness(m.Left, m.Top, right, m.Bottom);
    }
}
