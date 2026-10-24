// The pieces NRLive's settings are laid out from, each following the setting's NrLiveLayout and Panel: a row with
// its control at the end, a slider under its label with the value in a badge, two menus on one line, a nested box,
// a grid of HUD tiles, and the HUD preview. Which setting gets which is the table's business (NrLiveConfig.Settings),
// so a key NRLive adds lands in the right shape without a line here.

using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using AmdNr.Core;

namespace AmdNr.App;

public partial class NrLivePage
{
    private TextBlock? _preview;

    /// <summary>The settings of one list (a card's or its "More"), in table order: runs of tiles, of half-width
    /// menus and of one panel each become one piece.</summary>
    private void Lay(Panel host, List<NrLiveSetting> settings, bool main)
    {
        for (var i = 0; i < settings.Count;)
        {
            var first = settings[i];
            var run = settings.Skip(i).TakeWhile(s => first.Panel is { } p ? s.Panel == p
                : s.Panel is null && s.Layout == first.Layout && first.Layout is NrLiveLayout.Half or NrLiveLayout.Tile).ToList();
            if (run.Count == 0) run = [first];
            i += run.Count;
            if (first.Panel is not null) host.Children.Add(SubPanel(run));
            else if (first.Layout == NrLiveLayout.Half) host.Children.Add(Halves(run));
            else if (first.Layout == NrLiveLayout.Tile)
            {
                if (main) host.Children.Add(Caption("Str.NrLiveHudItems"));
                host.Children.Add(Tiles(run));
            }
            else host.Children.Add(Element(first));
        }
    }

    /// <summary>A setting as a whole row: a slider under its label, anything else at the end of it.</summary>
    private Control Element(NrLiveSetting setting) =>
        setting.Kind == NrLiveKind.Range ? SliderRow(setting) : Row(setting.LabelKey, Control(setting), setting.Hint ? setting.HintKey : null);

    private Control Control(NrLiveSetting setting, Action<bool>? switched = null)
    {
        var value = _config!.Get(setting);
        if (setting.Kind == NrLiveKind.Toggle)
            return Tagged(Switch(value != 0, on =>
            {
                switched?.Invoke(on);
                Store(setting, on ? 1 : 0);
            }), setting);
        var menu = new ComboBox { Width = 180, HorizontalAlignment = HorizontalAlignment.Right };
        for (var i = 0; i < setting.Choices; i++) menu.Items.Add(Ui.Text(setting.ChoiceKey(i)));
        menu.SelectedIndex = value;
        menu.SelectionChanged += (_, _) => { if (menu.SelectedIndex >= 0) Store(setting, menu.SelectedIndex); };
        AutomationProperties.SetName(menu, Ui.Text(setting.LabelKey));
        return Tagged(menu, setting);
    }

    // The setting's id on its control, for the headless flows to find it by.
    private static Control Tagged(Control control, NrLiveSetting setting)
    {
        control.Tag = setting.Id;
        return control;
    }

    private void Store(NrLiveSetting setting, int value)
    {
        Own();
        // Read again first: NRLive's overlay may have saved the file since the page read it.
        _config = NrLiveConfig.Load(NrLivePackage.Folder);
        _config.Set(setting, value);
        try { _config.Save(); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        Keep();
        ShowPreview();
        // The runtime decides which NR rows apply, and a preset rewrites the HUD items: lay the rows out again.
        if (setting.Id is "DLSSNR.runtime" or "FPS.preset" || (setting.Section == "FPS" && setting.Kind == NrLiveKind.Toggle))
            Avalonia.Threading.Dispatcher.UIThread.Post(ShowSettings);
    }

    /// <summary>The label and the value in a badge on one line, the slider the card's width under them.</summary>
    private Control SliderRow(NrLiveSetting setting)
    {
        var value = _config!.Get(setting);
        var shown = new TextBlock { Text = value + setting.Unit };
        var slider = new Slider
        {
            Minimum = setting.Min, Maximum = setting.Max, Value = value, IsSnapToTickEnabled = true, TickFrequency = 1,
            Margin = new Thickness(0, -8, 0, -10), Tag = setting.Id,
        };
        AutomationProperties.SetName(slider, Ui.Text(setting.LabelKey));
        // Written when the slider is let go of rather than on every step it passes through.
        slider.ValueChanged += (_, e) => shown.Text = (int)e.NewValue + setting.Unit;
        slider.AddHandler(PointerCaptureLostEvent, (_, _) => Store(setting, (int)slider.Value), RoutingStrategies.Bubble, true);
        slider.AddHandler(KeyUpEvent, (_, _) => Store(setting, (int)slider.Value), RoutingStrategies.Bubble, true);
        var label = Label(setting.LabelKey);
        var badge = new Border { Classes = { "nrValue" }, Child = shown };
        Grid.SetColumn(badge, 1);
        Grid.SetRow(slider, 1);
        Grid.SetColumnSpan(slider, 2);
        return new Grid { ColumnDefinitions = new("*,Auto"), RowDefinitions = new("Auto,Auto"), Children = { label, badge, slider } };
    }

    /// <summary>A nested box. Headed by a switch, the rest of it applies only while the switch is on, and says so
    /// by going grey.</summary>
    private Control SubPanel(List<NrLiveSetting> run)
    {
        var body = new StackPanel { Spacing = 8 };
        var box = new StackPanel { Spacing = 8 };
        var rest = run;
        if (run[0].Kind == NrLiveKind.Toggle)
        {
            var head = Row(run[0].LabelKey, Control(run[0], on => body.IsEnabled = on));
            ((TextBlock)head.Children[0]).FontWeight = FontWeight.SemiBold;
            box.Children.Add(head);
            body.IsEnabled = _config!.Get(run[0]) != 0;
            rest = run[1..];
        }
        foreach (var s in rest) body.Children.Add(Element(s));
        box.Children.Add(body);
        return new Border { Classes = { "nrPanel" }, Child = box };
    }

    /// <summary>Menus two to a line, each with its caption over it.</summary>
    private Control Halves(List<NrLiveSetting> run)
    {
        var grid = new UniformGrid { Columns = 2, Margin = new Thickness(0, 0, -12, -10) };
        foreach (var s in run)
        {
            var menu = Control(s);
            menu.Width = double.NaN;
            menu.HorizontalAlignment = HorizontalAlignment.Stretch;
            grid.Children.Add(new StackPanel { Spacing = 5, Margin = new Thickness(0, 0, 12, 10), Children = { Caption(s.LabelKey), menu } });
        }
        return grid;
    }

    /// <summary>HUD items as tiles, two across: the whole tile toggles, and an item that is on is tinted.</summary>
    private Control Tiles(List<NrLiveSetting> run)
    {
        var grid = new UniformGrid { Columns = 2, Margin = new Thickness(0, 0, -8, -8) };
        foreach (var s in run)
        {
            Border tile = null!;
            var toggle = (ToggleSwitch)Control(s, on => tile.Classes.Set("on", on));
            var label = new TextBlock { FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
            Ui.Localize(label, s.LabelKey);
            ToolTip.SetTip(label, Ui.Text(s.LabelKey));
            AutomationProperties.SetName(toggle, Ui.Text(s.LabelKey));
            Grid.SetColumn(toggle, 1);
            tile = new Border
            {
                Classes = { "nrTile" }, Margin = new Thickness(0, 0, 8, 8),
                Child = new Grid { ColumnDefinitions = new("*,Auto"), Children = { label, toggle } },
            };
            tile.Classes.Set("on", toggle.IsChecked == true);
            tile.PointerReleased += (_, e) =>
            {
                if (e.Source is Visual v && v.FindAncestorOfType<ToggleSwitch>(includeSelf: true) == toggle) return;
                toggle.IsChecked = toggle.IsChecked != true;
            };
            grid.Children.Add(tile);
        }
        return grid;
    }

    /// <summary>One line in the HUD's shape, from the items switched on: what the counter will carry, at a glance.</summary>
    private Control Preview()
    {
        _preview = new TextBlock
        {
            FontFamily = new FontFamily("Cascadia Mono, Consolas, monospace"), FontSize = 11, TextTrimming = TextTrimming.CharacterEllipsis,
            HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center,
        };
        var caption = Caption("Str.NrLiveHudPreview");
        caption.Margin = new Thickness(0, 0, 12, 0);
        Grid.SetColumn(_preview, 1);
        ShowPreview();
        return new Border { Classes = { "nrPreview" }, Child = new Grid { ColumnDefinitions = new("Auto,*"), Children = { caption, _preview } } };
    }

    private void ShowPreview()
    {
        if (_preview is null || _config is null) return;
        _preview.Inlines!.Clear();
        var supported = NrLiveConfig.Supported(NrLivePackage.Exe);
        var tags = _config.Tags(NrLivePackage.Folder, supported);
        var on = (string id) => NrLiveConfig.Settings.FirstOrDefault(s => s.Id == id) is { } s && _config.Get(s) != 0;
        // The counter switched off draws nothing at all, whatever its items say.
        List<NrLiveSetting> items = !on("FPS.enabled") ? [] : NrLiveConfig.Settings
            .Where(s => s.Sample is not null && NrLiveConfig.Shown(s, supported, tags) && _config.Get(s) != 0).ToList();
        foreach (var s in items)
        {
            if (_preview.Inlines.Count > 0) _preview.Inlines.Add(new Run("  |  ") { Foreground = Ui.Brush("Faint") });
            var text = s.Id == "FPS.pipeline" ? Pipeline(on) : s.Sample!;
            _preview.Inlines.Add(new Run(text) { Foreground = Ui.Brush(s.Id switch { "FPS.fps" => "Ok", "FPS.pipeline" => "AccentText", _ => "TextSoft" }) });
        }
        if (_preview.Inlines.Count == 0) _preview.Inlines.Add(new Run("—") { Foreground = Ui.Brush("Faint") });
    }

    /// <summary>The pipeline item as NRLive spells it: the upscaler, then NR and frame generation when they are on.</summary>
    private string Pipeline(Func<string, bool> on)
    {
        var parts = new List<string>();
        if (_profile.Upscaler switch { "fsr4" => "FSR4", "fsr31" => "FSR3.1", _ => null } is { } upscaler) parts.Add(upscaler);
        if (on("DLSSNR.enabled")) parts.Add("NR");
        if (on("FrameGeneration.enabled")) parts.Add("FG");
        return parts.Count > 0 ? string.Join(" + ", parts) : "—";
    }

    private static ToggleSwitch Switch(bool on, Action<bool> changed)
    {
        var toggle = new ToggleSwitch { IsChecked = on, OnContent = "", OffContent = "", MinWidth = 0, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
        toggle.IsCheckedChanged += (_, _) => changed(toggle.IsChecked == true);
        return toggle;
    }

    /// <summary>A switch in a card's header, its label before it.</summary>
    private static Control HeadSwitch(string labelKey, Control toggle)
    {
        // The label gives way before the card's title does: it is the title that says what the card is.
        var label = new TextBlock
        {
            Classes = { "muted" }, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right,
            TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 0, 8, 0),
        };
        Ui.Localize(label, labelKey);
        ToolTip.SetTip(label, Ui.Text(labelKey));
        AutomationProperties.SetName(toggle, Ui.Text(labelKey));
        Grid.SetColumn(toggle, 1);
        return new Grid { ColumnDefinitions = new("*,Auto"), Children = { label, toggle } };
    }

    /// <summary>A label (and a line under it, for a switch with a hint) at one end and its control at the other.</summary>
    private static Grid Row(string labelKey, Control control, string? hintKey = null)
    {
        Control label = Label(labelKey);
        if (hintKey is not null)
        {
            // One line, the rest behind its tooltip: two would make this card the one that pushes the page past the window.
            var hint = new TextBlock { Classes = { "caption" }, TextTrimming = TextTrimming.CharacterEllipsis };
            Ui.Localize(hint, hintKey);
            ToolTip.SetTip(hint, Ui.Text(hintKey));
            label = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center, Children = { label, hint } };
        }
        label.Margin = new Thickness(0, 0, 16, 0);
        if (control is ToggleSwitch or ComboBox) AutomationProperties.SetName(control, Ui.Text(labelKey));
        Grid.SetColumn(control, 1);
        return new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { label, control } };
    }

    private static TextBlock Label(string key)
    {
        var label = new TextBlock { TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
        Ui.Localize(label, key);
        return label;
    }

    private static TextBlock Caption(string key)
    {
        var caption = new TextBlock { Classes = { "caption" }, TextWrapping = TextWrapping.Wrap };
        Ui.Localize(caption, key);
        return caption;
    }
}
