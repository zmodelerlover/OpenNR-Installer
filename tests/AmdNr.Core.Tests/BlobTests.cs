using AmdNr.Core;

namespace AmdNr.Core.Tests;

/// <summary>Payloads written from disk instead of from memory, and the rollback that moves files aside.</summary>
public class BlobTests
{
    [Fact]
    public void AFileCopyHashesToWhatItWasPinnedAs()
    {
        var root = Fixture.Temp("blob-copy");
        var src = Path.Combine(root, "payload.bin");
        var data = Enumerable.Range(0, 3 << 20).Select(i => (byte)(i * 7)).ToArray();
        File.WriteAllBytes(src, data);

        var blob = Blob.OfFile(src);
        Assert.Equal(Engine.Sha(data), blob.Sha);
        Assert.Equal(data.LongLength, blob.Length);
        blob.WriteTo(Path.Combine(root, "copy.bin"));
        Assert.Equal(data, File.ReadAllBytes(Path.Combine(root, "copy.bin")));
    }

    [Fact]
    public void APayloadChangedAfterItWasCheckedFailsTheInstallAndEverythingGoesBack()
    {
        var root = Fixture.Temp("blob-changed");
        var game = Path.Combine(root, "game");
        Directory.CreateDirectory(game);
        File.WriteAllText(Path.Combine(game, "dxgi.dll"), "somebody's dxgi");
        var src = Path.Combine(root, "dxgi.dll");
        File.WriteAllText(src, "the pinned one");
        var files = new SortedDictionary<string, Blob>(StringComparer.Ordinal)
        {
            ["amd-nr.addon64"] = "the add-on"u8.ToArray(),
            ["dxgi.dll"] = Blob.OfFile(src),
        };
        File.WriteAllText(src, "swapped in between");

        var error = Assert.Throws<InstallException>(() => Transaction.Apply(game, "D3D11", Route.X64, files, []));
        Assert.Contains("SHA256 mismatch", error.Message, StringComparison.Ordinal);
        Assert.Equal("somebody's dxgi", File.ReadAllText(Path.Combine(game, "dxgi.dll")));
        Assert.False(File.Exists(Path.Combine(game, "amd-nr.addon64")));
        Assert.False(File.Exists(Path.Combine(game, Route.X64.ManifestFileName())));
        Assert.Empty(Directory.GetDirectories(Path.Combine(game, Engine.BackupDir), "*.rollback"));
    }

    [Fact]
    public void AnUpdateOfItsOwnFilesLeavesNoBackupFolderBehind()
    {
        var game = Fixture.Temp("blob-update");
        Transaction.Apply(game, "D3D11", Route.X64,
            new SortedDictionary<string, Blob>(StringComparer.Ordinal) { ["amd-nr.addon64"] = "one"u8.ToArray() }, []);
        Transaction.Apply(game, "D3D11", Route.X64,
            new SortedDictionary<string, Blob>(StringComparer.Ordinal) { ["amd-nr.addon64"] = "two"u8.ToArray() }, []);

        Assert.Equal("two", File.ReadAllText(Path.Combine(game, "amd-nr.addon64")));
        Assert.False(Directory.Exists(Path.Combine(game, Engine.BackupDir)));
    }

    [Fact]
    public void AScanFindsANameAcrossTheCutBetweenPieces()
    {
        var root = Fixture.Temp("blob-scan");
        var path = Path.Combine(root, "loader.dll");
        var data = new byte[(1 << 20) + 64];
        "d3d10.dll"u8.CopyTo(data.AsSpan((1 << 20) - 4));
        File.WriteAllBytes(path, data);

        Assert.True(Engine.ScanFile(path, b => b.IndexOf("d3d10.dll"u8) >= 0, 16));
        Assert.False(Engine.ScanFile(path, b => b.IndexOf("d3d11.dll"u8) >= 0, 16));
    }
}
