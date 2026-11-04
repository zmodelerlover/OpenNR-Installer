using System.Text;
using AmdNr.Core;

namespace AmdNr.Core.Tests;

/// <summary>REFramework beside either route: the RE Engine list in api-db.json, its builds in payload.json, its own
/// record, another mod's dinput8.dll left alone, another REFramework backed up and put back, and reframework\ taken
/// only while it is empty and this install found it missing.</summary>
public class ReFrameworkTests
{
    private const string Mark = "REFramework: Unable to load the original dinput8.dll";

    private static (string Dir, PayloadPins Pins) Payloads(string tag, string build = "stand-in build")
    {
        var (dir, pins) = OptiScalerRouteTests.Payloads(tag, "0.5.9-opennr");
        var files = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (path, text) in new[] { ("reframework/dinput8.dll", $"{build} {Mark}"), ("reframework/LICENSE.txt", "MIT License praydog") })
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            Directory.CreateDirectory(Path.Combine(dir, "reframework"));
            File.WriteAllBytes(Path.Combine(dir, path), bytes);
            files[path] = Engine.Sha(bytes);
        }
        pins.ReFrameworkFiles = files;
        pins.ReFrameworkBuild = "reframework-re9";
        pins.ReFrameworkVersion = "1.5.9.1437-nightly";
        pins.ReFramework = true;
        return (dir, pins);
    }

    private static string Game(string tag)
    {
        var game = Fixture.Temp(tag);
        File.WriteAllBytes(Path.Combine(game, "re9.exe"), Fixture.PeWithImports(true, ["d3d12.dll"]));
        return game;
    }

    private static string Dll(string game) => File.ReadAllText(Path.Combine(game, Work.ReFrameworkDll));

    [Fact]
    public void TheShippedListNamesABuildPerGameAndWarnsWhereItPlaysOnline()
    {
        var db = ApiDatabase.Parse(PayloadCache.Embedded("api-db.json")!);
        Assert.True(db.ReFramework.Count >= 15);
        Assert.All(db.ReFramework, g => Assert.False(string.IsNullOrWhiteSpace(g.Build), g.Title));
        Assert.Equal(db.ReFramework.SelectMany(g => g.SteamAppIds).Count(), db.ReFramework.SelectMany(g => g.SteamAppIds).Distinct().Count());
        Assert.Equal(db.ReFramework.Count, db.ReFramework.Select(g => g.Build).Distinct(StringComparer.OrdinalIgnoreCase).Count());

        var re9 = db.ReFrameworkGameFor("3764200", null);
        Assert.Equal("Resident Evil Requiem", re9?.Title);
        Assert.Equal("reframework-re9", re9!.Component);
        Assert.False(re9.Risky);
        Assert.Same(re9, db.ReFrameworkGameFor(null, @"E:\Steam\RESIDENT EVIL requiem BIOHAZARD requiem\RE9.EXE"));
        Assert.True(db.ReFrameworkGameFor("2246340", null)!.Risky);
        Assert.True(db.ReFrameworkGameFor(null, "StreetFighter6.exe")!.Risky);
        Assert.Null(db.ReFrameworkGameFor("1091500", "Cyberpunk2077.exe", "Cyberpunk 2077"));
        Assert.DoesNotContain(db.ReFramework, g => g.Needed);

        Assert.Equal(db.ReFramework.Count, ApiDatabase.Parse(db.Serialise()).ReFramework.Count);
        Assert.Empty(ApiDatabase.Parse("{\"schema\":1,\"games\":{}}").ReFramework);
    }

    private const string PayloadJson = """
        {"schema":1,"components":{"addon":{"version":"0.7.15","files":[{"name":"amd-nr.addon64","size":1,"sha256":"1111111111111111111111111111111111111111111111111111111111111111","url":"https://x/a"}]},
         "runtime":{"version":"0.6.0","files":[{"name":"dlssnr_amd_pass1.dll","size":1,"sha256":"2222222222222222222222222222222222222222222222222222222222222222","url":"https://x/r"},
                                              {"name":"dlssnr_on_amd_weights.bin","size":1,"sha256":"3333333333333333333333333333333333333333333333333333333333333333","url":"https://x/w"}]}},
         "releases":{"reframework":[
           {"version":"1.5.9.1437-nightly","components":{
             "reframework":{"version":"1.5.9.1437-nightly","files":[{"name":"LICENSE.txt","path":"reframework/LICENSE.txt","size":1,"sha256":"4444444444444444444444444444444444444444444444444444444444444444","url":"https://x/l"}]},
             "reframework-re9":{"version":"1.5.9.1437-nightly","files":[{"name":"dinput8.dll","path":"reframework/dinput8.dll","size":1,"sha256":"5555555555555555555555555555555555555555555555555555555555555555","url":"https://x/9"}]}}},
           {"version":"1.5.9.1","components":{
             "reframework":{"version":"1.5.9.1","files":[{"name":"LICENSE.txt","path":"reframework/LICENSE.txt","size":1,"sha256":"4444444444444444444444444444444444444444444444444444444444444444","url":"https://x/l"}]},
             "reframework-re9":{"version":"1.5.9.1","files":[{"name":"dinput8.dll","path":"reframework/dinput8.dll","size":1,"sha256":"6666666666666666666666666666666666666666666666666666666666666666","url":"https://x/old"}]},
             "reframework-re4":{"version":"1.5.9.1","files":[{"name":"dinput8.dll","path":"reframework/dinput8.dll","size":1,"sha256":"7777777777777777777777777777777777777777777777777777777777777777","url":"https://x/4"}]}}}]}}
        """;

    [Fact]
    public void ThePayloadCarriesEachBuildInTheNewestReleaseThatHasIt()
    {
        var m = PayloadManifest.Parse(PayloadJson);
        Assert.Empty(m.Pins().ReFrameworkFiles);
        Assert.False(m.Has(PayloadManifest.ReFrameworkComponent));

        var re9 = m.WithReFramework("RE9").Pins();
        Assert.Equal("reframework-re9", re9.ReFrameworkBuild);
        Assert.Equal("1.5.9.1437-nightly", re9.ReFrameworkVersion);
        Assert.Equal(new string('5', 64), re9.ReFrameworkFiles["reframework/dinput8.dll"]);
        Assert.Equal(new string('4', 64), re9.ReFrameworkFiles["reframework/LICENSE.txt"]);

        // A build only an older release carries comes from that one, and the one before it drops out.
        var re4 = m.WithReFramework("RE9").WithReFramework("re4");
        Assert.False(re4.Has("reframework-re9"));
        Assert.Equal("1.5.9.1", re4.Pins().ReFrameworkVersion);
        Assert.Equal(new string('7', 64), re4.Pins().ReFrameworkFiles["reframework/dinput8.dll"]);

        Assert.Same(m, m.WithReFramework("DMC5"));
        Assert.Equal("reframework-re2_tdb66", PayloadManifest.ReFrameworkBuildComponent("RE2_TDB66"));
    }

    [Fact]
    public void TickedItGoesInUnderItsOwnRecordAndUntickedItComesOut()
    {
        var game = Game("refw-tick");
        var (src, pins) = Payloads("refw-tick");
        var check = Work.Preflight(game, src, Preset.OptiScaler, pins);
        Assert.False(check.Failed, check.ToLog("preflight"));
        Assert.True(Fixture.HasAny(check, "goes in as dinput8.dll"), check.ToLog("preflight"));

        var report = Work.Install(game, src, Preset.OptiScaler, pins);
        Assert.False(report.Failed, report.ToLog("install"));
        Assert.Contains(Mark, Dll(game));
        Assert.Equal("MIT License praydog", File.ReadAllText(Path.Combine(game, Work.ReFrameworkLicense)));
        Assert.True(File.Exists(Path.Combine(game, Engine.ReFrameworkManifestName)));
        Assert.DoesNotContain("dinput8.dll", File.ReadAllText(Path.Combine(game, Engine.ManifestNameX64)));
        Assert.Contains("\"created\":[\"reframework\"]", File.ReadAllText(Path.Combine(game, Engine.ReFrameworkManifestName)));
        Assert.True(Work.HasReFramework(game));
        Assert.True(Fixture.HasAny(report, "both open with Insert"), report.ToLog("install"));

        // REFramework makes its folders the first time it runs; empty, they go with it.
        Directory.CreateDirectory(Path.Combine(game, "reframework", "autorun"));
        Directory.CreateDirectory(Path.Combine(game, "reframework", "plugins"));
        File.WriteAllText(Path.Combine(game, "re2_fw_config.txt"), "Menu_Key=45");
        Assert.False(Work.Install(game, src, Preset.OptiScaler, pins).Failed);

        pins.ReFramework = false;
        Assert.True(Fixture.HasAny(Work.Preflight(game, src, Preset.OptiScaler, pins), "left off this time"));
        var off = Work.Install(game, src, Preset.OptiScaler, pins);
        Assert.False(off.Failed, off.ToLog("off"));
        Assert.False(File.Exists(Path.Combine(game, Work.ReFrameworkDll)));
        Assert.False(File.Exists(Path.Combine(game, Work.ReFrameworkLicense)));
        Assert.False(File.Exists(Path.Combine(game, Engine.ReFrameworkManifestName)));
        Assert.False(Directory.Exists(Path.Combine(game, "reframework")));
        Assert.True(File.Exists(Path.Combine(game, "re2_fw_config.txt")));
        // OptiScaler is still in.
        Assert.True(File.Exists(Path.Combine(game, "dxgi.dll")));
    }

    [Fact]
    public void TheFolderStaysWhenItHoldsAScriptOrWasThereBefore()
    {
        var game = Game("refw-scripts");
        var (src, pins) = Payloads("refw-scripts");
        Assert.False(Work.Install(game, src, Preset.OptiScaler, pins).Failed);
        Directory.CreateDirectory(Path.Combine(game, "reframework", "autorun"));
        File.WriteAllText(Path.Combine(game, "reframework", "autorun", "mine.lua"), "-- the person's");
        var gone = Work.Uninstall(game, Preset.OptiScaler);
        Assert.False(gone.Failed, gone.ToLog("uninstall"));
        Assert.False(File.Exists(Path.Combine(game, Work.ReFrameworkDll)));
        Assert.True(File.Exists(Path.Combine(game, "reframework", "autorun", "mine.lua")));
        Assert.True(Fixture.HasAny(gone, "reframework\\"), gone.ToLog("uninstall"));

        var before = Game("refw-before");
        Directory.CreateDirectory(Path.Combine(before, "reframework", "plugins"));
        Assert.False(Work.Install(before, src, Preset.OptiScaler, pins).Failed);
        Assert.DoesNotContain("created", File.ReadAllText(Path.Combine(before, Engine.ReFrameworkManifestName)));
        Assert.False(Work.Uninstall(before, Preset.OptiScaler).Failed);
        Assert.True(Directory.Exists(Path.Combine(before, "reframework", "plugins")));
    }

    [Fact]
    public void AnotherModsDinput8IsLeftAloneAndTheRouteStillGoesIn()
    {
        var game = Game("refw-foreign");
        var (src, pins) = Payloads("refw-foreign");
        File.WriteAllText(Path.Combine(game, Work.ReFrameworkDll), "Ultimate ASI Loader");
        var check = Work.Preflight(game, src, Preset.OptiScaler, pins);
        Assert.False(check.Failed, check.ToLog("preflight"));
        Assert.True(Fixture.HasAny(check, "not REFramework"), check.ToLog("preflight"));
        var report = Work.Install(game, src, Preset.OptiScaler, pins);
        Assert.False(report.Failed, report.ToLog("install"));
        Assert.Equal("Ultimate ASI Loader", Dll(game));
        Assert.False(File.Exists(Path.Combine(game, Engine.ReFrameworkManifestName)));
        Assert.True(File.Exists(Path.Combine(game, "dxgi.dll")));
    }

    [Fact]
    public void AnotherReFrameworkIsBackedUpAndPutBack()
    {
        var game = Game("refw-older");
        var (src, pins) = Payloads("refw-older", "newer build");
        File.WriteAllText(Path.Combine(game, Work.ReFrameworkDll), $"older build {Mark}");
        Assert.True(Work.IsReFramework(Path.Combine(game, Work.ReFrameworkDll)));
        Assert.False(Work.HasReFramework(game));
        Assert.True(Fixture.HasAny(Work.Preflight(game, src, Preset.OptiScaler, pins), "another REFramework build"));
        Assert.False(Work.Install(game, src, Preset.OptiScaler, pins).Failed);
        Assert.StartsWith("newer build", Dll(game));
        Assert.True(Work.HasReFramework(game));

        var gone = Work.Uninstall(game, Preset.OptiScaler);
        Assert.False(gone.Failed, gone.ToLog("uninstall"));
        Assert.StartsWith("older build", Dll(game));
        Assert.False(File.Exists(Path.Combine(game, Engine.ReFrameworkManifestName)));
        Assert.False(Directory.Exists(Path.Combine(game, Engine.BackupDir)));
    }

    [Fact]
    public void ReShadeUnderItsNameIsRefused()
    {
        var game = Game("refw-reshade");
        var (src, pins) = Payloads("refw-reshade");
        Assert.True(Fixture.HasErr(Work.Preflight(game, src, Preset.Dx12, pins, proxy: "dinput8.dll"), "REFramework loads under"));
        var report = Work.Install(game, src, Preset.Dx12, pins, proxy: "dinput8.dll");
        Assert.True(report.Failed);
        Assert.False(File.Exists(Path.Combine(game, Work.ReFrameworkDll)));
        Assert.True(Work.ReFrameworkRoute(Preset.Dx12));
        Assert.False(Work.ReFrameworkRoute(Preset.FiveM));
        Assert.False(Work.ReFrameworkRoute(Preset.X86Dx11));
    }

    [Fact]
    public void ARecordedFolderIsOneThisAppMayMake()
    {
        var m = new Manifest(Engine.ReFrameworkPreset, Route.X64) { Created = ["reframework"] };
        Assert.Equal(["reframework"], Manifest.Decode(Manifest.Encode(m)).Created);
        Assert.DoesNotContain("created", Manifest.Encode(new Manifest("OptiScaler", Route.X64)));
        var tampered = Manifest.Encode(m).Replace("\"reframework\"", "\"..\"");
        Assert.Throws<InstallException>(() => Manifest.Decode(tampered));
    }
}
