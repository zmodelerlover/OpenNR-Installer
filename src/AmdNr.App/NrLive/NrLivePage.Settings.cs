// NRLive's settings, laid out from NrLiveConfig.Settings into the three cards (the pieces are in NrLivePage.Rows.cs),
// written to scaleconfig.ini the moment they change. A key newer NRLive builds read shows up by itself once the NRLive.exe
// in the folder reads it, so nothing here changes when one lands. And the hotkey boxes, which take the
// combination pressed in them, in the words NRLive reads ("ctrl+alt+s").

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
        _preview = null;
        var supported = NrLiveConfig.Supported(NrLivePackage.Exe);
        foreach (var rows in new[] { PictureRows, PictureMoreRows, NrRows, NrMoreRows, OverlayRows, OverlayMoreRows }) rows.Children.Clear();
        NrHead.Content = null;
        // NRLive's overlay as a whole, the bar and the counter, is a switch on its command line rather than a key,
        // and heads the overlay card: off, nothing else in it is drawn.
        OverlayHead.Content = HeadSwitch("Str.NrLiveOverlay", Switch(!_profile.NoOverlay, on =>
        {
            Own();
            _profile.NoOverlay = !on;
            SaveProfile();
        }));
        // The upscaler is a choice on NRLive's command line, kept in the profile as its launcher keeps it.
        if (supported.Contains(NrLiveConfig.UpscalerId))
        {
            var upscaler = new ComboBox { Width = 180, HorizontalAlignment = HorizontalAlignment.Right, Tag = NrLiveConfig.UpscalerId };
            for (var i = 0; i < NrLiveConfig.Upscalers.Length; i++) upscaler.Items.Add(Ui.Text($"Str.NrLiveUpscaler.{i}"));
            upscaler.SelectedIndex = Math.Max(0, Array.IndexOf(NrLiveConfig.Upscalers, _profile.Upscaler));
            upscaler.SelectionChanged += (_, _) =>
            {
                if (upscaler.SelectedIndex < 0) return;
                Own();
                _profile.Upscaler = NrLiveConfig.Upscalers[upscaler.SelectedIndex];
                SaveProfile();
                ShowPreview();
            };
            PictureRows.Children.Add(Row("Str.NrLiveUpscaler", upscaler));
        }
        var tags = _config.Tags(folder, supported);
        var shown = NrLiveConfig.Settings.Where(s => NrLiveConfig.Shown(s, supported, tags)).ToList();
        foreach (var (group, rows, more, head) in new[]
                 { ("picture", PictureRows, PictureMoreRows, (ContentControl?)null), ("nr", NrRows, NrMoreRows, NrHead), ("overlay", OverlayRows, OverlayMoreRows, OverlayHead) })
        {
            var mine = shown.Where(s => s.Group == group).ToList();
            // A header switch where the card has room for one, a row where it has not.
            var heading = mine.Where(s => s.Layout == NrLiveLayout.Header && head is not null && head.Content is null).Take(1).ToList();
            foreach (var s in heading) head!.Content = HeadSwitch(s.LabelKey, Control(s));
            Lay(rows, mine.Where(s => !s.Advanced && !heading.Contains(s)).ToList(), main: true);
            // More is a list to adjust rather than to read in order: its sliders together first, then its menus and
            // switches, each kind in table order (OrderBy keeps it).
            Lay(more, mine.Where(s => s.Advanced).OrderBy(s => s.Kind switch { NrLiveKind.Range => 0, NrLiveKind.Choice => 1, _ => 2 }).ToList(), main: false);
            if (mine.Any(s => s.Sample is not null)) rows.Children.Add(Preview());
        }
        PictureMore.IsVisible = PictureMoreRows.Children.Count > 0;
        NrMore.IsVisible = NrMoreRows.Children.Count > 0;
        OverlayMore.IsVisible = OverlayMoreRows.Children.Count > 0;
        // NR without NVIDIA's model does nothing, and NRLive only says so in its log. OpenNR's runtime reads it
        // from opennr\dlssnr-amd, where the download puts it; the 0.0.6 bridge from dlssnr-amd beside NRLive.exe.
        var runtime = tags.Contains(NrLiveConfig.OpenNr) ? NrLiveConfig.OpenNrRuntime(folder) : null;
        var openNr = runtime is not null;
        var model = Path.Combine(openNr ? Path.GetDirectoryName(runtime)! : folder, "dlssnr-amd");
        ModelBox.IsVisible = Installed && !File.Exists(Path.Combine(model, "dlssnr.bin")) && !File.Exists(Path.Combine(model, "dlssnr-small.bin"));
        Ui.Localize(ModelText, openNr ? "Str.NrLiveModelMissingOpenNr" : "Str.NrLiveModelMissing");
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
        // The scale hotkey is this app's and one for all; NRLive's own keys are part of a game's settings.
        if (box != ScaleKeyBox) Own();
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
        if ((BypassBox.Text ?? "").Trim() == _profile.BindBypass) return;
        Own();
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
