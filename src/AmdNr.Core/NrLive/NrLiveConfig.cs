// scaleconfig.ini, the file NRLive.exe reads its settings from when it starts and writes when Save is pressed in
// its overlay. The NRLive page edits it in place, so the two never disagree: whatever was last set, here or
// there, is what the next session runs with.
//
// What the page shows is this table, one row per key, rather than a form written by hand: a key NRLive learns
// is one line here, and the page lays out a switch, a slider or a menu for it on its own.

using System.Text;

namespace AmdNr.Core;

public enum NrLiveKind { Toggle, Range, Choice }

/// <summary>One key of scaleconfig.ini. <paramref name="Group"/> is the card it sits in ("picture", "nr",
/// "overlay"); <paramref name="Choices"/> is how many entries a menu has, each labelled by
/// <see cref="ChoiceKey"/>; an advanced one sits behind "More settings".
/// <paramref name="Pending"/> marks a key a newer NRLive is getting: it is shown only once the NRLive.exe in
/// the folder reads it (<see cref="NrLiveConfig.Supported"/>). <paramref name="Only"/> shows it with one NR
/// runtime or one HUD only (<see cref="NrLiveConfig.Tags"/>). <paramref name="Scale"/> above 1 is a decimal key
/// shown as a whole number (1.25 as 125 %); <paramref name="Values"/> are the words a menu writes for its entries.</summary>
public sealed record NrLiveSetting(string Group, string Section, string Key, NrLiveKind Kind, int Default,
    int Min = 0, int Max = 1, int Choices = 0, bool Advanced = false, bool Pending = false, string Unit = "",
    string? Only = null, int Scale = 1, string[]? Values = null)
{
    public string Id => $"{Section}.{Key}";
    public string LabelKey => $"Str.NrLiveSet.{Section}.{Key}";
    public string ChoiceKey(int value) => $"{LabelKey}.{value}";
}

public sealed class NrLiveConfig
{
    public const string FileName = "scaleconfig.ini";

    /// <summary>The tags of <see cref="NrLiveSetting.Only"/>: NR through OpenNR's runtime or through NRLive's 0.0.6
    /// bridge, and the HUD with presets or the frame counter before it.</summary>
    public const string OpenNr = "opennr", Bridge = "bridge", Hud = "hud", OldHud = "oldhud";

    /// <summary>NRLive's --upscaler, the one choice on its command line rather than in this file.</summary>
    public const string UpscalerId = "cli.upscaler";
    public static readonly string[] Upscalers = ["fsr4", "fsr31", "none"];

    /// <summary>The HUD items a preset switches, with the presets that switch each one on (NRLive's
    /// HudSettingsWindow, Preset_Click: 1 minimal, 2 compact, 3 standard, 4 full).</summary>
    private static readonly (string Key, int[] On)[] HudItems =
    [
        ("fps", [1, 2, 3, 4]), ("title", [3, 4]), ("fps_detail", [3, 4]), ("frametime", [2, 3, 4]),
        ("frame_timing", [3, 4]), ("lows", [3, 4]), ("gpu_load", [2, 3, 4]), ("gpu_temp", [2, 3, 4]), ("vram", [3, 4]),
        ("cpu_load", [2, 3, 4]), ("pipeline", [2, 3, 4]), ("resolution", [3, 4]), ("gpu_clock", [4]), ("ram", [4]),
        ("gpu_times", [4]), ("latency", [4]), ("clock", [4]),
    ];

    private static NrLiveSetting Mz(string key, NrLiveKind kind, int def, int min = 0, int max = 1, int choices = 0,
        bool advanced = false, int scale = 1, string unit = "") =>
        new("nr", "OpenNR", key, kind, def, min, max, choices, advanced, Pending: true, unit, OpenNr, scale);

    private static NrLiveSetting Br(string key, NrLiveKind kind, int def, int min = 0, int max = 1, int choices = 0,
        bool advanced = false, string unit = "") =>
        new("nr", "DLSSNR", key, kind, def, min, max, choices, advanced, Unit: unit, Only: Bridge);

    private static NrLiveSetting Fps(string key, NrLiveKind kind, int def, int min = 0, int max = 1, int choices = 0,
        bool advanced = false, bool pending = false, string unit = "", string? only = null) =>
        new("overlay", "FPS", key, kind, def, min, max, choices, advanced, pending, unit, only);

    /// <summary>Every key the page offers, in the order it shows them, with NRLive's own defaults and limits
    /// (overlay.cpp loadCfg, opennr.cpp readConfig with MochizukiRenoKeys.inc, hud_config.cpp loadHudConfig): a
    /// value outside them is what NRLive would clamp anyway.</summary>
    public static readonly IReadOnlyList<NrLiveSetting> Settings =
    [
        new("picture", "FSR", "sharpness", NrLiveKind.Range, 65, 0, 100, Unit: "%"),
        new("picture", "FrameGeneration", "enabled", NrLiveKind.Toggle, 0),
        new("picture", "FrameLimiter", "enabled", NrLiveKind.Toggle, 0),
        new("picture", "FrameLimiter", "fps", NrLiveKind.Range, 60, 15, 500, Unit: " fps"),
        new("picture", "FrameLimiter", "method", NrLiveKind.Choice, 0, Choices: 2, Advanced: true),

        new("nr", "DLSSNR", "enabled", NrLiveKind.Toggle, 0),
        // auto: OpenNR's runtime when NRLive finds MochizukiNrRuntime.dll, else the 0.0.6 bridge.
        new("nr", "DLSSNR", "runtime", NrLiveKind.Choice, 0, Choices: 3, Pending: true, Values: ["auto", "opennr", "bridge"]),
        Mz("MochizukiOriginalModel", NrLiveKind.Toggle, 0),
        Mz("MochizukiRenoStyle", NrLiveKind.Choice, 0, choices: 3),
        Mz("MochizukiRenoIntensity", NrLiveKind.Range, 100, 0, 200, scale: 100, unit: "%"),
        Mz("MochizukiRenoPasses", NrLiveKind.Range, 1, 1, 4),
        Mz("MochizukiRenoLocalTone", NrLiveKind.Range, 100, 0, 200, advanced: true, scale: 100, unit: "%"),
        Mz("MochizukiRenoLocalStructure", NrLiveKind.Range, 100, 0, 200, advanced: true, scale: 100, unit: "%"),
        Mz("MochizukiRenoSkinStructure", NrLiveKind.Range, -100, -100, 100, advanced: true, scale: 100, unit: "%"),
        Mz("MochizukiRenoAutoMask", NrLiveKind.Toggle, 0, advanced: true),
        Mz("MochizukiRenoSkinIndependent", NrLiveKind.Toggle, 1, advanced: true),
        Mz("MochizukiRenoColorStrength", NrLiveKind.Range, 100, 0, 100, advanced: true, scale: 100, unit: "%"),
        Mz("MochizukiTemporal", NrLiveKind.Toggle, 1, advanced: true),
        Mz("MochizukiHistoryStrength", NrLiveKind.Range, 100, 0, 100, advanced: true, scale: 100, unit: "%"),
        Mz("MochizukiStyleGrade", NrLiveKind.Toggle, 1, advanced: true),
        Mz("MochizukiAco", NrLiveKind.Toggle, 1, advanced: true),
        Br("model", NrLiveKind.Choice, 0, choices: 2),
        Br("style", NrLiveKind.Choice, 0, choices: 3),
        Br("scale", NrLiveKind.Range, 100, 25, 100, unit: "%"),
        Br("intensity", NrLiveKind.Range, 100, 0, 200, unit: "%"),
        Br("passes", NrLiveKind.Range, 1, 1, 3, advanced: true),
        Br("structure", NrLiveKind.Range, 100, 0, 200, advanced: true, unit: "%"),
        Br("color_strength", NrLiveKind.Range, 0, 0, 100, advanced: true, unit: "%"),
        Br("local_tone", NrLiveKind.Range, 100, 0, 200, advanced: true, unit: "%"),
        Br("max_ratio", NrLiveKind.Range, 200, 100, 800, advanced: true, unit: "%"),
        Br("skin_structure", NrLiveKind.Range, -100, -100, 200, advanced: true, unit: "%"),
        Br("history_strength", NrLiveKind.Range, 20, 0, 100, advanced: true, unit: "%"),
        Br("automatic_skin_mask", NrLiveKind.Toggle, 1, advanced: true),
        Br("stabilizer", NrLiveKind.Toggle, 1, advanced: true),

        Fps("enabled", NrLiveKind.Toggle, 1),
        Fps("preset", NrLiveKind.Choice, 3, choices: 5, pending: true, only: Hud),
        Fps("position", NrLiveKind.Choice, 0, choices: 4, only: OldHud),
        Fps("position", NrLiveKind.Choice, 0, choices: 6, pending: true, only: Hud),
        Fps("pipeline", NrLiveKind.Toggle, 1, pending: true, only: Hud),
        Fps("gpu_load", NrLiveKind.Toggle, 1, pending: true, only: Hud),
        Fps("gpu_temp", NrLiveKind.Toggle, 1, pending: true, only: Hud),
        Fps("fps", NrLiveKind.Toggle, 1, advanced: true),
        Fps("frametime", NrLiveKind.Toggle, 1, advanced: true),
        Fps("frame_timing", NrLiveKind.Toggle, 1, advanced: true),
        Fps("lows", NrLiveKind.Toggle, 1, advanced: true, pending: true, only: Hud),
        Fps("vram", NrLiveKind.Toggle, 1, advanced: true, pending: true, only: Hud),
        Fps("cpu_load", NrLiveKind.Toggle, 1, advanced: true, pending: true, only: Hud),
        Fps("gpu_times", NrLiveKind.Toggle, 0, advanced: true, pending: true, only: Hud),
        Fps("resolution", NrLiveKind.Toggle, 1, advanced: true),
        Fps("background", NrLiveKind.Toggle, 1, advanced: true),
        Fps("text_outline", NrLiveKind.Toggle, 1, advanced: true),
        Fps("font_size", NrLiveKind.Range, 24, 12, 48, advanced: true, unit: " px", only: OldHud),
        Fps("scale", NrLiveKind.Range, 100, 50, 250, advanced: true, pending: true, unit: "%", only: Hud),
        Fps("background_alpha", NrLiveKind.Range, 50, 0, 100, advanced: true, unit: "%", only: OldHud),
        Fps("background_alpha", NrLiveKind.Range, 80, 0, 100, advanced: true, pending: true, unit: "%", only: Hud),
    ];

    private readonly string _path;
    private readonly bool _unicode;
    private string _text;

    private NrLiveConfig(string path, string text, bool unicode)
    {
        _path = path;
        _text = text;
        _unicode = unicode;
    }

    /// <summary>The file in <paramref name="folder"/>, or an empty one to fill. Read as Latin-1 unless it starts
    /// with a UTF-16 mark: Windows writes these files in the ANSI code page, and Latin-1 gives every byte back
    /// as it was, so a screenshot path in any language survives the page saving around it.</summary>
    public static NrLiveConfig Load(string folder)
    {
        var path = Path.Combine(folder, FileName);
        byte[] bytes;
        try { bytes = File.Exists(path) ? File.ReadAllBytes(path) : []; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { bytes = []; }
        var unicode = bytes is [0xFF, 0xFE, ..];
        var text = unicode ? Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2) : Encoding.Latin1.GetString(bytes);
        return new NrLiveConfig(path, text, unicode);
    }

    /// <summary>The value NRLive would run with: what the file says, as NRLive reads it (a whole number, a decimal
    /// times <see cref="NrLiveSetting.Scale"/>, true/false/on/off, or one of the menu's words), clamped to the
    /// setting's range; its default when the file does not say.</summary>
    public int Get(NrLiveSetting s)
    {
        var raw = Engine.GetIni(_text, s.Section, s.Key).Trim();
        if (s.Values is { } words)
        {
            var at = Array.FindIndex(words, w => w.Equals(raw, StringComparison.OrdinalIgnoreCase));
            return at >= 0 ? at : s.Default;
        }
        if (s.Kind == NrLiveKind.Toggle && raw.ToLowerInvariant() is "true" or "on" or "false" or "off")
            return raw.ToLowerInvariant() is "true" or "on" ? 1 : 0;
        var digits = raw.Length > 0 && raw[0] == '-' ? 1 : 0;
        while (digits < raw.Length && (char.IsAsciiDigit(raw[digits]) || (s.Scale > 1 && raw[digits] == '.'))) digits++;
        if (!double.TryParse(raw.AsSpan(0, digits), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var number)) return s.Default;
        var value = (int)Math.Round(s.Scale > 1 ? number * s.Scale : Math.Truncate(number));
        return s.Kind switch
        {
            NrLiveKind.Toggle => value != 0 ? 1 : 0,
            NrLiveKind.Choice => value >= 0 && value < s.Choices ? value : s.Default,
            _ => Math.Clamp(value, s.Min, s.Max),
        };
    }

    public void Set(NrLiveSetting s, int value)
    {
        var text = s.Values is { } words ? words[Math.Clamp(value, 0, words.Length - 1)]
            : s.Scale > 1 ? (value / (double)s.Scale).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)
            : value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        _text = Engine.SetIni(_text, s.Section, s.Key, text);
        // A HUD item changed by hand is no preset any more; a preset sets the items, as NRLive's own settings do.
        if (s.Section == "FPS" && HudItems.Any(i => i.Key == s.Key)) _text = Engine.SetIni(_text, "FPS", "preset", "0");
        if (s.Section == "FPS" && s.Key == "preset" && value > 0)
        {
            foreach (var (key, on) in HudItems) _text = Engine.SetIni(_text, "FPS", key, on.Contains(value) ? "1" : "0");
            _text = Engine.SetIni(_text, "FPS", "layout", value == 2 ? "1" : "0");
        }
    }

    public void Save()
    {
        var body = _unicode ? [.. Encoding.Unicode.GetPreamble(), .. Encoding.Unicode.GetBytes(_text)] : Encoding.Latin1.GetBytes(_text);
        Engine.MakeParent(_path);
        File.WriteAllBytes(_path, body);
    }

    /// <summary>Which runtime NR goes through and which HUD NRLive draws, as tags for <see cref="NrLiveSetting.Only"/>.
    /// An NRLive that does not read [DLSSNR] runtime only has the bridge; "auto" is OpenNR's runtime when its DLL
    /// is in <paramref name="folder"/> (opennr\ or beside NRLive.exe), as NRLive decides it.</summary>
    public IReadOnlySet<string> Tags(string folder, IReadOnlySet<string> supported)
    {
        var runtime = Settings.First(s => s.Id == "DLSSNR.runtime");
        var choice = supported.Contains(runtime.Id) ? runtime.Values![Get(runtime)] : Bridge;
        var openNr = choice == OpenNr || (choice == "auto" && OpenNrRuntime(folder) is not null);
        return new HashSet<string> { openNr ? OpenNr : Bridge, supported.Contains("FPS.preset") ? Hud : OldHud };
    }

    /// <summary>OpenNR's MochizukiNrRuntime.dll where NRLive looks for it, or null.</summary>
    public static string? OpenNrRuntime(string folder) =>
        new[] { Path.Combine(folder, "opennr", "MochizukiNrRuntime.dll"), Path.Combine(folder, "MochizukiNrRuntime.dll") }
            .FirstOrDefault(File.Exists);

    /// <summary>Whether the page shows this row: a pending key only once NRLive reads it, a tagged one only with
    /// its runtime or HUD.</summary>
    public static bool Shown(NrLiveSetting s, IReadOnlySet<string> supported, IReadOnlySet<string> tags) =>
        (!s.Pending || supported.Contains(s.Id)) && (s.Only is null || tags.Contains(s.Only));

    /// <summary>The pending keys the NRLive.exe at <paramref name="exe"/> reads: those whose section and key are both
    /// in it, the section as the wide string GetPrivateProfile*W is called with and the key wide or, for the
    /// [OpenNR] keys NRLive widens at run time, narrow; each ending in its terminator so "mode" is not found inside
    /// "model". The keys every NRLive reads are not looked for. <see cref="UpscalerId"/> when it takes --upscaler.</summary>
    public static IReadOnlySet<string> Supported(string exe)
    {
        var found = new HashSet<string>(StringComparer.Ordinal);
        byte[] bytes;
        try { bytes = File.Exists(exe) ? File.ReadAllBytes(exe) : []; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return found; }
        bool Wide(string literal) => bytes.AsSpan().IndexOf(Encoding.Unicode.GetBytes(literal + "\0")) >= 0;
        bool Narrow(string literal) => bytes.AsSpan().IndexOf(Encoding.ASCII.GetBytes(literal + "\0")) >= 0;
        foreach (var s in Settings.Where(s => s.Pending))
            if (Wide(s.Section) && (Wide(s.Key) || (s.Section == "OpenNR" && Narrow(s.Key)))) found.Add(s.Id);
        if (Wide("--upscaler")) found.Add(UpscalerId);
        return found;
    }
}
