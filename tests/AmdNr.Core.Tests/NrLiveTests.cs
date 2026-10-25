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
    public void TheShippedPayloadNeverOffersAnNrLivePlaceholder()
    {
        // While NRLive is coming soon the shipped list leaves it out; once pinned it lists a real build.
        var shipped = PayloadManifest.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "payload.json")));
        if (shipped.Releases!.TryGetValue(PayloadManifest.NrLiveComponent, out var nrlive) && nrlive.All(PayloadManifest.IsPlaceholder))
            Assert.Null(shipped.NrLive());
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
        // NRLive 2.2.0 reads the old keys only.
        File.WriteAllBytes(exe, [.. "MZ"u8, .. Encoding.Unicode.GetBytes("DLSSNR\0model\0FPS\0position\0background_alpha\0")]);
        var old = NrLiveConfig.Supported(exe);
        Assert.DoesNotContain("DLSSNR.runtime", old);
        Assert.DoesNotContain(NrLiveConfig.UpscalerId, old);
        var config = NrLiveConfig.Load(dir);
        Assert.Equal([NrLiveConfig.Bridge, NrLiveConfig.OldHud], config.Tags(dir, old).Order());
        var shown = NrLiveConfig.Settings.Where(s => NrLiveConfig.Shown(s, old, config.Tags(dir, old))).ToList();
        Assert.Single(shown, s => s.Id == "FPS.position");
        Assert.Equal(4, shown.Single(s => s.Id == "FPS.position").Choices);
        Assert.DoesNotContain(shown, s => s.Section == "OpenNR");

        // The stacked NRLive: [DLSSNR] runtime and the HUD keys wide, the [OpenNR] keys narrow, --upscaler.
        File.WriteAllBytes(exe, [.. "MZ"u8, .. Encoding.Unicode.GetBytes("DLSSNR\0runtime\0OpenNR\0FPS\0preset\0position\0lows\0--upscaler\0"),
            .. "MochizukiRenoIntensity\0"u8]);
        var stack = NrLiveConfig.Supported(exe);
        Assert.Contains("DLSSNR.runtime", stack);
        Assert.Contains("OpenNR.MochizukiRenoIntensity", stack);
        Assert.Contains("FPS.lows", stack);
        Assert.Contains(NrLiveConfig.UpscalerId, stack);
        Assert.DoesNotContain("OpenNR.MochizukiRenoStyle", stack);
        // auto is the bridge until OpenNR's runtime is in the folder.
        Assert.Contains(NrLiveConfig.Bridge, config.Tags(dir, stack));
        Directory.CreateDirectory(Path.Combine(dir, "opennr"));
        File.WriteAllText(Path.Combine(dir, "opennr", "MochizukiNrRuntime.dll"), "x");
        Assert.Equal([NrLiveConfig.Hud, NrLiveConfig.OpenNr], config.Tags(dir, stack).Order());
        config.Set(NrLiveConfig.Settings.First(s => s.Id == "DLSSNR.runtime"), 2);
        Assert.Contains(NrLiveConfig.Bridge, config.Tags(dir, stack));
    }

    [Fact]
    public void OpenNrDecimalsWordsAndHudPresetsAreWrittenAsNrLiveReadsThem()
    {
        var dir = Fixture.Temp("nrlive-opennr");
        var setting = (string id) => NrLiveConfig.Settings.Last(s => s.Id == id);
        File.WriteAllText(Path.Combine(dir, NrLiveConfig.FileName),
            "[DLSSNR]\r\nruntime=OpenNR\r\n[OpenNR]\r\nMochizukiRenoIntensity=1.25\r\nMochizukiAco=false\r\nMochizukiRenoSkinStructure=-1\r\n");
        var config = NrLiveConfig.Load(dir);
        Assert.Equal(1, config.Get(setting("DLSSNR.runtime")));
        Assert.Equal(125, config.Get(setting("OpenNR.MochizukiRenoIntensity")));
        Assert.Equal(0, config.Get(setting("OpenNR.MochizukiAco")));
        Assert.Equal(1, config.Get(setting("OpenNR.MochizukiTemporal")));
        Assert.Equal(-100, config.Get(setting("OpenNR.MochizukiRenoSkinStructure")));
        config.Set(setting("OpenNR.MochizukiRenoIntensity"), 85);
        config.Set(setting("DLSSNR.runtime"), 0);
        config.Set(setting("FPS.preset"), 4);
        config.Save();
        var text = File.ReadAllText(Path.Combine(dir, NrLiveConfig.FileName));
        Assert.Contains("MochizukiRenoIntensity=0.85", text);
        Assert.Contains("runtime=auto", text);
        Assert.Contains("gpu_times=1", text);
        Assert.Contains("layout=0", text);
        // An item changed by hand makes the HUD custom.
        config.Set(setting("FPS.gpu_times"), 0);
        Assert.Equal(0, config.Get(setting("FPS.preset")));
        config.Set(setting("FPS.preset"), 2);
        Assert.Equal(0, config.Get(setting("FPS.gpu_times")));
        Assert.Equal(1, config.Get(setting("FPS.pipeline")));
    }

    [Fact]
    public void TheModelsAReleaseCarriesAreLinkedIntoOpenNrsFolder()
    {
        var (manifest, dir) = Payload("9.4.0", false, ("NRLive.exe", "v4"), ("opennr/dlssnr-amd/shaders/a.spv", "spv"));
        var zip = manifest.NrLive()!.Components[PayloadManifest.NrLiveComponent].Files[0];
        File.WriteAllText(Path.Combine(dir, "dlssnr-small.bin"), "small");
        Directory.CreateDirectory(Path.Combine(dir, "dlssnr-amd"));
        File.Move(Path.Combine(dir, "dlssnr-small.bin"), Path.Combine(dir, "dlssnr-amd", "dlssnr-small.bin"));
        var addon = new string('a', 64);
        var json = ($"{{'schema':1,'components':{{'addon':{{'version':'1','files':[{{'name':'amd-nr.addon64','size':1,'sha256':'{addon}'}}]}}}},"
            + $"'releases':{{'nrlive':[{{'version':'9.4.0','components':{{'nrlive':{{'version':'9.4.0','files':[{{'name':'NRLiveUI.zip',"
            + $"'url':'https://127.0.0.1:1/NRLiveUI.zip','size':{zip.Size},'sha256':'{zip.Sha256}'}}]}},"
            + $"'mochizuki-model-small':{{'version':'1','files':[{{'name':'dlssnr-small.bin','path':'dlssnr-amd/dlssnr-small.bin',"
            + $"'size':5,'sha256':'{Sha("small"u8.ToArray())}'}}]}}}}}}]}}}}").Replace('\'', '"');
        var withModel = PayloadManifest.Parse(json);
        var release = withModel.NrLive()!;
        NrLivePackage.Unpack(withModel.With(release), release, [(PayloadManifest.NrLiveComponent, dir), ("mochizuki-model-small", dir)]);
        Assert.Equal("small", File.ReadAllText(Path.Combine(NrLivePackage.Folder, "opennr", "dlssnr-amd", "dlssnr-small.bin")));
        Assert.Equal("spv", File.ReadAllText(Path.Combine(NrLivePackage.Folder, "opennr", "dlssnr-amd", "shaders", "a.spv")));
        Assert.False(File.Exists(Path.Combine(NrLivePackage.Folder, "dlssnr-amd", "dlssnr-small.bin")));
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
        Assert.DoesNotContain("--upscaler", profile.Arguments());
        profile.Upscaler = "fsr31";
        Assert.Equal(["--upscaler", "fsr31"], profile.Arguments(upscaler: true).TakeLast(2));
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
