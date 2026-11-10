// Taking an install back through its own manifest: what it wrote goes, what it displaced comes back,
// and what somebody changed since stays, said. Work.Uninstall runs this for every route.

using System.Text;

namespace AmdNr.Core;

public static partial class Transaction
{
    /// <summary>Undo an install using its own manifest. Files the installer did not own are left
    /// alone, files changed afterwards are kept with a warning, and anything displaced at install
    /// time is put back from its backup. <paramref name="ours"/> is asked about a changed file, by
    /// name and current hash: when it says the file is this app's anyway, it goes too.</summary>
    public static void Uninstall(string dir, Route route, bool removeConfigs, List<string> log,
        Func<string, string, bool>? ours = null, string? manifestName = null)
    {
        dir = Engine.Absolute(dir);
        Engine.SafePath(dir);
        if (manifestName is null) MigrateLegacyManifest(dir, route);
        var manifestPath = Path.Combine(dir, manifestName ?? route.ManifestFileName());
        Engine.SafePath(manifestPath);
        Engine.Require(File.Exists(manifestPath), "No install manifest");

        var m = Manifest.Decode(Engine.Read(manifestPath));
        m.FileName = manifestName;
        var keep = new List<Entry>();

        foreach (var e in m.Entries)
        {
            var dst = Path.Combine(dir, e.Name);
            Engine.SafePath(dst);

            if (!e.Owned)
            {
                log.Add($"PRESERVED pre-existing identical file: {e.Name}");
                continue;
            }

            var backupPath = Path.Combine(dir, e.Backup);
            // A file moved out that is back under its name already: the original put back by hand, which
            // is done, or another file, which is the person's now. Either way nothing is written over it,
            // and keeping the entry for it would count the folder as installed for ever.
            if (e.Hash == Engine.Sha([]) && File.Exists(dst) && Engine.HashFile(dst) is var back && back != e.Hash)
            {
                Engine.SafePath(backupPath);
                if (back == e.BackupHash) TryDelete(backupPath);
                log.Add(back == e.BackupHash ? $"RESTORED: {e.Name}" : $"PRESERVED put back by hand: {e.Name}");
                continue;
            }
            // What the install displaced was this app's own too -- an older copy of a file under a
            // name only this project writes. There is nobody else's file to put back, and putting it
            // back left the folder counting as installed after the uninstall said it was done.
            var discard = e.Backup.Length > 0 && ours?.Invoke(e.Name, e.BackupHash) == true;
            var restore = e.Backup.Length > 0 && !discard;
            if (discard) Engine.SafePath(backupPath);
            if (restore)
            {
                Engine.SafePath(backupPath);
                // Put back already, by an uninstall cut off before it could rewrite this manifest --
                // the window closed, the power went. The backup is gone because it was used, and the
                // file there is the original: done, not a backup that has to be read.
                if (!File.Exists(backupPath) && File.Exists(dst) && Engine.HashFile(dst) == e.BackupHash)
                {
                    log.Add($"RESTORED: {e.Name}");
                    continue;
                }
                // Deleted or damaged since: nothing to put back, and stopping here failed every uninstall.
                if (!File.Exists(backupPath) || Engine.HashFile(backupPath) != e.BackupHash)
                {
                    log.Add($"WARNING {e.Name}: {NoBackup}");
                    restore = false;
                }
            }

            var exists = File.Exists(dst);
            if (exists)
            {
                var current = Engine.HashFile(dst);
                if (current != e.Hash)
                {
                    if (m.State == "installing" && restore && current == e.BackupHash)
                    {
                        TryDelete(backupPath);
                        continue;
                    }
                    // A file that no longer hashes to what was written is somebody else's work now --
                    // unless it is under a name only this project uses, or a build this app pins.
                    // Keeping one of those was a folder that said installed for ever: Castle
                    // Crashers, with the pair replaced by hand. Configuration is the person's
                    // either way, and goes only when they say so.
                    var take = e.Configuration ? removeConfigs : ours?.Invoke(e.Name, current) == true;
                    if (!take)
                    {
                        log.Add(e.Configuration
                            ? $"PRESERVED personal/default configuration: {e.Name}"
                            : $"WARNING modified after install; retained with backup: {e.Name}");
                        keep.Add(e.Clone());
                        continue;
                    }
                    if (!e.Configuration) log.Add($"CHANGED: {e.Name}");
                }
            }
            else if (!restore)
            {
                if (discard) TryDelete(backupPath);
                continue;
            }

            if (e.Configuration && e.Backup.Length == 0 && !removeConfigs)
            {
                log.Add($"PRESERVED personal/default configuration: {e.Name}");
                keep.Add(e.Clone());
                continue;
            }

            if (restore)
            {
                Engine.CopyVerified(backupPath, dst, e.BackupHash, e.Name);
                TryDelete(backupPath);
                log.Add($"RESTORED: {e.Name}");
            }
            else if (TryDelete(dst))
            {
                if (discard) TryDelete(backupPath);
                log.Add($"REMOVED: {e.Name}");
            }
            else
            {
                // The file is still there, so the manifest goes on owning it. Dropping the entry
                // here was the whole bug: a DLL the running game still had open could not be
                // deleted, the log said REMOVED anyway, the entry went, and from then on every
                // uninstall found nothing to do while the add-on was still in the folder.
                log.Add($"WARNING {StillOpen}: {e.Name}. Close the game and uninstall again.");
                keep.Add(e.Clone());
            }
        }

        m.Created = m.Created.Where(f => !PruneCreated(dir, f, log)).ToList();
        if (keep.Count == 0)
        {
            TryDelete(manifestPath);
        }
        else
        {
            m.Entries = keep;
            m.State = "installed";
            Manifest.WriteAtomic(dir, m);
        }
        log.Add("Uninstall complete; retained files/backups are listed above.");
    }

    /// <summary>A folder the install recorded as its own (<see cref="Manifest.Created"/>), taken when nothing but
    /// empty folders is in it; one with any file in it, at any depth, is somebody's now and stays whole. True when
    /// it is gone.</summary>
    private static bool PruneCreated(string dir, string folder, List<string> log)
    {
        Engine.Require(Engine.Creatable.Contains(folder), $"Refusing to remove an unmanaged folder: {folder}");
        var path = Path.Combine(dir, folder);
        Engine.SafePath(path);
        if (!Directory.Exists(path)) return true;
        try
        {
            if (Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories).Any()
                || new DirectoryInfo(path).EnumerateDirectories("*", SearchOption.AllDirectories)
                    .Any(d => d.Attributes.HasFlag(FileAttributes.ReparsePoint)))
            {
                log.Add($"PRESERVED {folder}\\: it holds files that are not this app's (scripts, plugins, settings).");
                return false;
            }
            Directory.Delete(path, recursive: true);
            log.Add($"REMOVED: {folder}\\");
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            log.Add($"WARNING could not remove {folder}\\: {e.Message}");
            return false;
        }
    }

    /// <summary>The words a caller can look for to tell "the game still has it open" apart from
    /// "somebody changed it": one is waited out, the other is a decision.</summary>
    public const string StillOpen = "still open, so it is still here";

    /// <summary>A backup that is gone or no longer hashes to what was saved: nothing is put back.</summary>
    internal const string NoBackup = "its backup is gone or damaged, so there was no original to put back";

    /// <summary>True when the file is gone, which includes it never having been there. A file that
    /// will not delete does not fail the whole uninstall -- the rest still comes out -- but it is
    /// never reported as removed either, and the caller keeps its manifest entry so the next
    /// uninstall can finish the job.</summary>
    private static bool TryDelete(string path)
    {
        try
        {
            Engine.Writable(path);
            File.Delete(path);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
