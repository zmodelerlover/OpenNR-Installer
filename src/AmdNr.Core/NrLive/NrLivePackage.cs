// NRLive's own folder in this app's data folder: its portable package, unpacked from a zip that hashed to
// its pin, and the settings it and the NRLive page share. Nothing of it goes near a game folder -- that is
// the point of NRLive.
//
// One folder for every version, as NRLive's own updater keeps it: an update writes over the package and
// leaves scaleconfig.ini and the profiles alone, so what was set in NRLive's overlay survives it.

using System.IO.Compression;

namespace AmdNr.Core;

public static class NrLivePackage
{
    public static string Folder => Create(Path.Combine(AppPaths.Root, "nrlive"));
    public static string Exe => Path.Combine(Folder, "NRLive.exe");

    /// <summary>Which release was unpacked here and the SHA-256 of its zip, one per line. Written last, so a
    /// package cut off halfway has none and reads as not installed.</summary>
    private static string Marker => Path.Combine(Folder, "opennr-package.txt");

    /// <summary>What NRLive and its own launcher write and read back: never replaced by a package.</summary>
    private static bool Kept(string relative) =>
        relative.Equals(NrLiveConfig.FileName, StringComparison.OrdinalIgnoreCase)
        || relative.Equals("ui_config.json", StringComparison.OrdinalIgnoreCase)
        || relative.StartsWith("profiles/", StringComparison.OrdinalIgnoreCase);

    /// <summary>The version unpacked here, or null when there is none, or its executable is gone.</summary>
    public static string? InstalledVersion()
    {
        try
        {
            return File.Exists(Exe) && File.Exists(Marker) ? File.ReadAllLines(Marker).FirstOrDefault() is { Length: > 0 } v ? v : null : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Whether this release is the one unpacked here: same version and the same zip, so a build re-cut
    /// under an unchanged version is not taken for the one already here.</summary>
    public static bool IsCurrent(ComponentRelease release)
    {
        try
        {
            return File.Exists(Exe) && File.Exists(Marker)
                   && File.ReadAllLines(Marker) is [var version, var sha, ..]
                   && version == release.Version && sha == Fingerprint(release);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>Downloads whatever the release carries into the cache, each file checked against its pin as every
    /// download is, then unpacks it here. NRLive must not be running: its files are open while it is.</summary>
    public static async Task InstallAsync(PayloadCache cache, PayloadManifest manifest, ComponentRelease release,
        IProgress<DownloadProgress>? progress = null, CancellationToken cancel = default)
    {
        var pinned = manifest.With(release);
        var folders = new List<(string Name, string Dir)>();
        // NRLive first: the models beside it are useless without it, and it is what the page waits for.
        foreach (var name in release.Components.Keys.OrderBy(n => n == PayloadManifest.NrLiveComponent ? 0 : 1))
            folders.Add((name, await cache.EnsureAsync(pinned, name, progress, cancel)));
        await Task.Run(() => Unpack(pinned, release, folders), cancel);
    }

    internal static void Unpack(PayloadManifest pinned, ComponentRelease release, IEnumerable<(string Name, string Dir)> folders)
    {
        File.Delete(Marker);
        foreach (var (name, dir) in folders)
            foreach (var file in pinned.Component(name).Installed)
            {
                var source = Path.Combine(dir, file.RelativePath);
                if (name == PayloadManifest.NrLiveComponent && file.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                    ExtractAll(source);
                else
                    // Anything else the release carries goes where its path says: the models in dlssnr-amd\.
                    File.Copy(source, Inside(file.RelativePath), overwrite: true);
            }
        File.WriteAllLines(Marker, [release.Version, Fingerprint(release)]);
    }

    /// <summary>The SHA-256s of the release's NRLive files, which is what tells two builds of one version apart.</summary>
    private static string Fingerprint(ComponentRelease release) =>
        string.Join(",", release.Components[PayloadManifest.NrLiveComponent].Files.Select(f => Engine.Lower(f.Sha256)));

    /// <summary>Every entry of the package. NRLive's CI zips its files at the root; a zip with one folder around
    /// all of them is read as that folder, so a package re-zipped by hand still lands in the right shape.</summary>
    private static void ExtractAll(string zipPath)
    {
        using var zip = ZipFile.OpenRead(zipPath);
        var files = zip.Entries.Where(e => e.Name.Length > 0).ToList();
        var names = files.Select(e => e.FullName.Replace('\\', '/')).ToList();
        var top = names.Count > 0 && names[0].IndexOf('/') is > 0 and var cut
                  && names.All(n => n.StartsWith(names[0][..(cut + 1)], StringComparison.Ordinal))
            ? cut + 1
            : 0;
        for (var i = 0; i < files.Count; i++)
        {
            var relative = names[i][top..];
            var target = Inside(relative);
            if (Kept(relative) && File.Exists(target)) continue;
            files[i].ExtractToFile(target, overwrite: true);
        }
    }

    /// <summary>A path under the folder, refused when the archive or the manifest tries to climb out of it.</summary>
    private static string Inside(string relative)
    {
        var target = Path.GetFullPath(Path.Combine(Folder, relative));
        Engine.Require(Engine.IsInside(Folder, target), $"The NRLive package names a path outside its folder: {relative}");
        Engine.MakeParent(target);
        return target;
    }

    private static string Create(string dir)
    {
        Directory.CreateDirectory(dir);
        return dir;
    }
}
