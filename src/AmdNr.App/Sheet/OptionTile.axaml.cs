using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace AmdNr.App;

public partial class OptionTile : UserControl
{
    public static readonly StyledProperty<string?> TitleProperty = AvaloniaProperty.Register<OptionTile, string?>(nameof(Title));
    public static readonly StyledProperty<string?> NoteProperty = AvaloniaProperty.Register<OptionTile, string?>(nameof(Note));

    // A chip of this kind says what an unticked box means, and goes while the box is ticked.
    private const string OffKind = "off";
    private string _kind = "";

    public OptionTile()
    {
        InitializeComponent();
        Box.IsCheckedChanged += (_, _) => Lit();
    }

    /// <summary>The box's name, and what screen readers call it.</summary>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <summary>The (i)'s tooltip.</summary>
    public string? Note { get => GetValue(NoteProperty); set => SetValue(NoteProperty, value); }

    /// <summary>The box was clicked.</summary>
    public event EventHandler<RoutedEventArgs>? Click;

    public bool? IsChecked { get => Box.IsChecked; set => Box.IsChecked = value; }

    public bool BoxEnabled { set => Box.IsEnabled = value; }

    /// <summary>The line beside the chip, hidden when empty.</summary>
    public string Line
    {
        get => LineText.Text ?? "";
        set
        {
            LineText.Text = value;
            Lit();
        }
    }

    /// <summary>The chip: its text (none for no chip), its colour ("ok", "warn", "err", "" for grey, or "off" for a
    /// grey one shown only while unticked) and an optional tooltip.</summary>
    public void Chip(string text, string kind = "", string? tip = null)
    {
        ChipText.Text = text;
        _kind = kind;
        // A grey chip is a tag, as on the game tiles; a coloured one a status pill.
        var coloured = kind is "ok" or "warn" or "err";
        StateChip.Classes.Set("tag", !coloured);
        StateChip.Classes.Set("quiet", !coloured);
        StateChip.Classes.Set("status", coloured);
        foreach (var k in new[] { "ok", "warn", "err" }) StateChip.Classes.Set(k, k == kind);
        Dot.IsVisible = coloured;
        ToolTip.SetTip(StateChip, string.IsNullOrEmpty(tip) ? null : tip);
        Lit();
    }

    private void Lit()
    {
        var on = Box.IsChecked == true;
        Tile.Classes.Set("on", on);
        StateChip.IsVisible = ChipText.Text is { Length: > 0 } && !(on && _kind == OffKind);
        LineText.IsVisible = LineText.Text is { Length: > 0 };
        Under.IsVisible = StateChip.IsVisible || LineText.IsVisible;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TitleProperty)
        {
            Label.Text = Title;
            AutomationProperties.SetName(Box, Title);
        }
        else if (change.Property == NoteProperty) ToolTip.SetTip(Info, Note);
    }

    private void OnClick(object? sender, RoutedEventArgs e) => Click?.Invoke(this, e);
}
