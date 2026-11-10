using System.Text;
using AmdNr.Core;

namespace AmdNr.Core.Tests;

/// <summary>cache\staging got a folder per set of component versions and none was ever deleted: Explorer counted
/// 40 GB of links for one player, and on a volume without hard links they are whole copies. Staging a set now
/// deletes the folders an earlier run of the app left. In the collection that shares AppPaths.Cache.</summary>
[Collection("AppCache")]
public class StagingTests
{
    private const string File1 = "dlssnr_amd_pass1.dll";

    /// <summary>A manifest of one-file components already in the cache, as a download leaves them.</summary>
    private static (PayloadManifest Manifest, List<string> Cached) InCache(params string[] components)
    {
        var version = $"st-{Guid.NewGuid():N}"[..12];
        var cached = new List<string>();
        var entries = components.Select(component =>
        {
            var bytes = Encoding.UTF8.GetBytes($"stand-in {component} {version}");
            var path = Path.Combine(PayloadCache.FolderFor(component, version), component, File1);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, bytes);
            cached.Add(path);
            return $$"""
                "{{component}}": { "version": "{{version}}", "files": [
                  { "name": "{{File1}}", "path": "{{component}}/{{File1}}", "size": {{bytes.Length}}, "sha256": "{{Engine.Sha(bytes)}}" } ] }
                """;
        });
        return (PayloadManifest.Parse($$"""
            { "schema": 1, "owner": "someone", "repo": "Extras", "tag": "{{version}}", "components": { {{string.Join(",", entries)}} } }
            """), cached);
    }

    private static string Staging => Path.Combine(AppPaths.Cache, "staging");

    private static void Junction(string link, string target)
    {
        using var mklink = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
            "cmd.exe", $"/c mklink /J \"{link}\" \"{target}\"") { CreateNoWindow = true, UseShellExecute = false });
        mklink!.WaitForExit();
        Assert.True(Directory.Exists(link));
    }

    [Fact]
    public async Task StagingASetDeletesTheFoldersAnEarlierRunLeftAndNothingElse()
    {
        await PayloadCache.Sweep;
        var (manifest, cached) = InCache("runtime");
        // What an earlier run left: a folder for another set of versions, its files links into the cache.
        var old = Path.Combine(Staging, $"optiscaler.0.6.4-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(old, "OptiScaler"));
        File.WriteAllText(Path.Combine(old, "OptiScaler", "libxess.dll"), "an earlier version's");
        Assert.True(PayloadCache.TryHardLink(cached[0], Path.Combine(old, File1)));
        // Beside staging and named like it, and a file in it that is no folder of a set.
        var beside = Directory.CreateDirectory(Path.Combine(AppPaths.Cache, $"staging-{Guid.NewGuid():N}")).FullName;
        File.WriteAllText(Path.Combine(beside, "kept.txt"), "not in staging");
        var loose = Path.Combine(Staging, $"loose-{Guid.NewGuid():N}.txt");
        File.WriteAllText(loose, "not a folder");

        var staged = new PayloadCache(new HttpClient()).Stage(manifest, "runtime");
        await PayloadCache.Sweep;

        Assert.False(Directory.Exists(old), "the earlier run's folder is gone");
        Assert.True(File.Exists(Path.Combine(staged, "runtime", File1)), "the set just staged is whole");
        Assert.Equal(File.ReadAllBytes(cached[0]), File.ReadAllBytes(Path.Combine(staged, "runtime", File1)));
        Assert.True(File.Exists(cached[0]), "a link went, not the cached file it named");
        Assert.True(File.Exists(Path.Combine(beside, "kept.txt")));
        Assert.True(File.Exists(loose));
        Directory.Delete(beside, recursive: true);
        File.Delete(loose);
    }

    /// <summary>The pre-flight stages in the background and an install reads its folder for as long as it copies:
    /// whatever was staged since the app started is somebody's to read, and staging another set leaves it alone.</summary>
    [Fact]
    public async Task WhatThisRunStagedStaysWhileItMayBeRead()
    {
        await PayloadCache.Sweep;
        var cache = new PayloadCache(new HttpClient());
        var (first, _) = InCache("runtime");
        var reading = cache.Stage(first, "runtime");

        // A key cut at 120 characters that ends in a period: Windows drops it from the folder's name.
        var (long1, long2) = (new string('a', 50), new string('b', 55));
        var (dotted, _) = InCache(long1, long2);
        var cut = cache.Stage(dotted, long1, long2);
        Assert.EndsWith(".", cut);
        Assert.Equal(120, Path.GetFileName(cut).Length);

        var (second, _) = InCache("runtime");
        var other = cache.Stage(second, "runtime");
        await PayloadCache.Sweep;

        Assert.True(File.Exists(Path.Combine(reading, "runtime", File1)));
        Assert.True(File.Exists(Path.Combine(cut, long1, File1)));
        Assert.True(File.Exists(Path.Combine(cut, long2, File1)));
        Assert.True(File.Exists(Path.Combine(other, "runtime", File1)));
    }

    [Fact]
    public async Task ALinkInAStagingFolderIsNotFollowed()
    {
        await PayloadCache.Sweep;
        var outside = Fixture.Temp("staging-outside");
        File.WriteAllText(Path.Combine(outside, "kept.txt"), "outside the cache");
        var old = Directory.CreateDirectory(Path.Combine(Staging, $"optiscaler.0.6.4-{Guid.NewGuid():N}")).FullName;
        File.WriteAllText(Path.Combine(old, "OptiScaler.dll"), "an earlier version's");
        Junction(Path.Combine(old, "linked"), outside);
        var direct = Path.Combine(Staging, $"linked-{Guid.NewGuid():N}");
        Junction(direct, outside);

        var (manifest, _) = InCache("runtime");
        new PayloadCache(new HttpClient()).Stage(manifest, "runtime");
        await PayloadCache.Sweep;

        Assert.True(File.Exists(Path.Combine(outside, "kept.txt")), "what the link led to is not staging's to delete");
        // .NET takes the link out and then reports it could not: the folder is emptied now and goes with the next sweep.
        Assert.False(File.Exists(Path.Combine(old, "OptiScaler.dll")));
        Assert.True(Directory.Exists(direct), "and a link in staging itself is left where it is");
        Directory.Delete(direct);
        Assert.True(File.Exists(Path.Combine(outside, "kept.txt")));
    }
}
