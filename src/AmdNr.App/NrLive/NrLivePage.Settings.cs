// NRLive's settings, laid out from NrLiveConfig.Settings: a switch, a slider or a menu per key, written to
// scaleconfig.ini the moment it changes. A key newer NRLive builds read shows up by itself once the NRLive.exe
// in the folder reads it, so nothing here changes when one lands. And the hotkey boxes, which take the
// combination pressed in them, in the words NRLive reads ("ctrl+alt+s").

using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using AmdNr.Core;

namespace AmdNr.App;

public partial class NrLivePage
{
    private NrLiveConfig? _config;

    private void ShowSettings()
    {
        var folder = NrLivePackage.Folder;
        _config = NrLiveConfig.Load(folder);
        var supported = NrLiveConfig.Supported(NrLivePackage.Exe);
        foreach (var rows in new[] { PictureRows, PictureMoreRows, NrRows, NrMoreRows, OverlayRows, OverlayMoreRows }) rows.Children.Clear();
        // NRLive's overlay as a whole, the bar and the counter, is a switch on its command line rather than a key.
        OverlayRows.Children.Add(Row("Str.NrLiveOverlay", Switch(!_profile.NoOverlay, on =>
        {
            _profile.NoOverlay = !on;
            SaveProfile();
        })));
        foreach (var setting in NrLiveConfig.Settings.Where(s => !s.Pending || supported.Contains(s.Id)))
        {
            var rows = (setting.Group, setting.Advanced) switch
            {
                ("picture", false) => PictureRows, ("picture", true) => PictureMoreRows,
                ("nr", false) => NrRows, ("nr", true) => NrMoreRows,
                (_, false) => OverlayRows, _ => OverlayMoreRows,
            };
            rows.Children.Add(Row(setting.LabelKey, Control(setting)));
        }
        PictureMore.IsVisible = PictureMoreRows.Children.Count > 0;
        NrMore.IsVisible = NrMoreRows.Children.Count > 0;
        OverlayMore.IsVisible = OverlayMoreRows.Children.Count > 0;
        // NR without NVIDIA's model does nothing, and NRLive only says so in its log.
        var model = Path.Combine(folder, "dlssnr-amd");
        ModelBox.IsVisible = Installed && !File.Exists(Path.Combine(model, "dlssnr.bin")) && !File.Exists(Path.Combine(model, "dlssnr-small.bin"));
    }

    private Control Control(NrLiveSetting setting)
    {
        var value = _config!.Get(setting);
        void Store(int v)
        {
            _config.Set(setting, v);
            try { _config.Save(); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        }
        switch (setting.Kind)
        {
            case NrLiveKind.Toggle:
                return Switch(value != 0, on => Store(on ? 1 : 0));
            case NrLiveKind.Choice:
                var menu = new ComboBox { Width = 240, HorizontalAlignment = HorizontalAlignment.Right };
                for (var i = 0; i < setting.Choices; i++) menu.Items.Add(Ui.Text(setting.ChoiceKey(i)));
                menu.SelectedIndex = value;
                menu.SelectionChanged += (_, _) => { if (menu.SelectedIndex >= 0) Store(menu.SelectedIndex); };
                AutomationProperties.SetName(menu, Ui.Text(setting.LabelKey));
                return menu;
            default:
                var shown = new TextBlock { Width = 64, TextAlignment = Avalonia.Media.TextAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
                var slider = new Slider
                {
                    Width = 200, Minimum = setting.Min, Maximum = setting.Max, Value = value,
                    IsSnapToTickEnabled = true, TickFrequency = 1, VerticalAlignment = VerticalAlignment.Center,
                };
                AutomationProperties.SetName(slider, Ui.Text(setting.LabelKey));
                shown.Text = value + setting.Unit;
                // Written when the slider is let go of rather than on every step it passes through.
                slider.ValueChanged += (_, e) => shown.Text = (int)e.NewValue + setting.Unit;
                slider.AddHandler(PointerCaptureLostEvent, (_, _) => Store((int)slider.Value), RoutingStrategies.Bubble, true);
                slider.AddHandler(KeyUpEvent, (_, _) => Store((int)slider.Value), RoutingStrategies.Bubble, true);
                return new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { slider, shown } };
        }
    }

    private static ToggleSwitch Switch(bool on, Action<bool> changed)
    {
        var toggle = new ToggleSwitch { IsChecked = on, OnContent = "", OffContent = "", MinWidth = 0, HorizontalAlignment = HorizontalAlignment.Right };
        toggle.IsCheckedChanged += (_, _) => changed(toggle.IsChecked == true);
        return toggle;
    }

    /// <summary>A label at one end and its control at the other, as the settings page lays out its rows.</summary>
    private static Grid Row(string labelKey, Control control)
    {
        var label = new TextBlock { TextWrapping = Avalonia.Media.TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center, Margin = new Avalonia.Thickness(0, 0, 16, 0) };
        Ui.Localize(label, labelKey);
        if (control is ToggleSwitch or ComboBox) AutomationProperties.SetName(control, Ui.Text(labelKey));
        Grid.SetColumn(control, 1);
        return new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { label, control } };
    }

    // -- Hotkeys -----------------------------------------------------------------------------------------

    /// <summary>The combination pressed in a hotkey box, in NRLive's words: modifiers, then one key it reads.</summary>
    private void OnHotkeyKey(object? sender, KeyEventArgs e)
    {
        if (sender is not TextBox box || e.Key is Key.Tab) return;
        e.Handled = true;
        if (KeyName(e.Key) is not { } name) return;
        var parts = new List<string>();
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control)) parts.Add("ctrl");
        if (e.KeyModifiers.HasFlag(KeyModifiers.Alt)) parts.Add("alt");
        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift)) parts.Add("shift");
        if (e.KeyModifiers.HasFlag(KeyModifiers.Meta)) parts.Add("win");
        parts.Add(name);
        box.Text = string.Join("+", parts);
        _profile.ScaleHotkey = ScaleKeyBox.Text ?? "";
        _profile.StopKey = StopKeyBox.Text ?? "";
        _profile.OverlayKey = OverlayKeyBox.Text ?? "";
        SaveProfile();
        if (box == ScaleKeyBox) RegisterHotkey();
        ShowState();
    }

    private void OnBypassDone(object? sender, RoutedEventArgs e)
    {
        _profile.BindBypass = (BypassBox.Text ?? "").Trim();
        SaveProfile();
        ShowState();
    }

    /// <summary>A key NRLive's hotkey parser knows, by the name it knows it by; null for a modifier on its own
    /// or a key it does not read.</summary>
    internal static string? KeyName(Key key) => key switch
    {
        >= Key.A and <= Key.Z => ((char)('a' + (key - Key.A))).ToString(),
        >= Key.D0 and <= Key.D9 => ((char)('0' + (key - Key.D0))).ToString(),
        >= Key.NumPad0 and <= Key.NumPad9 => ((char)('0' + (key - Key.NumPad0))).ToString(),
        >= Key.F1 and <= Key.F24 => $"f{key - Key.F1 + 1}",
        Key.Home => "home", Key.End => "end", Key.Insert => "insert", Key.Delete => "delete",
        Key.PageUp => "pageup", Key.PageDown => "pagedown", Key.Up => "up", Key.Down => "down",
        Key.Left => "left", Key.Right => "right", Key.Space => "space", Key.Enter => "enter",
        Key.Escape => "esc", Key.Pause => "pause", Key.PrintScreen => "printscreen",
        _ => null,
    };
}
