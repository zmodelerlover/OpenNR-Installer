// Where the data folder is when someone moved it: one small file in AppPaths.Home naming the folder,
// read before anything else. No file, or one that cannot be read, is the default.

using System.Text;
using System.Text.Json;

namespace AmdNr.Core;

public static class DataLocation
{
    public const string FileName = "data-location.json";

    /// <param name="Root">The data folder.</param>
    /// <param name="Missing">The file names a folder that is not there.</param>
    public readonly record struct Found(string Root, bool Missing);

    public static Found Resolve(string home)
    {
        var folder = Read(home);
        return folder is null ? new Found(home, false) : new Found(folder, !Directory.Exists(folder));
    }

    /// <summary>The folder the file names, or null for the default: no file, a broken one, or a
    /// path that is not a full one.</summary>
    public static string? Read(string home)
    {
        try
        {
            var file = Path.Combine(home, FileName);
            if (!File.Exists(file)) return null;
            using var doc = JsonDocument.Parse(File.ReadAllText(file));
            return doc.RootElement.TryGetProperty("root", out var root) && root.GetString() is { Length: > 0 } path
                   && Path.IsPathFullyQualified(path)
                ? Path.GetFullPath(path)
                : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException
                                      or InvalidOperationException or ArgumentException)
        {
            return null;
        }
    }

    /// <summary>Points the data at <paramref name="root"/>; null, or <paramref name="home"/> itself,
    /// takes the file away. Written aside and moved over, so a crash leaves the old one.</summary>
    public static void Write(string home, string? root)
    {
        var file = Path.Combine(home, FileName);
        if (root is null || Same(root, home))
        {
            File.Delete(file);
            return;
        }
        using var buffer = new MemoryStream();
        using (var json = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true }))
        {
            json.WriteStartObject();
            json.WriteString("root", Path.GetFullPath(root));
            json.WriteEndObject();
        }
        File.WriteAllText(file + ".tmp", Encoding.UTF8.GetString(buffer.ToArray()));
        File.Move(file + ".tmp", file, overwrite: true);
    }

    internal static bool Same(string a, string b) =>
        string.Equals(Normal(a), Normal(b), StringComparison.OrdinalIgnoreCase);

    /// <summary>Whether <paramref name="path"/> is <paramref name="folder"/> or somewhere under it.</summary>
    internal static bool Inside(string path, string folder)
    {
        var p = Normal(path);
        var f = Normal(folder);
        return p.Equals(f, StringComparison.OrdinalIgnoreCase)
               || p.StartsWith(f.EndsWith('\\') ? f : f + '\\', StringComparison.OrdinalIgnoreCase);
    }

    private static string Normal(string path) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
}
