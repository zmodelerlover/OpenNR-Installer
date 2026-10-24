using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using AmdNr.Core;

namespace AmdNr.Core.Tests;

/// <summary>NRLive's package, its scaleconfig.ini and its launch profile. One class, so the tests that share
/// NrLivePackage.Folder (one per test run, under AMDNR_HOME) run one after another.</summary>
public class NrLiveTests
{
    private static string Sha(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    /// <summary>A payload list with NRLive under releases: its zip, pinned or a placeholder, and the zip itself in
    /// the cache where the download would have put it.</summary>
    private static (PayloadManifest Manifest, string Dir) Payload(string version, bool placeholder, params (string Path, string Text)[] entries)
    {
        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
            foreach (var (path, text) in entries)
            {
                using var w = new StreamWriter(zip.CreateEntry(path).Open());
                w.Write(text);
            }
        var bytes = buffer.ToArray();
        var dir = Fixture.Temp("nrlive-zip");
        var cached = Path.Combine(dir, "NRLiveUI.zip");
        Directory.CreateDirectory(Path.GetDirectoryName(cached)!);
        File.WriteAllBytes(cached, bytes);
        var sha = placeholder ? PayloadManifest.PlaceholderSha : Sha(bytes);
        var addon = new string('a', 64);
        var json = """{"schema":1,"components":{"addon":{"version":"1","files":[{"name":"amd-nr.addon64","size":1,"sha256":""" + $"\"{addon}\""
                   + """}]}},"releases":{"nrlive":[{"version":""" + $"\"{version}\"" + ""","components":{"nrlive":{"version":""" + $"\"{version}\""
                   + ""","files":[{"name":"NRLiveUI.zip","url":"https://127.0.0.1:1/NRLiveUI.zip","size":""" + $"{bytes.Length},\"sha256\":\"{sha}\""
                   + "}]}}}]}}";
        return (PayloadManifest.Parse(json), dir);
    }

    /// <summary>What InstallAsync does once the cache has the files, from a folder of the test's own: the cache is
    /// shared, and PayloadTests empties it while these run.</summary>
    private static void Unpack(PayloadManifest manifest, string dir)
    {
        var release = manifest.NrLive()!;
        NrLivePackage.Unpack(manifest.With(release), release, [(PayloadManifest.NrLiveComponent, dir)]);
    }

    [Fact]
    public void TheShippedPayloadListsNrLiveButDoesNotOfferItsPlaceholder()
    {
        var shipped = PayloadManifest.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "payload.json")));
        Assert.True(shipped.Releases!.ContainsKey(PayloadManifest.NrLiveComponent));
        if (shipped.Releases[PayloadManifest.NrLiveComponent].All(PayloadManifest.IsPlaceholder)) Assert.Null(shipped.NrLive());
        Assert.DoesNotContain(shipped.Everyday, c => c.Key == PayloadManifest.NrLiveComponent);
    }

    [Fact]
    public void APlaceholderIsNotOfferedAndAPinnedReleaseIs()
    {
        Assert.Null(Payload("9.0.0", placeholder: true, ("NRLive.exe", "x")).Manifest.NrLive());
        Assert.Equal("9.0.1", Payload("9.0.1", placeholder: false, ("NRLive.exe", "x")).Manifest.NrLive()?.Version);
    }

    [Fact]
    public void UnpacksThePackageAndKeepsTheSettingsNrLiveWrote()
    {
        var folder = NrLivePackage.Folder;
        File.WriteAllText(Path.Combine(folder, NrLiveConfig.FileName), "[FSR]\r\nsharpness=10\r\n");
        var (manifest, manifestDir) = Payload("9.1.0", false, ("NRLive.exe", "v1"), ("version.txt", "9.1.0"),
            ("dlssnr-amd/shaders/a.spv", "spv"), ("scaleconfig.ini", "[FSR]\r\nsharpness=65\r\n"), ("profiles/Default.json", "{}"));
        var release = manifest.NrLive()!;
        Assert.False(NrLivePackage.IsCurrent(release));

        Unpack(manifest, manifestDir);
        Assert.Equal("v1", File.ReadAllText(NrLivePackage.Exe));
        Assert.Equal("spv", File.ReadAllText(Path.Combine(folder, "dlssnr-amd", "shaders", "a.spv")));
        Assert.Contains("sharpness=10", File.ReadAllText(Path.Combine(folder, NrLiveConfig.FileName)));
        Assert.Equal("{}", File.ReadAllText(Path.Combine(folder, "profiles", "Default.json")));
        Assert.Equal("9.1.0", NrLivePackage.InstalledVersion());
        Assert.True(NrLivePackage.IsCurrent(release));

        // A zip with one folder around everything is read as that folder; one that climbs out is refused.
        var (wrapped, wrappedDir) = Payload("9.2.0", false, ("NRLive-9.2.0/NRLive.exe", "v2"), ("NRLive-9.2.0/version.txt", "9.2.0"));
        Unpack(wrapped, wrappedDir);
        Assert.Equal("v2", File.ReadAllText(NrLivePackage.Exe));
        Assert.False(NrLivePackage.IsCurrent(release));
        var (climbing, climbingDir) = Payload("9.3.0", false, ("../outside.txt", "no"), ("NRLive.exe", "v3"));
        Assert.Throws<InstallException>(() => Unpack(climbing, climbingDir));
        Assert.False(File.Exists(Path.Combine(AppPaths.Root, "outside.txt")));
        Assert.Null(NrLivePackage.InstalledVersion());
    }

    [Fact]
    public void ConfigReadsDefaultsClampsAndKeepsEveryOtherByte()
    {
        var dir = Fixture.Temp("nrlive-ini");
        var path = Path.Combine(dir, NrLiveConfig.FileName);
        var setting = (string id) => NrLiveConfig.Settings.First(s => s.Id == id);
        // A screenshot path in the ANSI code page, which is how Windows writes it.
        File.WriteAllBytes(path, [.. Encoding.Latin1.GetBytes("[General]\r\nscreenshot_path=C:\\Bilder\\"), 0xE9, .. "\r\n[DLSSNR]\r\nscale=900\r\npasses=2 ; two\r\n"u8]);
        var config = NrLiveConfig.Load(dir);
        Assert.Equal(100, config.Get(setting("DLSSNR.scale")));
        Assert.Equal(2, config.Get(setting("DLSSNR.passes")));
        Assert.Equal(65, config.Get(setting("FSR.sharpness")));
        config.Set(setting("DLSSNR.enabled"), 1);
        config.Set(setting("FSR.sharpness"), 40);
        config.Save();

        var bytes = File.ReadAllBytes(path);
        Assert.Contains((byte)0xE9, bytes);
        var again = NrLiveConfig.Load(dir);
        Assert.Equal(1, again.Get(setting("DLSSNR.enabled")));
        Assert.Equal(40, again.Get(setting("FSR.sharpness")));

        // A file NRLive wrote as UTF-16 stays UTF-16.
        File.WriteAllBytes(path, [.. Encoding.Unicode.GetPreamble(), .. Encoding.Unicode.GetBytes("[FrameGeneration]\r\nenabled=1\r\n")]);
        var wide = NrLiveConfig.Load(dir);
        Assert.Equal(1, wide.Get(setting("FrameGeneration.enabled")));
        wide.Save();
        Assert.Equal([0xFF, 0xFE], File.ReadAllBytes(path)[..2]);
    }

    [Fact]
    public void APendingSettingIsSupportedOnlyWhenNrLiveReadsIt()
    {
        var dir = Fixture.Temp("nrlive-exe");
        var exe = Path.Combine(dir, "NRLive.exe");
        File.WriteAllBytes(exe, [.. "MZ"u8, .. Encoding.Unicode.GetBytes("DLSSNR\0model\0FSR\0")]);
        Assert.Empty(NrLiveConfig.Supported(exe));
        File.WriteAllBytes(exe, [.. "MZ"u8, .. Encoding.Unicode.GetBytes("Upscaler\0mode\0RenoDX\0enabled\0")]);
        Assert.Equal(["RenoDX.enabled", "Upscaler.mode"], NrLiveConfig.Supported(exe).Order());
    }

    [Fact]
    public void TheProfileIsNrLivesOwnAndBuildsItsCommandLine()
    {
        var dir = Fixture.Temp("nrlive-profile");
        Directory.CreateDirectory(Path.Combine(dir, "profiles"));
        // NRLive's launcher's own Default.json.
        File.WriteAllText(Path.Combine(dir, "profiles", "Default.json"),
            """{ "target_mode": "picker", "delay": 7, "no_overlay": false, "motion": "amdof", "scale_hotkey": "ctrl+alt+s", "stop_key": "ctrl+shift+a", "overlay_key": "ctrl+home", "exe_path": "" }""");
        var profile = NrLiveProfile.Load(dir);
        Assert.Equal(7, profile.Delay);
        Assert.Equal("amdof", profile.Motion);
        Assert.Equal(["-picker", "7", "--mv", "amdof", "--capture", "wgc", "--key", "ctrl+shift+a", "--overlaykey", "ctrl+home",
            "--bindbypass", "home,insert,end,pageup,pagedown"], profile.Arguments());

        profile.TargetMode = "pname";
        profile.TargetText = NrLiveProfile.ExactProcess("Game (x64).exe");
        profile.NoOverlay = true;
        Assert.Equal(@"^Game \(x64\)\.exe$", profile.TargetText);
        Assert.Equal(["-pname", profile.TargetText, "-delay", "7", "-nooverlay"], profile.Arguments().Take(5));
        profile.Save(dir);
        Assert.Contains("\"target_mode\": \"pname\"", File.ReadAllText(Path.Combine(dir, "profiles", "Default.json")));
        Assert.Null(profile.Problem());

        profile.StopKey = "alt+CTRL+s";
        Assert.Equal(NrLiveProblem.HotkeyConflict, profile.Problem());
        profile.StopKey = "ctrl+shift+a+b";
        Assert.Equal(NrLiveProblem.HotkeyInvalid, profile.Problem());
        profile.StopKey = "ctrl+shift+a";
        profile.BindBypass = "home,nonsense";
        Assert.Equal(NrLiveProblem.BypassInvalid, profile.Problem());
        profile.BindBypass = "home";
        profile.TargetMode = "pid";
        profile.TargetText = "";
        Assert.Equal(NrLiveProblem.NoTarget, profile.Problem());

        Assert.True(NrLiveHotkey.TryParse("ctrl+alt+F12", out var mods, out var vk));
        Assert.Equal((NrLiveHotkey.Control | NrLiveHotkey.Alt, 0x7Bu), (mods, vk));
    }
}
