// What NRLive.exe is started with: the window to capture, how, and its hotkeys. Kept in profiles\Default.json in
// the shape NRLive's own launcher writes, so a person who opens that launcher too finds the same setup there.

using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AmdNr.Core;

public sealed class NrLiveProfile
{
    /// <summary>"picker" (the window in front when the countdown ends), "pid", "pname", "window" or "front".</summary>
    public string TargetMode { get; set; } = "picker";
    public string TargetText { get; set; } = "";

    /// <summary>The countdown, in seconds, to switch to the game in.</summary>
    public int Delay { get; set; } = 5;

    /// <summary>No overlay at all: neither NRLive's bar nor its frame counter.</summary>
    public bool NoOverlay { get; set; }

    /// <summary>"fast" (FastMV) or "amdof" (AMD's optical flow).</summary>
    public string Motion { get; set; } = "fast";

    /// <summary>"wgc" (Windows Graphics Capture) or "dxgi" (desktop duplication, cropped to the window).</summary>
    public string CaptureMode { get; set; } = "wgc";

    /// <summary>"fsr4", "fsr31" or "none" (native scaling), NRLive's --upscaler. Its launcher keeps it here too.</summary>
    public string Upscaler { get; set; } = "fsr4";

    /// <summary>1 tells NRLive's launcher this profile needs no migration.</summary>
    public int? SchemaVersion { get; set; } = 1;

    /// <summary>Started elevated, which a game that runs elevated needs for its input to reach it -- as most games
    /// with anti-cheat do. NRLive's launcher has it on, and so does this.</summary>
    public bool RunAsAdmin { get; set; } = true;

    public string ScaleHotkey { get; set; } = "ctrl+alt+s";
    public string StopKey { get; set; } = "ctrl+shift+a";
    public string OverlayKey { get; set; } = "ctrl+home";

    /// <summary>Keys NRLive hands to OptiScaler or ReShade in the game rather than keeping.</summary>
    public string BindBypass { get; set; } = "home,insert,end,pageup,pagedown";

    /// <summary>NRLive's launcher's own: which NRLive.exe it starts. Kept as read; this app starts its own copy.</summary>
    public string ExePath { get; set; } = "";

    private static string PathIn(string folder) => System.IO.Path.Combine(folder, "profiles", "Default.json");

    public static NrLiveProfile Load(string folder)
    {
        try
        {
            var path = PathIn(folder);
            return File.Exists(path)
                ? JsonSerializer.Deserialize(File.ReadAllText(path), NrLiveJson.Default.NrLiveProfile) ?? new NrLiveProfile()
                : new NrLiveProfile();
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException)
        {
            return new NrLiveProfile();
        }
    }

    public void Save(string folder)
    {
        var path = PathIn(folder);
        Engine.MakeParent(path);
        File.WriteAllText(path, JsonSerializer.Serialize(this, NrLiveJson.Default.NrLiveProfile));
    }

    /// <summary>What is wrong with this profile for a launch, or null: the same checks NRLive's launcher makes
    /// before it starts anything, because NRLive itself exits on the first argument it cannot read.</summary>
    public NrLiveProblem? Problem()
    {
        if (NrLiveHotkey.Same(ScaleHotkey, StopKey) || NrLiveHotkey.Same(ScaleHotkey, OverlayKey) || NrLiveHotkey.Same(StopKey, OverlayKey))
            return NrLiveProblem.HotkeyConflict;
        if (!NrLiveHotkey.TryParse(ScaleHotkey, out _, out _) || !NrLiveHotkey.TryParse(StopKey, out _, out _)
            || !NrLiveHotkey.TryParse(OverlayKey, out _, out _))
            return NrLiveProblem.HotkeyInvalid;
        var bypass = BindBypass.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (bypass.Length == 0 || bypass.Any(k => !NrLiveHotkey.TryParse(k, out _, out _))) return NrLiveProblem.BypassInvalid;
        if (TargetMode == "pid" && !uint.TryParse(TargetText, out _)) return NrLiveProblem.NoTarget;
        if (TargetMode is "pname" or "window" && string.IsNullOrWhiteSpace(TargetText)) return NrLiveProblem.NoTarget;
        return null;
    }

    /// <summary>NRLive.exe's command line, as NRLive's launcher builds it (target.cpp, parseTargetArgs).
    /// --upscaler only for an NRLive that reads it: one that does not exits on it. <paramref name="pid"/> is the
    /// window already picked when the countdown ran here (NrLivePage), taken at once: 0 when no process was in front.</summary>
    public IReadOnlyList<string> Arguments(bool upscaler = false, uint? pid = null)
    {
        var delay = Math.Clamp(Delay, 1, 30).ToString();
        List<string> args = pid is { } p ? (p != 0 ? ["-pid", p.ToString(), "-delay", "0"] : ["-front", "-delay", "0"]) : TargetMode switch
        {
            "front" => ["-front", "-delay", delay],
            "pid" => ["-pid", TargetText, "-delay", delay],
            "pname" => ["-pname", TargetText, "-delay", delay],
            "window" => ["-window", TargetText, "-delay", delay],
            _ => ["-picker", delay],
        };
        if (NoOverlay) args.Add("-nooverlay");
        args.AddRange(["--mv", Motion, "--capture", CaptureMode, "--key", StopKey, "--overlaykey", OverlayKey, "--bindbypass", BindBypass]);
        if (upscaler) args.AddRange(["--upscaler", NrLiveConfig.Upscalers.Contains(Upscaler) ? Upscaler : NrLiveConfig.Upscalers[0]]);
        return args;
    }

    /// <summary>A -pname pattern that matches one executable's name and nothing else. NRLive reads it as an
    /// ECMAScript regex and searches with it, so "Game.exe" alone would also match "MyGame.exe" and "Game_exe".</summary>
    public static string ExactProcess(string exeName)
    {
        var o = new StringBuilder("^");
        foreach (var c in exeName)
        {
            if (@"\^$.|?*+()[]{}/".Contains(c)) o.Append('\\');
            o.Append(c);
        }
        return o.Append('$').ToString();
    }
}

public enum NrLiveProblem { HotkeyConflict, HotkeyInvalid, BypassInvalid, NoTarget }

/// <summary>Hotkeys as NRLive writes them: "ctrl+alt+s", modifiers and one key.</summary>
public static class NrLiveHotkey
{
    public const uint Alt = 0x1, Control = 0x2, Shift = 0x4, Win = 0x8;

    /// <summary>The RegisterHotKey modifiers and virtual key of a hotkey, as NRLive's launcher reads one.</summary>
    public static bool TryParse(string text, out uint modifiers, out uint vk)
    {
        modifiers = 0;
        vk = 0;
        var key = "";
        foreach (var raw in text.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            switch (raw.ToLowerInvariant())
            {
                case "ctrl" or "control": modifiers |= Control; break;
                case "alt": modifiers |= Alt; break;
                case "shift": modifiers |= Shift; break;
                case "win" or "windows": modifiers |= Win; break;
                default:
                    if (key.Length != 0) return false;
                    key = raw.ToLowerInvariant();
                    break;
            }
        }
        if (key.Length == 1 && char.IsAsciiLetterOrDigit(key[0])) { vk = char.ToUpperInvariant(key[0]); return true; }
        if (key.Length >= 2 && key[0] == 'f' && int.TryParse(key[1..], out var f) && f is >= 1 and <= 24) { vk = (uint)(0x70 + f - 1); return true; }
        vk = key switch
        {
            "home" => 0x24, "end" => 0x23, "insert" => 0x2D, "delete" or "del" => 0x2E,
            "space" or "spacebar" => 0x20, "tab" => 0x09, "enter" or "return" => 0x0D, "esc" or "escape" => 0x1B,
            "pageup" => 0x21, "pagedown" => 0x22, "up" => 0x26, "down" => 0x28, "left" => 0x25, "right" => 0x27,
            "pause" => 0x13, "printscreen" => 0x2C,
            _ => 0u,
        };
        return vk != 0;
    }

    /// <summary>The same combination, whatever the order and case it was written in.</summary>
    public static bool Same(string a, string b) => Normalize(a) == Normalize(b);

    private static string Normalize(string s) => string.Join("+",
        s.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Select(x => x.ToLowerInvariant()).Order());
}

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNameCaseInsensitive = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
[JsonSerializable(typeof(NrLiveProfile))]
[JsonSerializable(typeof(NrLiveProfileCopy))]
internal sealed partial class NrLiveJson : JsonSerializerContext;
