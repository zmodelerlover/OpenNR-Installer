// Moving the data folder somewhere else: whether a folder will do, and the move itself. Every file is
// copied and read back before the pointer changes, and the old copy goes only after it has; a move that
// fails anywhere before that takes back what it wrote and leaves the old folder as it was.
//
// Games do not read any of this: an install copies its files into the game's folder. The one thing
// that is read from here at run time is the Vulkan layer, and it stays in AppPaths.Home.

using System.Security.Cryptography;

namespace AmdNr.Core;

public enum DataFolderProblem { None, Nested, SystemFolder, GameFolder, NotEmpty, NotWritable, NoSpace }

public static class DataMove
{
    /// <summary>What the data goes into when the folder picked already has other things in it.</summary>
    public const string OwnFolder = "OpenNR Installer";

    private const string CacheFolder = "cache";

    /// <summary>The pointer, and the Vulkan layer games load from where it was registered.</summary>
    private static bool Stays(string name) =>
        name.Equals(DataLocation.FileName, StringComparison.OrdinalIgnoreCase)
        || name.Equals(Work.VulkanLayerFolder, StringComparison.OrdinalIgnoreCase);

    /// <summary>The folder picked when it is empty or not there yet, a folder of its own inside it otherwise:
    /// picking D:\ should not spread cache\ and logs\ over the drive.</summary>
    public static string Target(string picked)
    {
        var full = Path.GetFullPath(picked);
        return Directory.Exists(full) && Directory.EnumerateFileSystemEntries(full).Any()
            ? Path.Combine(full, OwnFolder)
            : full;
    }

    /// <summary>Why <paramref name="target"/> will not do, or None. <paramref name="need"/> is the bytes that
    /// would move. The Game in the answer is the game folder it is inside of, for GameFolder.</summary>
    public static (DataFolderProblem Problem, string? Game) Check(
        string target, string current, string home, IEnumerable<string> games, ulong need)
    {
        var toHome = DataLocation.Same(target, home);
        if (DataLocation.Inside(target, current) || (!toHome && (DataLocation.Inside(current, target)
                                                                 || DataLocation.Inside(target, home))))
            return (DataFolderProblem.Nested, null);
        foreach (var system in new[]
                 {
                     Environment.SpecialFolder.ProgramFiles, Environment.SpecialFolder.ProgramFilesX86,
                     Environment.SpecialFolder.Windows,
                 }.Select(Environment.GetFolderPath))
            if (system.Length > 0 && DataLocation.Inside(target, system)) return (DataFolderProblem.SystemFolder, null);
        if (games.FirstOrDefault(g => DataLocation.Inside(target, g)) is { } game) return (DataFolderProblem.GameFolder, game);
        if (!toHome && Directory.Exists(target) && Directory.EnumerateFileSystemEntries(target).Any())
            return (DataFolderProblem.NotEmpty, null);
        if (!Writable(target)) return (DataFolderProblem.NotWritable, null);
        return Engine.FreeBytes(target) is { } free && free < need ? (DataFolderProblem.NoSpace, null) : (DataFolderProblem.None, null);
    }

    /// <summary>Makes and deletes a file there. A folder made for the test is taken away again.</summary>
    private static bool Writable(string target)
    {
        var made = !Directory.Exists(target);
        try
        {
            Directory.CreateDirectory(target);
            var probe = Path.Combine(target, $"write-test-{Guid.NewGuid():N}.tmp");
            File.WriteAllText(probe, "");
            File.Delete(probe);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return false;
        }
        finally
        {
            try { if (made && Directory.Exists(target)) Directory.Delete(target, recursive: true); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        }
    }

    /// <summary>The bytes a move would copy; without the cache when it is left to come back by download.</summary>
    public static ulong Size(string root, bool withCache) =>
        (ulong)Files(root, withCache).Sum(f => new FileInfo(f).Length);

    /// <summary>Everything that moves, as full paths: all but what <see cref="Stays"/>, and the cache only when asked.</summary>
    private static List<string> Files(string root, bool withCache)
    {
        var files = new List<string>();
        foreach (var entry in new DirectoryInfo(root).EnumerateFileSystemInfos())
        {
            if (Stays(entry.Name) || (!withCache && entry.Name.Equals(CacheFolder, StringComparison.OrdinalIgnoreCase))) continue;
            if (entry is DirectoryInfo dir)
                files.AddRange(dir.EnumerateFiles("*", SearchOption.AllDirectories).Select(f => f.FullName)
                    .Where(f => !InStaging(root, f)));
            else files.Add(entry.FullName);
        }
        return files;
    }

    /// <summary>cache\staging\ holds hard links into the cache, one folder per set of components installed, and is made
    /// again at every install (PayloadCache.Stage). Copied to another drive each link became a whole file: 8 GB of cache
    /// turned into 40. It stays behind and goes with the old folder.</summary>
    private static bool InStaging(string root, string file)
    {
        var parts = Path.GetRelativePath(root, file).Split(Path.DirectorySeparatorChar);
        return parts.Length > 2 && parts[0].Equals(CacheFolder, StringComparison.OrdinalIgnoreCase)
               && parts[1].Equals("staging", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Copies the data from <paramref name="from"/> into <paramref name="to"/>, reads every file back,
    /// points <paramref name="home"/>'s pointer at it, and then deletes the old copy -- the cache too when it did not
    /// come along. Returns how many of the old entries could not be deleted (a file something holds open); they are
    /// only leftovers by then. Throws with nothing changed when anything before the pointer fails.</summary>
    public static int Move(string from, string to, string home, bool withCache, IProgress<double>? progress = null)
    {
        var files = Files(from, withCache);
        var total = Math.Max(1L, files.Sum(f => new FileInfo(f).Length));
        var done = 0L;
        var made = !Directory.Exists(to);
        var written = new List<string>();
        try
        {
            foreach (var file in files)
            {
                var copy = Path.Combine(to, Path.GetRelativePath(from, file));
                Directory.CreateDirectory(Path.GetDirectoryName(copy)!);
                written.Add(copy);
                CopyChecked(file, copy, n => progress?.Report((double)(done += n) / total));
            }
            DataLocation.Write(home, to);
        }
        catch
        {
            Undo(to, made, written);
            throw;
        }
        return Sweep(from, home);
    }

    private static void CopyChecked(string source, string copy, Action<long> advance)
    {
        byte[] hash;
        using (var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
        using (var output = new FileStream(copy, FileMode.Create, FileAccess.Write, FileShare.None))
        using (var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
        {
            var buffer = new byte[1 << 20];
            int read;
            while ((read = input.Read(buffer)) > 0)
            {
                sha.AppendData(buffer, 0, read);
                output.Write(buffer, 0, read);
                advance(read);
            }
            output.Flush(flushToDisk: true);
            hash = sha.GetHashAndReset();
        }
        using var back = File.OpenRead(copy);
        if (!SHA256.HashData(back).AsSpan().SequenceEqual(hash))
            throw new IOException($"{copy} does not read back as it was written.");
    }

    private static void Undo(string to, bool made, List<string> written)
    {
        try
        {
            if (made) Directory.Delete(to, recursive: true);
            else foreach (var file in written) File.Delete(file);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // What could not be taken back is a stray copy; the old folder and the pointer are as they were.
        }
    }

    /// <summary>The old copy, gone. Home itself stays, with the pointer and the Vulkan layer; another folder goes
    /// with what was in it when nothing is left.</summary>
    private static int Sweep(string from, string home)
    {
        var left = 0;
        foreach (var entry in new DirectoryInfo(from).EnumerateFileSystemInfos().ToList())
        {
            if (Stays(entry.Name)) continue;
            try
            {
                if (entry is DirectoryInfo dir) dir.Delete(recursive: true);
                else entry.Delete();
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                left++;
            }
        }
        try
        {
            if (left == 0 && !DataLocation.Same(from, home) && !Directory.EnumerateFileSystemEntries(from).Any())
                Directory.Delete(from);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        return left;
    }
}
