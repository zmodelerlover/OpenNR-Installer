// Which anti-cheat a game folder carries, read off the names of its files and folders. Every route this
// app installs puts a DLL in the game, which is exactly what an anti-cheat looks for, and the account is
// what it costs. NRLive captures the game's window instead and touches nothing in its folder, so the
// sheet points there when one of these is found.
//
// Names only, never contents: each of these ships a folder, a service or a driver under a name of its
// own, and reading a few thousand names takes milliseconds where opening the files would not.
//
// Steam was looked at and left out. Its store API (appdetails) has no anti-cheat field; the store page's
// "Uses Kernel Level Anti-Cheat" box is HTML a developer may or may not fill in, and only for kernel
// drivers; appinfo.vdf only says "UnsupportedAntiCheatConfiguration" in some Steam Deck test results.
// None of it says more than the folder does, and all of it would be a network call or a binary format
// to keep up with.

using System.Text.RegularExpressions;

namespace AmdNr.Core;

public static class AntiCheat
{
    /// <summary>Each anti-cheat by the name people know it by, the folder it installs under (null when it has
    /// none of its own) and the files it ships, matched against whole names, case-insensitive.</summary>
    private static readonly (string Name, Regex? Folder, Regex File)[] Signs =
    [
        ("Easy Anti-Cheat", R("easyanticheat(_eos)?"),
            R(@"easyanticheat.*|eac(_?launcher)?\.exe|.*_eac(_eos)?\.exe|start_protected_game\.exe")),
        ("BattlEye", R("battleye"), R(@"beservice(_x64)?\.exe|beclient(_x64)?\.dll|bedaisy\.sys|.*_be\.exe")),
        // Vanguard installs under Program Files, not in the game: the games are known by their executables.
        ("Riot Vanguard", R("riot vanguard"),
            R(@"vgk\.sys|vgc\.exe|valorant(-win64-shipping)?\.exe|league of legends\.exe")),
        ("nProtect GameGuard", R("gameguard"), R(@"gamemon(64)?\.des|gameguard\.des|npgg.*\.erl|npggnt\.des")),
        ("XIGNCODE3", R("xigncode3?"), R(@"x3\.xem|xhunter1\.sys|xxd-0\.xem")),
        ("Ricochet", null, R(@"(atvi-)?randgrid.*\.sys")),
        ("ACE", R("anticheatexpert"), R(@"ace-(base|tray|guard|safe).*|sguard(svc)?(64)?\.exe")),
        ("mhyprot", null, R(@"mhyprot\d*\.sys|hoyokprotect\.sys")),
        ("FACEIT", R("faceit( ac)?"), R(@"faceit(ac|client)?\.(exe|sys)")),
        ("EA Javelin", R("eaanticheat"), R(@"eaanticheat\..*")),
        ("PunkBuster", null, R(@"pnkbstr[ab]\.exe|pbsvc\.exe")),
        ("NetEase Anti-Cheat", null, R(@"neacsafe.*\.sys|neac(client)?\.exe")),
        ("EQU8", R("equ8"), R(@"equ8.*\.(exe|dll|sys)")),
    ];

    private static Regex R(string whole) =>
        new($"^(?:{whole})$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>How deep under the game folder to look: deep enough for an Unreal game's
    /// Game\Binaries\Win64\BattlEye, and no deeper, where only the game's own data is.</summary>
    private const int Depth = 4;

    /// <summary>Where to stop on a folder with a huge tree: a game whose anti-cheat sits that far in is rare, and
    /// a sheet that waits seconds to open is not.</summary>
    private const int MaxEntries = 20_000;

    /// <summary>The anti-cheats found in a game folder, and beside its executable when that is elsewhere, in the
    /// order of the table above; empty when there is none, or the folder cannot be read.</summary>
    public static IReadOnlyList<string> Find(string folder, string? executable = null)
    {
        var found = new HashSet<string>(StringComparer.Ordinal);
        var seen = 0;
        Walk(folder, found, ref seen);
        if (executable is not null && Path.GetDirectoryName(executable) is { } beside
            && !Engine.SamePath(Engine.WeaklyCanonical(folder), Engine.WeaklyCanonical(beside))
            && !Engine.IsInside(Engine.WeaklyCanonical(folder), Engine.WeaklyCanonical(beside)))
            Walk(beside, found, ref seen);
        return Signs.Select(s => s.Name).Where(found.Contains).ToList();
    }

    /// <summary>Breadth first, so the folders nearest the root, where these sit, are read before the cap.
    /// Links are not followed: a junction can lead anywhere, the whole drive included.</summary>
    private static void Walk(string root, HashSet<string> found, ref int seen)
    {
        var queue = new Queue<(string Dir, int Depth)>();
        queue.Enqueue((root, 0));
        while (queue.Count > 0 && seen < MaxEntries)
        {
            var (dir, depth) = queue.Dequeue();
            IEnumerable<FileSystemInfo> entries;
            try { entries = new DirectoryInfo(dir).EnumerateFileSystemInfos().ToList(); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            {
                continue;
            }
            foreach (var entry in entries)
            {
                if (++seen > MaxEntries) return;
                var isDir = entry is DirectoryInfo;
                foreach (var (name, folderSign, fileSign) in Signs)
                    if (isDir ? folderSign?.IsMatch(entry.Name) == true : fileSign.IsMatch(entry.Name))
                        found.Add(name);
                if (isDir && depth + 1 < Depth && !Engine.IsLink(entry)) queue.Enqueue((entry.FullName, depth + 1));
            }
        }
    }
}
