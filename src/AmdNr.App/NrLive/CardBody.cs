// The body of an NRLive settings card: its rows, then its "More" expander at the foot. The three cards share one
// row of the page, so a card that grew when its More opened made the other two grow with it, empty at the bottom.
// Here a card asks for its rows and a closed More whether More is open or not; open, More takes the room left
// under the rows and its own scroller shows the rest of it.

using Avalonia;
using Avalonia.Controls;

namespace AmdNr.App;

public sealed class CardBody : Panel
{
    /// <summary>How much of an open More shows at the least, header included, when the rows leave less room than
    /// that under them (the tallest card of the row has none): it rises over the last rows rather than showing a
    /// header with nothing under it.</summary>
    public double MinRoom { get; set; } = 160;

    /// <summary>Between the rows and the More header.</summary>
    public double Gap { get; set; } = 12;

    /// <summary>For a card alone on its line of the page (two or one across), which grows with its More instead:
    /// it holds no other card up, and the page scrolls there anyway.</summary>
    public bool Grows
    {
        get => _grows;
        set { _grows = value; InvalidateMeasure(); }
    }

    private bool _grows;
    private double _closed;
    private double _full;
    private double _room = double.PositiveInfinity;

    private Control Rows => Children[0];
    private Expander More => (Expander)Children[1];

    protected override Size MeasureOverride(Size available)
    {
        var width = new Size(available.Width, double.PositiveInfinity);
        Rows.Measure(width);
        More.Measure(width);
        // Measured closed, More is its header; that height is what the card keeps asking for once it opens.
        if (!More.IsExpanded) _closed = More.DesiredSize.Height;
        _full = More.DesiredSize.Height;
        var more = !More.IsVisible ? 0 : Gap + (Grows ? _full : _closed);
        // Open, More is measured again in the room the last arrange gave it: its scroller only scrolls when it
        // was measured that short, not just arranged so.
        if (More.IsExpanded && !Grows) More.Measure(new Size(available.Width, _room));
        return new Size(Math.Max(Rows.DesiredSize.Width, More.DesiredSize.Width), Rows.DesiredSize.Height + more);
    }

    protected override Size ArrangeOverride(Size final)
    {
        var rows = Rows.DesiredSize.Height;
        Rows.Arrange(new Rect(0, 0, final.Width, rows));
        if (!More.IsExpanded)
        {
            More.Arrange(new Rect(0, final.Height - More.DesiredSize.Height, final.Width, More.DesiredSize.Height));
            return final;
        }
        var want = Math.Min(_full, MinRoom);
        var top = Math.Max(0, Math.Min(rows + Gap, final.Height - want));
        var room = final.Height - top;
        if (!Grows && Math.Abs(room - _room) >= 0.5)
        {
            _room = room;
            More.Measure(new Size(final.Width, room));
        }
        More.Arrange(new Rect(0, top, final.Width, room));
        return final;
    }
}