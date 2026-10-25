// The body of an NRLive settings card: its rows, then its "More" expander right under them. The cards on one line of
// the page share a grid row, so a card that grew when its More opened stretched the others with it, empty at the
// bottom. The cards are top-aligned instead and each body asks for the tallest closed body of its line (Peers): with
// every More closed they are as tall as one another, and an open More makes its own card taller and no other. Three
// across, that stops at the foot of the window (Room) and the rest of More scrolls inside it.

using Avalonia;
using Avalonia.Controls;

namespace AmdNr.App;

public sealed class CardBody : Panel
{
    /// <summary>Between the rows and the More header.</summary>
    public double Gap { get; set; } = 12;

    /// <summary>What an open More shows at the least under its header, when the window has no room left under the
    /// card (two rows or so): the page scrolls then, rather than More opening on a sliver.</summary>
    public double MinMore { get; set; } = 80;

    /// <summary>The bodies on this one's line of the page, itself included, whose closed heights it matches.</summary>
    public CardBody[] Peers
    {
        get => _peers;
        set { _peers = value; InvalidateMeasure(); }
    }

    /// <summary>The most this body may take with its More open: down to the foot of the window three across, where
    /// the page fits it; unbounded on fewer columns, where the page scrolls anyway.</summary>
    public double Room
    {
        get => _room;
        set
        {
            if (Math.Abs(_room - value) < 0.5) return;
            _room = value;
            if (More.IsExpanded) InvalidateMeasure();
        }
    }

    /// <summary>The rows and a closed More: what this body asks for on its own with More closed.</summary>
    public double Closed { get; private set; }

    private CardBody[] _peers = [];
    private double _room = double.PositiveInfinity;
    private double _header;

    private Control Rows => Children[0];
    private Expander More => (Expander)Children[1];

    protected override Size MeasureOverride(Size available)
    {
        var width = new Size(available.Width, double.PositiveInfinity);
        Rows.Measure(width);
        More.Measure(width);
        if (!More.IsExpanded) _header = More.DesiredSize.Height;
        var rows = Rows.DesiredSize.Height;
        var closed = rows + (More.IsVisible ? Gap + _header : 0);
        if (Math.Abs(closed - Closed) >= 0.5)
        {
            Closed = closed;
            // The others on the line match this one, so they ask again.
            foreach (var peer in _peers) if (peer != this) peer.InvalidateMeasure();
        }
        var line = _peers.Where(p => p.IsVisible).Select(p => p.Closed).DefaultIfEmpty(0).Max();
        var height = Math.Max(line, closed);
        if (More.IsVisible && More.IsExpanded)
        {
            var full = rows + Gap + More.DesiredSize.Height;
            height = Math.Max(height, Math.Min(full, Math.Max(_room, closed + MinMore)));
            // Measured again in the room it gets: its scroller only scrolls when it was measured that short.
            More.Measure(new Size(available.Width, height - rows - Gap));
        }
        return new Size(Math.Max(Rows.DesiredSize.Width, More.DesiredSize.Width), height);
    }

    protected override Size ArrangeOverride(Size final)
    {
        var rows = Rows.DesiredSize.Height;
        Rows.Arrange(new Rect(0, 0, final.Width, rows));
        More.Arrange(new Rect(0, rows + Gap, final.Width, More.DesiredSize.Height));
        return final;
    }
}
