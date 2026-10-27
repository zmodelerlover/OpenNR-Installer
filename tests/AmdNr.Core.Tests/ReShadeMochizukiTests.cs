using System.Text;
using AmdNr.Core;

namespace AmdNr.Core.Tests;

/// <summary>The mochizuki runtime on the ReShade routes (add-on v0.6.8 on): the build the newest
/// OptiScaler release carries, installed beside the add-on only when asked for, taken out when it is
/// not, and never a reason to touch amd-nr.ini.</summary>
public class ReShadeMochizukiTests
{
    /// <summary>Payload paths as the archive keeps them, and the name each takes in the game.</summary>
    private static readonly (string Payload, string Game)[] Files =
    [
        ("MochizukiNrRuntime.dll", "MochizukiNrRuntime.dll"),
        ("dlssnr-amd.shaders/g_attn.spv", "dlssnr-amd/shaders/g_attn.spv"),
        ("dlssnr-amd.shaders.runtime/cascade_blur.spv", "dlssnr-amd/shaders/runtime/cascade_blur.spv"),
        ("dlssnr-amd.prewarm/manifest.txt", "dlssnr-amd/prewarm/manifest.txt"),
        ("dlssnr-amd/dlssnr.bin", "dlssnr-amd/dlssnr.bin"),
    ];

    /// <summary>The fixture's add-on payload folder with the mochizuki files beside it, and pins for both.</summary>
    private static (string Src, PayloadPins Pins) Payloads(string tag)
    {
        var (src, pins) = Fixture.Payloads(tag);
        var mochizuki = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (payload, _) in Files)
        {
            var full = Path.Combine(src, payload);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, $"stand-in {payload}");
            mochizuki[payload] = Engine.Sha(Encoding.UTF8.GetBytes($"stand-in {payload}"));
        }
        return (src, new PayloadPins
        {
            AddonSha = pins.AddonSha, AddonSize = pins.AddonSize,
            RuntimeSha = pins.RuntimeSha, RuntimeSize = pins.RuntimeSize,
            WeightsSha = pins.WeightsSha, WeightsSize = pins.WeightsSize,
            MochizukiFiles = mochizuki,
        });
    }

    [Fact]
    public void TheReShadeRoutesTakeTheMochizukiBuildTheNewestOptiScalerReleaseCarries()
    {
        var payload = MochizukiTests.Payload();
        // The payload lists it only inside OptiScaler's releases, so an add-on install has none of its own.
        Assert.Empty(payload.Pins().MochizukiFiles);

        var withIt = payload.WithMochizuki();
        var fromOpti = payload.Newest(PayloadManifest.OptiScalerComponent);
        Assert.Equal(fromOpti.Pins().MochizukiFiles.OrderBy(p => p.Key, StringComparer.Ordinal),
            withIt.Pins().MochizukiFiles.OrderBy(p => p.Key, StringComparer.Ordinal));
        // Nothing else moves: the add-on and the runtime are still the manifest's own.
        Assert.Equal(payload.Pins().AddonSha, withIt.Pins().AddonSha);
        Assert.Equal(payload.Pins().RuntimeSha, withIt.Pins().RuntimeSha);

        // A release that carries the runtime but no model, or a payload with no such release, adds nothing.
        Assert.Empty(MochizukiTests.Payload(model: false).WithMochizuki().Pins().MochizukiFiles);
        Assert.Empty(MochizukiTests.Payload(placeholder: true).WithMochizuki().Pins().MochizukiFiles);
    }

    /// <summary>The sheet installs a version picked from the add-on's GitHub releases through
    /// AddonReleases.With, and v0.6.8 is always one of them. That manifest lost the OptiScaler releases
    /// the mochizuki build comes from, so installer v0.6.2 never showed the box on a ReShade route.</summary>
    [Fact]
    public void AnAddOnPickedFromItsReleasesStillCarriesTheMochizukiBuild()
    {
        var payload = MochizukiTests.Payload();
        var files = new[] { Work.AddonName, Work.Addon32Name, Work.Host64Name, AddonReleases.BridgeSums };
        var release = new AddonRelease
        {
            Version = new Version(0, 6, 8),
            Tag = "v0.6.8",
            Title = "v0.6.8",
            Published = DateTimeOffset.Parse("2026-09-26T00:00:00Z"),
            Assets = files.ToDictionary(f => f, f => new ReleaseAsset(f, $"https://example.invalid/{f}", 1024),
                StringComparer.OrdinalIgnoreCase),
            Sums = files.ToDictionary(f => f, _ => new string('a', 64), StringComparer.OrdinalIgnoreCase),
        };

        var chosen = AddonReleases.With(payload, release).WithMochizuki();
        Assert.Equal("0.6.8", chosen.Component(PayloadManifest.AddonComponent).Version);
        Assert.Equal(payload.WithMochizuki().Pins().MochizukiFiles.OrderBy(p => p.Key, StringComparer.Ordinal),
            chosen.Pins().MochizukiFiles.OrderBy(p => p.Key, StringComparer.Ordinal));
        Assert.NotEmpty(chosen.Pins().MochizukiFiles);
    }

    [Fact]
    public void TheShippedManifestPinsAnAddOnThatDrivesMochizukiAndOffersItsBuild()
    {
        var shipped = PayloadManifest.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "payload.json")));
        foreach (var own in new[] { PayloadManifest.AddonComponent, PayloadManifest.BridgeComponent })
            Assert.True(AddonReleases.Version(shipped.Component(own).Version) >= new Version(0, 6, 8), own);
        var pins = shipped.WithMochizuki().Pins();
        // The RX 7000 network is the OptiScaler route's opt-in only; the add-on takes the rest of the newest build.
        var newest = shipped.Newest(PayloadManifest.OptiScalerComponent).Without(PayloadManifest.MochizukiRdna3Component).Pins();
        Assert.NotEmpty(pins.MochizukiFiles);
        Assert.Equal(newest.MochizukiFiles.OrderBy(p => p.Key, StringComparer.Ordinal),
            pins.MochizukiFiles.OrderBy(p => p.Key, StringComparer.Ordinal));
        foreach (var path in pins.MochizukiFiles.Keys)
            Assert.True(Engine.IsAllowed(Work.MochizukiDestination(path)), path);
    }

    [Fact]
    public void TickedItGoesInBesideTheAddOnUntickedItComesOutAndTheIniIsNeverTouched()
    {
        var game = Fixture.Temp("rs-mochizuki");
        var (src, pins) = Payloads("rs-mochizuki");
        const string ini = "[amd-nr]\r\nNrBackend=mochizuki\r\nScale=0.7\r\n";
        File.WriteAllText(Path.Combine(game, "amd-nr.ini"), ini);

        var first = Work.Install(game, src, Preset.Dx11, pins, mochizuki: true);
        Assert.False(first.Failed, first.ToLog("with mochizuki"));
        foreach (var (payload, name) in Files)
            Assert.Equal($"stand-in {payload}", File.ReadAllText(Path.Combine(game, name)));
        Assert.True(Fixture.HasAny(first, "NR runtime"), first.ToLog("says where to pick it"));
        Assert.True(Work.HasMochizuki(game));
        Assert.Equal(ini, File.ReadAllText(Path.Combine(game, "amd-nr.ini")));

        // What the runtime writes beside itself while a game runs.
        File.WriteAllText(Path.Combine(game, "dlssnr-amd", "pipeline.cache"), "cache");

        var second = Work.Install(game, src, Preset.Dx11, pins, mochizuki: false);
        Assert.False(second.Failed, second.ToLog("without mochizuki"));
        Assert.False(File.Exists(Path.Combine(game, "MochizukiNrRuntime.dll")));
        Assert.False(Directory.Exists(Path.Combine(game, "dlssnr-amd")));
        Assert.True(File.Exists(Path.Combine(game, Work.AddonName)));
        Assert.False(Work.HasMochizuki(game));
        // The add-on falls back to danielblnc when the ini names mochizuki and its files are gone.
        Assert.Equal(ini, File.ReadAllText(Path.Combine(game, "amd-nr.ini")));
    }

    [Fact]
    public void UninstallTakesMochizukiAndWhatItWroteWithTheAddOn()
    {
        var game = Fixture.Temp("rs-mochizuki-uninstall");
        var (src, pins) = Payloads("rs-mochizuki-uninstall");
        var report = Work.Install(game, src, Preset.Dx11, pins, mochizuki: true);
        Assert.False(report.Failed, report.ToLog("install"));
        File.WriteAllText(Path.Combine(game, "dlssnr-amd", "pipeline.cache"), "cache");
        File.WriteAllText(Path.Combine(game, "mochizuki_nr.log"), "log");

        var removed = Work.Uninstall(game, Preset.Dx11);
        Assert.False(removed.Failed, removed.ToLog("uninstall"));
        Assert.False(File.Exists(Path.Combine(game, "MochizukiNrRuntime.dll")));
        Assert.False(Directory.Exists(Path.Combine(game, "dlssnr-amd")));
        Assert.False(File.Exists(Path.Combine(game, "mochizuki_nr.log")));
        Assert.False(GameScanner.IsInstalled(game));
    }

    [Fact]
    public void AskedForWithoutABuildInThePayloadNothingIsWritten()
    {
        var game = Fixture.Temp("rs-mochizuki-none");
        var (src, pins) = Fixture.Payloads("rs-mochizuki-none");
        var report = Work.Install(game, src, Preset.Dx11, pins, mochizuki: true);
        Assert.True(report.Failed);
        Assert.True(Fixture.HasErr(report, "no mochizuki runtime"), report.ToLog("none"));
        Assert.False(File.Exists(Path.Combine(game, Work.AddonName)));
    }

    [Fact]
    public void TheBridgeInstallerWritesTheExtraFilesAndRetiresThemLater()
    {
        var game = Fixture.Temp("x86-mochizuki");
        var exe = Path.Combine(game, "game.exe");
        File.WriteAllBytes(exe, Fixture.Pe(false));
        var app = UninstallInvariantTests.X86Release("mochizuki").Installer;
        var extra = new Dictionary<string, byte[]>
        {
            ["MochizukiNrRuntime.dll"] = "runtime"u8.ToArray(),
            ["dlssnr-amd/dlssnr.bin"] = "model"u8.ToArray(),
        };

        app.Install(exe, "D3D9", extra: extra);
        Assert.Equal("model", File.ReadAllText(Path.Combine(game, "dlssnr-amd", "dlssnr.bin")));
        Assert.True(Work.HasMochizuki(game));

        new X86Installer(app.Release)
        {
            RuntimeSha = app.RuntimeSha, WeightsSha = app.WeightsSha, ReShadeSha = app.ReShadeSha, D3d8To9Sha = app.D3d8To9Sha,
        }.Install(exe, "D3D9", retire: extra.Keys);
        Assert.False(File.Exists(Path.Combine(game, "MochizukiNrRuntime.dll")));
        Assert.False(File.Exists(Path.Combine(game, "dlssnr-amd", "dlssnr.bin")));
        Assert.True(File.Exists(Path.Combine(game, "amd-nr.addon32")));
    }
}
