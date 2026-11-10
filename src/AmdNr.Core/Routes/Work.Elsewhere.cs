// An install of this app's in another folder of the same game: found by its record, and taken out before the
// game is installed where it runs from.
//
// Phantom Blade Zero was read as the 7-Zip in its ProjectPBZ\Binaries\Win64, so v0.9.5 installed beside that; the
// game is platform\steam\PBZ.exe. Once detection pointed there the app saw nothing installed, installed again, and
// the first copy stayed where it was. Pointing a game at an executable in another folder by hand leaves one behind
// the same way.

namespace AmdNr.Core;

/// <summary>A game's folder as the list holds it, and the folders of every other game the list knows: an install in
/// one of those that is inside this game's folder, or below it, is that game's.</summary>
public sealed record GameTree(string Root, IReadOnlyCollection<string> Others);

public static partial class Work
{
    /// <summary>How far below the game's folder an install is looked for, which is as far as detection goes for an
    /// executable (Project\Binaries\Win64, game\bin\win64), and how many folders are read before the search stops.</summary>
    private const int ElsewhereDepth = 3, ElsewhereFolders = 2048;

    /// <summary>The folders of this game that hold an earlier install of this app's, other than the one this
    /// install goes into: what an install here takes out first. Only a record counts (<see cref="Recorded"/>):
    /// files that look like ours without one are left for whoever put them there. Links are not followed. Two kinds
    /// of folder are never in the answer: another game's in the list, and one that holds its own copy of the file
    /// this install goes beside, which is another build of the game (The Witcher 3 has witcher3.exe in bin\x64 and
    /// in bin\x64_dx12).</summary>
    public static IReadOnlyList<string> InstalledElsewhere(string gameDir, Preset preset, GameTree? tree) =>
        Elsewhere(gameDir, preset, tree).Where(e => !e.Twin).Select(e => e.Folder).ToList();

    private static List<(string Folder, bool Twin)> Elsewhere(string gameDir, Preset preset, GameTree? tree)
    {
        var found = new List<(string, bool)>();
        // FiveM writes across FiveM.app from a record at its top, and nothing moves that. A folder with no
        // executable found in it (a game its launcher uninstalled) says nothing about where the game runs from.
        if (tree is null || preset == Preset.FiveM || !File.Exists(ResolveTarget(gameDir))) return found;
        var beside = NameOf(ResolveTarget(gameDir));
        string? here;
        try { here = InstallFolder(gameDir, preset); }
        catch (InstallException) { return found; }
        if (here is null) return found;
        here = Engine.WeaklyCanonical(here);

        var root = Engine.WeaklyCanonical(tree.Root);
        var others = tree.Others.Select(Engine.WeaklyCanonical).Where(o => Engine.IsInside(root, o)).ToList();
        var queue = new Queue<(string Dir, int Depth)>([(root, 0)]);
        for (var read = 0; queue.Count > 0 && read < ElsewhereFolders; read++)
        {
            var (dir, depth) = queue.Dequeue();
            if (others.Any(o => Engine.SamePath(o, dir))) continue;
            if (!Engine.SamePath(dir, here) && Recorded(dir))
                found.Add((dir, File.Exists(Path.Combine(dir, beside))));
            if (depth == ElsewhereDepth) continue;
            try
            {
                foreach (var sub in new DirectoryInfo(dir).EnumerateDirectories())
                    if (!Engine.IsLink(sub)) queue.Enqueue((sub.FullName, depth + 1));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        }
        return found;
    }

    /// <summary>A record this build can read that still owns a file in the folder, which is what the tile's badge
    /// goes by (GameScanner.InstalledAs); one kept only for the settings it preserved is not an install.</summary>
    private static bool Recorded(string dir) =>
        Manifests(dir, migrate: false).Any(m => m.Preset != Preset.FiveM.ManifestPreset()
            && m.Entries.Any(e => e.Owned && !e.Configuration && File.Exists(Path.Combine(dir, e.Name))));

    /// <summary>What the pre-flight and the install say about one of <see cref="InstalledElsewhere"/>.</summary>
    public static string EarlierInstall(string folder) =>
        $"An earlier install is in {folder}, which is not where this game installs now. Install and Uninstall both "
        + "take it out: the files it replaced go back, and its settings stay there.";

    /// <summary>Takes out every install <see cref="InstalledElsewhere"/> names, the way Uninstall would, keeping
    /// the settings, and says so in <paramref name="report"/>. Returns how many there were. Nothing is touched
    /// while a program runs from one of those folders or holds a recorded file open: half an install taken out is
    /// worse than a whole one left, so the report fails instead and says what to close.</summary>
    private static int ClearElsewhere(string gameDir, Preset preset, GameTree? tree, IEnumerable<string>? pinned, Report report)
    {
        var all = Elsewhere(gameDir, preset, tree);
        foreach (var (folder, _) in all.Where(e => e.Twin))
            report.Info($"{folder} has an install too. It holds its own {NameOf(ResolveTarget(gameDir))}, so it is "
                        + "another build of the game and is left as it is.");
        var folders = all.Where(e => !e.Twin).Select(e => e.Folder).ToList();
        foreach (var folder in folders) report.Info(EarlierInstall(folder));

        var busy = new List<string>();
        foreach (var folder in folders)
        {
            var held = Manifests(folder, migrate: false).SelectMany(m => m.Entries).Where(e => e.Owned)
                .Select(e => e.Name).Distinct().Where(n => Engine.IsLocked(Path.Combine(folder, n))).ToList();
            if (Running(folder, Preset.Dx11) is { } running) busy.Add($"In {folder}: {running}");
            else if (held.Count > 0) busy.Add($"In {folder}: {Engine.OpenElsewhere(folder, held)}");
        }
        foreach (var why in busy) report.Err(why);
        if (busy.Count > 0)
        {
            report.Info("Nothing was taken out of the earlier install.");
            return folders.Count;
        }

        foreach (var folder in folders)
        {
            // Whichever route the record names: Uninstall undoes every record in the folder.
            var removed = Uninstall(folder, Preset.Dx11, removeConfig: false, pinned);
            report.Append(removed);
            // A file that would not delete after all is a warning to Uninstall, which can be run again. Here the
            // game would go on loading it beside the new install.
            var left = removed.Lines.Any(l => l.Text.Contains(Transaction.StillOpen, StringComparison.Ordinal));
            if (left) report.Err($"The earlier install in {folder} is not all out: close the game and run this again.");
            if (left || removed.Failed) break;
        }
        return folders.Count;
    }
}
