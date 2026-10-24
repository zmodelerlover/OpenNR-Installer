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
/// the folder reads it (<see cref="NrLiveConfig.Supported"/>).</summary>
public sealed record NrLiveSetting(string Group, string Section, string Key, NrLiveKind Kind, int Default,
    int Min = 0, int Max = 1, int Choices = 0, bool Advanced = false, bool Pending = false, string Unit = "")
{
    public string Id => $"{Section}.{Key}";
    public string LabelKey => $"Str.NrLiveSet.{Section}.{Key}";
    public string ChoiceKey(int value) => $"{LabelKey}.{value}";
}

public sealed class NrLiveConfig
{
    public const string FileName = "scaleconfig.ini";

    /// <summary>Every key the page offers, in the order it shows them, with NRLive's own defaults and limits
    /// (overlay.cpp, loadCfg): a value outside them is what NRLive would clamp anyway.</summary>
    public static readonly IReadOnlyList<NrLiveSetting> Settings =
    [
        // TODO(NRLive upscaler choice): the key the runtime work settles on, 0 FSR 4, 1 FSR 3.1, 2 none. Until
        // then FSR is on whenever it starts, and its button in NRLive's overlay turns it off.
        new("picture", "Upscaler", "mode", NrLiveKind.Choice, 0, Choices: 3, Pending: true),
        new("picture", "FSR", "sharpness", NrLiveKind.Range, 65, 0, 100, Unit: "%"),
        new("picture", "FrameGeneration", "enabled", NrLiveKind.Toggle, 0),
        new("picture", "FrameLimiter", "enabled", NrLiveKind.Toggle, 0),
        new("picture", "FrameLimiter", "fps", NrLiveKind.Range, 60, 15, 500, Unit: " fps"),
        new("picture", "FrameLimiter", "method", NrLiveKind.Choice, 0, Choices: 2, Advanced: true),

        new("nr", "DLSSNR", "enabled", NrLiveKind.Toggle, 0),
        // TODO(NRLive OpenNR backend): 0 the bridge NRLive ships, 1 OpenNR's mochizuki runtime.
        new("nr", "DLSSNR", "runtime", NrLiveKind.Choice, 0, Choices: 2, Pending: true),
        new("nr", "DLSSNR", "model", NrLiveKind.Choice, 0, Choices: 2),
        new("nr", "DLSSNR", "style", NrLiveKind.Choice, 0, Choices: 3),
        new("nr", "DLSSNR", "scale", NrLiveKind.Range, 100, 25, 100, Unit: "%"),
        new("nr", "DLSSNR", "intensity", NrLiveKind.Range, 100, 0, 200, Unit: "%"),
        // TODO(NRLive RenoDX look): the switch for the RenoDX colour composition; its strength and the rest of
        // its keys are rows like these once they exist.
        new("nr", "RenoDX", "enabled", NrLiveKind.Toggle, 0, Pending: true),
        new("nr", "DLSSNR", "passes", NrLiveKind.Range, 1, 1, 3, Advanced: true),
        new("nr", "DLSSNR", "structure", NrLiveKind.Range, 100, 0, 200, Advanced: true, Unit: "%"),
        new("nr", "DLSSNR", "color_strength", NrLiveKind.Range, 0, 0, 100, Advanced: true, Unit: "%"),
        new("nr", "DLSSNR", "local_tone", NrLiveKind.Range, 100, 0, 200, Advanced: true, Unit: "%"),
        new("nr", "DLSSNR", "max_ratio", NrLiveKind.Range, 200, 100, 800, Advanced: true, Unit: "%"),
        new("nr", "DLSSNR", "skin_structure", NrLiveKind.Range, -100, -100, 200, Advanced: true, Unit: "%"),
        new("nr", "DLSSNR", "history_strength", NrLiveKind.Range, 20, 0, 100, Advanced: true, Unit: "%"),
        new("nr", "DLSSNR", "automatic_skin_mask", NrLiveKind.Toggle, 1, Advanced: true),
        new("nr", "DLSSNR", "stabilizer", NrLiveKind.Toggle, 1, Advanced: true),

        new("overlay", "FPS", "enabled", NrLiveKind.Toggle, 1),
        new("overlay", "FPS", "position", NrLiveKind.Choice, 0, Choices: 4),
        // TODO(NRLive overlay): the MangoHud-like rows (GPU load, temperature, ...) go here as they land.
        new("overlay", "FPS", "gpu", NrLiveKind.Toggle, 0, Pending: true),
        new("overlay", "FPS", "fps", NrLiveKind.Toggle, 1, Advanced: true),
        new("overlay", "FPS", "frametime", NrLiveKind.Toggle, 1, Advanced: true),
        new("overlay", "FPS", "frame_timing", NrLiveKind.Toggle, 1, Advanced: true),
        new("overlay", "FPS", "resolution", NrLiveKind.Toggle, 1, Advanced: true),
        new("overlay", "FPS", "background", NrLiveKind.Toggle, 1, Advanced: true),
        new("overlay", "FPS", "text_outline", NrLiveKind.Toggle, 1, Advanced: true),
        new("overlay", "FPS", "font_size", NrLiveKind.Range, 24, 12, 48, Advanced: true, Unit: " px"),
        new("overlay", "FPS", "background_alpha", NrLiveKind.Range, 50, 0, 100, Advanced: true, Unit: "%"),
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

    /// <summary>The value NRLive would run with: what the file says, as GetPrivateProfileInt reads it, clamped
    /// to the setting's range; its default when the file does not say.</summary>
    public int Get(NrLiveSetting s)
    {
        var raw = Engine.GetIni(_text, s.Section, s.Key).Trim();
        var digits = raw.Length > 0 && raw[0] == '-' ? 1 : 0;
        while (digits < raw.Length && char.IsAsciiDigit(raw[digits])) digits++;
        if (!int.TryParse(raw.AsSpan(0, digits), out var value)) return s.Default;
        return s.Kind switch
        {
            NrLiveKind.Toggle => value != 0 ? 1 : 0,
            NrLiveKind.Choice => value >= 0 && value < s.Choices ? value : s.Default,
            _ => Math.Clamp(value, s.Min, s.Max),
        };
    }

    public void Set(NrLiveSetting s, int value) => _text = Engine.SetIni(_text, s.Section, s.Key, value.ToString());

    public void Save()
    {
        var body = _unicode ? [.. Encoding.Unicode.GetPreamble(), .. Encoding.Unicode.GetBytes(_text)] : Encoding.Latin1.GetBytes(_text);
        Engine.MakeParent(_path);
        File.WriteAllBytes(_path, body);
    }

    /// <summary>The pending keys the NRLive.exe at <paramref name="exe"/> reads: those whose section and key are both
    /// in it as the wide strings GetPrivateProfileIntW is called with, each ending in its terminator so "mode"
    /// is not found inside "model". The keys every NRLive reads are not looked for.</summary>
    public static IReadOnlySet<string> Supported(string exe)
    {
        var found = new HashSet<string>(StringComparer.Ordinal);
        byte[] bytes;
        try { bytes = File.Exists(exe) ? File.ReadAllBytes(exe) : []; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return found; }
        bool Has(string literal) => bytes.AsSpan().IndexOf(Encoding.Unicode.GetBytes(literal + "\0")) >= 0;
        foreach (var s in Settings.Where(s => s.Pending))
            if (Has(s.Section) && Has(s.Key)) found.Add(s.Id);
        return found;
    }
}
