// WrongGPU's launcher, which converts its model and RR data, from WrongGPU's own release page and nowhere else: the
// release zip of the version the kit is, checked against the .sha256 asset published beside it, and the exe inside
// against the zip's SHA256SUMS.txt. Fetched only after the player said yes, into a folder the app deletes after the
// install; nothing of it is cached, mirrored or put in a game.

using System.IO.Compression;

namespace AmdNr.Core;

public static partial class Work
{
    public const string WrongGpuHome = "https://github.com/phantoslayer21/wronggpu";

    /// <summary>The release zip of this WrongGPU version on its release page.</summary>
    public static string WrongGpuZipUrl(string version) => $"{WrongGpuHome}/releases/download/v{version}/WrongGPU-{version}.zip";

    /// <summary>Downloads WrongGPU's release zip into <paramref name="into"/>, checks it, and gives the path of the
    /// launcher taken out of it. The zip itself is deleted.</summary>
    public static async Task<string> FetchWrongGpuLauncherAsync(HttpClient http, string version, string into,
        CancellationToken cancel = default)
    {
        Directory.CreateDirectory(into);
        var url = WrongGpuZipUrl(version);
        var zipName = $"WrongGPU-{version}.zip";
        var published = (await http.GetStringAsync(url + ".sha256", cancel))
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.ToLowerInvariant();
        Engine.Require(published is { Length: 64 }, $"WrongGPU's release page gives no SHA-256 for {zipName}.");

        var zip = Path.Combine(into, zipName);
        using (var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancel))
        {
            Engine.Require(response.IsSuccessStatusCode,
                $"{zipName} could not be downloaded: the server answered {(int)response.StatusCode} {response.ReasonPhrase}.");
            await using var file = File.Create(zip);
            await response.Content.CopyToAsync(file, cancel);
        }
        Engine.Require(Engine.HashFile(zip) == published,
            $"{zipName} does not match the SHA-256 on WrongGPU's release page, so it was not used.");

        var exeName = $"WrongGPU-{version}.exe";
        var exe = Path.Combine(into, exeName);
        using (var archive = ZipFile.OpenRead(zip))
        {
            var sums = archive.GetEntry("SHA256SUMS.txt");
            var entry = archive.GetEntry(exeName);
            Engine.Require(sums is not null && entry is not null, $"{zipName} has no {exeName} or SHA256SUMS.txt.");
            using var reader = new StreamReader(sums!.Open());
            var expected = (await reader.ReadToEndAsync(cancel)).Split('\n').Select(l => l.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries))
                .FirstOrDefault(p => p.Length == 2 && p[1].TrimStart('*') == exeName)?[0].ToLowerInvariant();
            entry!.ExtractToFile(exe, overwrite: true);
            Engine.Require(expected is not null && Engine.HashFile(exe) == expected,
                $"{exeName} does not match the SHA256SUMS.txt in {zipName}, so it was not used.");
        }
        File.Delete(zip);
        return exe;
    }
}
